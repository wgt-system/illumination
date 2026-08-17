using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Illumination.Desktop;

public interface IDesktopInteractionService
{
    Task CopyTextAsync(string text);
    Task<string?> LoadJsonFileAsync();
    Task<bool> SaveSqliteFileAsync(string suggestedFileName, byte[] content) => Task.FromResult(false);
    Task<byte[]?> LoadSqliteFileAsync() => Task.FromResult<byte[]?>(null);
}

public sealed class AvaloniaDesktopInteractionService(Func<Window?> windowProvider) : IDesktopInteractionService
{
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
            FileTypeFilter =
            [
                new FilePickerFileType("JSON files")
                {
                    Patterns = ["*.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        });
        var file = files.SingleOrDefault();
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
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

    public async Task<byte[]?> LoadSqliteFileAsync()
    {
        var storage = GetWindow().StorageProvider;
        if (!storage.CanOpen) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Illumination backup to restore",
            AllowMultiple = false,
            FileTypeFilter = [SqliteFiles],
        });
        var file = files.SingleOrDefault();
        if (file is null) return null;

        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private Window GetWindow() => windowProvider() ?? throw new InvalidOperationException("The application window is unavailable.");
}
