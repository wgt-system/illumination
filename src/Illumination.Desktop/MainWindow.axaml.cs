using Avalonia.Controls;

namespace Illumination.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel viewModel) return;
            viewModel.InitializeStudySelection();
            ProductSurface.AttachDesktopInteractions(viewModel);
        };
    }
}
