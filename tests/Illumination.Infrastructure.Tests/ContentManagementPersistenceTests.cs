using Illumination.Application.ContentManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Illumination.Infrastructure.Persistence;
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
        var deck = await service.CreateDeckAsync(new CreateDeckCommand("Deck"));
        await service.AddLearningItemToDeckAsync(deck.Id, item.Id);
        await service.UpdateLearningItemAsync(item.Id, new UpdateLearningItemCommand(
            "Changed",
            "Changed solution",
            LowInteractionEligible: true));

        var reloaded = await service.GetLearningItemAsync(item.Id);
        var deckView = await service.GetDeckAsync(deck.Id);

        Assert.Equal("Changed", reloaded.Prompt);
        Assert.True(reloaded.LowInteractionEligible);
        Assert.Equal([deck.Id], reloaded.DeckIds);
        Assert.Equal([item.Id], deckView.LearningItemIds);

        await service.DeleteDeckAsync(deck.Id);

        Assert.Equal(item.Id, (await service.GetLearningItemAsync(item.Id)).Id);
        await Assert.ThrowsAsync<ContentNotFoundException>(() => service.GetDeckAsync(deck.Id));
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
