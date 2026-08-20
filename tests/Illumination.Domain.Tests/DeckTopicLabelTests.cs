using Illumination.Domain.Decks;
using Xunit;

namespace Illumination.Domain.Tests;

public sealed class DeckTopicLabelTests
{
    [Fact]
    public void Topic_labels_are_optional_trimmed_many_valued_and_case_insensitively_unique()
    {
        var deck = Deck.Create("Indo", [" Indonesian ", "Geography", "indonesian"]);

        Assert.Equal(["Geography", "Indonesian"], deck.TopicLabels);
    }

    [Fact]
    public void Replacing_topic_labels_does_not_change_membership_or_identity()
    {
        var deck = Deck.Create("Deck", ["Old"]);
        var itemId = Illumination.Domain.Identity.LearningItemId.New();
        var deckId = deck.Id;
        deck.AddLearningItem(itemId);

        deck.ReplaceTopicLabels(["Language", "Travel"]);

        Assert.Equal(deckId, deck.Id);
        Assert.Contains(itemId, deck.LearningItemIds);
        Assert.Equal(["Language", "Travel"], deck.TopicLabels);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_topic_labels_are_rejected(string label)
    {
        Assert.Throws<ArgumentException>(() => Deck.Create("Deck", [label]));
    }

    [Fact]
    public void Excessively_long_topic_labels_are_rejected()
    {
        var label = new string('x', Deck.MaximumTopicLabelLength + 1);

        Assert.Throws<ArgumentException>(() => Deck.Create("Deck", [label]));
    }
}
