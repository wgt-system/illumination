namespace Illumination.Application.ContentManagement;

public enum CurationQualityReviewOutcome
{
    Pass,
    Warning,
    NeedsReview,
}

public enum CurationQualityReviewEvidenceType
{
    ModelReview,
    SourceGroundedReview,
    UserReview,
}

public sealed record CreateUserFlagDefinitionCommand(string Name, string Meaning);

public sealed record UserFlagDefinitionView(Guid Id, string Name, string Meaning);

public sealed record QualityReviewView(
    Guid Id,
    Guid LearningItemId,
    int ContentRevision,
    CurationQualityReviewOutcome Outcome,
    CurationQualityReviewEvidenceType EvidenceType,
    string Findings,
    string? SuggestedCorrection,
    Guid? SupersededBy);

public sealed record CurrentQualityStateView(CurationQualityReviewOutcome Outcome);

public sealed record CuratedLearningItemView(
    Guid Id,
    string Prompt,
    LearningItemLifecycle Lifecycle,
    int ContentRevision,
    IReadOnlyList<Guid> UserFlagDefinitionIds,
    IReadOnlyList<QualityReviewView> QualityReviews,
    CurrentQualityStateView? CurrentQualityState);

public sealed record AcceptQualityReviewCommand(
    CurationQualityReviewOutcome Outcome,
    CurationQualityReviewEvidenceType EvidenceType,
    string Findings,
    string? SuggestedCorrection = null,
    IReadOnlyList<Guid>? SupersededReviewIds = null);
