using Illumination.Application.ContentManagement;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class DeckTopicApplicationTests
{
    [Fact]
    public async Task Create_and_explicit_topic_update_round_trip_through_application_contracts()
    {
        var persistence = new InMemoryContentPersistence();
        var service = new ContentManagementService(persistence, TimeProvider.System);

        var created = await service.CreateDeckAsync(new CreateDeckCommand("Indo", [" Indonesian ", "Geography", "indonesian"]));
        var updated = await service.SetDeckTopicLabelsAsync(created.Id, new SetDeckTopicLabelsCommand(["Language", "Travel"]));
        var reloaded = await service.GetDeckAsync(created.Id);

        Assert.Equal(["Geography", "Indonesian"], created.TopicLabels);
        Assert.Equal(["Language", "Travel"], updated.TopicLabels);
        Assert.Equal(updated.TopicLabels, reloaded.TopicLabels);
    }

    [Fact]
    public async Task Rename_and_membership_changes_preserve_topic_labels()
    {
        var persistence = new InMemoryContentPersistence();
        var service = new ContentManagementService(persistence, TimeProvider.System);
        var deck = await service.CreateDeckAsync(new CreateDeckCommand("Deck", ["Algorithms", "C++"]));
        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Q", "A"));

        await service.RenameDeckAsync(deck.Id, new RenameDeckCommand("Renamed"));
        var withItem = await service.AddLearningItemToDeckAsync(deck.Id, item.Id);
        var withoutItem = await service.RemoveLearningItemFromDeckAsync(deck.Id, item.Id);

        Assert.Equal(["Algorithms", "C++"], withItem.TopicLabels);
        Assert.Equal(["Algorithms", "C++"], withoutItem.TopicLabels);
    }

    private sealed class InMemoryContentPersistence : IContentPersistence
    {
        private readonly Dictionary<Guid, LearningItemSnapshot> _items = [];
        private readonly Dictionary<Guid, DeckSnapshot> _decks = [];

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
