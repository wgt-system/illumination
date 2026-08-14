namespace Illumination.Application.Study;

public interface IStudySessionPersistence
{
    Task<IReadOnlyList<StudyDeckSnapshot>> LoadDecksAsync(IReadOnlyList<Guid> deckIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudyLearningItemSnapshot>> LoadLearningItemsAsync(IReadOnlyList<Guid> learningItemIds, CancellationToken cancellationToken = default);

    Task<StudyLearningItemSnapshot?> FindLearningItemAsync(Guid learningItemId, CancellationToken cancellationToken = default);

    Task<StudySessionSnapshot?> FindStudySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task SaveStartedStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default);

    Task CommitReviewAsync(StudyLearningItemSnapshot learningItem, StudyReviewSnapshot review, StudySessionSnapshot session, CancellationToken cancellationToken = default);

    Task CompleteStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default);
}

public interface IStudyEvaluationPreferencePersistence
{
    Task<StudyEvaluationMode> LoadDefaultEvaluationModeAsync(CancellationToken cancellationToken = default);

    Task SaveDefaultEvaluationModeAsync(StudyEvaluationMode mode, CancellationToken cancellationToken = default);
}

public interface IStudySessionOrdering
{
    IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds);
}

public sealed class RandomStudySessionOrdering : IStudySessionOrdering
{
    public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds)
    {
        var result = learningItemIds.ToArray();
        for (var index = result.Length - 1; index > 0; index--)
        {
            var other = Random.Shared.Next(index + 1);
            (result[index], result[other]) = (result[other], result[index]);
        }

        return result;
    }
}
