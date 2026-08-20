using Illumination.Domain.Identity;

namespace Illumination.Domain.Decks;

public sealed class Deck
{
    public const int MaximumTopicLabelLength = 80;

    private readonly HashSet<LearningItemId> _learningItemIds = [];
    private readonly HashSet<string> _topicLabels = new(StringComparer.OrdinalIgnoreCase);

    private Deck(DeckId id, string name, IEnumerable<string>? topicLabels = null)
    {
        DomainName.RequireNonWhitespace(name, nameof(name));
        Id = id;
        Name = name;
        ReplaceTopicLabels(topicLabels);
    }

    public DeckId Id { get; }

    public string Name { get; private set; }

    public IReadOnlyCollection<LearningItemId> LearningItemIds => _learningItemIds.ToArray();

    public IReadOnlyCollection<string> TopicLabels => _topicLabels
        .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static Deck Create(string name, IEnumerable<string>? topicLabels = null) => new(DeckId.New(), name, topicLabels);

    public static Deck Create(DeckId id, string name, IEnumerable<string>? topicLabels = null) => new(id, name, topicLabels);

    public void Rename(string name)
    {
        DomainName.RequireNonWhitespace(name, nameof(name));
        Name = name;
    }

    public void ReplaceTopicLabels(IEnumerable<string>? topicLabels)
    {
        var normalized = (topicLabels ?? [])
            .Select(NormalizeTopicLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _topicLabels.Clear();
        foreach (var label in normalized)
            _topicLabels.Add(label);
    }

    public void AddLearningItem(LearningItemId learningItemId)
    {
        _learningItemIds.Add(learningItemId);
    }

    public void RemoveLearningItem(LearningItemId learningItemId)
    {
        _learningItemIds.Remove(learningItemId);
    }

    private static string NormalizeTopicLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Deck topic label must not be null, empty, or whitespace.", nameof(value));

        var normalized = value.Trim();
        if (normalized.Length > MaximumTopicLabelLength)
            throw new ArgumentException($"Deck topic label must not exceed {MaximumTopicLabelLength} characters.", nameof(value));

        return normalized;
    }
}
