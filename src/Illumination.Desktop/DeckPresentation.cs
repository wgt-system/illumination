using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed record DeckPresentationItem(DeckView Deck, string DisplayName)
{
    public Guid Id => Deck.Id;
    public IReadOnlyList<Guid> LearningItemIds => Deck.LearningItemIds;
    public int LearningItemCount => Deck.LearningItemIds.Count;
}

public static class DeckPresentationLabeler
{
    public static IReadOnlyList<DeckPresentationItem> Label(IEnumerable<DeckView> decks)
    {
        var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        return decks.Select(deck =>
        {
            counts.TryGetValue(deck.Name, out var count);
            count++;
            counts[deck.Name] = count;
            return new DeckPresentationItem(deck, count == 1 ? deck.Name : $"{deck.Name} ({count})");
        }).ToArray();
    }
}
