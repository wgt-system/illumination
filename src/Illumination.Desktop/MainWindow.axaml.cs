using Avalonia.Controls;
using Avalonia.Input;

namespace Illumination.Desktop;

public partial class MainWindow : Window
{
    private async void OnBundleDrop(object? sender, DragEventArgs e)
    {
        var paths = DroppedFileDataTransfer.GetLocalPaths(e);
        if (DataContext is MainWindowViewModel vm) await vm.ContentAcquisition.LoadBundleFromDropAsync(paths);
        e.Handled = true;
    }

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) vm.InitializeStudySelection();
        };
    }
}
