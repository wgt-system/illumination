using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Infrastructure.Persistence;

namespace Illumination.Desktop;

public sealed partial class LocalDataViewModel : ObservableObject
{
    private readonly IConfigurableLocalSqliteBackupService _backupService;
    private readonly LocalDataSettingsStore _settingsStore;
    private readonly string _databasePath;
    private readonly string _defaultBackupDirectory;
    private readonly Action<string> _reportStatus;
    private IDesktopInteractionService? _interactions;

    public LocalDataViewModel(
        IConfigurableLocalSqliteBackupService backupService,
        LocalDataSettingsStore settingsStore,
        string databasePath,
        string defaultBackupDirectory,
        Action<string> reportStatus)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _databasePath = Path.GetFullPath(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
        _defaultBackupDirectory = Path.GetFullPath(defaultBackupDirectory ?? throw new ArgumentNullException(nameof(defaultBackupDirectory)));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));
        _backupDirectoryInput = _backupService.BackupDirectory;
    }

    public string DatabaseLocation => _databasePath;
    public string BackupLocation => _backupService.BackupDirectory;

    [ObservableProperty]
    private string _backupDirectoryInput;

    [ObservableProperty]
    private string _localStatus = "Local data is stored on this device.";

    public void AttachDesktopInteractions(IDesktopInteractionService interactions)
    {
        _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
    }

    [RelayCommand]
    private void ApplyBackupDirectory()
    {
        try
        {
            var path = Path.GetFullPath(BackupDirectoryInput.Trim());
            Directory.CreateDirectory(path);
            _backupService.SetBackupDirectory(path);
            _settingsStore.SaveBackupDirectory(path);
            BackupDirectoryInput = path;
            OnPropertyChanged(nameof(BackupLocation));
            LocalStatus = $"Local backup location: {path}";
            _reportStatus(LocalStatus);
        }
        catch (Exception exception)
        {
            LocalStatus = exception.Message;
            _reportStatus(LocalStatus);
        }
    }

    [RelayCommand]
    private void ResetBackupDirectory()
    {
        try
        {
            Directory.CreateDirectory(_defaultBackupDirectory);
            _backupService.SetBackupDirectory(_defaultBackupDirectory);
            _settingsStore.SaveBackupDirectory(_defaultBackupDirectory);
            BackupDirectoryInput = _defaultBackupDirectory;
            OnPropertyChanged(nameof(BackupLocation));
            LocalStatus = $"Local backup location reset: {_defaultBackupDirectory}";
            _reportStatus(LocalStatus);
        }
        catch (Exception exception)
        {
            LocalStatus = exception.Message;
            _reportStatus(LocalStatus);
        }
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
