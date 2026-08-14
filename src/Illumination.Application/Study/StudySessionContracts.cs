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

public enum StudyEvaluationMode { Manual, Assisted }
public sealed record StudyAnswerChoiceView(string Id, string Text);

public sealed record StartStudySessionCommand(
    IReadOnlyList<Guid> SelectedDeckIds,
    int? NewItemLimit = null,
    bool AllNew = false,
    StudyEvaluationMode EvaluationMode = StudyEvaluationMode.Manual,
    bool ConsiderAssistance = false,
    bool LowInteractionOnly = false);

public sealed record SubmitStudyReviewCommand(
    Guid SessionId,
    Guid LearningItemId,
    StudyLearningAssessment Assessment);

public sealed record SubmitStudyResponseCommand(Guid SessionId, Guid LearningItemId, IReadOnlyList<string>? SelectedChoiceIds = null, string? ShortTextResponse = null, string? CodeResponse = null);
public sealed record StudyResponseEvaluationResult(Guid SessionId, Guid LearningItemId, bool? AutomaticCorrectness, StudyLearningAssessment? SuggestedAssessment, string? SubmittedResponse);
public sealed record StudyInteractionStateView(Guid SessionId, Guid LearningItemId, IReadOnlyList<string> RevealedHintTexts, bool AssistanceAnswerChoicesRevealed, bool ReferenceSolutionRevealed, string? SubmittedResponse, IReadOnlyList<StudyAnswerChoiceView>? RevealedAssistanceAnswerChoices = null, string? RevealedReferenceSolution = null);

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
    string ReferenceSolution,
    ContentManagement.LearningItemResponseMode ResponseMode = ContentManagement.LearningItemResponseMode.SelfAssessed,
    IReadOnlyList<StudyAnswerChoiceView>? DirectAnswerChoices = null,
    IReadOnlyList<StudyAnswerChoiceView>? AssistanceAnswerChoices = null,
    IReadOnlyList<string>? Hints = null,
    IReadOnlyList<string>? AcceptedShortAnswers = null,
    bool LowInteractionEligible = false);

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
    string? SubmittedResponse,
    bool? AutomaticCorrectness = null,
    StudyLearningAssessment? SuggestedAssessment = null,
    int HintCount = 0,
    bool AssistanceAnswerChoicesRevealed = false,
    bool ReferenceSolutionRevealed = false);

public sealed record StudySessionSnapshot(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<Guid> SelectedDeckIds,
    IReadOnlyList<Guid> Queue,
    IReadOnlyList<Guid> ReviewIds,
    StudyEvaluationMode EvaluationMode = StudyEvaluationMode.Manual,
    bool ConsiderAssistance = false,
    bool LowInteractionOnly = false);

public sealed class StudyNotFoundException : Exception
{
    public StudyNotFoundException(string message) : base(message) { }
}

public sealed class StudyValidationException : Exception
{
    public StudyValidationException(string message, Exception? innerException = null) : base(message, innerException) { }
}
