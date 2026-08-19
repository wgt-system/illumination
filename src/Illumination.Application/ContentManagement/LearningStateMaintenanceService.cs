namespace Illumination.Application.ContentManagement;

public sealed record RestartLearningResult(int LearningItemCount, DateTimeOffset RestartedAt);

public sealed class LearningStateMaintenanceService
{
    private const double InitialDifficulty = 5.0;
    private const double InitialStabilityDays = 0.5;

    private readonly IContentPersistence _content;
    private readonly ILearningStateBatchPersistence _learningStatePersistence;
    private readonly TimeProvider _timeProvider;

    public LearningStateMaintenanceService(
        IContentPersistence content,
        ILearningStateBatchPersistence learningStatePersistence,
        TimeProvider timeProvider)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _learningStatePersistence = learningStatePersistence ?? throw new ArgumentNullException(nameof(learningStatePersistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RestartLearningResult> RestartLearningItemAsync(
        Guid learningItemId,
        CancellationToken cancellationToken = default)
    {
        if (learningItemId == Guid.Empty)
            throw new ContentValidationException("Learning Item ID must not be empty.", new ArgumentException(nameof(learningItemId)));

        var item = await _content.FindLearningItemAsync(learningItemId, cancellationToken)
            ?? throw new ContentNotFoundException($"Learning Item '{learningItemId}' was not found.");
        var restartedAt = _timeProvider.GetUtcNow();
        await _learningStatePersistence.SaveLearningStatesAtomicallyAsync(
            [Reset(item, restartedAt)], cancellationToken);
        return new RestartLearningResult(1, restartedAt);
    }

    public async Task<RestartLearningResult> RestartDeckAsync(
        Guid deckId,
        CancellationToken cancellationToken = default)
    {
        if (deckId == Guid.Empty)
            throw new ContentValidationException("Deck ID must not be empty.", new ArgumentException(nameof(deckId)));

        var deck = await _content.FindDeckAsync(deckId, cancellationToken)
            ?? throw new ContentNotFoundException($"Deck '{deckId}' was not found.");
        var distinctItemIds = deck.LearningItemIds.Distinct().ToArray();
        var restartedAt = _timeProvider.GetUtcNow();
        if (distinctItemIds.Length == 0)
            return new RestartLearningResult(0, restartedAt);

        var states = new List<LearningStateMaintenanceSnapshot>(distinctItemIds.Length);
        foreach (var itemId in distinctItemIds)
        {
            var item = await _content.FindLearningItemAsync(itemId, cancellationToken)
                ?? throw new ContentNotFoundException($"Learning Item '{itemId}' referenced by Deck '{deckId}' was not found.");
            states.Add(Reset(item, restartedAt));
        }

        await _learningStatePersistence.SaveLearningStatesAtomicallyAsync(states, cancellationToken);
        return new RestartLearningResult(states.Count, restartedAt);
    }

    private static LearningStateMaintenanceSnapshot Reset(LearningItemSnapshot item, DateTimeOffset restartedAt) =>
        new(
            item.Id,
            IsNew: true,
            DueAt: restartedAt,
            Difficulty: InitialDifficulty,
            StabilityDays: InitialStabilityDays,
            IsInShortTermRelearning: false);
}
