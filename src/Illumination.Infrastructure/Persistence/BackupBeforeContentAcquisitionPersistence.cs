using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;

namespace Illumination.Infrastructure.Persistence;

public sealed class BackupBeforeContentAcquisitionPersistence : IContentAcquisitionPersistence
{
    private readonly IContentAcquisitionPersistence _inner;
    private readonly ILocalSqliteBackupService _backupService;
    private readonly string _databasePath;

    public BackupBeforeContentAcquisitionPersistence(
        IContentAcquisitionPersistence inner,
        ILocalSqliteBackupService backupService,
        string databasePath)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
        _inner.LoadLearningItemsAsync(cancellationToken);

    public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
        _inner.LoadDecksAsync(cancellationToken);

    public async Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Provenance.AcceptedOperationCount > 0 && File.Exists(_databasePath))
        {
            _backupService.CreateRollingBackup(_databasePath);
        }

        await _inner.CommitAsync(snapshot, cancellationToken);
    }
}
