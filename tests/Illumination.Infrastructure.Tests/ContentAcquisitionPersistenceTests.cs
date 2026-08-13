using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
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
    public async Task Reviewed_create_import_persists_quality_review_on_stable_item()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();
        var acquisition = new ContentAcquisitionService(new EfCoreContentAcquisitionPersistence(factory), new FixedTimeProvider(Now));
        var bundle = Bundle("{\"op\":\"create_learning_item\",\"localRef\":\"item\",\"item\":{\"prompt\":\"Reviewed Prompt\",\"referenceSolution\":\"Reviewed Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}");

        var prompt = await acquisition.GeneratePreImportQualityReviewPromptAsync(new GeneratePreImportQualityReviewPromptCommand(bundle));
        var review = $"{{\"contract\":\"{ContentAcquisitionService.PreImportQualityReviewContract}\",\"version\":\"1.0\",\"results\":[{{\"localRef\":\"item\",\"contentFingerprint\":\"{prompt.Items[0].ContentFingerprint}\",\"outcome\":\"warning\",\"evidenceType\":\"model_review\",\"findings\":\"Check source wording.\"}}]}}";

        var import = await acquisition.CommitContentBundleAsync(new CommitContentBundleCommand(bundle, [0], new PreImportQualityReviewSelection(review, QualityReviewPromptMode.Standard, [0])));

        await using var verify = await factory.CreateDbContextAsync();
        var record = Assert.Single(await verify.LearningItems.Include(x => x.QualityReviews).ToArrayAsync());
        var qualityReview = Assert.Single(record.QualityReviews);
        Assert.Equal(import.CreatedLearningItemIds.Single(), record.LearningItemId);
        Assert.Equal(record.LearningItemId, qualityReview.LearningItemId);
        Assert.Equal(1, qualityReview.ContentRevision);
        Assert.Equal(1, (int)qualityReview.Outcome);
    }

    [Fact]
    public async Task Bulk_import_persists_exact_item_count_and_is_immediately_available_to_content_and_study()
    {
        const int itemCount = 30;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();

        var operations = new List<string>
        {
            "{\"op\":\"create_deck\",\"localRef\":\"bulk-deck\",\"deck\":{\"name\":\"Bulk Deck\"}}",
        };
        operations.AddRange(Enumerable.Range(1, itemCount).Select(index =>
            $"{{\"op\":\"create_learning_item\",\"localRef\":\"item-{index}\",\"item\":{{\"prompt\":\"Bulk Prompt {index}\",\"referenceSolution\":\"Bulk Solution {index}\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}"));
        operations.AddRange(Enumerable.Range(1, itemCount).Select(index =>
            $"{{\"op\":\"assign_item_to_decks\",\"item\":{{\"itemLocalRef\":\"item-{index}\"}},\"decks\":[{{\"deckLocalRef\":\"bulk-deck\"}}]}}"));
        var bundle = Bundle([.. operations]);
        var acquisition = new ContentAcquisitionService(new EfCoreContentAcquisitionPersistence(factory), new FixedTimeProvider(Now));
        var preview = await acquisition.PreviewContentBundleAsync(bundle);
        Assert.True(preview.IsValid, string.Join(" | ", preview.Diagnostics.Concat(preview.Operations.SelectMany(x => x.Diagnostics)).Select(x => x.Code + ":" + x.Message)));

        var result = await acquisition.CommitContentBundleAsync(new CommitContentBundleCommand(bundle, Enumerable.Range(0, operations.Count).ToArray()));

        await using (var verify = await factory.CreateDbContextAsync())
        {
            Assert.Equal(itemCount, await verify.LearningItems.CountAsync());
            Assert.Single(await verify.Decks.ToArrayAsync());
            Assert.Equal(itemCount, await verify.DeckLearningItems.CountAsync());
        }

        var timeProvider = new FixedTimeProvider(Now);
        var content = new ContentManagementService(new EfCoreContentPersistence(factory), timeProvider);
        Assert.Equal(itemCount, (await content.ListLearningItemsAsync()).Count);
        var deck = Assert.Single(await content.ListDecksAsync());
        Assert.Equal(itemCount, deck.LearningItemIds.Count);

        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), timeProvider, new IdentityOrdering());
        var session = await study.StartStudySessionAsync(new StartStudySessionCommand([deck.Id], AllNew: true));
        Assert.Equal(itemCount, session.Queue.Count);
        Assert.StartsWith("Bulk Prompt ", (await study.GetNextStudySessionItemAsync(session.Id))!.Prompt, StringComparison.Ordinal);
        Assert.Equal(itemCount, result.CreatedLearningItemIds.Count);
        Assert.Equal(itemCount, result.AppliedMembershipCount);
    }

    [Fact]
    public async Task Malformed_bundle_cannot_mutate_real_sqlite_persistence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync()) await setup.Database.MigrateAsync();
        var acquisition = new ContentAcquisitionService(new EfCoreContentAcquisitionPersistence(factory), new FixedTimeProvider(Now));

        var preview = await acquisition.PreviewContentBundleAsync("{ malformed");
        Assert.False(preview.IsValid);
        await Assert.ThrowsAsync<ContentAcquisitionValidationException>(() => acquisition.CommitContentBundleAsync(new CommitContentBundleCommand("{ malformed", [])));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.LearningItems.ToArrayAsync());
        Assert.Empty(await verify.Decks.ToArrayAsync());
        Assert.Empty(await verify.ImportProvenance.ToArrayAsync());
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
        await persistence.CommitAsync(CommitSnapshot(batchId, Guid.NewGuid(), "First", reviewed: true));

        await Assert.ThrowsAnyAsync<Exception>(() => persistence.CommitAsync(CommitSnapshot(batchId, Guid.NewGuid(), "Second", reviewed: true)));
        await using var verify = await factory.CreateDbContextAsync();
        Assert.Single(await verify.LearningItems.ToArrayAsync());
        Assert.Single(await verify.ImportProvenance.ToArrayAsync());
        Assert.DoesNotContain(await verify.LearningItems.ToArrayAsync(), item => item.Prompt == "Second");
        Assert.Single(await verify.QualityReviews.ToArrayAsync());
    }

    [Fact]
    public async Task Acquisition_persists_the_domain_revision_for_multiple_updates_return_to_original_and_noop()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var factory = new FixedDbContextFactory(connection);
        var itemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await using (var setup = await factory.CreateDbContextAsync())
        {
            await setup.Database.MigrateAsync();
            setup.LearningItems.Add(DomainPersistenceMapper.ToRecord(LearningItem.Create(LearningItemId.From(itemId), "Original", "Solution", Now)));
            await setup.SaveChangesAsync();
        }

        var acquisition = new ContentAcquisitionService(new EfCoreContentAcquisitionPersistence(factory), new FixedTimeProvider(Now));
        await acquisition.CommitContentBundleAsync(new CommitContentBundleCommand(Bundle(
            $"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Changed one\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}",
            $"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Changed two\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}"), [0, 1]));
        Assert.Equal(3, await ReadRevisionAsync(factory, itemId));

        await acquisition.CommitContentBundleAsync(new CommitContentBundleCommand(Bundle(
            $"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Changed three\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}",
            $"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Original\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}"), [0, 1]));
        Assert.Equal(5, await ReadRevisionAsync(factory, itemId));
        await using (var verify = await factory.CreateDbContextAsync()) Assert.Equal("Original", (await verify.LearningItems.SingleAsync()).Prompt);

        await acquisition.CommitContentBundleAsync(new CommitContentBundleCommand(Bundle(
            $"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Original\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}"), [0]));
        Assert.Equal(5, await ReadRevisionAsync(factory, itemId));
    }

    private static async Task<int> ReadRevisionAsync(FixedDbContextFactory factory, Guid itemId)
    {
        await using var context = await factory.CreateDbContextAsync();
        return await context.LearningItems.Where(x => x.LearningItemId == itemId).Select(x => x.ContentRevision).SingleAsync();
    }

    private static ContentAcquisitionCommitSnapshot CommitSnapshot(Guid batchId, Guid itemId, string prompt, bool reviewed = false) => new(
        [new LearningItemSnapshot(itemId, prompt, "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false, LearningItemLifecycle.Active, true, Now, 5.0, 0.5, false, [], QualityReviews: reviewed ? [new QualityReviewSnapshot(Guid.NewGuid(), itemId, 1, QualityReviewOutcomeSnapshot.Warning, QualityReviewEvidenceTypeSnapshot.ModelReview, "Review finding.", null, null)] : null)],
        [],
        new ContentAcquisitionProvenanceSnapshot(batchId, Now, ContentAcquisitionService.Contract, ContentAcquisitionService.Version, null, null, 1, 1, 0, 0, 0, 0));

    private static string Bundle(params string[] operations) => $"{{\"contract\":\"{ContentAcquisitionService.Contract}\",\"version\":\"1.0\",\"operations\":[{string.Join(',', operations)}]}}";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class IdentityOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.ToArray();
    }

    private sealed class FixedDbContextFactory(SqliteConnection connection) : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
