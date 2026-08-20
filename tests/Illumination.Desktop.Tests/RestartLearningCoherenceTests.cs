using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Desktop;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class RestartLearningCoherenceTests
{
    private static readonly DateTimeOffset Now = new(2033, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public async Task Restart_selected_item_refreshes_real_state_and_a_new_study_can_select_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await MigrateAsync(factory);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand("makan", "essen"));
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Indo"));
        await content.AddLearningItemToDeckAsync(deck.Id, item.Id);

        var vm = CreateViewModel(content, study, persistence, time);
        vm.ConfigureLearningStateMaintenance(new LearningStateMaintenanceService(
            persistence,
            new EfCoreLearningStateBatchPersistence(factory),
            time));
        await vm.InitializeAsync();

        // Move the item into normal future scheduling through the real Study path first.
        vm.SelectedStudyDeck = vm.Decks.Single(x => x.Id == deck.Id);
        await vm.StartSessionCommand.ExecuteAsync(null);
        Assert.Equal(item.Id, vm.CurrentStudyItem?.Id);
        await vm.GradeGutCommand.ExecuteAsync(null);
        await vm.CompleteSessionCommand.ExecuteAsync(null);

        var future = vm.LearningItems.Single(x => x.Id == item.Id);
        Assert.False(future.IsNew);
        Assert.True(future.DueAt > Now);

        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deck.Id);
        vm.SelectedDeckItem = Assert.Single(vm.SelectedDeckItems);
        await vm.RestartSelectedDeckItemLearningCommand.ExecuteAsync(null); // arm confirmation
        await vm.RestartSelectedDeckItemLearningCommand.ExecuteAsync(null); // commit

        var restarted = vm.LearningItems.Single(x => x.Id == item.Id);
        Assert.True(restarted.IsNew);
        Assert.Equal(Now, restarted.DueAt);
        Assert.Equal(item.Id, vm.SelectedDeckItem?.Id);
        Assert.Contains("refreshed state confirms", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

        // A fresh scheduled Study with this one-card Deck must see the restarted item.
        vm.SelectedStudyDeck = vm.Decks.Single(x => x.Id == deck.Id);
        await vm.StartSessionCommand.ExecuteAsync(null);
        Assert.True(vm.SessionIsActive);
        Assert.Equal(item.Id, vm.CurrentStudyItem?.Id);

        await using var verify = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await verify.Reviews.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Restart_deck_resets_every_member_but_default_study_limit_still_selects_only_twenty_new_items()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await MigrateAsync(factory);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Large"));
        for (var index = 0; index < 25; index++)
        {
            var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand($"item-{index:00}", $"answer-{index:00}"));
            await content.AddLearningItemToDeckAsync(deck.Id, item.Id);
        }

        // Put all items in established future state so the only new items after the operation are restarted ones.
        await using (var mutate = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            foreach (var record in await mutate.LearningItems.ToArrayAsync(TestContext.Current.CancellationToken))
            {
                record.IsNew = false;
                record.DueAt = Now.AddDays(90);
                record.Difficulty = 3;
                record.StabilityDays = 45;
                record.IsInShortTermRelearning = false;
            }
            await mutate.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var vm = CreateViewModel(content, study, persistence, time);
        vm.ConfigureLearningStateMaintenance(new LearningStateMaintenanceService(
            persistence,
            new EfCoreLearningStateBatchPersistence(factory),
            time));
        await vm.InitializeAsync();
        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deck.Id);

        await vm.RestartSelectedDeckLearningCommand.ExecuteAsync(null);
        await vm.RestartSelectedDeckLearningCommand.ExecuteAsync(null);

        Assert.Equal(25, vm.LearningItems.Count(item => item.DeckIds.Contains(deck.Id) && item.IsNew && item.DueAt <= Now));
        Assert.Contains("current new-card limit (20)", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

        vm.SelectedStudyDeck = vm.Decks.Single(x => x.Id == deck.Id);
        vm.StudyAllNew = false;
        vm.StudyNewItemLimitText = "20";
        await vm.StartConfiguredSessionCommand.ExecuteAsync(null);

        Assert.True(vm.SessionIsActive);
        Assert.NotNull(vm.CurrentStudyItem);
        Assert.Equal(20, vm.RemainingQueueEntryCount + 1);
    }

    private static MainWindowViewModel CreateViewModel(
        ContentManagementService content,
        StudySessionService study,
        EfCoreContentPersistence persistence,
        TimeProvider time) =>
        new(
            content,
            study,
            new ContentAcquisitionService(new FakeAcquisitionPersistence(), time),
            new ContentCurationService(persistence, persistence),
            new QualityReviewExchangeService(persistence, persistence),
            time);

    private static async Task MigrateAsync(FixedDbContextFactory factory)
    {
        await using var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
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
