using Microsoft.Data.Sqlite;

namespace Illumination.Infrastructure.Persistence;

public interface ILocalSqliteBackupService
{
    string CreateRollingBackup(string sourceDatabasePath);

    string BackupTo(string sourceDatabasePath, string destinationPath);
}

public sealed class LocalSqliteBackupService : ILocalSqliteBackupService
{
    private const string RollingBackupPrefix = "illumination-backup-";
    private const string SqliteExtension = ".sqlite";

    private readonly string _backupDirectory;
    private readonly int _retentionCount;
    private readonly TimeProvider _timeProvider;

    public LocalSqliteBackupService(string backupDirectory, int retentionCount, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionCount);

        _backupDirectory = Path.GetFullPath(backupDirectory);
        _retentionCount = retentionCount;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string CreateRollingBackup(string sourceDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        Directory.CreateDirectory(_backupDirectory);

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff");
        var destinationPath = Path.Combine(_backupDirectory, $"{RollingBackupPrefix}{timestamp}{NextSuffix(timestamp)}{SqliteExtension}");
        var backupPath = BackupTo(sourceDatabasePath, destinationPath);

        EnforceRetention();
        return backupPath;
    }

    public string BackupTo(string sourceDatabasePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourcePath = Path.GetFullPath(sourceDatabasePath);
        var targetPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The backup destination must differ from the source database.", nameof(destinationPath));
        }

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (targetDirectory is null)
        {
            throw new ArgumentException("The backup destination must include a directory.", nameof(destinationPath));
        }

        Directory.CreateDirectory(targetDirectory);
        using var sourceConnection = OpenSource(sourcePath);
        using var destinationConnection = OpenDestination(targetPath);
        sourceConnection.BackupDatabase(destinationConnection);
        return targetPath;
    }

    private string NextSuffix(string timestamp)
    {
        for (var suffix = 1; ; suffix++)
        {
            var candidateSuffix = $"-{suffix:000}";
            var candidatePath = Path.Combine(_backupDirectory, $"{RollingBackupPrefix}{timestamp}{candidateSuffix}{SqliteExtension}");
            if (!File.Exists(candidatePath))
            {
                return candidateSuffix;
            }
        }
    }

    private void EnforceRetention()
    {
        var rollingBackups = Directory.EnumerateFiles(_backupDirectory, $"{RollingBackupPrefix}*{SqliteExtension}")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        foreach (var obsoleteBackup in rollingBackups.Skip(_retentionCount))
        {
            File.Delete(obsoleteBackup);
        }
    }

    private static SqliteConnection OpenSource(string sourcePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static SqliteConnection OpenDestination(string destinationPath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}
