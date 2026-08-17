using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Infrastructure.Persistence;

namespace Illumination.Desktop;

public sealed partial class LocalDataViewModel : ObservableObject
{
    private readonly ILocalSqliteBackupService _backupService;
    private readonly string _databasePath;
    private readonly string _backupDirectory;
    private readonly Action<string> _reportStatus;
    private IDesktopInteractionService? _interactions;

    public LocalDataViewModel(
        ILocalSqliteBackupService backupService,
        string databasePath,
        string backupDirectory,
        Action<string> reportStatus)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _databasePath = Path.GetFullPath(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
        _backupDirectory = Path.GetFullPath(backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory)));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));
    }

    public string DatabaseLocation => _databasePath;
    public string BackupLocation => _backupDirectory;

    [ObservableProperty]
    private string _localStatus = "Local data is stored on this device.";

    public void AttachDesktopInteractions(IDesktopInteractionService interactions)
    {
        _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
    }

    [RelayCommand]
    private void CreateBackup()
    {
        try
        {
            var path = _backupService.CreateRollingBackup(_databasePath);
            LocalStatus = $"Backup created: {Path.GetFileName(path)}";
            _reportStatus(LocalStatus);
        }
        catch (Exception exception)
        {
            LocalStatus = exception.Message;
            _reportStatus(LocalStatus);
        }
    }

    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        if (_interactions is null)
        {
            LocalStatus = "File export is unavailable.";
            _reportStatus(LocalStatus);
            return;
        }

        try
        {
            var localBackup = _backupService.CreateRollingBackup(_databasePath);
            var bytes = await File.ReadAllBytesAsync(localBackup);
            var suggestedName = $"illumination-backup-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite";
            var saved = await _interactions.SaveSqliteFileAsync(suggestedName, bytes);
            LocalStatus = saved ? "Portable backup exported." : "Backup export cancelled.";
            _reportStatus(LocalStatus);
        }
        catch (Exception exception)
        {
            LocalStatus = exception.Message;
            _reportStatus(LocalStatus);
        }
    }
}
