using Illumination.Application.ContentManagement;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class LearningStateMaintenanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2031, 4, 5, 6, 7, 8, TimeSpan.Zero);

    [Fact]
    public async Task Restart_item_resets_only_current_scheduling_projection()
    {
        var itemId = Guid.NewGuid();
        var content = new FakeContentPersistence(
            [Item(itemId, isNew: false, dueAt: Now.AddDays(90), difficulty: 8.2, stability: 42, relearning: true)],
            []);
        var batch = new CapturingBatchPersistence();
        var service = new LearningStateMaintenanceService(content, batch, new FixedTimeProvider(Now));

        var result = await service.RestartLearningItemAsync(itemId);

        Assert.Equal(1, result.LearningItemCount);
        var state = Assert.Single(batch.LastBatch!);
        Assert.Equal(itemId, state.LearningItemId);
        Assert.True(state.IsNew);
        Assert.Equal(Now, state.DueAt);
        Assert.Equal(5.0, state.Difficulty);
        Assert.Equal(0.5, state.StabilityDays);
        Assert.False(state.IsInShortTermRelearning);
    }

    [Fact]
    public async Task Restart_deck_submits_one_atomic_batch_for_all_distinct_members()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var content = new FakeContentPersistence(
            [Item(first), Item(second)],
            [new DeckSnapshot(deckId, "Deck", [first, second, first])]);
        var batch = new CapturingBatchPersistence();
        var service = new LearningStateMaintenanceService(content, batch, new FixedTimeProvider(Now));

        var result = await service.RestartDeckAsync(deckId);

        Assert.Equal(2, result.LearningItemCount);
        Assert.Equal(1, batch.CallCount);
        Assert.Equal([first, second], batch.LastBatch!.Select(x => x.LearningItemId).OrderBy(x => x).ToArray().OrderBy(x => x).ToArray());
        Assert.All(batch.LastBatch!, state =>
        {
            Assert.True(state.IsNew);
            Assert.Equal(Now, state.DueAt);
            Assert.Equal(5.0, state.Difficulty);
            Assert.Equal(0.5, state.StabilityDays);
            Assert.False(state.IsInShortTermRelearning);
        });
    }

    [Fact]
    public async Task Empty_deck_is_a_noop()
    {
        var deckId = Guid.NewGuid();
        var content = new FakeContentPersistence([], [new DeckSnapshot(deckId, "Empty", [])]);
        var batch = new CapturingBatchPersistence();
        var service = new LearningStateMaintenanceService(content, batch, new FixedTimeProvider(Now));

        var result = await service.RestartDeckAsync(deckId);

        Assert.Equal(0, result.LearningItemCount);
        Assert.Equal(0, batch.CallCount);
    }

    [Fact]
    public async Task Missing_deck_member_prevents_any_batch_write()
    {
        var existing = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var content = new FakeContentPersistence(
            [Item(existing)],
            [new DeckSnapshot(deckId, "Broken", [existing, missing])]);
        var batch = new CapturingBatchPersistence();
        var service = new LearningStateMaintenanceService(content, batch, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ContentNotFoundException>(() => service.RestartDeckAsync(deckId));

        Assert.Equal(0, batch.CallCount);
    }

    private static LearningItemSnapshot Item(
        Guid id,
        bool isNew = false,
        DateTimeOffset? dueAt = null,
        double difficulty = 7.0,
        double stability = 12.0,
        bool relearning = false) =>
        new(
            id,
            "Prompt",
            "Solution",
            LearningItemResponseMode.SelfAssessed,
            [], [], [], [],
            false,
            LearningItemLifecycle.Active,
            isNew,
            dueAt ?? Now.AddDays(10),
            difficulty,
            stability,
            relearning,
            [],
            ContentRevision: 4,
            QualityReviews:
            [
                new QualityReviewSnapshot(Guid.NewGuid(), id, 4, QualityReviewOutcomeSnapshot.Pass, QualityReviewEvidenceTypeSnapshot.UserReview, "good", null, null),
            ],
            UserFlagDefinitionIds: [Guid.NewGuid()]);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingBatchPersistence : ILearningStateBatchPersistence
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<LearningStateMaintenanceSnapshot>? LastBatch { get; private set; }

        public Task SaveLearningStatesAtomicallyAsync(IReadOnlyList<LearningStateMaintenanceSnapshot> states, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastBatch = states.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContentPersistence : IContentPersistence
    {
        private readonly Dictionary<Guid, LearningItemSnapshot> _items;
        private readonly Dictionary<Guid, DeckSnapshot> _decks;

        public FakeContentPersistence(IEnumerable<LearningItemSnapshot> items, IEnumerable<DeckSnapshot> decks)
        {
            _items = items.ToDictionary(x => x.Id);
            _decks = decks.ToDictionary(x => x.Id);
        }

        public Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(_items.Values.ToArray());

        public Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>(_decks.Values.ToArray());

        public Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_decks.GetValueOrDefault(id));

        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default)
        {
            _decks[deck.Id] = deck;
            return Task.CompletedTask;
        }

        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _decks.Remove(id);
            return Task.CompletedTask;
        }
    }
}
