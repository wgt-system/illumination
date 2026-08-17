using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentAcquisition;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    private ContentBundleExportService? _bundleExport;
    private IDesktopInteractionService? _deckExportInteractions;

    public void ConfigureDeckExport(ContentBundleExportService bundleExport)
    {
        _bundleExport = bundleExport ?? throw new ArgumentNullException(nameof(bundleExport));
    }

    public void AttachDeckExportInteractions(IDesktopInteractionService interactions)
    {
        _deckExportInteractions = interactions ?? throw new ArgumentNullException(nameof(interactions));
    }

    [RelayCommand]
    private async Task ExportSelectedDeckBundleAsync()
    {
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck to export.";
            return;
        }
        if (_bundleExport is null || _deckExportInteractions is null)
        {
            StatusMessage = "Deck export is unavailable.";
            return;
        }

        await RunAsync(async () =>
        {
            var export = await _bundleExport.ExportDeckAsync(SelectedDeck.Id);
            var saved = await _deckExportInteractions.SaveJsonFileAsync(export.SuggestedFileName, export.Json);
            StatusMessage = saved
                ? $"Exported '{SelectedDeck.Name}' with {export.LearningItemCount} Learning Items as Content Bundle 1.0."
                : "Deck export cancelled.";
        });
    }

    [RelayCommand]
    private async Task CopySelectedDeckBundleAsync()
    {
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck to export.";
            return;
        }
        if (_bundleExport is null || _deckExportInteractions is null)
        {
            StatusMessage = "Deck export is unavailable.";
            return;
        }

        await RunAsync(async () =>
        {
            var export = await _bundleExport.ExportDeckAsync(SelectedDeck.Id);
            await _deckExportInteractions.CopyTextAsync(export.Json);
            StatusMessage = $"Copied '{SelectedDeck.Name}' as Content Bundle 1.0 ({export.LearningItemCount} Learning Items).";
        });
    }
}
