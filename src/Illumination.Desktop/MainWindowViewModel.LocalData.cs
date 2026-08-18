using Illumination.Infrastructure.Persistence;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    public LocalDataViewModel? LocalData { get; private set; }

    public void ConfigureLocalData(
        ILocalSqliteBackupService backupService,
        string databasePath,
        string backupDirectory)
    {
        LocalData = new LocalDataViewModel(
            backupService,
            databasePath,
            backupDirectory,
            message => StatusMessage = message);
        OnPropertyChanged(nameof(LocalData));
    }
}
