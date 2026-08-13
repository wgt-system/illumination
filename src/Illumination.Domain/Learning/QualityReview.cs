using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed class QualityReview
{
    private QualityReview(
        QualityReviewId id,
        LearningItemId learningItemId,
        int contentRevision,
        QualityReviewOutcome outcome,
        QualityReviewEvidenceType evidenceType,
        string findings,
        string? suggestedCorrection,
        QualityReviewId? supersededBy)
    {
        if (learningItemId.Value == Guid.Empty)
        {
            throw new ArgumentException("A Quality Review must reference a Learning Item.", nameof(learningItemId));
        }

        if (contentRevision < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(contentRevision), "Content revision must be positive.");
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported quality review outcome.");
        }

        if (!Enum.IsDefined(evidenceType))
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceType), evidenceType, "Unsupported quality review evidence type.");
        }

        DomainText.RequireNonWhitespace(findings, nameof(findings));
        if (suggestedCorrection is not null)
        {
            DomainText.RequireNonWhitespace(suggestedCorrection, nameof(suggestedCorrection));
        }

        Id = id;
        LearningItemId = learningItemId;
        ContentRevision = contentRevision;
        Outcome = outcome;
        EvidenceType = evidenceType;
        Findings = findings;
        SuggestedCorrection = suggestedCorrection;
        SupersededBy = supersededBy;
    }

    public QualityReviewId Id { get; }

    public LearningItemId LearningItemId { get; }

    public int ContentRevision { get; }

    public QualityReviewOutcome Outcome { get; }

    public QualityReviewEvidenceType EvidenceType { get; }

    public string Findings { get; }

    public string? SuggestedCorrection { get; }

    public QualityReviewId? SupersededBy { get; }

    public bool IsSuperseded => SupersededBy.HasValue;

    public static QualityReview Create(
        LearningItemId learningItemId,
        int contentRevision,
        QualityReviewOutcome outcome,
        QualityReviewEvidenceType evidenceType,
        string findings,
        string? suggestedCorrection = null) =>
        Create(QualityReviewId.New(), learningItemId, contentRevision, outcome, evidenceType, findings, suggestedCorrection);

    public static QualityReview Create(
        QualityReviewId id,
        LearningItemId learningItemId,
        int contentRevision,
        QualityReviewOutcome outcome,
        QualityReviewEvidenceType evidenceType,
        string findings,
        string? suggestedCorrection = null) =>
        new(id, learningItemId, contentRevision, outcome, evidenceType, findings, suggestedCorrection, null);

    internal QualityReview SupersededByReview(QualityReviewId supersedingReviewId) =>
        new(Id, LearningItemId, ContentRevision, Outcome, EvidenceType, Findings, SuggestedCorrection, supersedingReviewId);
}
