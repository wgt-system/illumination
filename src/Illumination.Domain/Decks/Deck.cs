using Illumination.Domain.Identity;

namespace Illumination.Domain.Decks;

public sealed class Deck
{
    private readonly HashSet<LearningItemId> _learningItemIds = [];

    private Deck(DeckId id, string name)
    {
        DomainName.RequireNonWhitespace(name, nameof(name));
        Id = id;
        Name = name;
    }

    public DeckId Id { get; }

    public string Name { get; private set; }

    public IReadOnlyCollection<LearningItemId> LearningItemIds => _learningItemIds.ToArray();

    public static Deck Create(string name) => new(DeckId.New(), name);

    public static Deck Create(DeckId id, string name) => new(id, name);

    public void Rename(string name)
    {
        DomainName.RequireNonWhitespace(name, nameof(name));
        Name = name;
    }

    public void AddLearningItem(LearningItemId learningItemId)
    {
        _learningItemIds.Add(learningItemId);
    }

    public void RemoveLearningItem(LearningItemId learningItemId)
    {
        _learningItemIds.Remove(learningItemId);
    }
}
