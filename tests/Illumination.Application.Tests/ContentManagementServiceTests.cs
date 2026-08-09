using System.Collections.Concurrent;
using System.Reflection;
using Illumination.Application.ContentManagement;
using Xunit;

#pragma warning disable xUnit1051

namespace Illumination.Application.Tests;

public class ContentManagementServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Creates_and_updates_a_learning_item_using_application_contracts()
    {
        var store = new InMemoryContentPersistence();
        var service = CreateService(store);

        var created = await service.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt",
            "Solution",
            LearningItemResponseMode.ShortText,
            Hints: [new HintInput("Hint")],
            AcceptedShortAnswers: ["accepted"],
            LowInteractionEligible: true));

        var updated = await service.UpdateLearningItemAsync(created.Id, new UpdateLearningItemCommand(
            "Changed prompt",
            "Changed solution",
            Hints: [new HintInput("First"), new HintInput("Second")],
            LowInteractionEligible: false));

        Assert.Equal(Now, created.DueAt);
        Assert.Equal("Changed prompt", updated.Prompt);
        Assert.Equal("Changed solution", updated.ReferenceSolution);
        Assert.Equal(["First", "Second"], updated.Hints.Select(x => x.Text));
        Assert.Equal(LearningItemResponseMode.ShortText, created.ResponseMode);
        Assert.False(updated.LowInteractionEligible);
        Assert.DoesNotContain(
            PublicContractTypes(typeof(LearningItemView)),
            type => type.FullName == "Illumination.Domain.Learning.LearningItem");
    }

    [Fact]
    public async Task Lifecycle_operations_use_the_injected_time_and_preserve_state_data()
    {
        var store = new InMemoryContentPersistence();
        var service = CreateService(store);
        var created = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Prompt", "Solution"));

        await service.SuspendLearningItemAsync(created.Id);
        await service.ReactivateLearningItemAsync(created.Id);
        var reactivated = await service.GetLearningItemAsync(created.Id);

        Assert.Equal(LearningItemLifecycle.Active, reactivated.Lifecycle);
        Assert.Equal(Now, reactivated.DueAt);
        Assert.True(reactivated.IsNew);

        await service.MarkLearningItemMasteredAsync(created.Id);
        await service.UnmarkLearningItemMasteredAsync(created.Id);
        var unmastered = await service.GetLearningItemAsync(created.Id);

        Assert.Equal(LearningItemLifecycle.Active, unmastered.Lifecycle);
        Assert.Equal(Now, unmastered.DueAt);
    }

    [Fact]
    public async Task Deck_operations_preserve_membership_identity_and_delete_only_the_deck()
    {
        var store = new InMemoryContentPersistence();
        var service = CreateService(store);
        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Prompt", "Solution"));
        var deck = await service.CreateDeckAsync(new CreateDeckCommand("Deck"));

        await service.AddLearningItemToDeckAsync(deck.Id, item.Id);
        var renamed = await service.RenameDeckAsync(deck.Id, new RenameDeckCommand("Renamed"));
        var inspected = await service.GetDeckAsync(deck.Id);

        Assert.Equal("Renamed", renamed.Name);
        Assert.Equal([item.Id], inspected.LearningItemIds);

        await service.RemoveLearningItemFromDeckAsync(deck.Id, item.Id);
        await service.DeleteDeckAsync(deck.Id);

        Assert.Equal(item.Id, (await service.GetLearningItemAsync(item.Id)).Id);
        await Assert.ThrowsAsync<ContentNotFoundException>(() => service.GetDeckAsync(deck.Id));
    }

    [Fact]
    public async Task Lists_learning_items_and_decks_through_application_owned_views()
    {
        var service = CreateService(new InMemoryContentPersistence());
        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Prompt", "Solution"));
        var deck = await service.CreateDeckAsync(new CreateDeckCommand("Deck"));
        await service.AddLearningItemToDeckAsync(deck.Id, item.Id);

        var items = await service.ListLearningItemsAsync();
        var decks = await service.ListDecksAsync();

        Assert.Equal([item.Id], items.Select(x => x.Id));
        Assert.Equal([deck.Id], decks.Select(x => x.Id));
        Assert.Equal([item.Id], decks[0].LearningItemIds);
    }

    [Fact]
    public async Task Core_content_management_capabilities_run_without_a_remote_service()
    {
        var service = CreateService(new InMemoryContentPersistence());

        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand("Prompt", "Solution"));
        var deck = await service.CreateDeckAsync(new CreateDeckCommand("Local deck"));
        var membership = await service.AddLearningItemToDeckAsync(deck.Id, item.Id);

        Assert.Equal([item.Id], membership.LearningItemIds);
        Assert.DoesNotContain(
            typeof(ContentManagementService).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == "System.Net.Http");
    }

    [Fact]
    public async Task Missing_content_and_invalid_domain_input_are_explicit_and_do_not_save_partial_state()
    {
        var store = new InMemoryContentPersistence();
        var service = CreateService(store);

        await Assert.ThrowsAsync<ContentNotFoundException>(() => service.GetLearningItemAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ContentValidationException>(() => service.CreateLearningItemAsync(new CreateLearningItemCommand(" ", "Solution")));
        await Assert.ThrowsAsync<ContentValidationException>(() => service.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt",
            "Solution",
            (LearningItemResponseMode)999)));
        Assert.Equal(0, store.SaveLearningItemCount);
    }

    [Fact]
    public void Public_content_management_contracts_do_not_expose_domain_types()
    {
        var publicApiTypes = new[]
        {
            typeof(ContentManagementService),
            typeof(IContentPersistence),
            typeof(CreateLearningItemCommand),
            typeof(UpdateLearningItemCommand),
            typeof(CreateDeckCommand),
            typeof(RenameDeckCommand),
            typeof(LearningItemView),
            typeof(DeckView),
        };

        var exposedTypes = publicApiTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
            .SelectMany(FlattenType)
            .ToArray();

        Assert.DoesNotContain(exposedTypes, type => type.FullName?.StartsWith("Illumination.Domain.", StringComparison.Ordinal) == true);
    }

    private static ContentManagementService CreateService(InMemoryContentPersistence store) =>
        new(store, new FixedTimeProvider(Now));

    private static IEnumerable<Type> PublicContractTypes(Type type)
    {
        yield return type;
        foreach (var property in type.GetProperties())
        {
            yield return property.PropertyType;
        }
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;
        if (type.IsArray)
        {
            foreach (var nested in FlattenType(type.GetElementType()!)) yield return nested;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(FlattenType)) yield return argument;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryContentPersistence : IContentPersistence
    {
        private readonly ConcurrentDictionary<Guid, LearningItemSnapshot> _items = new();
        private readonly ConcurrentDictionary<Guid, DeckSnapshot> _decks = new();

        public int SaveLearningItemCount { get; private set; }

        public Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(_items.Values.ToArray());

        public Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

        public Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default)
        {
            _items[item.Id] = item;
            SaveLearningItemCount++;
            return Task.CompletedTask;
        }

        public Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_decks.TryGetValue(id, out var deck) ? deck : null);

        public Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>(_decks.Values.ToArray());

        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default)
        {
            _decks[deck.Id] = deck;
            return Task.CompletedTask;
        }

        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _decks.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }
}
