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
        var defaultBackupDirectory = Path.Combine(dataDirectory, "backups");
        var settingsStore = new LocalDataSettingsStore(Path.Combine(dataDirectory, "local-data-settings.json"));
        var backupDirectory = settingsStore.LoadBackupDirectory(defaultBackupDirectory);
        var timeProvider = TimeProvider.System;
        var backupService = new LocalSqliteBackupService(
            backupDirectory,
            BackupRetentionCount,
            timeProvider);
        var databaseExistedAtStartup = File.Exists(databasePath);

        var options = new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        await new SqliteMigrationCoordinator(options, backupService).MigrateAsync();
        if (databaseExistedAtStartup)
        {
            new LocalSqliteAutomaticBackupPolicy(backupService).CreateStartupBackupIfNeeded(databasePath);
        }

        var contextFactory = new DesktopDbContextFactory(options);
        var contentPersistence = new EfCoreContentPersistence(contextFactory);
        var content = new ContentManagementService(contentPersistence, timeProvider);
        var learningStateMaintenance = new LearningStateMaintenanceService(
            contentPersistence,
            new EfCoreLearningStateBatchPersistence(contextFactory),
            timeProvider);
        var curation = new ContentCurationService(contentPersistence, contentPersistence);
        var qualityExchange = new QualityReviewExchangeService(contentPersistence, contentPersistence);
        var insights = new LearningInsightService(new EfCoreLearningInsightPersistence(contextFactory), timeProvider);
        var acquisitionPersistence = new BackupBeforeContentAcquisitionPersistence(
            new EfCoreContentAcquisitionPersistence(contextFactory),
            backupService,
            databasePath);
        var acquisition = new ContentAcquisitionService(acquisitionPersistence, timeProvider);
        var study = new StudySessionService(
            new EfCoreStudySessionPersistence(contextFactory),
            timeProvider,
            new RandomStudySessionOrdering(),
            new EfCoreStudyEvaluationPreferencePersistence(contextFactory));
        var viewModel = new MainWindowViewModel(content, study, acquisition, curation, qualityExchange, timeProvider, insights);
        viewModel.ConfigureLocalData(backupService, settingsStore, databasePath, defaultBackupDirectory);
        viewModel.ConfigureDeckExport(new ContentBundleExportService(content));
        viewModel.ConfigureLearningStateMaintenance(learningStateMaintenance);
        viewModel.ContentAcquisition.ConfigureContentPreview(content);
        viewModel.ContentAcquisition.ConfigureExistingDeckContent(() => viewModel.LearningItems.ToArray());
        await viewModel.InitializeAsync();
        return viewModel;
    }

    private sealed class DesktopDbContextFactory(DbContextOptions<IlluminationDbContext> options)
        : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(options);
    }
}
