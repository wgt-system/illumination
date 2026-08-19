using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Desktop;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class DeckProjectionCoherenceTests
{
    private static readonly DateTimeOffset Now = new(2030, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Deleting_deck_removes_it_from_live_deck_backed_desktop_selections()
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
        var itemA = await content.CreateLearningItemAsync(new CreateLearningItemCommand("First", "One"));
        var itemB = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Second", "Two"));
        var deckA = await content.CreateDeckAsync(new CreateDeckCommand("Keep"));
        var deckB = await content.CreateDeckAsync(new CreateDeckCommand("Delete"));
        await content.AddLearningItemToDeckAsync(deckA.Id, itemA.Id);
        await content.AddLearningItemToDeckAsync(deckB.Id, itemB.Id);

        var vm = new MainWindowViewModel(
            content,
            study,
            new ContentAcquisitionService(new FakeAcquisitionPersistence(), time),
            new ContentCurationService(persistence, persistence),
            new QualityReviewExchangeService(persistence, persistence),
            time);
        await vm.InitializeAsync();
        vm.InitializeStudySelection();

        var deletedPresentation = vm.DeckPresentationItems.Single(x => x.Id == deckB.Id);
        vm.SelectedStudyDecks.Clear();
        vm.SelectedStudyDecks.Add(deletedPresentation);
        vm.ContentCuration.FilterDeckPresentation = deletedPresentation;
        vm.ContentCuration.BulkTargetDeckPresentation = deletedPresentation;
        vm.ContentAcquisition.SelectedExistingDeck = vm.ContentAcquisition.ExistingDecks.Single(x => x.Id == deckB.Id);
        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deckB.Id);

        await vm.DeleteSelectedDeckCommand.ExecuteAsync(null);
        await vm.DeleteSelectedDeckCommand.ExecuteAsync(null);

        Assert.DoesNotContain(vm.Decks, x => x.Id == deckB.Id);
        Assert.DoesNotContain(vm.DeckPresentationItems, x => x.Id == deckB.Id);
        Assert.DoesNotContain(vm.SelectedStudyDecks, x => x.Id == deckB.Id);
        Assert.DoesNotContain(vm.ContentAcquisition.ExistingDecks, x => x.Id == deckB.Id);
        Assert.NotEqual(deckB.Id, vm.ContentAcquisition.SelectedExistingDeck?.Id);
        Assert.Null(vm.ContentCuration.FilterDeckPresentation);
        Assert.Null(vm.ContentCuration.FilterDeck);
        Assert.Null(vm.ContentCuration.BulkTargetDeckPresentation);
        Assert.Equal(deckA.Id, vm.SelectedDeck?.Id);
        Assert.Equal(itemA.Id, (await content.GetLearningItemAsync(itemA.Id)).Id);
        Assert.Equal(itemB.Id, (await content.GetLearningItemAsync(itemB.Id)).Id);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class IdentityOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.ToArray();
    }

    private sealed class FakeAcquisitionPersistence : IContentAcquisitionPersistence
    {
        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>([]);

        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);

        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
