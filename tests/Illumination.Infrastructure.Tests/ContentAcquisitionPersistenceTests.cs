using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Illumination.Infrastructure.Persistence;
using Xunit;

#pragma warning disable xUnit1051

namespace Illumination.Infrastructure.Tests;

public sealed class ContentAcquisitionPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Acquisition_commit_persists_items_decks_assignment_and_provenance_atomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();
        var service = new ContentAcquisitionService(new EfCoreContentAcquisitionPersistence(factory), new FixedTimeProvider(Now));
        var bundle = Bundle(
            """{"op":"create_deck","localRef":"deck","deck":{"name":"Imported Deck"}}""",
            """{"op":"create_learning_item","localRef":"item","item":{"prompt":"Imported Prompt","referenceSolution":"Imported Solution","responseMode":"self_assessed","lowInteractionEligible":false}}""",
            """{"op":"assign_item_to_decks","item":{"itemLocalRef":"item"},"decks":[{"deckLocalRef":"deck"}]}""");
        var preview = await service.PreviewContentBundleAsync(bundle);
        Assert.True(preview.IsValid, string.Join(" | ", preview.Diagnostics.Concat(preview.Operations.SelectMany(x => x.Diagnostics)).Select(x => x.Code + ":" + x.Message)));
        var result = await service.CommitContentBundleAsync(new CommitContentBundleCommand(bundle, [0, 1, 2]));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Single(await verify.LearningItems.ToArrayAsync());
        Assert.Single(await verify.Decks.ToArrayAsync());
        Assert.Single(await verify.DeckLearningItems.ToArrayAsync());
        Assert.Single(await verify.ImportProvenance.ToArrayAsync());
        Assert.Equal(1, result.AppliedMembershipCount);
    }

    [Fact]
    public async Task Provenance_conflict_rolls_back_all_mutations_in_the_transaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();
        var persistence = new EfCoreContentAcquisitionPersistence(factory);
        var batchId = Guid.NewGuid();
        await persistence.CommitAsync(CommitSnapshot(batchId, Guid.NewGuid(), "First"));

        await Assert.ThrowsAnyAsync<Exception>(() => persistence.CommitAsync(CommitSnapshot(batchId, Guid.NewGuid(), "Second")));
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Single(await verify.LearningItems.ToArrayAsync());
        Assert.Single(await verify.ImportProvenance.ToArrayAsync());
        Assert.DoesNotContain(await verify.LearningItems.ToArrayAsync(), item => item.Prompt == "Second");
    }

    private static ContentAcquisitionCommitSnapshot CommitSnapshot(Guid batchId, Guid itemId, string prompt) => new(
        [new LearningItemSnapshot(itemId, prompt, "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false, LearningItemLifecycle.Active, true, Now, 5.0, 0.5, false, [])],
        [],
        new ContentAcquisitionProvenanceSnapshot(batchId, Now, ContentAcquisitionService.Contract, ContentAcquisitionService.Version, null, null, 1, 1, 0, 0, 0, 0));

    private static string Bundle(params string[] operations) => $"{{\"contract\":\"{ContentAcquisitionService.Contract}\",\"version\":\"1.0\",\"operations\":[{string.Join(',', operations)}]}}";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FixedDbContextFactory(SqliteConnection connection) : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
