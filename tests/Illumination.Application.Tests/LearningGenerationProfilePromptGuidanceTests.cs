using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class LearningGenerationProfilePromptGuidanceTests
{
    [Fact]
    public void Profile_separates_weak_established_and_unreviewed_evidence()
    {
        var context = new DeckLearningContext(
            Guid.NewGuid(),
            "Indo",
            [
                Item("weak", "schwach", reviews: 4, last: StudyLearningAssessment.Schwer,
                    distribution: new AssessmentDistribution(1, 2, 1, 0, 0), relearning: true, difficulty: 8.2, stability: 1.2),
                Item("stable", "stabil", reviews: 6, last: StudyLearningAssessment.Leicht,
                    distribution: new AssessmentDistribution(0, 0, 0, 2, 4), isNew: false, difficulty: 2.8, stability: 40),
                Item("new", "neu", reviews: 0, last: null,
                    distribution: AssessmentDistribution.Empty, isNew: true),
            ]);

        var profile = LearningGenerationProfilePromptGuidance.Build(context);

        Assert.Equal(3, profile.TotalItemCount);
        Assert.Equal(2, profile.ReviewedItemCount);
        Assert.Equal(1, profile.UnreviewedItemCount);
        Assert.Equal(1, profile.NewItemCount);
        Assert.Equal(1, profile.RelearningItemCount);
        Assert.Equal(1, profile.AssessmentDistribution.Nochmal);
        Assert.Equal(2, profile.AssessmentDistribution.Schwer);
        Assert.Single(profile.ReinforcementCandidates);
        Assert.Equal("weak", profile.ReinforcementCandidates[0].Prompt);
        Assert.Single(profile.EstablishedCandidates);
        Assert.Equal("stable", profile.EstablishedCandidates[0].Prompt);
        Assert.Single(profile.UnreviewedExamples);
        Assert.Equal("new", profile.UnreviewedExamples[0].Prompt);
    }

    [Theory]
    [InlineData(FollowUpProgressionMode.Reinforce, "concentrate on the reinforcement pattern")]
    [InlineData(FollowUpProgressionMode.Continue, "introduce genuinely new material at the current level")]
    [InlineData(FollowUpProgressionMode.Advance, "weak/relearning evidence below is a prerequisite caution")]
    public void Prompt_uses_progression_specific_interpretation(
        FollowUpProgressionMode progression,
        string expected)
    {
        var context = new DeckLearningContext(
            Guid.NewGuid(),
            "Indo",
            [Item("makan", "essen", 3, StudyLearningAssessment.Unsicher,
                new AssessmentDistribution(0, 1, 2, 0, 0), isNew: false, difficulty: 7, stability: 2)]);

        var generated = LearningGenerationProfilePromptGuidance.Apply(
            new GeneratedContentPrompt("BASE"),
            context,
            progression);

        Assert.Contains("Illumination-derived learning generation profile:", generated.Prompt);
        Assert.Contains("reviewed=1, unreviewed=0", generated.Prompt);
        Assert.Contains("Nochmal=0, Schwer=1, Unsicher=2, Gut=0, Leicht=0", generated.Prompt);
        Assert.Contains(expected, generated.Prompt);
        Assert.Contains("makan => essen", generated.Prompt);
        Assert.Contains("do not invent a mastery score", generated.Prompt);
    }

    [Fact]
    public void Evidence_examples_are_bounded_and_guidance_is_idempotent()
    {
        var items = Enumerable.Range(0, 30)
            .Select(index => Item(
                $"weak-{index:00}",
                $"answer-{index:00}",
                2,
                StudyLearningAssessment.Schwer,
                new AssessmentDistribution(0, 2, 0, 0, 0),
                isNew: false,
                difficulty: 7,
                stability: 1))
            .ToArray();
        var context = new DeckLearningContext(Guid.NewGuid(), "Large", items);

        var once = LearningGenerationProfilePromptGuidance.Apply(
            new GeneratedContentPrompt("BASE"), context, FollowUpProgressionMode.Reinforce);
        var twice = LearningGenerationProfilePromptGuidance.Apply(
            once, context, FollowUpProgressionMode.Reinforce);

        Assert.Equal(once.Prompt, twice.Prompt);
        Assert.Contains("Reinforcement candidates (12 representative item(s), bounded)", once.Prompt);
        Assert.DoesNotContain("weak-29 => answer-29", once.Prompt);
    }

    private static DeckLearningContextItem Item(
        string prompt,
        string solution,
        int reviews,
        StudyLearningAssessment? last,
        AssessmentDistribution distribution,
        bool isNew = false,
        bool relearning = false,
        double difficulty = 5,
        double stability = 5) =>
        new(
            Guid.NewGuid(),
            prompt,
            solution,
            LearningItemResponseMode.SelfAssessed,
            LearningItemLifecycle.Active,
            isNew,
            DateTimeOffset.UtcNow,
            difficulty,
            stability,
            relearning,
            reviews,
            last,
            distribution);
}
