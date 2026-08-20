namespace Illumination.Application.ContentManagement;

public interface IContentPersistence
{
    Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default);

    Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default);

    Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default);

    Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default);

    Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default);

}

public interface ILearningStateBatchPersistence
{
    Task SaveLearningStatesAtomicallyAsync(
        IReadOnlyList<LearningStateMaintenanceSnapshot> states,
        CancellationToken cancellationToken = default);
}

public interface IUserFlagDefinitionPersistence
{
    Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default);
    Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default);
}

public sealed record LearningItemSnapshot(
    Guid Id,
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode,
    IReadOnlyList<HintSnapshot> Hints,
    IReadOnlyList<AnswerChoiceSnapshot> DirectAnswerChoices,
    IReadOnlyList<AnswerChoiceSnapshot> AssistanceAnswerChoices,
    IReadOnlyList<string> AcceptedShortAnswers,
    bool LowInteractionEligible,
    LearningItemLifecycle Lifecycle,
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning,
    IReadOnlyList<Guid> DeckIds,
    int ContentRevision = 1,
    IReadOnlyList<QualityReviewSnapshot>? QualityReviews = null,
    IReadOnlyList<Guid>? UserFlagDefinitionIds = null);

public sealed record LearningStateMaintenanceSnapshot(
    Guid LearningItemId,
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning);

public sealed record HintSnapshot(string Text);

public sealed record AnswerChoiceSnapshot(string Text, bool IsCorrect, string Id = "");

public sealed record DeckSnapshot(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> LearningItemIds,
    IReadOnlyList<string> TopicLabels,
    IReadOnlyList<DeckLearningActivityProfile> LearningActivityProfiles)
{
    public DeckSnapshot(Guid id, string name, IReadOnlyList<Guid> learningItemIds)
        : this(id, name, learningItemIds, [], [])
    {
    }

    public DeckSnapshot(Guid id, string name, IReadOnlyList<Guid> learningItemIds, IReadOnlyList<string> topicLabels)
        : this(id, name, learningItemIds, topicLabels, [])
    {
    }
}

public enum QualityReviewOutcomeSnapshot { Pass, Warning, NeedsReview }

public enum QualityReviewEvidenceTypeSnapshot { ModelReview, SourceGroundedReview, UserReview }

public sealed record QualityReviewSnapshot(
    Guid Id,
    Guid LearningItemId,
    int ContentRevision,
    QualityReviewOutcomeSnapshot Outcome,
    QualityReviewEvidenceTypeSnapshot EvidenceType,
    string Findings,
    string? SuggestedCorrection,
    Guid? SupersededBy);

public sealed record UserFlagDefinitionSnapshot(Guid Id, string Name, string Meaning);