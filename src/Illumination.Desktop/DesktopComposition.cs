using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Application.Insights;
using Illumination.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Desktop;

internal static class DesktopComposition
{
    private const int BackupRetentionCount = 5;

    public static async Task<MainWindowViewModel> CreateAsync()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Illumination");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "illumination.sqlite");
        var backupDirectory = Path.Combine(dataDirectory, "backups");
        var timeProvider = TimeProvider.System;
        var backupService = new LocalSqliteBackupService(
            backupDirectory,
            BackupRetentionCount,
            timeProvider);
        var restoreService = new LocalSqliteRestoreService(
            Path.Combine(dataDirectory, "restore-pending.sqlite"));

        await restoreService.ApplyPendingRestoreAsync(databasePath, backupService);

        var options = new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        await new SqliteMigrationCoordinator(options, backupService).MigrateAsync();

        var contextFactory = new DesktopDbContextFactory(options);
        var content = new ContentManagementService(new EfCoreContentPersistence(contextFactory), timeProvider);
        var contentPersistence = new EfCoreContentPersistence(contextFactory);
        var curation = new ContentCurationService(contentPersistence, contentPersistence);
        var qualityExchange = new QualityReviewExchangeService(contentPersistence, contentPersistence);
        var insights = new LearningInsightService(new EfCoreLearningInsightPersistence(contextFactory), timeProvider);
        var acquisition = new ContentAcquisitionService(
            new EfCoreContentAcquisitionPersistence(contextFactory),
            timeProvider);
        var study = new StudySessionService(
            new EfCoreStudySessionPersistence(contextFactory),
            timeProvider,
            new RandomStudySessionOrdering(),
            new EfCoreStudyEvaluationPreferencePersistence(contextFactory));
        var viewModel = new MainWindowViewModel(content, study, acquisition, curation, qualityExchange, timeProvider, insights);
        viewModel.ConfigureLocalData(backupService, restoreService, databasePath, backupDirectory);
        viewModel.ConfigureDeckExport(new ContentBundleExportService(content));
        await viewModel.InitializeAsync();
        return viewModel;
    }

    private sealed class DesktopDbContextFactory(DbContextOptions<IlluminationDbContext> options)
        : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(options);
    }
}
