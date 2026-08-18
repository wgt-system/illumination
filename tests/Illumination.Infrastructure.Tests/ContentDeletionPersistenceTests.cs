using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public sealed class ContentDeletionPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Permanent_learning_item_delete_removes_history_membership_and_live_queue_reference_but_not_deck()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Prompt", "Solution"));
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Deck"));
        await content.AddLearningItemToDeckAsync(deck.Id, item.Id);
        var session = await study.StartStudySessionAsync(new StartStudySessionCommand([deck.Id]));
        await study.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, item.Id, StudyLearningAssessment.Nochmal));

        await using (var beforeDelete = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await beforeDelete.Reviews.AnyAsync(x => x.LearningItemId == item.Id));
            Assert.True(await beforeDelete.StudySessionQueue.AnyAsync(x => x.LearningItemId == item.Id));
        }

        await content.DeleteLearningItemAsync(item.Id);

        await using var verify = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.False(await verify.LearningItems.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.False(await verify.DeckLearningItems.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.False(await verify.Reviews.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.False(await verify.QualityReviews.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.False(await verify.LearningItemUserFlags.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.False(await verify.StudySessionQueue.AnyAsync(x => x.LearningItemId == item.Id));
        Assert.True(await verify.Decks.AnyAsync(x => x.DeckId == deck.Id));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class IdentityOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.ToArray();
    }

    private sealed class FixedDbContextFactory(SqliteConnection connection) : IDbContextFactory<IlluminationDbContext>, IAsyncDisposable
    {
        public IlluminationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
