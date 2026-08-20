using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Xunit;

namespace Illumination.Domain.Tests;

public sealed class DeckLearningActivityProfileTests
{
    [Fact]
    public void Profiles_are_optional_unique_and_composable()
    {
        var deck = Deck.Create(
            "Travel Indonesian",
            topicLabels: ["Indonesian", "Travel"],
            learningActivityProfiles:
            [
                LearningActivityProfile.Geospatial,
                LearningActivityProfile.LanguageLearning,
                LearningActivityProfile.LanguageLearning,
            ]);

        Assert.Equal(
            [LearningActivityProfile.LanguageLearning, LearningActivityProfile.Geospatial],
            deck.LearningActivityProfiles);
        Assert.Equal(["Indonesian", "Travel"], deck.TopicLabels);
    }

    [Fact]
    public void Replacing_profiles_preserves_identity_membership_and_topics()
    {
        var deckId = DeckId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var itemId = LearningItemId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var deck = Deck.Create(
            deckId,
            "Algorithms",
            topicLabels: ["Algorithms"],
            learningActivityProfiles: [LearningActivityProfile.GeneralRecall]);
        deck.AddLearningItem(itemId);

        deck.ReplaceLearningActivityProfiles(
            [LearningActivityProfile.CodingProblemSolving, LearningActivityProfile.GeneralRecall]);

        Assert.Equal(deckId, deck.Id);
        Assert.Equal([itemId], deck.LearningItemIds);
        Assert.Equal(["Algorithms"], deck.TopicLabels);
        Assert.Equal(
            [LearningActivityProfile.GeneralRecall, LearningActivityProfile.CodingProblemSolving],
            deck.LearningActivityProfiles);
    }

    [Fact]
    public void Profiles_can_be_cleared_without_inventing_a_default()
    {
        var deck = Deck.Create(
            "Unclassified",
            learningActivityProfiles: [LearningActivityProfile.GeneralRecall]);

        deck.ReplaceLearningActivityProfiles([]);

        Assert.Empty(deck.LearningActivityProfiles);
    }

    [Fact]
    public void Undefined_profile_value_is_rejected()
    {
        var invalid = (LearningActivityProfile)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Deck.Create("Invalid", learningActivityProfiles: [invalid]));
    }
}
