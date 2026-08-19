using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private const string ExistingDeckInventoryMarker = "Existing target Deck anti-duplication inventory:";
    private Func<IReadOnlyList<LearningItemView>>? _existingDeckContentProvider;

    public void ConfigureExistingDeckContent(Func<IReadOnlyList<LearningItemView>> provider) =>
        _existingDeckContentProvider = provider ?? throw new ArgumentNullException(nameof(provider));

    private GeneratedContentPrompt ApplyExistingDeckInventory(GeneratedContentPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (!UseExistingDeck || SelectedExistingDeck is null || _existingDeckContentProvider is null ||
            prompt.Prompt.Contains(ExistingDeckInventoryMarker, StringComparison.Ordinal))
            return prompt;

        var deckId = SelectedExistingDeck.Id;
        var existing = _existingDeckContentProvider()
            .Where(item => item.DeckIds.Contains(deckId))
            .OrderBy(item => item.Prompt, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        if (existing.Length == 0) return prompt;

        var lines = new List<string>
        {
            ExistingDeckInventoryMarker,
            $"The selected existing Deck `{SelectedExistingDeck.Name}` already contains {existing.Length} Learning Item(s). Treat the prompt/referenceSolution pairs below as existing content, not suggestions to regenerate.",
            "For ordinary extension, create genuinely new material and reject duplicates or cosmetic near-duplicates. Reuse existing content only when an explicit Reinforce follow-up intentionally calls for repetition.",
        };
        lines.AddRange(existing.Select(item => $"- prompt={item.Prompt} | referenceSolution={item.ReferenceSolution}"));

        return new GeneratedContentPrompt(prompt.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
