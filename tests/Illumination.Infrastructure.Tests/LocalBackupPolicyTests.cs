using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public sealed class LocalBackupPolicyTests
{
    [Fact]
    public void Automatic_startup_backup_runs_only_when_database_changed_since_latest_snapshot()
    {
        using var fixture = new BackupFixture();
        fixture.CreateDatabase();
        var service = new LocalSqliteBackupService(fixture.BackupDirectory, 5, TimeProvider.System);
        var policy = new LocalSqliteAutomaticBackupPolicy(service);

        var first = policy.CreateStartupBackupIfNeeded(fixture.DatabasePath);
        var unchanged = policy.CreateStartupBackupIfNeeded(fixture.DatabasePath);
        File.SetLastWriteTimeUtc(fixture.DatabasePath, File.GetLastWriteTimeUtc(first!).AddSeconds(2));
        var changed = policy.CreateStartupBackupIfNeeded(fixture.DatabasePath);

        Assert.NotNull(first);
        Assert.Null(unchanged);
        Assert.NotNull(changed);
        Assert.Equal(2, Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite").Length);
    }

    [Fact]
    public void Configured_backup_directory_is_persisted_and_used_for_rolling_backups()
    {
        using var fixture = new BackupFixture();
        fixture.CreateDatabase();
        var settingsPath = Path.Combine(fixture.Root, "local-data-settings.json");
        var defaultDirectory = fixture.BackupDirectory;
        var configuredDirectory = Path.Combine(fixture.Root, "configured-backups");
        var store = new LocalDataSettingsStore(settingsPath);
        store.SaveBackupDirectory(configuredDirectory);

        var loaded = new LocalDataSettingsStore(settingsPath).LoadBackupDirectory(defaultDirectory);
        var service = new LocalSqliteBackupService(defaultDirectory, 5, TimeProvider.System);
        service.SetBackupDirectory(loaded);
        var backup = service.CreateRollingBackup(fixture.DatabasePath);

        Assert.Equal(Path.GetFullPath(configuredDirectory), loaded);
        Assert.Equal(Path.GetFullPath(configuredDirectory), service.BackupDirectory);
        Assert.Equal(Path.GetFullPath(configuredDirectory), Path.GetDirectoryName(backup));
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public async Task Content_bundle_commit_is_preceded_by_a_readable_backup()
    {
        using var fixture = new BackupFixture();
        fixture.CreateDatabase();
        var inner = new RecordingContentAcquisitionPersistence();
        var backupService = new LocalSqliteBackupService(fixture.BackupDirectory, 5, TimeProvider.System);
        var persistence = new BackupBeforeContentAcquisitionPersistence(inner, backupService, fixture.DatabasePath);

        await persistence.CommitAsync(CreateCommitSnapshot(), TestContext.Current.CancellationToken);

        Assert.True(inner.CommitCalled);
        var backup = Assert.Single(Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite"));
        using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM marker";
        Assert.Equal("before-import", command.ExecuteScalar());
    }

    [Fact]
    public async Task Failed_pre_import_backup_prevents_content_mutation()
    {
        using var fixture = new BackupFixture();
        fixture.CreateDatabase();
        var inner = new RecordingContentAcquisitionPersistence();
        var persistence = new BackupBeforeContentAcquisitionPersistence(inner, new ThrowingBackupService(), fixture.DatabasePath);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            persistence.CommitAsync(CreateCommitSnapshot(), TestContext.Current.CancellationToken));

        Assert.False(inner.CommitCalled);
    }

    private static ContentAcquisitionCommitSnapshot CreateCommitSnapshot() => new(
        Array.Empty<LearningItemSnapshot>(),
        Array.Empty<DeckSnapshot>(),
        new ContentAcquisitionProvenanceSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "illumination.content-bundle",
            "1.0",
            null,
            null,
            AcceptedOperationCount: 1,
            CreatedLearningItemCount: 0,
            UpdatedLearningItemCount: 0,
            CreatedDeckCount: 0,
            UpdatedDeckCount: 0,
            AssignmentCount: 0));

    private sealed class RecordingContentAcquisitionPersistence : IContentAcquisitionPersistence
    {
        public bool CommitCalled { get; private set; }

        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Array.Empty<LearningItemSnapshot>());

        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>(Array.Empty<DeckSnapshot>());

        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            CommitCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingBackupService : ILocalSqliteBackupService
    {
        public string CreateRollingBackup(string sourceDatabasePath) => throw new InvalidOperationException("Backup failed.");
        public string BackupTo(string sourceDatabasePath, string destinationPath) => throw new InvalidOperationException("Backup failed.");
    }

    private sealed class BackupFixture : IDisposable
    {
        public BackupFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "illumination-v08-backup-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "illumination.sqlite");
            BackupDirectory = Path.Combine(Root, "backups");
        }

        public string Root { get; }
        public string DatabasePath { get; }
        public string BackupDirectory { get; }

        public void CreateDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE marker (value TEXT NOT NULL); INSERT INTO marker (value) VALUES ('before-import');";
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
