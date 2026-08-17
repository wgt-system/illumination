using CommunityToolkit.Mvvm.Input;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private void PrepareSelectedDeckStudy()
    {
        if (SelectedDeckPresentation is null)
        {
            StatusMessage = "Select a Deck to study.";
            return;
        }

        SetSelectedStudyDeckIds([SelectedDeckPresentation.Id]);
        SelectedStudyDeckPresentation = DeckPresentationItems.FirstOrDefault(x => x.Id == SelectedDeckPresentation.Id);
        SelectedStudyDeck = SelectedStudyDeckPresentation?.Deck;
        SelectedPage = DesktopPage.Study;
        StatusMessage = $"Study prepared for '{SelectedDeckPresentation.DisplayName}'. Adjust session options or start when ready.";
    }

    [RelayCommand]
    private void PrepareAllDecksStudy()
    {
        if (DeckPresentationItems.Count == 0)
        {
            StatusMessage = "There are no Decks to study.";
            return;
        }

        SetSelectedStudyDeckIds(DeckPresentationItems.Select(x => x.Id));
        SelectedStudyDeckPresentation = DeckPresentationItems.FirstOrDefault();
        SelectedStudyDeck = SelectedStudyDeckPresentation?.Deck;
        SelectedPage = DesktopPage.Study;
        StatusMessage = $"Study prepared across {DeckPresentationItems.Count} Decks. Adjust session options or start when ready.";
    }

    [RelayCommand]
    private async Task GenerateFollowUpFromSelectedDeckAsync()
    {
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck to generate a follow-up.";
            return;
        }
        if (_insightService is null)
        {
            StatusMessage = "Learning-aware follow-up generation is unavailable.";
            return;
        }

        await RunAsync(async () =>
        {
            var deck = (await _insightService.GetDeckInsightsAsync()).FirstOrDefault(x => x.Id == SelectedDeck.Id);
            if (deck is null)
            {
                StatusMessage = "The selected Deck could not be resolved in Learning Insights.";
                return;
            }
            await GenerateFollowUpAsync(deck);
        });
    }
}
