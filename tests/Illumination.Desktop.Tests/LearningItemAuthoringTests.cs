using Illumination.Application.ContentManagement;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class LearningItemAuthoringTests
{
    private static readonly DateTimeOffset Now = new(2030, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public async Task Basic_create_flow_can_place_new_item_directly_in_a_deck()
    {
        var persistence = new FakeContentPersistence();
        var content = new ContentManagementService(persistence, new FixedTimeProvider(Now));
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Indo"));
        var editor = new LearningItemEditorViewModel(content, _ => { }, () => Task.CompletedTask)
        {
            Prompt = "tertidur",
            ReferenceSolution = "einschlafen",
            SelectedDeckPresentation = new DeckPresentationItem(deck, deck.Name),
        };

        await editor.SaveCommand.ExecuteAsync(null);

        var item = Assert.Single(await content.ListLearningItemsAsync());
        var updatedDeck = await content.GetDeckAsync(deck.Id);
        Assert.Contains(item.Id, updatedDeck.LearningItemIds);
        Assert.Equal("tertidur", item.Prompt);
        Assert.Equal("einschlafen", item.ReferenceSolution);
    }

    [Fact]
    public async Task Basic_create_flow_allows_an_unassigned_item_when_no_deck_is_selected()
    {
        var persistence = new FakeContentPersistence();
        var content = new ContentManagementService(persistence, new FixedTimeProvider(Now));
        var editor = new LearningItemEditorViewModel(content, _ => { }, () => Task.CompletedTask)
        {
            Prompt = "question",
            ReferenceSolution = "answer",
        };

        await editor.SaveCommand.ExecuteAsync(null);

        var item = Assert.Single(await content.ListLearningItemsAsync());
        Assert.Empty(item.DeckIds);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeContentPersistence : IContentPersistence
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
