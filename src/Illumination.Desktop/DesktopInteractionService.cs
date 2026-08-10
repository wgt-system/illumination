using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Illumination.Desktop;

public interface IDesktopInteractionService
{
    Task CopyTextAsync(string text);
    Task<string?> LoadJsonFileAsync();
}

public sealed class AvaloniaDesktopInteractionService(Func<Window?> windowProvider) : IDesktopInteractionService
{
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

    private Window GetWindow() => windowProvider() ?? throw new InvalidOperationException("The application window is unavailable.");
}
