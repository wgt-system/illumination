using Illumination.Application.ContentAcquisition;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class LanguageContentPromptGuidanceTests
{
    [Fact]
    public void Vocabulary_profile_emits_level_direction_and_flashcard_guardrails()
    {
        var generated = LanguageContentPromptGuidance.Apply(
            new GeneratedContentPrompt("BASE"),
            new LanguageGenerationGuidance(
                InstructionLanguage: "German",
                SourceLanguage: "Indonesian",
                TargetLanguage: "German",
                ProficiencyLevel: LanguageProficiencyLevel.B1,
                ExerciseProfile: LanguageExerciseProfile.VocabularyFlashcards,
                ProgressionMode: FollowUpProgressionMode.Continue,
                HasSourceDeckContext: true));

        Assert.Contains("CEFR B1", generated.Prompt);
        Assert.Contains("Indonesian → German", generated.Prompt);
        Assert.Contains("Exercise profile: vocabulary flashcards", generated.Prompt);
        Assert.Contains("one useful word, fixed expression, collocation, or short phrase per Learning Item", generated.Prompt);
        Assert.Contains("stay within CEFR B1", generated.Prompt);
        Assert.Contains("anti-duplication inventory", generated.Prompt);
        Assert.Contains("Do not generate the same word, phrase, question, answer pair", generated.Prompt);
    }

    [Theory]
    [InlineData(LanguageProficiencyLevel.A1, "A2")]
    [InlineData(LanguageProficiencyLevel.A2, "B1")]
    [InlineData(LanguageProficiencyLevel.B1, "B2")]
    [InlineData(LanguageProficiencyLevel.B2, "C1")]
    [InlineData(LanguageProficiencyLevel.C1, "C2")]
    public void Advance_moves_only_toward_adjacent_proficiency_band(LanguageProficiencyLevel level, string expectedNext)
    {
        var generated = LanguageContentPromptGuidance.Apply(
            new GeneratedContentPrompt("BASE"),
            new LanguageGenerationGuidance(
                ProficiencyLevel: level,
                ExerciseProfile: LanguageExerciseProfile.MixedPractice,
                ProgressionMode: FollowUpProgressionMode.Advance));

        Assert.Contains($"toward adjacent level {expectedNext}", generated.Prompt);
        Assert.Contains("do not skip proficiency bands", generated.Prompt);
    }

    [Fact]
    public void C2_advance_does_not_invent_a_higher_cefr_level()
    {
        var generated = LanguageContentPromptGuidance.Apply(
            new GeneratedContentPrompt("BASE"),
            new LanguageGenerationGuidance(
                ProficiencyLevel: LanguageProficiencyLevel.C2,
                ProgressionMode: FollowUpProgressionMode.Advance));

        Assert.Contains("remain within CEFR C2", generated.Prompt);
        Assert.Contains("rather than inventing a level above C2", generated.Prompt);
        Assert.DoesNotContain("C3", generated.Prompt);
    }

    [Fact]
    public void Applying_guidance_twice_does_not_duplicate_the_block()
    {
        var guidance = new LanguageGenerationGuidance(
            SourceLanguage: "German",
            TargetLanguage: "Indonesian",
            ProficiencyLevel: LanguageProficiencyLevel.A2,
            ExerciseProfile: LanguageExerciseProfile.PhrasesAndChunks);

        var once = LanguageContentPromptGuidance.Apply(new GeneratedContentPrompt("BASE"), guidance);
        var twice = LanguageContentPromptGuidance.Apply(once, guidance);

        Assert.Equal(once.Prompt, twice.Prompt);
        Assert.Equal(1, Count(twice.Prompt, "Explicit language-learning controls:"));
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
