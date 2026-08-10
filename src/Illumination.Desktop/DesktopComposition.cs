using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
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
        var options = new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        var timeProvider = TimeProvider.System;
        var backupService = new LocalSqliteBackupService(
            Path.Combine(dataDirectory, "backups"),
            BackupRetentionCount,
            timeProvider);

        await new SqliteMigrationCoordinator(options, backupService).MigrateAsync();

        var contextFactory = new DesktopDbContextFactory(options);
        var content = new ContentManagementService(new EfCoreContentPersistence(contextFactory), timeProvider);
        var acquisition = new ContentAcquisitionService(
            new EfCoreContentAcquisitionPersistence(contextFactory),
            timeProvider);
        var study = new StudySessionService(
            new EfCoreStudySessionPersistence(contextFactory),
            timeProvider,
            new RandomStudySessionOrdering());
        var viewModel = new MainWindowViewModel(content, study, acquisition, timeProvider);
        await viewModel.InitializeAsync();
        return viewModel;
    }

    private sealed class DesktopDbContextFactory(DbContextOptions<IlluminationDbContext> options)
        : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(options);
    }
}
