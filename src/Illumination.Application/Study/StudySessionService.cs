using Illumination.Application.ContentManagement;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;

namespace Illumination.Application.Study;

public sealed class StudySessionService
{
    private const int DefaultNewItemLimit = 20;

    private readonly IStudySessionPersistence _persistence;
    private readonly TimeProvider _timeProvider;
    private readonly IStudySessionOrdering _ordering;

    public StudySessionService(IStudySessionPersistence persistence, TimeProvider timeProvider, IStudySessionOrdering ordering)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _ordering = ordering ?? throw new ArgumentNullException(nameof(ordering));
    }

    public async Task<StudySessionView> StartStudySessionAsync(StartStudySessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
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
        var eligible = items.Where(IsActive).ToArray();
        var queue = new List<Guid>();
        queue.AddRange(Order(eligible.Where(item => item.IsInShortTermRelearning).Select(item => item.Id).ToArray()));
        queue.AddRange(Order(eligible.Where(item => !item.IsInShortTermRelearning && !item.IsNew && item.DueAt <= sessionStart).Select(item => item.Id).ToArray()));
        var newItems = Order(eligible.Where(item => item.IsNew).Select(item => item.Id).ToArray());
        queue.AddRange(command.AllNew ? newItems : newItems.Take(newItemLimit!.Value));

        var session = new StudySessionSnapshot(Guid.NewGuid(), sessionStart, null, selectedDeckIds, queue, []);
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
        return new StudySessionItemView(item.Id, item.Prompt, item.ReferenceSolution);
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
        var review = item.CompleteReview(completedAt, ToDomain(command.Assessment), command.SubmittedResponse);
        var updatedQueue = session.Queue.Where(id => id != command.LearningItemId).ToList();
        if (item.LearningState.IsInShortTermRelearning)
        {
            InsertRelearningItem(updatedQueue, command.LearningItemId, item.LearningState.InterveningCardTarget!.Value);
        }

        var updatedSession = session with
        {
            Queue = updatedQueue,
            ReviewIds = session.ReviewIds.Append(review.Id.Value).ToArray(),
        };
        var updatedItem = ToSnapshot(item, snapshot.DeckIds);
        var reviewSnapshot = new StudyReviewSnapshot(review.Id.Value, review.LearningItemId.Value, review.CompletedAt, ToApplication(review.Assessment), review.SubmittedResponse);
        await _persistence.CommitReviewAsync(updatedItem, reviewSnapshot, updatedSession, cancellationToken);
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

    private IReadOnlyList<Guid> Order(IReadOnlyList<Guid> ids)
    {
        var ordered = _ordering.Order(ids);
        if (ordered.Count != ids.Count || ordered.Distinct().Count() != ids.Count || ordered.Any(id => !ids.Contains(id)))
        {
            throw new StudyValidationException("The Study Session ordering returned an invalid item set.");
        }

        return ordered;
    }

    private static void InsertRelearningItem(List<Guid> queue, Guid learningItemId, int target)
    {
        queue.RemoveAll(id => id == learningItemId);
        if (queue.Count == 0)
        {
            return;
        }

        queue.Insert(queue.Count >= target ? target : queue.Count, learningItemId);
    }

    private static bool IsActive(StudyLearningItemSnapshot item) => item.Lifecycle == LearningItemLifecycle.Active;

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

    private static StudyLearningItemSnapshot ToSnapshot(LearningItem item, IReadOnlyList<Guid> deckIds) => new(
        item.Id.Value, item.Prompt, item.ReferenceSolution.Content, ToApplication(item.ResponseMode),
        item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(),
        item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        item.AcceptedShortAnswers.ToArray(), item.LowInteractionEligible, ToApplication(item.LifecycleState),
        item.LearningState.IsNew, item.LearningState.DueAt, item.LearningState.Difficulty,
        item.LearningState.StabilityDays, item.LearningState.IsInShortTermRelearning,
        item.LearningState.InterveningCardTarget, deckIds);

    private static LearningItem ToDomain(StudyLearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id), snapshot.Prompt, snapshot.ReferenceSolution, snapshot.DueAt,
        snapshot.IsNew, ToDomain(snapshot.ResponseMode), snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)), snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible, ToDomain(snapshot.Lifecycle), snapshot.Difficulty, snapshot.StabilityDays,
        snapshot.IsInShortTermRelearning, snapshot.InterveningCardTarget);

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
