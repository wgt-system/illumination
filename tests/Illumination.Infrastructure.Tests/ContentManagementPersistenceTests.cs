using Illumination.Application.ContentManagement;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

#pragma warning disable xUnit1051

namespace Illumination.Infrastructure.Tests;

public class ContentManagementPersistenceTests
{
    [Fact]
    public async Task Application_content_capabilities_round_trip_through_the_ef_core_port()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync())
        {
            await setup.Database.MigrateAsync();
        }

        var service = new ContentManagementService(new EfCoreContentPersistence(factory), new FixedTimeProvider(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)));

        var item = await service.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt",
            "Solution",
            Hints: [new HintInput("Hint")],
            LowInteractionEligible: true));
        var deck = await service.CreateDeckAsync(new CreateDeckCommand(
            "Deck",
            [" Indonesian ", "Geography", "indonesian"],
            [DeckLearningActivityProfile.LanguageLearning, DeckLearningActivityProfile.Geospatial]));
        await service.AddLearningItemToDeckAsync(deck.Id, item.Id);
        await service.SetDeckTopicLabelsAsync(deck.Id, new SetDeckTopicLabelsCommand(["Language", "Travel"]));
        await service.SetDeckLearningActivityProfilesAsync(
            deck.Id,
            new SetDeckLearningActivityProfilesCommand(
                [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.LanguageLearning]));
        await service.UpdateLearningItemAsync(item.Id, new UpdateLearningItemCommand(
            "Changed",
            "Changed solution",
            LowInteractionEligible: true));

        var reloaded = await service.GetLearningItemAsync(item.Id);
        var deckView = await service.GetDeckAsync(deck.Id);
        var listedItems = await service.ListLearningItemsAsync();
        var listedDecks = await service.ListDecksAsync();

        Assert.Equal("Changed", reloaded.Prompt);
        Assert.Equal(LearningItemResponseMode.SelfAssessed, reloaded.ResponseMode);
        Assert.True(reloaded.LowInteractionEligible);
        Assert.Equal([deck.Id], reloaded.DeckIds);
        Assert.Equal([item.Id], deckView.LearningItemIds);
        Assert.Equal(["Language", "Travel"], deckView.TopicLabels);
        Assert.Equal(
            [DeckLearningActivityProfile.GeneralRecall, DeckLearningActivityProfile.LanguageLearning],
            deckView.LearningActivityProfiles);
        Assert.Equal(deckView.TopicLabels, Assert.Single(listedDecks).TopicLabels);
        Assert.Equal(deckView.LearningActivityProfiles, Assert.Single(listedDecks).LearningActivityProfiles);
        Assert.Equal([item.Id], listedItems.Select(x => x.Id));
        Assert.Equal([deck.Id], listedDecks.Select(x => x.Id));

        await using (var verifyFacets = await factory.CreateDbContextAsync())
        {
            var topicRows = await verifyFacets.DeckTopicLabels.AsNoTracking().OrderBy(x => x.Label).ToArrayAsync();
            Assert.Equal(["Language", "Travel"], topicRows.Select(x => x.Label));

            var profileRows = await verifyFacets.DeckLearningActivityProfiles.AsNoTracking().OrderBy(x => x.Profile).ToArrayAsync();
            Assert.Equal(2, profileRows.Length);
            Assert.Equal(
                ["GeneralRecall", "LanguageLearning"],
                profileRows.Select(x => x.Profile.ToString()));
        }

        await service.DeleteDeckAsync(deck.Id);

        Assert.Equal(item.Id, (await service.GetLearningItemAsync(item.Id)).Id);
        await Assert.ThrowsAsync<ContentNotFoundException>(() => service.GetDeckAsync(deck.Id));
        await using var verifyDelete = await factory.CreateDbContextAsync();
        Assert.Empty(await verifyDelete.DeckTopicLabels.ToListAsync());
        Assert.Empty(await verifyDelete.DeckLearningActivityProfiles.ToListAsync());
    }

    [Fact]
    public async Task Migrated_existing_decks_start_with_no_invented_topic_labels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        var deckId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await using (var setup = await factory.CreateDbContextAsync())
        {
            var migrations = setup.Database.GetMigrations().ToArray();
            var topicMigrationIndex = Array.FindIndex(migrations, migration => migration.EndsWith("AddDeckTopicLabels", StringComparison.Ordinal));
            Assert.True(topicMigrationIndex > 0);
            await setup.Database.MigrateAsync(migrations[topicMigrationIndex - 1]);
            setup.Decks.Add(new DeckRecord { DeckId = deckId, Name = "Existing" });
            await setup.SaveChangesAsync();
            await setup.Database.MigrateAsync();
        }

        var service = new ContentManagementService(new EfCoreContentPersistence(factory), TimeProvider.System);
        var migrated = await service.GetDeckAsync(deckId);

        Assert.Empty(migrated.TopicLabels);
    }

    [Fact]
    public async Task Existing_decks_migrate_to_learning_activity_profiles_without_inventing_a_default()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        var deckId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await using (var setup = await factory.CreateDbContextAsync())
        {
            var migrations = setup.Database.GetMigrations().ToArray();
            Assert.EndsWith("AddDeckLearningActivityProfiles", Assert.IsType<string>(migrations[^1]));
            await setup.Database.MigrateAsync(migrations[^2]);
            setup.Decks.Add(new DeckRecord { DeckId = deckId, Name = "Existing profile-less Deck" });
            await setup.SaveChangesAsync();
            await setup.Database.MigrateAsync();
        }

        var service = new ContentManagementService(new EfCoreContentPersistence(factory), TimeProvider.System);
        var migrated = await service.GetDeckAsync(deckId);

        Assert.Empty(migrated.LearningActivityProfiles);
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.DeckLearningActivityProfiles.Where(x => x.DeckId == deckId).ToArrayAsync());
    }

    [Fact]
    public async Task Invalid_application_enum_is_rejected_before_persistence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync())
        {
            await setup.Database.MigrateAsync();
        }

        var persistence = new EfCoreContentPersistence(factory);
        var invalid = new LearningItemSnapshot(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Prompt",
            "Solution",
            (LearningItemResponseMode)999,
            [],
            [],
            [],
            [],
            false,
            LearningItemLifecycle.Active,
            true,
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            5.0,
            0.5,
            false,
            []);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => persistence.SaveLearningItemAsync(invalid));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.LearningItems.ToListAsync());
    }

    [Fact]
    public async Task Invalid_Deck_learning_activity_profile_is_rejected_before_persistence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync())
        {
            await setup.Database.MigrateAsync();
        }

        var service = new ContentManagementService(new EfCoreContentPersistence(factory), TimeProvider.System);

        await Assert.ThrowsAsync<ContentValidationException>(() => service.CreateDeckAsync(new CreateDeckCommand(
            "Invalid",
            LearningActivityProfiles: [(DeckLearningActivityProfile)999])));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.Decks.ToListAsync());
        Assert.Empty(await verify.DeckLearningActivityProfiles.ToListAsync());
    }

    [Fact]
    public async Task Authored_answer_choice_ids_round_trip_without_reordering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();

        var service = new ContentManagementService(new EfCoreContentPersistence(factory), new FixedTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        var created = await service.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt", "Solution", LearningItemResponseMode.Selection,
            DirectAnswerChoices: [new AnswerChoiceInput("First", Id: "authored-first"), new AnswerChoiceInput("Second", true, "authored-second")],
            AssistanceAnswerChoices: [new AnswerChoiceInput("Help one", Id: "authored-help-one"), new AnswerChoiceInput("Help two", Id: "authored-help-two")]));

        var reloaded = await service.GetLearningItemAsync(created.Id);
        Assert.Equal(["authored-first", "authored-second"], reloaded.DirectAnswerChoices.Select(x => x.Id));
        Assert.Equal(["authored-help-one", "authored-help-two"], reloaded.AssistanceAnswerChoices.Select(x => x.Id));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedDbContextFactory(SqliteConnection connection) : IDbContextFactory<IlluminationDbContext>, IAsyncDisposable
    {
        public IlluminationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);

        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
