using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel
{
    [ObservableProperty]
    private DeckPresentationItem? _filterDeckPresentation;

    partial void OnFilterDeckPresentationChanged(DeckPresentationItem? value) => FilterDeck = value?.Deck;

    [RelayCommand]
    private void ClearLibraryFilters()
    {
        SearchText = string.Empty;
        FilterDeckPresentation = null;
        FilterLifecycle = null;
        FilterResponseMode = null;
        FilterFlag = null;
    }
}
