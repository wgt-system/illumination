using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private Func<IReadOnlyList<LearningItemView>>? _existingDeckContentProvider;

    public void ConfigureExistingDeckContent(Func<IReadOnlyList<LearningItemView>> provider) =>
        _existingDeckContentProvider = provider ?? throw new ArgumentNullException(nameof(provider));

    private IReadOnlyList<ContentGenerationInventoryItem> BuildExistingDeckInventory()
    {
        if (!UseExistingDeck || SelectedExistingDeck is null || _existingDeckContentProvider is null)
            return [];

        var deckId = SelectedExistingDeck.Id;
        return _existingDeckContentProvider()
            .Where(item => item.DeckIds.Contains(deckId))
            .Select(item => new ContentGenerationInventoryItem(item.Id, item.Prompt, item.ReferenceSolution))
            .ToArray();
    }
}
