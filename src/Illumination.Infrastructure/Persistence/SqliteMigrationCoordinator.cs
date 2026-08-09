using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class SqliteMigrationCoordinator
{
    private readonly DbContextOptions<IlluminationDbContext> _options;
    private readonly ILocalSqliteBackupService _backupService;

    public SqliteMigrationCoordinator(
        DbContextOptions<IlluminationDbContext> options,
        ILocalSqliteBackupService backupService)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = new IlluminationDbContext(_options);
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length == 0)
        {
            return;
        }

        var databasePath = context.Database.GetDbConnection().DataSource;
        if (IsExistingDatabase(databasePath))
        {
            _backupService.CreateRollingBackup(databasePath);
        }

        await context.Database.MigrateAsync(cancellationToken);
    }

    private static bool IsExistingDatabase(string databasePath) =>
        !string.IsNullOrWhiteSpace(databasePath)
        && !string.Equals(databasePath, ":memory:", StringComparison.OrdinalIgnoreCase)
        && File.Exists(databasePath);
}
