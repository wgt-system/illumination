namespace Illumination.Application.Insights;

public interface ILearningInsightPersistence
{
    Task<IReadOnlyList<LearningInsightItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InsightDeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InsightReviewSnapshot>> LoadReviewsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InsightStudySessionSnapshot>> LoadStudySessionsAsync(CancellationToken cancellationToken = default);
}
