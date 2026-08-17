using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    [RelayCommand]
    private void ClearSourceDeckContext()
    {
        _sourceDeckContext = null;
        ProgressionMode = null;
        OnPropertyChanged(nameof(HasSourceDeckContext));
        OnPropertyChanged(nameof(SourceDeckName));
        GeneratePromptCommand.NotifyCanExecuteChanged();
        _reportStatus("Follow-up source cleared; generation is now independent of prior Deck learning context.");
    }
}
