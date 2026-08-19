using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Illumination.Desktop;

internal static class DroppedFileDataTransfer
{
    public static IReadOnlyList<string> GetLocalPaths(DragEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var items = e.DataTransfer.TryGetFiles() ?? [];
        try
        {
            return items
                .Select(item => item.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .ToArray();
        }
        finally
        {
            foreach (var item in items) item.Dispose();
        }
    }
}
