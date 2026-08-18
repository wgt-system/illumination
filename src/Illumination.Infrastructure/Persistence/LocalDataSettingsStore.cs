using System.Text.Json;

namespace Illumination.Infrastructure.Persistence;

public sealed class LocalDataSettingsStore
{
    private readonly string _settingsPath;

    public LocalDataSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public string LoadBackupDirectory(string defaultBackupDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultBackupDirectory);
        var fallback = Path.GetFullPath(defaultBackupDirectory);
        if (!File.Exists(_settingsPath)) return fallback;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<LocalDataSettings>(json);
            return string.IsNullOrWhiteSpace(settings?.BackupDirectory)
                ? fallback
                : Path.GetFullPath(settings.BackupDirectory);
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    public void SaveBackupDirectory(string backupDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        var normalized = Path.GetFullPath(backupDirectory);
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The local settings path has no parent directory.");
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(new LocalDataSettings(normalized), new JsonSerializerOptions { WriteIndented = true });
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    private sealed record LocalDataSettings(string BackupDirectory);
}
