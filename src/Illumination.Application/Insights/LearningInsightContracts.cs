using Illumination.Application.ContentManagement;
using Illumination.Application.Study;

namespace Illumination.Application.Insights;

public sealed record AssessmentDistribution(int Nochmal, int Schwer, int Unsicher, int Gut, int Leicht)
{
    public int Total => Nochmal + Schwer + Unsicher + Gut + Leicht;

    public static AssessmentDistribution Empty { get; } = new(0, 0, 0, 0, 0);
}

public sealed record LearningInsightOverview(
    int TotalLearningItems,
    int ActiveCount,
    int SuspendedCount,
    int MasteredCount,
    int NewItemCount,
    int DueNowCount,
    int ShortTermRelearningCount,
    int TotalCompletedReviewCount,
    int ReviewsLast7Days,
    int ReviewsLast30Days,
    DateTimeOffset? MostRecentReviewAt);

public sealed record DeckInsight(
    Guid Id,
    string Name,
    int CurrentItemCount,
    int ActiveCount,
    int SuspendedCount,
    int MasteredCount,
    int NewCount,
    int DueNowCount,
    int RelearningCount,
    int TotalReviewCount,
    DateTimeOffset? LastReviewAt,
    AssessmentDistribution AssessmentDistribution);

public sealed record LearningItemInsight(
    Guid LearningItemId,
    string Prompt,
    LearningItemResponseMode ResponseMode,
    LearningItemLifecycle LifecycleState,
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning,
    int ReviewCount,
    DateTimeOffset? LastReviewAt,
    StudyLearningAssessment? LastConfirmedAssessment,
    AssessmentDistribution AssessmentDistribution);

public sealed record ReviewHistoryEntry(
    Guid ReviewId,
    Guid LearningItemId,
    string Prompt,
    DateTimeOffset CompletedAt,
    StudyLearningAssessment Assessment,
    IReadOnlyList<Guid> StudySessionIds,
    string? SubmittedResponse,
    bool? AutomaticCorrectness,
    StudyLearningAssessment? SuggestedAssessment,
    int HintCount,
    bool AssistanceAnswerChoicesRevealed,
    bool ReferenceSolutionRevealed);

public sealed record StudySessionHistoryEntry(
    Guid SessionId,
    IReadOnlyList<InsightDeckIdentity> SelectedDecks,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    StudyEvaluationMode EvaluationMode,
    bool ConsiderAssistance,
    bool LowInteractionOnly,
    int ReviewCount);

public sealed record InsightDeckIdentity(Guid Id, string Name);

public sealed record LearningItemInsightQuery(
    Guid? DeckId = null,
    string? PromptContains = null,
    LearningItemLifecycle? Lifecycle = null,
    bool NewOnly = false,
    bool DueNowOnly = false,
    bool RelearningOnly = false,
    int? Limit = null);

public sealed record DeckLearningContext(
    Guid DeckId,
    string DeckName,
    IReadOnlyList<DeckLearningContextItem> Items);

public sealed record DeckLearningContextItem(
    Guid LearningItemId,
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode,
    LearningItemLifecycle LifecycleState,
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning,
    int ReviewCount,
    StudyLearningAssessment? LastConfirmedAssessment,
    AssessmentDistribution AssessmentDistribution);

public sealed record LearningInsightItemSnapshot(
    Guid Id,
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode,
    LearningItemLifecycle Lifecycle,
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning,
    IReadOnlyList<Guid> CurrentDeckIds,
    IReadOnlyList<InsightReviewSnapshot> Reviews);

public sealed record InsightDeckSnapshot(Guid Id, string Name, IReadOnlyList<Guid> CurrentLearningItemIds);

public sealed record InsightReviewSnapshot(
    Guid Id,
    Guid LearningItemId,
    DateTimeOffset CompletedAt,
    StudyLearningAssessment Assessment,
    string? SubmittedResponse,
    bool? AutomaticCorrectness,
    StudyLearningAssessment? SuggestedAssessment,
    int HintCount,
    bool AssistanceAnswerChoicesRevealed,
    bool ReferenceSolutionRevealed,
    IReadOnlyList<Guid> StudySessionIds);

public sealed record InsightStudySessionSnapshot(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<InsightDeckIdentity> SelectedDecks,
    IReadOnlyList<Guid> ReviewIds,
    StudyEvaluationMode EvaluationMode,
    bool ConsiderAssistance,
    bool LowInteractionOnly);
