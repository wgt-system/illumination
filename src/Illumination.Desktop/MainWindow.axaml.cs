using Avalonia.Controls;

namespace Illumination.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) vm.InitializeStudySelection();
        };
    }
}
