using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed class Review
{
    private Review(
        ReviewId id,
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse)
    {
        Id = id;
        LearningItemId = learningItemId;
        CompletedAt = completedAt;
        Assessment = assessment;
        SubmittedResponse = submittedResponse;
    }

    public ReviewId Id { get; }

    public LearningItemId LearningItemId { get; }

    public DateTimeOffset CompletedAt { get; }

    public LearningAssessment Assessment { get; }

    public string? SubmittedResponse { get; }

    public static Review Create(
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse = null) =>
        Create(ReviewId.New(), learningItemId, completedAt, assessment, submittedResponse);

    public static Review Create(
        ReviewId id,
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse = null)
    {
        if (!Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        return new Review(id, learningItemId, completedAt, assessment, submittedResponse);
    }
}
