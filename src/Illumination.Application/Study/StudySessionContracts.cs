using Illumination.Application.ContentManagement;

namespace Illumination.Application.Study;

public enum StudyLearningAssessment
{
    Nochmal,
    Schwer,
    Unsicher,
    Gut,
    Leicht,
}

public sealed record StartStudySessionCommand(
    IReadOnlyList<Guid> SelectedDeckIds,
    int? NewItemLimit = null,
    bool AllNew = false);

public sealed record SubmitStudyReviewCommand(
    Guid SessionId,
    Guid LearningItemId,
    StudyLearningAssessment Assessment,
    string? SubmittedResponse = null);

public sealed record StudySessionView(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<Guid> SelectedDeckIds,
    IReadOnlyList<Guid> Queue,
    IReadOnlyList<Guid> ReviewIds);

public sealed record StudySessionItemView(
    Guid Id,
    string Prompt,
    string ReferenceSolution);

public sealed record StudyReviewResult(
    Guid ReviewId,
    Guid LearningItemId,
    DateTimeOffset CompletedAt,
    StudySessionView Session);

public sealed record StudyAssessmentPreview(
    StudyLearningAssessment Assessment,
    bool RemainsInSession,
    bool Graduates,
    int? ProjectedInterveningEntryCount,
    int? ProjectedQueuePosition,
    DateTimeOffset? ProjectedDueAt);

public sealed record StudySessionQueueItemView(
    Guid Id,
    string Prompt,
    bool ReinforcementRequired);

public sealed record StudySessionTransparencyView(
    StudySessionView Session,
    StudySessionQueueItemView? CurrentItem,
    int RemainingQueueEntryCount,
    IReadOnlyList<StudySessionQueueItemView> UpcomingItems,
    IReadOnlyList<StudyAssessmentPreview> AssessmentPreviews);

public sealed record StudyDeckSnapshot(Guid Id, IReadOnlyList<Guid> LearningItemIds);

public sealed record StudyLearningItemSnapshot(
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
    IReadOnlyList<Guid> DeckIds);

public sealed record StudyReviewSnapshot(
    Guid Id,
    Guid LearningItemId,
    DateTimeOffset CompletedAt,
    StudyLearningAssessment Assessment,
    string? SubmittedResponse);

public sealed record StudySessionSnapshot(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<Guid> SelectedDeckIds,
    IReadOnlyList<Guid> Queue,
    IReadOnlyList<Guid> ReviewIds);

public sealed class StudyNotFoundException : Exception
{
    public StudyNotFoundException(string message) : base(message) { }
}

public sealed class StudyValidationException : Exception
{
    public StudyValidationException(string message, Exception? innerException = null) : base(message, innerException) { }
}
