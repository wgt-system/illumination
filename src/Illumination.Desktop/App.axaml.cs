using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Illumination.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = DesktopComposition.CreateAsync().GetAwaiter().GetResult();
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            viewModel.ContentAcquisition.AttachDesktopInteractions(
                new AvaloniaDesktopInteractionService(() => window));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
