using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class ContentGenerationPromptComposerTests
{
    private const string ContractMarker = "Canonical Content Bundle 1.0 contract guidance:";

    [Fact]
    public void Learning_aware_composer_replaces_raw_scheduler_dump_and_preserves_exact_contract_appendix()
    {
        var service = new ContentAcquisitionService(new FakePersistence(), TimeProvider.System);
        var context = new DeckLearningContext(
            Guid.NewGuid(),
            "Indo",
            [new DeckLearningContextItem(
                Guid.NewGuid(),
                "makan",
                "essen",
                LearningItemResponseMode.SelfAssessed,
                LearningItemLifecycle.Active,
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                8.25,
                1.5,
                true,
                4,
                StudyLearningAssessment.Schwer,
                new AssessmentDistribution(1, 2, 1, 0, 0))]);
        var command = new GenerateContentPromptCommand(
            "Indonesian vocabulary",
            20,
            ExistingDeckId: context.DeckId,
            SourceDeckContext: context,
            ProgressionMode: FollowUpProgressionMode.Continue);

        var legacy = service.GenerateContentPrompt(command);
        var composed = ContentGenerationPromptComposer.Compose(
            service,
            command,
            new LanguageGenerationGuidance(
                InstructionLanguage: "German",
                SourceLanguage: "Indonesian",
                TargetLanguage: "German",
                ProficiencyLevel: LanguageProficiencyLevel.B1,
                ExerciseProfile: LanguageExerciseProfile.VocabularyFlashcards,
                ProgressionMode: FollowUpProgressionMode.Continue,
                HasSourceDeckContext: true),
            [new ContentGenerationInventoryItem(context.Items[0].LearningItemId, "makan", "essen")],
            "Indo");

        Assert.Contains("Illumination-derived learning generation profile:", composed.Prompt);
        Assert.Contains("Explicit language-learning controls:", composed.Prompt);
        Assert.Contains("Existing target Deck anti-duplication inventory:", composed.Prompt);
        Assert.DoesNotContain("difficulty=8.25", composed.Prompt);
        Assert.DoesNotContain("stabilityDays=1.5", composed.Prompt);
        Assert.DoesNotContain("Learning-aware follow-up context from source Deck", composed.Prompt);

        var contractIndex = composed.Prompt.IndexOf(ContractMarker, StringComparison.Ordinal);
        Assert.True(contractIndex > composed.Prompt.IndexOf("Existing target Deck anti-duplication inventory:", StringComparison.Ordinal));
        Assert.Equal(1, Count(composed.Prompt, ContractMarker));

        var legacyContract = legacy.Prompt[legacy.Prompt.IndexOf(ContractMarker, StringComparison.Ordinal)..].Trim();
        var composedContract = composed.Prompt[contractIndex..].Trim();
        Assert.Equal(legacyContract, composedContract);
    }

    [Fact]
    public void Existing_inventory_is_bounded_and_reinforce_still_forbids_exact_duplicate_cards()
    {
        var service = new ContentAcquisitionService(new FakePersistence(), TimeProvider.System);
        var deckId = Guid.NewGuid();
        var inventory = Enumerable.Range(0, 300)
            .Select(index => new ContentGenerationInventoryItem(Guid.NewGuid(), $"prompt-{index:000}", $"answer-{index:000}"))
            .ToArray();
        var command = new GenerateContentPromptCommand(
            "Practice",
            20,
            ExistingDeckId: deckId,
            ProgressionMode: FollowUpProgressionMode.Reinforce);

        var composed = ContentGenerationPromptComposer.Compose(
            service,
            command,
            existingDeckInventory: inventory,
            existingDeckName: "Large");

        Assert.Contains("contains 300 Learning Item(s)", composed.Prompt);
        Assert.Contains("bounded inventory below contains 250 item(s)", composed.Prompt);
        Assert.Contains("50 item(s) are omitted", composed.Prompt);
        Assert.Contains("Never create an exact prompt/referenceSolution duplicate", composed.Prompt);
        Assert.Contains("Reinforce may revisit the same underlying knowledge", composed.Prompt);
        Assert.Contains("prompt-249 => answer-249", composed.Prompt);
        Assert.DoesNotContain("prompt-250 => answer-250", composed.Prompt);
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
            count++;
        return count;
    }

    private sealed class FakePersistence : IContentAcquisitionPersistence
    {
        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>([]);

        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);

        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
