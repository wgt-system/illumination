using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task EditFromPreviewSuggestionAsync(QualityReviewExchangeRowViewModel result)
    {
        if (result.LearningItemId == Guid.Empty || !result.HasSuggestedCorrection)
        {
            StatusMessage = "This Quality Review has no applicable suggested correction.";
            return;
        }

        await Editor.BeginEditAsync(result.LearningItemId);
        Editor.ShowAdvisorySuggestion(result.SuggestedCorrection ?? string.Empty);
        SelectedPage = DesktopPage.Library;
        StatusMessage = "Suggested correction opened as advisory guidance. Review the actual fields and save the content change explicitly.";
    }
}
