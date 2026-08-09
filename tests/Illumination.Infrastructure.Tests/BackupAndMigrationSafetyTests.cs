using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public sealed class BackupAndMigrationSafetyTests
{
    [Fact]
    public void Manual_backup_is_a_readable_sqlite_copy()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabaseWithMarker();
        var destination = Path.Combine(fixture.Root, "manual", "copy.sqlite");
        var service = new LocalSqliteBackupService(fixture.BackupDirectory, 3, fixture.TimeProvider);

        service.BackupTo(fixture.DatabasePath, destination);

        using var connection = new SqliteConnection($"Data Source={destination};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM marker";
        Assert.Equal("before", command.ExecuteScalar());
    }

    [Fact]
    public void Rolling_backup_retention_is_deterministic()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabaseWithMarker();
        var service = new LocalSqliteBackupService(fixture.BackupDirectory, 2, fixture.TimeProvider);

        service.CreateRollingBackup(fixture.DatabasePath);
        service.CreateRollingBackup(fixture.DatabasePath);
        service.CreateRollingBackup(fixture.DatabasePath);

        var backups = Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite")
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, backups.Length);
        Assert.Contains("illumination-backup-20300102T030405000-002.sqlite", backups);
        Assert.Contains("illumination-backup-20300102T030405000-003.sqlite", backups);
    }

    [Fact]
    public async Task Existing_database_is_backed_up_before_migration_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabaseWithMarker();
        var backupService = new LocalSqliteBackupService(fixture.BackupDirectory, 1, fixture.TimeProvider);
        var coordinator = new SqliteMigrationCoordinator(fixture.CreateOptions(), backupService);

        await coordinator.MigrateAsync(cancellationToken);

        var backup = Assert.Single(Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite"));
        Assert.True(HasTable(backup, "marker"));
        Assert.False(HasTable(backup, "LearningItems"));
        Assert.True(HasTable(fixture.DatabasePath, "LearningItems"));
    }

    [Fact]
    public async Task Failed_backup_prevents_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabaseWithMarker();
        var backupService = new ThrowingBackupService();
        var coordinator = new SqliteMigrationCoordinator(fixture.CreateOptions(), backupService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.MigrateAsync(cancellationToken));

        Assert.True(HasTable(fixture.DatabasePath, "marker"));
        Assert.False(HasTable(fixture.DatabasePath, "LearningItems"));
    }

    [Fact]
    public async Task First_time_database_creation_does_not_require_a_backup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new DatabaseFixture();
        var backupService = new RecordingBackupService();
        var coordinator = new SqliteMigrationCoordinator(fixture.CreateOptions(), backupService);

        await coordinator.MigrateAsync(cancellationToken);

        Assert.Empty(backupService.SourcePaths);
        Assert.True(File.Exists(fixture.DatabasePath));
        Assert.True(HasTable(fixture.DatabasePath, "LearningItems"));
    }

    private static bool HasTable(string databasePath, string tableName)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name)";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private sealed class ThrowingBackupService : ILocalSqliteBackupService
    {
        public string CreateRollingBackup(string sourceDatabasePath) => throw new InvalidOperationException("Backup failed.");

        public string BackupTo(string sourceDatabasePath, string destinationPath) => throw new InvalidOperationException("Backup failed.");
    }

    private sealed class RecordingBackupService : ILocalSqliteBackupService
    {
        public List<string> SourcePaths { get; } = [];

        public string CreateRollingBackup(string sourceDatabasePath)
        {
            SourcePaths.Add(sourceDatabasePath);
            return sourceDatabasePath;
        }

        public string BackupTo(string sourceDatabasePath, string destinationPath)
        {
            SourcePaths.Add(sourceDatabasePath);
            return destinationPath;
        }
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "illumination-backup-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            DatabasePath = Path.Combine(Root, "illumination.sqlite");
            BackupDirectory = Path.Combine(Root, "backups");
            TimeProvider = new FixedTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        }

        public string Root { get; }
        public string DatabasePath { get; }
        public string BackupDirectory { get; }
        public FixedTimeProvider TimeProvider { get; }

        public DbContextOptions<IlluminationDbContext> CreateOptions() =>
            new DbContextOptionsBuilder<IlluminationDbContext>()
                .UseSqlite($"Data Source={DatabasePath};Pooling=False")
                .Options;

        public void CreateDatabaseWithMarker()
        {
            using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE marker (value TEXT NOT NULL); INSERT INTO marker (value) VALUES ('before');";
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
