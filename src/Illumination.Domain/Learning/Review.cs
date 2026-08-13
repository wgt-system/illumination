using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed class Review
{
    private Review(
        ReviewId id,
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse,
        bool? automaticCorrectness,
        LearningAssessment? suggestedAssessment,
        int hintCount,
        bool assistanceAnswerChoicesRevealed,
        bool referenceSolutionRevealed)
    {
        Id = id;
        LearningItemId = learningItemId;
        CompletedAt = completedAt;
        Assessment = assessment;
        SubmittedResponse = submittedResponse;
        AutomaticCorrectness = automaticCorrectness;
        SuggestedAssessment = suggestedAssessment;
        HintCount = hintCount;
        AssistanceAnswerChoicesRevealed = assistanceAnswerChoicesRevealed;
        ReferenceSolutionRevealed = referenceSolutionRevealed;
    }

    public ReviewId Id { get; }

    public LearningItemId LearningItemId { get; }

    public DateTimeOffset CompletedAt { get; }

    public LearningAssessment Assessment { get; }

    public string? SubmittedResponse { get; }
    public bool? AutomaticCorrectness { get; }
    public LearningAssessment? SuggestedAssessment { get; }
    public int HintCount { get; }
    public bool AssistanceAnswerChoicesRevealed { get; }
    public bool ReferenceSolutionRevealed { get; }

    public static Review Create(
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse = null,
        bool? automaticCorrectness = null,
        LearningAssessment? suggestedAssessment = null,
        int hintCount = 0,
        bool assistanceAnswerChoicesRevealed = false,
        bool referenceSolutionRevealed = false) =>
        Create(ReviewId.New(), learningItemId, completedAt, assessment, submittedResponse, automaticCorrectness, suggestedAssessment, hintCount, assistanceAnswerChoicesRevealed, referenceSolutionRevealed);

    public static Review Create(
        ReviewId id,
        LearningItemId learningItemId,
        DateTimeOffset completedAt,
        LearningAssessment assessment,
        string? submittedResponse = null,
        bool? automaticCorrectness = null,
        LearningAssessment? suggestedAssessment = null,
        int hintCount = 0,
        bool assistanceAnswerChoicesRevealed = false,
        bool referenceSolutionRevealed = false)
    {
        if (!Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        if (hintCount < 0) throw new ArgumentOutOfRangeException(nameof(hintCount));
        if (suggestedAssessment is { } suggestion && !Enum.IsDefined(suggestion)) throw new ArgumentOutOfRangeException(nameof(suggestedAssessment));
        return new Review(id, learningItemId, completedAt, assessment, submittedResponse, automaticCorrectness, suggestedAssessment, hintCount, assistanceAnswerChoicesRevealed, referenceSolutionRevealed);
    }
}
