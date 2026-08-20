using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task ToggleSelectedDeckLearningActivityProfileAsync(string? profileName)
    {
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck before changing learning profiles.";
            return;
        }

        if (!Enum.TryParse<DeckLearningActivityProfile>(profileName, out var profile) || !Enum.IsDefined(profile))
        {
            StatusMessage = "Unsupported Deck learning profile.";
            return;
        }

        var deckId = SelectedDeck.Id;
        var profiles = SelectedDeck.LearningActivityProfiles.ToHashSet();
        if (!profiles.Add(profile)) profiles.Remove(profile);

        await RunAsync(async () =>
        {
            var updated = await _content.SetDeckLearningActivityProfilesAsync(
                deckId,
                new SetDeckLearningActivityProfilesCommand(profiles.OrderBy(value => value).ToArray()));

            await RefreshContentAsync(deckId);
            StatusMessage = updated.LearningActivityProfiles.Count == 0
                ? $"Cleared learning profiles for Deck '{updated.Name}'."
                : $"Updated learning profiles for Deck '{updated.Name}'.";
        });
    }
}
