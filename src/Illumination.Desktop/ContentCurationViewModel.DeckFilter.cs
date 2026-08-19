using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel
{
    [ObservableProperty]
    private DeckPresentationItem? _filterDeckPresentation;

    partial void OnFilterDeckPresentationChanged(DeckPresentationItem? value) => FilterDeck = value?.Deck;

    public void NormalizeDeckPresentations(IReadOnlyList<DeckPresentationItem> presentations)
    {
        ArgumentNullException.ThrowIfNull(presentations);

        var filterDeckId = FilterDeckPresentation?.Id;
        FilterDeckPresentation = filterDeckId is { } filterId
            ? presentations.FirstOrDefault(x => x.Id == filterId)
            : null;

        var bulkDeckId = BulkTargetDeckPresentation?.Id;
        BulkTargetDeckPresentation = bulkDeckId is { } bulkId
            ? presentations.FirstOrDefault(x => x.Id == bulkId)
            : null;
    }

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
