using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed class LearningItem
{
    private readonly List<Hint> _hints;
    private List<AnswerChoice> _directAnswerChoices;
    private List<AnswerChoice> _assistanceAnswerChoices;
    private List<string> _acceptedShortAnswers;
    private readonly List<QualityReview> _qualityReviews;
    private readonly HashSet<UserFlagDefinitionId> _userFlagDefinitionIds;

    private LearningItem(
        LearningItemId id,
        string prompt,
        ReferenceSolution referenceSolution,
        DateTimeOffset initialDueAt,
        ResponseMode responseMode,
        IEnumerable<Hint>? hints,
        IEnumerable<AnswerChoice>? directAnswerChoices,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices,
        IEnumerable<string>? acceptedShortAnswers,
        bool lowInteractionEligible,
        LearningItemLifecycleState lifecycleState = LearningItemLifecycleState.Active,
        bool isNew = true,
        double difficulty = 5.0,
        double stabilityDays = 0.5,
        bool isInShortTermRelearning = false,
        int contentRevision = 1,
        IEnumerable<QualityReview>? qualityReviews = null,
        IEnumerable<UserFlagDefinitionId>? userFlagDefinitionIds = null)
    {
        DomainText.RequireNonWhitespace(prompt, nameof(prompt));
        if (contentRevision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(contentRevision), "Content revision must be positive.");
        }

        Id = id;
        Prompt = prompt;
        ReferenceSolution = referenceSolution ?? throw new ArgumentNullException(nameof(referenceSolution));
        _hints = CopyHints(hints);
        _directAnswerChoices = CopyAnswerChoices(directAnswerChoices);
        _assistanceAnswerChoices = CopyAnswerChoices(assistanceAnswerChoices);
        _acceptedShortAnswers = CopyAcceptedShortAnswers(acceptedShortAnswers);
        ValidateInteractionConfiguration(responseMode, _directAnswerChoices, _assistanceAnswerChoices, _acceptedShortAnswers);

        ResponseMode = responseMode;
        LowInteractionEligible = lowInteractionEligible;
        LifecycleState = lifecycleState;
        LearningState = new LearningState(isNew, initialDueAt, difficulty, stabilityDays, isInShortTermRelearning);
        ContentRevision = contentRevision;
        _qualityReviews = qualityReviews?.ToList() ?? [];
        _userFlagDefinitionIds = userFlagDefinitionIds is null ? [] : [.. userFlagDefinitionIds];

        if (_qualityReviews.Any(review => review is null || review.LearningItemId != id))
        {
            throw new ArgumentException("Quality Reviews must belong to the Learning Item.", nameof(qualityReviews));
        }
    }

    public LearningItemId Id { get; }

    public string Prompt { get; private set; }

    public ReferenceSolution ReferenceSolution { get; private set; }

    public IReadOnlyList<Hint> Hints => _hints.AsReadOnly();

    public ResponseMode ResponseMode { get; private set; }

    public IReadOnlyList<AnswerChoice> DirectAnswerChoices => _directAnswerChoices.AsReadOnly();

    public IReadOnlyList<AnswerChoice> AssistanceAnswerChoices => _assistanceAnswerChoices.AsReadOnly();

    public IReadOnlyList<string> AcceptedShortAnswers => _acceptedShortAnswers.AsReadOnly();

    public bool LowInteractionEligible { get; private set; }

    public LearningItemLifecycleState LifecycleState { get; private set; }

    public LearningState LearningState { get; }

    public int ContentRevision { get; private set; }

    public IReadOnlyList<QualityReview> QualityReviews => _qualityReviews.AsReadOnly();

    public IReadOnlySet<UserFlagDefinitionId> UserFlagDefinitionIds => _userFlagDefinitionIds;

    public CurrentQualityState? CurrentQualityState => DeriveCurrentQualityState();

    public static LearningItem Create(
        string prompt,
        string referenceSolution,
        DateTimeOffset initialDueAt,
        ResponseMode responseMode = ResponseMode.SelfAssessed,
        IEnumerable<Hint>? hints = null,
        IEnumerable<AnswerChoice>? directAnswerChoices = null,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices = null,
        IEnumerable<string>? acceptedShortAnswers = null,
        bool lowInteractionEligible = false)
    {
        return Create(
            LearningItemId.New(),
            prompt,
            referenceSolution,
            initialDueAt,
            responseMode,
            hints,
            directAnswerChoices,
            assistanceAnswerChoices,
            acceptedShortAnswers,
            lowInteractionEligible);
    }

    public static LearningItem Restore(
        LearningItemId id,
        string prompt,
        string referenceSolution,
        DateTimeOffset dueAt,
        bool isNew,
        ResponseMode responseMode,
        IEnumerable<Hint>? hints,
        IEnumerable<AnswerChoice>? directAnswerChoices,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices,
        IEnumerable<string>? acceptedShortAnswers,
        bool lowInteractionEligible,
        LearningItemLifecycleState lifecycleState)
    {
        return Restore(
            id, prompt, referenceSolution, dueAt, isNew, responseMode, hints, directAnswerChoices,
            assistanceAnswerChoices, acceptedShortAnswers, lowInteractionEligible, lifecycleState,
            difficulty: 5.0, stabilityDays: 0.5, isInShortTermRelearning: false);
    }

    public static LearningItem Restore(
        LearningItemId id,
        string prompt,
        string referenceSolution,
        DateTimeOffset dueAt,
        bool isNew,
        ResponseMode responseMode,
        IEnumerable<Hint>? hints,
        IEnumerable<AnswerChoice>? directAnswerChoices,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices,
        IEnumerable<string>? acceptedShortAnswers,
        bool lowInteractionEligible,
        LearningItemLifecycleState lifecycleState,
        double difficulty,
        double stabilityDays,
        bool isInShortTermRelearning,
        int contentRevision = 1,
        IEnumerable<QualityReview>? qualityReviews = null,
        IEnumerable<UserFlagDefinitionId>? userFlagDefinitionIds = null)
    {
        return new LearningItem(
            id, prompt, new ReferenceSolution(referenceSolution), dueAt, responseMode,
            hints, directAnswerChoices, assistanceAnswerChoices, acceptedShortAnswers,
            lowInteractionEligible, lifecycleState, isNew, difficulty, stabilityDays,
            isInShortTermRelearning, contentRevision, qualityReviews, userFlagDefinitionIds);
    }

    public static LearningItem Create(
        LearningItemId id,
        string prompt,
        string referenceSolution,
        DateTimeOffset initialDueAt,
        ResponseMode responseMode = ResponseMode.SelfAssessed,
        IEnumerable<Hint>? hints = null,
        IEnumerable<AnswerChoice>? directAnswerChoices = null,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices = null,
        IEnumerable<string>? acceptedShortAnswers = null,
        bool lowInteractionEligible = false)
    {
        return new LearningItem(
            id,
            prompt,
            new ReferenceSolution(referenceSolution),
            initialDueAt,
            responseMode,
            hints,
            directAnswerChoices,
            assistanceAnswerChoices,
            acceptedShortAnswers,
            lowInteractionEligible);
    }

    public void ChangeLowInteractionEligibility(bool lowInteractionEligible)
    {
        LowInteractionEligible = lowInteractionEligible;
    }

    public bool UpdateContent(
        string prompt,
        string referenceSolution,
        ResponseMode responseMode,
        IEnumerable<Hint>? hints = null,
        IEnumerable<AnswerChoice>? directAnswerChoices = null,
        IEnumerable<AnswerChoice>? assistanceAnswerChoices = null,
        IEnumerable<string>? acceptedShortAnswers = null)
    {
        DomainText.RequireNonWhitespace(prompt, nameof(prompt));
        var newReferenceSolution = new ReferenceSolution(referenceSolution);
        var newHints = CopyHints(hints);
        var newDirectAnswerChoices = CopyAnswerChoices(directAnswerChoices);
        var newAssistanceAnswerChoices = CopyAnswerChoices(assistanceAnswerChoices);
        var newAcceptedShortAnswers = CopyAcceptedShortAnswers(acceptedShortAnswers);
        ValidateInteractionConfiguration(responseMode, newDirectAnswerChoices, newAssistanceAnswerChoices, newAcceptedShortAnswers);

        var changed = Prompt != prompt
            || ReferenceSolution != newReferenceSolution
            || ResponseMode != responseMode
            || !_hints.SequenceEqual(newHints)
            || !_directAnswerChoices.SequenceEqual(newDirectAnswerChoices)
            || !_assistanceAnswerChoices.SequenceEqual(newAssistanceAnswerChoices)
            || !_acceptedShortAnswers.SequenceEqual(newAcceptedShortAnswers, StringComparer.Ordinal);

        if (!changed)
        {
            return false;
        }

        Prompt = prompt;
        ReferenceSolution = newReferenceSolution;
        ResponseMode = responseMode;
        _hints.Clear();
        _hints.AddRange(newHints);
        _directAnswerChoices = newDirectAnswerChoices;
        _assistanceAnswerChoices = newAssistanceAnswerChoices;
        _acceptedShortAnswers = newAcceptedShortAnswers;
        checked { ContentRevision++; }
        return true;
    }

    public void AcceptQualityReview(QualityReview review, IEnumerable<QualityReviewId>? supersededReviewIds = null)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (review.LearningItemId != Id || review.ContentRevision != ContentRevision)
        {
            throw new InvalidOperationException("A Quality Review must target the Learning Item's current content revision.");
        }

        if (_qualityReviews.Any(existing => existing.Id == review.Id))
        {
            throw new InvalidOperationException("The Quality Review has already been accepted.");
        }

        var supersededIds = supersededReviewIds?.Distinct().ToArray() ?? [];
        foreach (var supersededId in supersededIds)
        {
            var existing = _qualityReviews.FirstOrDefault(candidate => candidate.Id == supersededId);
            if (existing is null || existing.LearningItemId != Id || existing.ContentRevision != ContentRevision || existing.IsSuperseded)
            {
                throw new InvalidOperationException("Only non-superseded reviews for this Learning Item and current content revision may be superseded.");
            }
        }

        for (var index = 0; index < _qualityReviews.Count; index++)
        {
            if (supersededIds.Contains(_qualityReviews[index].Id))
            {
                _qualityReviews[index] = _qualityReviews[index].SupersededByReview(review.Id);
            }
        }

        _qualityReviews.Add(review);
    }

    public void AddUserFlag(UserFlagDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _userFlagDefinitionIds.Add(definition.Id);
    }

    public void AddUserFlag(UserFlagDefinitionId definitionId)
    {
        if (definitionId.Value == Guid.Empty)
        {
            throw new ArgumentException("A User Flag Definition ID must not be empty.", nameof(definitionId));
        }

        _userFlagDefinitionIds.Add(definitionId);
    }

    public bool RemoveUserFlag(UserFlagDefinitionId definitionId) => _userFlagDefinitionIds.Remove(definitionId);

    public bool HasUserFlag(UserFlagDefinitionId definitionId) => _userFlagDefinitionIds.Contains(definitionId);

    public void ResetSchedulingForSemanticContentChange(DateTimeOffset resetAt)
    {
        LearningState.ResetForSemanticContentChange(resetAt);
    }

    public void Suspend()
    {
        RequireLifecycleState(LearningItemLifecycleState.Active);
        LifecycleState = LearningItemLifecycleState.Suspended;
    }

    public void Reactivate(DateTimeOffset dueAt)
    {
        if (LifecycleState != LearningItemLifecycleState.Suspended)
        {
            throw new InvalidOperationException("Only Suspended Learning Items can be reactivated.");
        }

        LifecycleState = LearningItemLifecycleState.Active;
        LearningState.MarkImmediatelyDue(dueAt);
    }

    public void MarkMastered()
    {
        RequireLifecycleState(LearningItemLifecycleState.Active);
        LifecycleState = LearningItemLifecycleState.Mastered;
    }

    public void UnmarkMastered(DateTimeOffset dueAt)
    {
        if (LifecycleState != LearningItemLifecycleState.Mastered)
        {
            throw new InvalidOperationException("Only Mastered Learning Items can be unmarked.");
        }

        LifecycleState = LearningItemLifecycleState.Active;
        LearningState.MarkImmediatelyDue(dueAt);
    }

    public Review CompleteReview(
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse = null)
    {
        RequireLifecycleState(LearningItemLifecycleState.Active);
        var review = Review.Create(Id, completedAt, assessment, submittedResponse);
        LearningState.ApplyReview(completedAt, assessment);
        return review;
    }

    public LearningStateProjection PreviewReview(DateTimeOffset completedAt, LearningAssessment assessment)
    {
        RequireLifecycleState(LearningItemLifecycleState.Active);
        return LearningState.ProjectReview(completedAt, assessment);
    }

    private void RequireLifecycleState(LearningItemLifecycleState expected)
    {
        if (LifecycleState != expected)
        {
            throw new InvalidOperationException($"The Learning Item must be {expected} for this operation.");
        }
    }

    private static List<Hint> CopyHints(IEnumerable<Hint>? hints)
    {
        return hints?.Select(hint => hint ?? throw new ArgumentException("Hints must not contain null values.", nameof(hints))).ToList()
            ?? [];
    }

    private static List<AnswerChoice> CopyAnswerChoices(IEnumerable<AnswerChoice>? choices)
    {
        return choices?.Select(choice => choice ?? throw new ArgumentException("Answer choices must not contain null values.", nameof(choices))).ToList()
            ?? [];
    }

    private static List<string> CopyAcceptedShortAnswers(IEnumerable<string>? acceptedShortAnswers)
    {
        var values = acceptedShortAnswers?.ToList() ?? [];
        foreach (var value in values)
        {
            DomainText.RequireNonWhitespace(value, nameof(acceptedShortAnswers));
        }

        return values;
    }

    private static void ValidateInteractionConfiguration(
        ResponseMode responseMode,
        IReadOnlyCollection<AnswerChoice> directAnswerChoices,
        IReadOnlyCollection<AnswerChoice> assistanceAnswerChoices,
        IReadOnlyCollection<string> acceptedShortAnswers)
    {
        if (assistanceAnswerChoices.Count == 1)
        {
            throw new ArgumentException("Assistance answer choices require at least two choices.", nameof(assistanceAnswerChoices));
        }

        if (responseMode != ResponseMode.Selection && directAnswerChoices.Count > 0)
        {
            throw new ArgumentException("Direct answer choices are only valid for Selection mode.", nameof(directAnswerChoices));
        }

        if (responseMode == ResponseMode.Selection)
        {
            if (directAnswerChoices.Count < 2)
            {
                throw new ArgumentException("Selection mode requires at least two direct answer choices.", nameof(directAnswerChoices));
            }

            if (!directAnswerChoices.Any(choice => choice.IsCorrect))
            {
                throw new ArgumentException("Selection mode requires at least one correct direct answer choice.", nameof(directAnswerChoices));
            }
        }

        if (responseMode != ResponseMode.ShortText && acceptedShortAnswers.Count > 0)
        {
            throw new ArgumentException("Accepted short answers are only valid for ShortText mode.", nameof(acceptedShortAnswers));
        }

        if (responseMode == ResponseMode.ShortText && acceptedShortAnswers.Count == 0)
        {
            throw new ArgumentException("ShortText mode requires at least one accepted short answer.", nameof(acceptedShortAnswers));
        }
    }

    private CurrentQualityState? DeriveCurrentQualityState()
    {
        var currentReviews = _qualityReviews
            .Where(review => review.ContentRevision == ContentRevision && !review.IsSuperseded)
            .ToArray();
        if (currentReviews.Length == 0)
        {
            return null;
        }

        var outcome = currentReviews.MaxBy(candidate => OutcomePriority(candidate.Outcome))!.Outcome;
        return new CurrentQualityState(outcome);
    }

    private static int OutcomePriority(QualityReviewOutcome outcome) => outcome switch
    {
        QualityReviewOutcome.NeedsReview => 3,
        QualityReviewOutcome.Warning => 2,
        QualityReviewOutcome.Pass => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported quality review outcome."),
    };
}
