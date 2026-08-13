using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed record CurrentQualityState(
    QualityReviewOutcome Outcome,
    QualityReviewEvidenceType EvidenceType,
    string Findings,
    string? SuggestedCorrection,
    QualityReviewId ReviewId);
