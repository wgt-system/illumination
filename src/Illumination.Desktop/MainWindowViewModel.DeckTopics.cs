using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task ReplaceSelectedDeckTopicsAsync(string? input)
    {
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck before changing topics.";
            return;
        }

        var deckId = SelectedDeck.Id;
        var labels = ParseTopicLabels(input);

        await RunAsync(async () =>
        {
            var updated = await _content.SetDeckTopicLabelsAsync(
                deckId,
                new SetDeckTopicLabelsCommand(labels));

            await RefreshContentAsync(deckId);
            StatusMessage = updated.TopicLabels.Count == 0
                ? $"Cleared topics for Deck '{updated.Name}'."
                : $"Updated topics for Deck '{updated.Name}'.";
        });
    }

    private static IReadOnlyList<string> ParseTopicLabels(string? input) =>
        string.IsNullOrWhiteSpace(input)
            ? []
            : input.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
