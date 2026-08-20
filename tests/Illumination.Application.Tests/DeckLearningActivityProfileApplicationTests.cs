using Illumination.Application.ContentManagement;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class DeckLearningActivityProfileApplicationTests
{
    [Fact]
    public async Task Create_and_explicit_profile_update_round_trip_through_application_contracts()
    {
        var persistence = new InMemoryContentPersistence();
        var service = new ContentManagementService(persistence, TimeProvider.System);

        var created = await service.CreateDeckAsync(new CreateDeckCommand(
            "Indo travel",
            ["Indonesian", "Travel"],
            [DeckLearningActivityProfile.LanguageLearning, DeckLearningActivityProfile.Geospatial]));
        var updated = await service.SetDeckLearningActivityProfilesAsync(
            created.Id,
            new SetDeckLearningActivityProfilesCommand(
                [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.LanguageLearning]));
        var reloaded = await service.GetDeckAsync(created.Id);

        Assert.Equal(
            [DeckLearningActivityProfile.LanguageLearning, DeckLearningActivityProfile.Geospatial],
            created.LearningActivityProfiles);
        Assert.Equal(
            [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.LanguageLearning],
            updated.LearningActivityProfiles);
        Assert.Equal(updated.LearningActivityProfiles, reloaded.LearningActivityProfiles);
        Assert.Equal(["Indonesian", "Travel"], reloaded.TopicLabels);
    }

    [Fact]
    public async Task Rename_membership_and_topic_changes_preserve_profiles()
    {
        var persistence = new InMemoryContentPersistence();
        var service = new ContentManagementService(persistence, TimeProvider.System);
        var deck = await service.CreateDeckAsync(new CreateDeckCommand(
            "Deck",
            ["Algorithms"],
            [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.CodingProblemSolving]));
        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Q", "A"));

        await service.RenameDeckAsync(deck.Id, new RenameDeckCommand("Renamed"));
        await service.SetDeckTopicLabelsAsync(deck.Id, new SetDeckTopicLabelsCommand(["C++"]));
        var withItem = await service.AddLearningItemToDeckAsync(deck.Id, item.Id);
        var withoutItem = await service.RemoveLearningItemFromDeckAsync(deck.Id, item.Id);

        Assert.Equal(
            [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.CodingProblemSolving],
            withItem.LearningActivityProfiles);
        Assert.Equal(withItem.LearningActivityProfiles, withoutItem.LearningActivityProfiles);
        Assert.Equal(["C++"], withoutItem.TopicLabels);
    }

    [Fact]
    public async Task Explicit_update_can_clear_profiles_without_inventing_general_recall()
    {
        var persistence = new InMemoryContentPersistence();
        var service = new ContentManagementService(persistence, TimeProvider.System);
        var deck = await service.CreateDeckAsync(new CreateDeckCommand(
            "Deck",
            LearningActivityProfiles: [DeckLearningActivityProfile.GeneralRecall]));

        var updated = await service.SetDeckLearningActivityProfilesAsync(
            deck.Id,
            new SetDeckLearningActivityProfilesCommand([]));

        Assert.Empty(updated.LearningActivityProfiles);
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
