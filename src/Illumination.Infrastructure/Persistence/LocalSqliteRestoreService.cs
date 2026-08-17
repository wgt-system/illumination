using Microsoft.Data.Sqlite;

namespace Illumination.Infrastructure.Persistence;

public interface ILocalSqliteRestoreService
{
    string PendingRestorePath { get; }
    bool HasPendingRestore { get; }
    Task StageRestoreAsync(byte[] databaseBytes, CancellationToken cancellationToken = default);
    Task<string?> ApplyPendingRestoreAsync(string targetDatabasePath, ILocalSqliteBackupService backupService, CancellationToken cancellationToken = default);
    void CancelPendingRestore();
}

public sealed class LocalSqliteRestoreService : ILocalSqliteRestoreService
{
    private readonly string _pendingRestorePath;

    public LocalSqliteRestoreService(string pendingRestorePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pendingRestorePath);
        _pendingRestorePath = Path.GetFullPath(pendingRestorePath);
    }

    public string PendingRestorePath => _pendingRestorePath;
    public bool HasPendingRestore => File.Exists(_pendingRestorePath);

    public async Task StageRestoreAsync(byte[] databaseBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databaseBytes);
        if (databaseBytes.Length == 0) throw new InvalidDataException("The selected backup is empty.");

        var directory = Path.GetDirectoryName(_pendingRestorePath)
            ?? throw new InvalidOperationException("The pending restore path has no directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _pendingRestorePath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, databaseBytes, cancellationToken);
            await ValidateIlluminationDatabaseAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, _pendingRestorePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<string?> ApplyPendingRestoreAsync(
        string targetDatabasePath,
        ILocalSqliteBackupService backupService,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDatabasePath);
        ArgumentNullException.ThrowIfNull(backupService);
        if (!HasPendingRestore) return null;

        var targetPath = Path.GetFullPath(targetDatabasePath);
        await ValidateIlluminationDatabaseAsync(_pendingRestorePath, cancellationToken);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("The target database path has no directory.");
        Directory.CreateDirectory(targetDirectory);

        if (File.Exists(targetPath)) backupService.CreateRollingBackup(targetPath);

        var replacementPath = targetPath + ".restore-new";
        try
        {
            File.Copy(_pendingRestorePath, replacementPath, overwrite: true);
            await ValidateIlluminationDatabaseAsync(replacementPath, cancellationToken);
            File.Move(replacementPath, targetPath, overwrite: true);
            DeleteSqliteSidecars(targetPath);
            File.Delete(_pendingRestorePath);
            return targetPath;
        }
        finally
        {
            if (File.Exists(replacementPath)) File.Delete(replacementPath);
        }
    }

    public void CancelPendingRestore()
    {
        if (File.Exists(_pendingRestorePath)) File.Delete(_pendingRestorePath);
        var temporaryPath = _pendingRestorePath + ".tmp";
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
    }

    private static void DeleteSqliteSidecars(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var sidecar = databasePath + suffix;
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }
    }

    private static async Task ValidateIlluminationDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            await connection.OpenAsync(cancellationToken);

            await using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The selected SQLite backup failed its integrity check.");
            }

            await using var schema = connection.CreateCommand();
            schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('LearningItems', 'Decks');";
            var knownTableCount = Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken));
            if (knownTableCount != 2)
                throw new InvalidDataException("The selected SQLite file is not an Illumination database backup.");
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("The selected file is not a readable SQLite database.", exception);
        }
    }
}
