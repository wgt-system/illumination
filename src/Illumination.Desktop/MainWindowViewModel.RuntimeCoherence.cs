using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task OpenInsightsAsync()
    {
        SelectedPage = DesktopPage.Insights;
        await Insights.RefreshAsync();
    }
}
