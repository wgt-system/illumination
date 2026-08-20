using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Desktop;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class PracticeNowTests
{
    private static readonly DateTimeOffset Now = new(2032, 5, 6, 7, 8, 9, TimeSpan.Zero);

    [Fact]
    public async Task Practice_deck_ignores_future_due_state_without_creating_reviews_or_mutating_scheduling()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var first = await content.CreateLearningItemAsync(new CreateLearningItemCommand("makan", "essen"));
        var second = await content.CreateLearningItemAsync(new CreateLearningItemCommand("tertidur", "einschlafen"));
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Indo"));
        await content.AddLearningItemToDeckAsync(deck.Id, first.Id);
        await content.AddLearningItemToDeckAsync(deck.Id, second.Id);

        var futureDue = Now.AddDays(120);
        await using (var mutate = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            foreach (var record in await mutate.LearningItems.ToArrayAsync(TestContext.Current.CancellationToken))
            {
                record.IsNew = false;
                record.DueAt = futureDue;
                record.Difficulty = 7.5;
                record.StabilityDays = 60;
            }
            await mutate.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var vm = new MainWindowViewModel(
            content,
            study,
            new ContentAcquisitionService(new FakeAcquisitionPersistence(), time),
            new ContentCurationService(persistence, persistence),
            new QualityReviewExchangeService(persistence, persistence),
            time);
        await vm.InitializeAsync();
        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deck.Id);

        vm.StartSelectedDeckPracticeCommand.Execute(null);

        Assert.True(vm.PracticeIsActive);
        Assert.NotNull(vm.CurrentPracticeItem);
        Assert.Equal(2, vm.PracticeItems.Count);
        vm.RevealPracticeSolutionCommand.Execute(null);
        Assert.True(vm.PracticeSolutionRevealed);
        vm.NextPracticeItemCommand.Execute(null);
        Assert.True(vm.PracticeIsActive);
        Assert.False(vm.PracticeSolutionRevealed);
        vm.ClosePracticeCommand.Execute(null);
        Assert.False(vm.PracticeIsActive);

        var firstAfter = await content.GetLearningItemAsync(first.Id);
        var secondAfter = await content.GetLearningItemAsync(second.Id);
        Assert.False(firstAfter.IsNew);
        Assert.False(secondAfter.IsNew);
        Assert.Equal(futureDue, firstAfter.DueAt);
        Assert.Equal(futureDue, secondAfter.DueAt);
        await using var verify = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, await verify.Reviews.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await verify.StudySessions.CountAsync(TestContext.Current.CancellationToken));
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
