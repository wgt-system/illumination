using Avalonia.Controls;
using Avalonia.Input;

namespace Illumination.Desktop;

public partial class ImportView : UserControl
{
    public ImportView() => InitializeComponent();

    private void OnBundleDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnBundleDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(file => file.Path.LocalPath)
            .ToArray() ?? [];
        if (sender is Control control && control.DataContext is ContentAcquisitionViewModel vm)
            await vm.LoadBundleFromDropAsync(paths);
        e.Handled = true;
    }
}
