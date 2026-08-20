using Illumination.Domain.Identity;

namespace Illumination.Domain.Decks;

public sealed class Deck
{
    public const int MaximumTopicLabelLength = 80;

    private readonly HashSet<LearningItemId> _learningItemIds = [];
    private readonly HashSet<string> _topicLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<LearningActivityProfile> _learningActivityProfiles = [];

    private Deck(
        DeckId id,
        string name,
        IEnumerable<string>? topicLabels = null,
        IEnumerable<LearningActivityProfile>? learningActivityProfiles = null)
    {
        DomainName.RequireNonWhitespace(name, nameof(name));
        Id = id;
        Name = name;
        ReplaceTopicLabels(topicLabels);
        ReplaceLearningActivityProfiles(learningActivityProfiles);
    }

    public DeckId Id { get; }

    public string Name { get; private set; }

    public IReadOnlyCollection<LearningItemId> LearningItemIds => _learningItemIds.ToArray();

    public IReadOnlyCollection<string> TopicLabels => _topicLabels
        .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyCollection<LearningActivityProfile> LearningActivityProfiles => _learningActivityProfiles
        .OrderBy(profile => profile)
        .ToArray();

    public static Deck Create(
        string name,
        IEnumerable<string>? topicLabels = null,
        IEnumerable<LearningActivityProfile>? learningActivityProfiles = null) =>
        new(DeckId.New(), name, topicLabels, learningActivityProfiles);

    public static Deck Create(
        DeckId id,
        string name,
        IEnumerable<string>? topicLabels = null,
        IEnumerable<LearningActivityProfile>? learningActivityProfiles = null) =>
        new(id, name, topicLabels, learningActivityProfiles);

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

    public void ReplaceLearningActivityProfiles(IEnumerable<LearningActivityProfile>? profiles)
    {
        var normalized = (profiles ?? []).Distinct().ToArray();
        if (normalized.Any(profile => !Enum.IsDefined(profile)))
            throw new ArgumentOutOfRangeException(nameof(profiles), "Unsupported Deck learning activity profile.");

        _learningActivityProfiles.Clear();
        foreach (var profile in normalized)
            _learningActivityProfiles.Add(profile);
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
