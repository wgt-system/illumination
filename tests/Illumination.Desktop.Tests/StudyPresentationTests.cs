using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Desktop;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class StudyPresentationTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Preview_formatter_uses_application_projection_values()
    {
        var nochmal = new StudyAssessmentPreview(StudyLearningAssessment.Nochmal, true, false, 1, 1, null);
        var schwerFallback = new StudyAssessmentPreview(StudyLearningAssessment.Schwer, true, false, 3, 3, null);
        var unsicher = new StudyAssessmentPreview(StudyLearningAssessment.Unsicher, true, false, 4, 4, null);
        var gut = new StudyAssessmentPreview(StudyLearningAssessment.Gut, false, true, null, null, Now.AddDays(12));
        var leicht = new StudyAssessmentPreview(StudyLearningAssessment.Leicht, false, true, null, null, Now.AddDays(21));

        Assert.Equal("after 1 card", StudyPresentationFormatter.FormatPreview(nochmal, Now));
        Assert.Equal("after 3 cards", StudyPresentationFormatter.FormatPreview(schwerFallback, Now));
        Assert.Equal("end of stack", StudyPresentationFormatter.FormatPreview(unsicher, Now));
        Assert.Equal("12 days", StudyPresentationFormatter.FormatPreview(gut, Now));
        Assert.Equal("3 weeks", StudyPresentationFormatter.FormatPreview(leicht, Now));
    }

    [Fact]
    public void Preview_formatter_shows_single_card_fallback_without_scheduler_calculation()
    {
        var preview = new StudyAssessmentPreview(StudyLearningAssessment.Nochmal, true, false, 0, 0, null);

        Assert.Equal("again immediately", StudyPresentationFormatter.FormatPreview(preview, Now));
    }

    [Fact]
    public async Task ViewModel_refreshes_transparency_after_grade_and_clears_after_completion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var timeProvider = new FixedTimeProvider(Now);
        var content = new ContentManagementService(new EfCoreContentPersistence(factory), timeProvider);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), timeProvider, new IdentityOrdering());
        var first = await content.CreateLearningItemAsync(new CreateLearningItemCommand("First prompt", "First solution"), TestContext.Current.CancellationToken);
        var second = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Second prompt", "Second solution"), TestContext.Current.CancellationToken);
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Deck"), TestContext.Current.CancellationToken);
        await content.AddLearningItemToDeckAsync(deck.Id, first.Id, TestContext.Current.CancellationToken);
        await content.AddLearningItemToDeckAsync(deck.Id, second.Id, TestContext.Current.CancellationToken);

        var acquisition = new ContentAcquisitionService(new FakeAcquisitionPersistence(), timeProvider);
        var viewModel = new MainWindowViewModel(content, study, acquisition, timeProvider);
        await viewModel.InitializeAsync();
        await viewModel.StartSessionCommand.ExecuteAsync(null);

        Assert.True(viewModel.SessionIsActive);
        Assert.NotNull(viewModel.CurrentStudyItem);
        Assert.Equal(5, viewModel.AssessmentPreviews.Count);
        Assert.Equal(1, viewModel.RemainingQueueEntryCount);
        Assert.Single(viewModel.UpcomingStudyItems);

        viewModel.IsSolutionRevealed = true;
        await viewModel.GradeNochmalCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSolutionRevealed);
        Assert.Equal(5, viewModel.AssessmentPreviews.Count);
        Assert.Equal(1, viewModel.RemainingQueueEntryCount);
        Assert.True(Assert.Single(viewModel.UpcomingStudyItems).ReinforcementRequired);

        await viewModel.CompleteSessionCommand.ExecuteAsync(null);

        Assert.False(viewModel.SessionIsActive);
        Assert.Null(viewModel.CurrentStudyItem);
        Assert.Empty(viewModel.AssessmentPreviews);
        Assert.Empty(viewModel.UpcomingStudyItems);
        Assert.Equal("Study Session completed.", viewModel.StatusMessage);
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
