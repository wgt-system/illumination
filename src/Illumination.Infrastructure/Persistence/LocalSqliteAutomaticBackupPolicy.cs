namespace Illumination.Infrastructure.Persistence;

public sealed class LocalSqliteAutomaticBackupPolicy
{
    private readonly IConfigurableLocalSqliteBackupService _backupService;

    public LocalSqliteAutomaticBackupPolicy(IConfigurableLocalSqliteBackupService backupService)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    public string? CreateStartupBackupIfNeeded(string sourceDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        var sourcePath = Path.GetFullPath(sourceDatabasePath);
        if (!File.Exists(sourcePath)) return null;

        var backupDirectory = _backupService.BackupDirectory;
        var latestBackup = Directory.Exists(backupDirectory)
            ? Directory.EnumerateFiles(backupDirectory, LocalSqliteBackupService.RollingBackupSearchPattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        var sourceModifiedAt = File.GetLastWriteTimeUtc(sourcePath);
        if (latestBackup is not null && File.GetLastWriteTimeUtc(latestBackup) >= sourceModifiedAt)
        {
            return null;
        }

        return _backupService.CreateRollingBackup(sourcePath);
    }
}
