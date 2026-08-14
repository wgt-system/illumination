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

    [Fact]
    public async Task Released_v03_database_is_backed_up_and_preserved_before_v04_migration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new DatabaseFixture();
        await using (var oldContext = new IlluminationDbContext(fixture.CreateOptions()))
        {
            await oldContext.Database.MigrateAsync("20260809223433_AddContentImportProvenance", cancellationToken);
            await oldContext.Database.OpenConnectionAsync(cancellationToken);
            await using var command = oldContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "INSERT INTO LearningItems (LearningItemId, Prompt, ReferenceSolutionContent, ResponseMode, LowInteractionEligible, LifecycleState, IsNew, DueAt, Difficulty, StabilityDays, IsInShortTermRelearning) VALUES ($id, 'v0.3 prompt', 'v0.3 solution', 'SelfAssessed', 1, 'Active', 0, '2030-01-02T03:04:05.0000000+00:00', 4.25, 12.5, 0)";
            command.Parameters.Add(new SqliteParameter("$id", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
            await command.ExecuteNonQueryAsync(cancellationToken);
            await using var review = oldContext.Database.GetDbConnection().CreateCommand();
            review.CommandText = "INSERT INTO Reviews (ReviewId, LearningItemId, CompletedAt, Assessment, SubmittedResponse) VALUES ($review, $item, '2030-01-02T03:04:05.0000000+00:00', 'Good', NULL)";
            review.Parameters.Add(new SqliteParameter("$review", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
            review.Parameters.Add(new SqliteParameter("$item", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
            await review.ExecuteNonQueryAsync(cancellationToken);
        }

        var coordinator = new SqliteMigrationCoordinator(fixture.CreateOptions(), new LocalSqliteBackupService(fixture.BackupDirectory, 1, fixture.TimeProvider));
        await coordinator.MigrateAsync(cancellationToken);

        var backup = Assert.Single(Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite"));
        Assert.Equal("v0.3 prompt", ReadScalar(backup, "SELECT Prompt FROM LearningItems"));
        Assert.False(HasTable(backup, "QualityReviews"));
        Assert.Equal("1", ReadScalar(backup, "SELECT COUNT(*) FROM Reviews"));
        Assert.Equal("v0.3 prompt", ReadScalar(fixture.DatabasePath, "SELECT Prompt FROM LearningItems"));
        Assert.Equal("1", ReadScalar(fixture.DatabasePath, "SELECT ContentRevision FROM LearningItems"));
        Assert.Equal("1", ReadScalar(fixture.DatabasePath, "SELECT COUNT(*) FROM Reviews"));
        Assert.True(HasTable(fixture.DatabasePath, "QualityReviews"));
    }

    [Fact]
    public async Task Released_v04_database_is_backed_up_and_receives_v05_neutral_defaults_and_legacy_choice_ids()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = new DatabaseFixture();
        const string itemId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string reviewId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        const string sessionId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        await using (var oldContext = new IlluminationDbContext(fixture.CreateOptions()))
        {
            await oldContext.Database.MigrateAsync("20260813164131_PersistV04ContentQuality", cancellationToken);
            await oldContext.Database.OpenConnectionAsync(cancellationToken);
            await using var command = oldContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "INSERT INTO LearningItems (LearningItemId, Prompt, ReferenceSolutionContent, ResponseMode, LowInteractionEligible, LifecycleState, IsNew, DueAt, Difficulty, StabilityDays, IsInShortTermRelearning, ContentRevision) VALUES ($item, 'v0.4 prompt', 'v0.4 solution', 'Selection', 0, 'Active', 1, '2030-01-02T03:04:05.0000000+00:00', 5.0, 0.5, 0, 1); INSERT INTO AnswerChoices (LearningItemId, Role, Position, Text, IsCorrect) VALUES ($item, 'Direct', 0, 'A', 1), ($item, 'Direct', 1, 'B', 0), ($item, 'Assistance', 0, 'Help A', 0), ($item, 'Assistance', 1, 'Help B', 0); INSERT INTO Reviews (ReviewId, LearningItemId, CompletedAt, Assessment, SubmittedResponse) VALUES ($review, $item, '2030-01-02T03:04:05.0000000+00:00', 'Gut', 'A'); INSERT INTO StudySessions (StudySessionId, StartedAt, CompletedAt) VALUES ($session, '2030-01-02T03:04:05.0000000+00:00', NULL); INSERT INTO StudySessionQueue (StudySessionId, Position, LearningItemId) VALUES ($session, 0, $item); INSERT INTO StudySessionReviews (StudySessionId, Position, ReviewId) VALUES ($session, 0, $review);";
            command.Parameters.Add(new SqliteParameter("$item", Guid.Parse(itemId)));
            command.Parameters.Add(new SqliteParameter("$review", Guid.Parse(reviewId)));
            command.Parameters.Add(new SqliteParameter("$session", Guid.Parse(sessionId)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await new SqliteMigrationCoordinator(fixture.CreateOptions(), new LocalSqliteBackupService(fixture.BackupDirectory, 1, fixture.TimeProvider)).MigrateAsync(cancellationToken);

        var backup = Assert.Single(Directory.GetFiles(fixture.BackupDirectory, "illumination-backup-*.sqlite"));
        Assert.Equal("4", ReadScalar(backup, "SELECT COUNT(*) FROM AnswerChoices"));
        Assert.False(HasColumn(backup, "AnswerChoices", "ChoiceId"));
        Assert.Equal("legacy-direct-0", ReadScalar(fixture.DatabasePath, "SELECT ChoiceId FROM AnswerChoices WHERE Role = 'Direct' AND Position = 0"));
        Assert.Equal("legacy-direct-1", ReadScalar(fixture.DatabasePath, "SELECT ChoiceId FROM AnswerChoices WHERE Role = 'Direct' AND Position = 1"));
        Assert.Equal("legacy-assistance-0", ReadScalar(fixture.DatabasePath, "SELECT ChoiceId FROM AnswerChoices WHERE Role = 'Assistance' AND Position = 0"));
        Assert.Equal("Manual", ReadScalar(fixture.DatabasePath, "SELECT EvaluationMode FROM StudySessions"));
        Assert.Equal("0", ReadScalar(fixture.DatabasePath, "SELECT CAST(ConsiderAssistance AS INTEGER) FROM StudySessions"));
        Assert.Equal("0", ReadScalar(fixture.DatabasePath, "SELECT HintCount FROM Reviews"));
        Assert.Equal("0", ReadScalar(fixture.DatabasePath, "SELECT CAST(ReferenceSolutionRevealed AS INTEGER) FROM Reviews"));
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

    private static string ReadScalar(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }

    private static bool HasColumn(string databasePath, string tableName, string columnName)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pragma_table_info($table) WHERE name = $column)";
        command.Parameters.AddWithValue("$table", tableName);
        command.Parameters.AddWithValue("$column", columnName);
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
