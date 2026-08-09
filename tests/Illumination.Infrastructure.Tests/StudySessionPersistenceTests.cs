using Illumination.Application.Study;
using Illumination.Application.ContentManagement;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public sealed class StudySessionPersistenceTests
{
    [Fact]
    public async Task Study_state_review_and_session_round_trip_across_context_recreation()
    {
        using var fixture = new DatabaseFixture();
        await fixture.MigrateLatestAsync();
        var itemId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        await fixture.SeedItemAndDeckAsync(itemId, deckId);
        var persistence = fixture.CreatePersistence();
        var started = new StudySessionSnapshot(sessionId, fixture.Now, null, [deckId], [itemId], []);
        await persistence.SaveStartedStudySessionAsync(started);

        var updatedItem = fixture.CreateSnapshot(itemId, deckId, isNew: false, dueAt: fixture.Now.AddDays(2), difficulty: 5.6,
            stabilityDays: 2.0, isRelearning: false, interveningCardTarget: null);
        var review = new StudyReviewSnapshot(reviewId, itemId, fixture.Now, StudyLearningAssessment.Gut, "submitted answer");
        var completed = started with { Queue = [], ReviewIds = [reviewId] };
        await persistence.CommitReviewAsync(updatedItem, review, completed);

        var recreatedPersistence = fixture.CreatePersistence();
        var item = Assert.IsType<StudyLearningItemSnapshot>(await recreatedPersistence.FindLearningItemAsync(itemId));
        var session = Assert.IsType<StudySessionSnapshot>(await recreatedPersistence.FindStudySessionAsync(sessionId));
        await using var context = fixture.CreateContext();
        var storedReview = await context.Reviews.AsNoTracking().SingleAsync(x => x.ReviewId == reviewId);

        Assert.False(item.IsNew);
        Assert.Equal(5.6, item.Difficulty);
        Assert.Equal(2.0, item.StabilityDays);
        Assert.False(item.IsInShortTermRelearning);
        Assert.Null(item.InterveningCardTarget);
        Assert.Equal([deckId], session.SelectedDeckIds);
        Assert.Empty(session.Queue);
        Assert.Equal([reviewId], session.ReviewIds);
        Assert.Equal("submitted answer", storedReview.SubmittedResponse);
    }

    [Fact]
    public async Task CommitReview_is_atomic_when_the_session_does_not_exist()
    {
        using var fixture = new DatabaseFixture();
        await fixture.MigrateLatestAsync();
        var itemId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        await fixture.SeedItemAndDeckAsync(itemId, deckId);
        var persistence = fixture.CreatePersistence();
        var updatedItem = fixture.CreateSnapshot(itemId, deckId, isNew: false, dueAt: fixture.Now.AddDays(2), difficulty: 6.0,
            stabilityDays: 2.0, isRelearning: false, interveningCardTarget: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.CommitReviewAsync(
            updatedItem,
            new StudyReviewSnapshot(Guid.NewGuid(), itemId, fixture.Now, StudyLearningAssessment.Gut, null),
            new StudySessionSnapshot(Guid.NewGuid(), fixture.Now, null, [deckId], [], [])));

        var recreated = fixture.CreatePersistence();
        var item = Assert.IsType<StudyLearningItemSnapshot>(await recreated.FindLearningItemAsync(itemId));
        await using var context = fixture.CreateContext();
        Assert.Empty(await context.Reviews.ToArrayAsync());
        Assert.True(item.IsNew);
        Assert.Equal(5.0, item.Difficulty);
        Assert.Equal(0.5, item.StabilityDays);
    }

    [Fact]
    public async Task Deck_membership_changes_and_deletion_do_not_delete_learning_state_or_review_history()
    {
        using var fixture = new DatabaseFixture();
        await fixture.MigrateLatestAsync();
        var itemId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        var secondDeckId = Guid.NewGuid();
        await fixture.SeedItemAndDeckAsync(itemId, deckId);
        await using (var context = fixture.CreateContext())
        {
            context.Decks.Add(new DeckRecord { DeckId = secondDeckId, Name = "Second" });
            context.DeckLearningItems.Remove(new DeckLearningItemRecord { DeckId = deckId, LearningItemId = itemId });
            context.DeckLearningItems.Add(new DeckLearningItemRecord { DeckId = secondDeckId, LearningItemId = itemId });
            context.Reviews.Add(new ReviewRecord
            {
                ReviewId = Guid.NewGuid(), LearningItemId = itemId, CompletedAt = fixture.Now,
                Assessment = LearningAssessment.Gut, SubmittedResponse = null,
            });
            await context.SaveChangesAsync();
            context.Decks.Remove(await context.Decks.SingleAsync(x => x.DeckId == deckId));
            await context.SaveChangesAsync();
        }

        await using var recreated = fixture.CreateContext();
        var item = await recreated.LearningItems.SingleAsync(x => x.LearningItemId == itemId);
        Assert.Equal(5.0, item.Difficulty);
        Assert.Equal(0.5, item.StabilityDays);
        Assert.Single(await recreated.Reviews.ToArrayAsync());
        Assert.Single(await recreated.DeckLearningItems.ToArrayAsync());
        Assert.Equal(secondDeckId, (await recreated.DeckLearningItems.SingleAsync()).DeckId);
    }

    [Fact]
    public async Task V01_database_is_backed_up_before_migration_and_receives_scheduler_defaults()
    {
        using var fixture = new DatabaseFixture();
        await fixture.MigrateV01Async();
        var itemId = Guid.NewGuid();
        var deckId = Guid.NewGuid();
        await fixture.SeedV01ItemAndDeckAsync(itemId, deckId);
        var backupService = new LocalSqliteBackupService(fixture.BackupDirectory, 2, fixture.FixedTimeProvider);
        var coordinator = new SqliteMigrationCoordinator(fixture.CreateOptions(), backupService);

        await coordinator.MigrateAsync();

        var backup = Assert.Single(Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite"));
        await using (var backupConnection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False"))
        {
            await backupConnection.OpenAsync();
            await using var command = backupConnection.CreateCommand();
            command.CommandText = "SELECT Prompt FROM LearningItems WHERE LearningItemId = $id";
            command.Parameters.AddWithValue("$id", itemId);
            Assert.Equal("v0.1 prompt", await command.ExecuteScalarAsync());
            command.CommandText = "SELECT 1 FROM pragma_table_info('LearningItems') WHERE name = 'Difficulty'";
            Assert.Null(await command.ExecuteScalarAsync());
        }

        await using var context = fixture.CreateContext();
        var item = await context.LearningItems.SingleAsync(x => x.LearningItemId == itemId);
        Assert.Equal("v0.1 prompt", item.Prompt);
        Assert.Equal("v0.1 solution", item.ReferenceSolutionContent);
        Assert.Equal(LearningItemLifecycleState.Active, item.LifecycleState);
        Assert.True(item.IsNew);
        Assert.Equal(fixture.Now, item.DueAt);
        Assert.Equal(deckId, (await context.DeckLearningItems.SingleAsync()).DeckId);
        Assert.Equal(5.0, item.Difficulty);
        Assert.Equal(0.5, item.StabilityDays);
        Assert.False(item.IsInShortTermRelearning);
        Assert.Null(item.InterveningCardTarget);
        Assert.Empty(await context.Reviews.ToArrayAsync());
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "illumination-v02-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "illumination.sqlite");
            BackupDirectory = Path.Combine(Root, "backups");
            FixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        }

        public string Root { get; }
        public string DatabasePath { get; }
        public string BackupDirectory { get; }
        public FixedTimeProvider FixedTimeProvider { get; }
        public DateTimeOffset Now => FixedTimeProvider.GetUtcNow();

        public DbContextOptions<IlluminationDbContext> CreateOptions() => new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite($"Data Source={DatabasePath};Pooling=False").Options;

        public IlluminationDbContext CreateContext() => new(CreateOptions());

        public EfCoreStudySessionPersistence CreatePersistence() => new(new FixedDbContextFactory(CreateOptions()));

        public async Task MigrateLatestAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public async Task MigrateV01Async()
        {
            await using var context = CreateContext();
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260809050452_InitialPersistence");
        }

        public async Task SeedItemAndDeckAsync(Guid itemId, Guid deckId)
        {
            await using var context = CreateContext();
            context.LearningItems.Add(DomainPersistenceMapper.ToRecord(LearningItem.Create(
                LearningItemId.From(itemId), "prompt", "solution", Now)));
            context.Decks.Add(new DeckRecord { DeckId = deckId, Name = "Deck" });
            context.DeckLearningItems.Add(new DeckLearningItemRecord { DeckId = deckId, LearningItemId = itemId });
            await context.SaveChangesAsync();
        }

        public async Task SeedV01ItemAndDeckAsync(Guid itemId, Guid deckId)
        {
            await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO LearningItems (LearningItemId, Prompt, ReferenceSolutionContent, ResponseMode, LowInteractionEligible, LifecycleState, IsNew, DueAt) VALUES ($itemId, 'v0.1 prompt', 'v0.1 solution', 'SelfAssessed', 0, 'Active', 1, $dueAt);";
            command.Parameters.AddWithValue("$itemId", itemId);
            command.Parameters.AddWithValue("$dueAt", Now.ToUniversalTime().ToString("O"));
            await command.ExecuteNonQueryAsync();
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Decks (DeckId, Name) VALUES ($deckId, 'v0.1 deck');";
            command.Parameters.AddWithValue("$deckId", deckId);
            await command.ExecuteNonQueryAsync();
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO DeckLearningItems (DeckId, LearningItemId) VALUES ($deckId, $itemId);";
            command.Parameters.AddWithValue("$deckId", deckId);
            command.Parameters.AddWithValue("$itemId", itemId);
            await command.ExecuteNonQueryAsync();
        }

        public StudyLearningItemSnapshot CreateSnapshot(Guid itemId, Guid deckId, bool isNew, DateTimeOffset dueAt,
            double difficulty, double stabilityDays, bool isRelearning, int? interveningCardTarget) => new(
            itemId, "prompt", "solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false,
            LearningItemLifecycle.Active, isNew, dueAt, difficulty, stabilityDays, isRelearning,
            interveningCardTarget, [deckId]);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FixedDbContextFactory(DbContextOptions<IlluminationDbContext> options) : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new IlluminationDbContext(options));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
