using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Illumination.Desktop;

public interface IDesktopInteractionService
{
    Task CopyTextAsync(string text);
    Task<string?> LoadJsonFileAsync();
    Task<bool> SaveJsonFileAsync(string suggestedFileName, string content) => Task.FromResult(false);
    Task<bool> SaveSqliteFileAsync(string suggestedFileName, byte[] content) => Task.FromResult(false);
}

public sealed class AvaloniaDesktopInteractionService(Func<Window?> windowProvider) : IDesktopInteractionService
{
    private static readonly FilePickerFileType JsonFiles = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private static readonly FilePickerFileType SqliteFiles = new("SQLite databases")
    {
        Patterns = ["*.sqlite", "*.db"],
        MimeTypes = ["application/vnd.sqlite3", "application/octet-stream"],
    };

    public async Task CopyTextAsync(string text)
    {
        var window = GetWindow();
        var clipboard = window.Clipboard ?? throw new InvalidOperationException("Clipboard is unavailable.");
        await clipboard.SetTextAsync(text);
    }

    public async Task<string?> LoadJsonFileAsync()
    {
        var files = await GetWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Content Bundle",
            AllowMultiple = false,
            FileTypeFilter = [JsonFiles],
        });
        var file = files.SingleOrDefault();
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task<bool> SaveJsonFileAsync(string suggestedFileName, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(content);

        var storage = GetWindow().StorageProvider;
        if (!storage.CanSave) return false;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Illumination Content Bundle",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = [JsonFiles],
            ShowOverwritePrompt = true,
        });
        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        await writer.FlushAsync();
        return true;
    }

    public async Task<bool> SaveSqliteFileAsync(string suggestedFileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(content);

        var storage = GetWindow().StorageProvider;
        if (!storage.CanSave) return false;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Illumination backup",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "sqlite",
            FileTypeChoices = [SqliteFiles],
            ShowOverwritePrompt = true,
        });
        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await stream.WriteAsync(content);
        await stream.FlushAsync();
        return true;
    }

    private Window GetWindow() => windowProvider() ?? throw new InvalidOperationException("The application window is unavailable.");
}
