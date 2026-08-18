using Illumination.Infrastructure.Persistence;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    public LocalDataViewModel? LocalData { get; private set; }

    public void ConfigureLocalData(
        IConfigurableLocalSqliteBackupService backupService,
        LocalDataSettingsStore settingsStore,
        string databasePath,
        string defaultBackupDirectory)
    {
        LocalData = new LocalDataViewModel(
            backupService,
            settingsStore,
            databasePath,
            defaultBackupDirectory,
            message => StatusMessage = message);
        OnPropertyChanged(nameof(LocalData));
    }
}
