using Illumination.Application.ContentManagement;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using System.Text;

namespace Illumination.Application.Study;

public sealed class StudySessionService
{
    private const int DefaultNewItemLimit = 20;

    private readonly IStudySessionPersistence _persistence;
    private readonly TimeProvider _timeProvider;
    private readonly IStudySessionOrdering _ordering;
    private readonly Dictionary<(Guid SessionId, Guid ItemId), AppearanceState> _appearances = [];

    public StudySessionService(IStudySessionPersistence persistence, TimeProvider timeProvider, IStudySessionOrdering ordering)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _ordering = ordering ?? throw new ArgumentNullException(nameof(ordering));
    }

    public async Task<StudySessionView> StartStudySessionAsync(StartStudySessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.EvaluationMode)) throw new StudyValidationException("Unsupported evaluation mode.");
        var selectedDeckIds = ValidateSelectedDecks(command.SelectedDeckIds);
        var newItemLimit = ValidateNewItemOptions(command.NewItemLimit, command.AllNew);
        var decks = await _persistence.LoadDecksAsync(selectedDeckIds, cancellationToken);
        if (decks.Select(deck => deck.Id).Distinct().Count() != selectedDeckIds.Count || selectedDeckIds.Any(id => decks.All(deck => deck.Id != id)))
        {
            throw new StudyNotFoundException("One or more selected Decks were not found.");
        }

        var learningItemIds = decks.SelectMany(deck => deck.LearningItemIds).Distinct().ToArray();
        var items = await _persistence.LoadLearningItemsAsync(learningItemIds, cancellationToken);
        if (items.Select(item => item.Id).Distinct().Count() != learningItemIds.Length || learningItemIds.Any(id => items.All(item => item.Id != id)))
        {
            throw new StudyNotFoundException("A Learning Item referenced by the selected Decks was not found.");
        }

        foreach (var item in items)
        {
            _ = ToDomain(item);
        }

        var sessionStart = _timeProvider.GetUtcNow();
        var eligible = items.Where(IsActive).Where(item => !command.LowInteractionOnly || item.LowInteractionEligible).ToArray();
        var queue = new List<Guid>();
        queue.AddRange(Order(eligible.Where(item => item.IsInShortTermRelearning).Select(item => item.Id).ToArray()));
        queue.AddRange(Order(eligible.Where(item => !item.IsInShortTermRelearning && !item.IsNew && item.DueAt <= sessionStart).Select(item => item.Id).ToArray()));
        var newItems = Order(eligible.Where(item => item.IsNew).Select(item => item.Id).ToArray());
        queue.AddRange(command.AllNew ? newItems : newItems.Take(newItemLimit!.Value));

        var session = new StudySessionSnapshot(Guid.NewGuid(), sessionStart, null, selectedDeckIds, queue, [], command.EvaluationMode, command.ConsiderAssistance, command.LowInteractionOnly);
        await _persistence.SaveStartedStudySessionAsync(session, cancellationToken);
        return ToView(session);
    }

    public async Task<StudySessionItemView?> GetNextStudySessionItemAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session.Queue.Count == 0)
        {
            return null;
        }

        var item = await _persistence.FindLearningItemAsync(session.Queue[0], cancellationToken)
            ?? throw new StudyNotFoundException($"Learning Item '{session.Queue[0]}' was not found.");
        return ToItemView(item);
    }

    public async Task<IReadOnlyList<StudyAssessmentPreview>> GetAssessmentPreviewsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var item = await LoadCurrentItemAsync(session, cancellationToken);
        return BuildAssessmentPreviews(session, item, _timeProvider.GetUtcNow());
    }

    public async Task<StudyInteractionStateView> RevealNextHintAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var item = await LoadCurrentItemAsync(session, cancellationToken);
        var state = GetAppearance(sessionId, item.Id);
        if (state.RevealedHintCount < item.Hints.Count) state.RevealedHintCount++;
        return ToInteractionView(sessionId, item.Id, state, item);
    }

    public async Task<StudyInteractionStateView> RevealAssistanceAnswerChoicesAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var item = await LoadCurrentItemAsync(session, cancellationToken);
        var state = GetAppearance(sessionId, item.Id);
        state.AssistanceRevealed = true;
        return ToInteractionView(sessionId, item.Id, state, item);
    }

    public async Task<StudyInteractionStateView> RevealReferenceSolutionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        var item = await LoadCurrentItemAsync(session, cancellationToken);
        var state = GetAppearance(sessionId, item.Id);
        state.ReferenceRevealed = true;
        return ToInteractionView(sessionId, item.Id, state, item);
    }

    public async Task<StudyResponseEvaluationResult> SubmitResponseAsync(SubmitStudyResponseCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var session = await LoadSessionAsync(command.SessionId, cancellationToken);
        var item = await LoadCurrentItemAsync(session, cancellationToken);
        if (item.Id != command.LearningItemId) throw new StudyValidationException("The submitted Learning Item is not the current queue item.");
        var state = GetAppearance(session.Id, item.Id);
        var evaluation = session.EvaluationMode == StudyEvaluationMode.Assisted
            ? EvaluateResponse(item, command)
            : (null, CaptureResponse(item, command));
        state.AutomaticCorrectness = evaluation.Correctness;
        state.SubmittedResponse = evaluation.Response;
        state.SuggestedAssessment = SuggestAssessment(session, item, evaluation.Correctness, state);
        return new(session.Id, item.Id, evaluation.Correctness, state.SuggestedAssessment, evaluation.Response);
    }

    public async Task<StudySessionTransparencyView> GetStudySessionTransparencyAsync(
        Guid sessionId,
        int maxUpcomingEntries = 5,
        CancellationToken cancellationToken = default)
    {
        if (maxUpcomingEntries < 0)
        {
            throw new StudyValidationException("The upcoming-entry limit must not be negative.");
        }

        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session.Queue.Count == 0)
        {
            return new StudySessionTransparencyView(ToView(session), null, 0, [], []);
        }

        var current = await LoadQueueItemAsync(session.Queue[0], cancellationToken);
        var upcomingSnapshots = new List<StudyLearningItemSnapshot>();
        foreach (var id in session.Queue.Skip(1).Take(maxUpcomingEntries))
        {
            upcomingSnapshots.Add(await LoadQueueItemAsync(id, cancellationToken));
        }

        return new StudySessionTransparencyView(
            ToView(session),
            ToQueueItemView(current),
            session.Queue.Count - 1,
            upcomingSnapshots.Select(ToQueueItemView).ToArray(),
            BuildAssessmentPreviews(session, current, _timeProvider.GetUtcNow()));
    }

    public async Task<StudyReviewResult> SubmitReviewAsync(SubmitStudyReviewCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var session = await LoadSessionAsync(command.SessionId, cancellationToken);
        if (session.CompletedAt is not null)
        {
            throw new StudyValidationException("A completed Study Session cannot accept Reviews.");
        }

        if (session.Queue.Count == 0)
        {
            throw new StudyValidationException("The Study Session has no current queue item.");
        }

        if (session.Queue[0] != command.LearningItemId)
        {
            throw new StudyValidationException("The submitted Learning Item is not the current queue item.");
        }

        var snapshot = await _persistence.FindLearningItemAsync(command.LearningItemId, cancellationToken)
            ?? throw new StudyNotFoundException($"Learning Item '{command.LearningItemId}' was not found.");
        var item = ToDomain(snapshot);
        var completedAt = _timeProvider.GetUtcNow();
        var assessment = ToDomain(command.Assessment);
        var appearance = GetAppearance(session.Id, item.Id.Value);
        var automaticCorrectness = appearance.AutomaticCorrectness;
        var suggestedAssessment = appearance.SuggestedAssessment;
        var submittedResponse = appearance.SubmittedResponse;
        var review = item.CompleteReview(completedAt, assessment, submittedResponse, automaticCorrectness, suggestedAssessment is { } suggested ? ToDomain(suggested) : null, appearance.RevealedHintCount, appearance.AssistanceRevealed, appearance.ReferenceRevealed);
        var updatedQueue = session.Queue.Skip(1).ToList();
        switch (assessment)
        {
            case LearningAssessment.Nochmal:
                InsertReinforcementItem(updatedQueue, command.LearningItemId, 1);
                break;
            case LearningAssessment.Schwer:
                InsertReinforcementItem(updatedQueue, command.LearningItemId, 5);
                break;
            case LearningAssessment.Unsicher:
                updatedQueue.Add(command.LearningItemId);
                break;
            case LearningAssessment.Gut:
            case LearningAssessment.Leicht:
                break;
            default:
                throw new StudyValidationException("Unsupported Study Learning Assessment.");
        }

        var updatedSession = session with
        {
            Queue = updatedQueue,
            ReviewIds = session.ReviewIds.Append(review.Id.Value).ToArray(),
        };
        var updatedItem = ToSnapshot(item, snapshot.DeckIds);
        var reviewSnapshot = new StudyReviewSnapshot(review.Id.Value, review.LearningItemId.Value, review.CompletedAt, ToApplication(review.Assessment), review.SubmittedResponse, review.AutomaticCorrectness, review.SuggestedAssessment is { } acceptedSuggestion ? ToApplication(acceptedSuggestion) : null, review.HintCount, review.AssistanceAnswerChoicesRevealed, review.ReferenceSolutionRevealed);
        await _persistence.CommitReviewAsync(updatedItem, reviewSnapshot, updatedSession, cancellationToken);
        _appearances.Remove((session.Id, item.Id.Value));
        return new StudyReviewResult(review.Id.Value, review.LearningItemId.Value, review.CompletedAt, ToView(updatedSession));
    }

    public async Task<StudySessionView> CompleteStudySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(sessionId, cancellationToken);
        if (session.CompletedAt is not null)
        {
            throw new StudyValidationException("The Study Session is already completed.");
        }

        var completed = session with { CompletedAt = _timeProvider.GetUtcNow() };
        await _persistence.CompleteStudySessionAsync(completed, cancellationToken);
        return ToView(completed);
    }

    private async Task<StudySessionSnapshot> LoadSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await _persistence.FindStudySessionAsync(sessionId, cancellationToken)
        ?? throw new StudyNotFoundException($"Study Session '{sessionId}' was not found.");

    private async Task<StudyLearningItemSnapshot> LoadCurrentItemAsync(StudySessionSnapshot session, CancellationToken cancellationToken)
    {
        if (session.Queue.Count == 0)
        {
            throw new StudyValidationException("The Study Session has no current queue item.");
        }

        return await LoadQueueItemAsync(session.Queue[0], cancellationToken);
    }

    private async Task<StudyLearningItemSnapshot> LoadQueueItemAsync(Guid id, CancellationToken cancellationToken) =>
        await _persistence.FindLearningItemAsync(id, cancellationToken)
        ?? throw new StudyNotFoundException($"Learning Item '{id}' was not found.");

    private IReadOnlyList<Guid> Order(IReadOnlyList<Guid> ids)
    {
        var ordered = _ordering.Order(ids);
        if (ordered.Count != ids.Count || ordered.Distinct().Count() != ids.Count || ordered.Any(id => !ids.Contains(id)))
        {
            throw new StudyValidationException("The Study Session ordering returned an invalid item set.");
        }

        return ordered;
    }

    private static void InsertReinforcementItem(List<Guid> queue, Guid learningItemId, int target)
    {
        queue.Insert(queue.Count >= target ? target : queue.Count, learningItemId);
    }

    private static IReadOnlyList<StudyAssessmentPreview> BuildAssessmentPreviews(
        StudySessionSnapshot session,
        StudyLearningItemSnapshot snapshot,
        DateTimeOffset completedAt)
    {
        var item = ToDomain(snapshot);
        var remainingCount = session.Queue.Count - 1;
        return Enum.GetValues<StudyLearningAssessment>()
            .Select(assessment =>
            {
                var projection = item.PreviewReview(completedAt, ToDomain(assessment));
                var graduates = assessment is StudyLearningAssessment.Gut or StudyLearningAssessment.Leicht;
                if (graduates)
                {
                    return new StudyAssessmentPreview(assessment, false, true, null, null, projection.DueAt);
                }

                var intervening = assessment switch
                {
                    StudyLearningAssessment.Nochmal => Math.Min(1, remainingCount),
                    StudyLearningAssessment.Schwer => Math.Min(5, remainingCount),
                    StudyLearningAssessment.Unsicher => remainingCount,
                    _ => throw new StudyValidationException("Unsupported Study Learning Assessment."),
                };
                return new StudyAssessmentPreview(assessment, true, false, intervening, intervening, null);
            })
            .ToArray();
    }

    private static bool IsActive(StudyLearningItemSnapshot item) => item.Lifecycle switch
    {
        LearningItemLifecycle.Active => true,
        LearningItemLifecycle.Suspended or LearningItemLifecycle.Mastered => false,
        _ => throw new StudyValidationException("Unsupported Learning Item lifecycle."),
    };

    private static IReadOnlyList<Guid> ValidateSelectedDecks(IReadOnlyList<Guid>? deckIds)
    {
        if (deckIds is null || deckIds.Count == 0)
        {
            throw new StudyValidationException("At least one Deck must be selected.");
        }

        var distinct = deckIds.Distinct().ToArray();
        if (distinct.Any(id => id == Guid.Empty))
        {
            throw new StudyValidationException("Selected Deck IDs must not be empty.");
        }

        return distinct;
    }

    private static int? ValidateNewItemOptions(int? newItemLimit, bool allNew)
    {
        if (allNew && newItemLimit is not null)
        {
            throw new StudyValidationException("All-new mode cannot be combined with a new-item limit.");
        }

        if (newItemLimit is <= 0)
        {
            throw new StudyValidationException("The new-item limit must be positive.");
        }

        return newItemLimit ?? DefaultNewItemLimit;
    }

    private static StudySessionView ToView(StudySessionSnapshot session) => new(session.Id, session.StartedAt, session.CompletedAt, session.SelectedDeckIds, session.Queue, session.ReviewIds);

    private static StudySessionItemView ToItemView(StudyLearningItemSnapshot item) => new(
        item.Id, item.Prompt, item.ReferenceSolution, item.ResponseMode,
        item.DirectAnswerChoices.Select((choice, index) => new StudyAnswerChoiceView(ChoiceId(choice, index, "choice"), choice.Text)).ToArray(),
        item.AssistanceAnswerChoices.Select((choice, index) => new StudyAnswerChoiceView(ChoiceId(choice, index, "assistance"), choice.Text)).ToArray(),
        item.Hints.Select(x => x.Text).ToArray(), item.AcceptedShortAnswers, item.LowInteractionEligible);

    private AppearanceState GetAppearance(Guid sessionId, Guid itemId) => _appearances.TryGetValue((sessionId, itemId), out var state) ? state : (_appearances[(sessionId, itemId)] = new AppearanceState());

    private static StudyInteractionStateView ToInteractionView(Guid sessionId, Guid itemId, AppearanceState state, StudyLearningItemSnapshot item) => new(sessionId, itemId, item.Hints.Take(state.RevealedHintCount).Select(x => x.Text).ToArray(), state.AssistanceRevealed, state.ReferenceRevealed, state.SubmittedResponse, state.AssistanceRevealed ? item.AssistanceAnswerChoices.Select((choice, index) => new StudyAnswerChoiceView(ChoiceId(choice, index, "assistance"), choice.Text)).ToArray() : null, state.ReferenceRevealed ? item.ReferenceSolution : null);

    private static (bool? Correctness, string? Response) EvaluateResponse(StudyLearningItemSnapshot item, SubmitStudyResponseCommand command)
    {
        return item.ResponseMode switch
        {
            LearningItemResponseMode.Selection => (command.SelectedChoiceIds is not null && command.SelectedChoiceIds.ToHashSet(StringComparer.Ordinal).SetEquals(item.DirectAnswerChoices.Select((choice, index) => (choice, index)).Where(x => x.choice.IsCorrect).Select(x => ChoiceId(x.choice, x.index, "choice"))), string.Join(",", command.SelectedChoiceIds ?? [])),
            LearningItemResponseMode.ShortText => (command.ShortTextResponse is not null && item.AcceptedShortAnswers.Any(answer => string.Equals(NormalizeShortText(answer), NormalizeShortText(command.ShortTextResponse), StringComparison.OrdinalIgnoreCase)), command.ShortTextResponse),
            LearningItemResponseMode.Code => (null, command.CodeResponse),
            _ => (null, command.ShortTextResponse ?? command.CodeResponse),
        };
    }

    private static string? CaptureResponse(StudyLearningItemSnapshot item, SubmitStudyResponseCommand command) => item.ResponseMode switch
    {
        LearningItemResponseMode.Selection => string.Join(",", command.SelectedChoiceIds ?? []),
        LearningItemResponseMode.ShortText => command.ShortTextResponse,
        LearningItemResponseMode.Code => command.CodeResponse,
        _ => command.ShortTextResponse ?? command.CodeResponse,
    };

    private static string NormalizeShortText(string value) => value.Trim().Normalize(NormalizationForm.FormC);

    private static string ChoiceId(AnswerChoiceSnapshot choice, int index, string fallbackPrefix) => string.IsNullOrWhiteSpace(choice.Id) ? $"{fallbackPrefix}-{index}" : choice.Id;

    private static StudyLearningAssessment? SuggestAssessment(StudySessionSnapshot session, StudyLearningItemSnapshot item, bool? correctness, AppearanceState state) => session.EvaluationMode == StudyEvaluationMode.Assisted && item.ResponseMode is LearningItemResponseMode.Selection or LearningItemResponseMode.ShortText && correctness is not null ? correctness.Value && session.ConsiderAssistance && (state.RevealedHintCount > 0 || state.AssistanceRevealed) ? StudyLearningAssessment.Unsicher : correctness.Value ? StudyLearningAssessment.Gut : StudyLearningAssessment.Schwer : null;

    private sealed class AppearanceState
    {
        public int RevealedHintCount { get; set; }
        public bool AssistanceRevealed { get; set; }
        public bool ReferenceRevealed { get; set; }
        public bool? AutomaticCorrectness { get; set; }
        public StudyLearningAssessment? SuggestedAssessment { get; set; }
        public string? SubmittedResponse { get; set; }
    }

    private static StudySessionQueueItemView ToQueueItemView(StudyLearningItemSnapshot snapshot) => new(snapshot.Id, snapshot.Prompt, snapshot.IsInShortTermRelearning);

    private static StudyLearningItemSnapshot ToSnapshot(LearningItem item, IReadOnlyList<Guid> deckIds) => new(
        item.Id.Value, item.Prompt, item.ReferenceSolution.Content, ToApplication(item.ResponseMode),
        item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(),
        item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(),
        item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(),
        item.AcceptedShortAnswers.ToArray(), item.LowInteractionEligible, ToApplication(item.LifecycleState),
        item.LearningState.IsNew, item.LearningState.DueAt, item.LearningState.Difficulty,
        item.LearningState.StabilityDays, item.LearningState.IsInShortTermRelearning, deckIds);

    private static LearningItem ToDomain(StudyLearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id), snapshot.Prompt, snapshot.ReferenceSolution, snapshot.DueAt,
        snapshot.IsNew, ToDomain(snapshot.ResponseMode), snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select((x, index) => new AnswerChoice(x.Text, x.IsCorrect, ChoiceId(x, index, "choice"))),
        snapshot.AssistanceAnswerChoices.Select((x, index) => new AnswerChoice(x.Text, x.IsCorrect, ChoiceId(x, index, "assistance"))), snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible, ToDomain(snapshot.Lifecycle), snapshot.Difficulty, snapshot.StabilityDays,
        snapshot.IsInShortTermRelearning);

    private static ResponseMode ToDomain(LearningItemResponseMode mode) => mode switch
    {
        LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed,
        LearningItemResponseMode.Selection => ResponseMode.Selection,
        LearningItemResponseMode.ShortText => ResponseMode.ShortText,
        LearningItemResponseMode.Code => ResponseMode.Code,
        _ => throw new StudyValidationException("Unsupported Learning Item response mode."),
    };

    private static LearningItemLifecycleState ToDomain(LearningItemLifecycle lifecycle) => lifecycle switch
    {
        LearningItemLifecycle.Active => LearningItemLifecycleState.Active,
        LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended,
        LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered,
        _ => throw new StudyValidationException("Unsupported Learning Item lifecycle."),
    };

    private static LearningItemResponseMode ToApplication(ResponseMode mode) => mode switch
    {
        ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed,
        ResponseMode.Selection => LearningItemResponseMode.Selection,
        ResponseMode.ShortText => LearningItemResponseMode.ShortText,
        ResponseMode.Code => LearningItemResponseMode.Code,
        _ => throw new StudyValidationException("Unsupported Domain response mode."),
    };

    private static LearningItemLifecycle ToApplication(LearningItemLifecycleState lifecycle) => lifecycle switch
    {
        LearningItemLifecycleState.Active => LearningItemLifecycle.Active,
        LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended,
        LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered,
        _ => throw new StudyValidationException("Unsupported Domain lifecycle."),
    };

    private static LearningAssessment ToDomain(StudyLearningAssessment assessment) => assessment switch
    {
        StudyLearningAssessment.Nochmal => LearningAssessment.Nochmal,
        StudyLearningAssessment.Schwer => LearningAssessment.Schwer,
        StudyLearningAssessment.Unsicher => LearningAssessment.Unsicher,
        StudyLearningAssessment.Gut => LearningAssessment.Gut,
        StudyLearningAssessment.Leicht => LearningAssessment.Leicht,
        _ => throw new StudyValidationException("Unsupported Study Learning Assessment."),
    };

    private static StudyLearningAssessment ToApplication(LearningAssessment assessment) => assessment switch
    {
        LearningAssessment.Nochmal => StudyLearningAssessment.Nochmal,
        LearningAssessment.Schwer => StudyLearningAssessment.Schwer,
        LearningAssessment.Unsicher => StudyLearningAssessment.Unsicher,
        LearningAssessment.Gut => StudyLearningAssessment.Gut,
        LearningAssessment.Leicht => StudyLearningAssessment.Leicht,
        _ => throw new StudyValidationException("Unsupported Domain Learning Assessment."),
    };
}
