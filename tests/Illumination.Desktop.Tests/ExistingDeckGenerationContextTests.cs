using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ExistingDeckGenerationContextTests
{
    [Fact]
    public async Task Existing_deck_generation_includes_current_card_inventory_and_language_profile()
    {
        var deckId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var service = new ContentAcquisitionService(new FakePersistence(), TimeProvider.System);
        var vm = new ContentAcquisitionViewModel(service, () => Task.CompletedTask, _ => { });
        vm.UpdateDecks([new DeckView(deckId, "Indo", [firstId, secondId])]);
        vm.ConfigureExistingDeckContent(() =>
        [
            Item(firstId, "makan", "essen", deckId),
            Item(secondId, "tertidur", "einschlafen", deckId),
        ]);

        vm.Subject = "Indonesian vocabulary";
        vm.UseExistingDeck = true;
        vm.SelectedExistingDeck = vm.ExistingDecks.Single();
        vm.InstructionLanguage = "German";
        vm.SourceLanguage = "Indonesian";
        vm.TargetLanguage = "German";
        vm.SelectedLanguageProficiency = vm.LanguageProficiencyOptions.Single(x => x.Level == LanguageProficiencyLevel.B1);
        vm.SelectedLanguageExerciseProfile = vm.LanguageExerciseProfileOptions.Single(x => x.Profile == LanguageExerciseProfile.VocabularyFlashcards);

        await vm.GeneratePromptCommand.ExecuteAsync(null);

        Assert.Contains("Existing target Deck anti-duplication inventory:", vm.GeneratedPrompt);
        Assert.Contains("prompt=makan | referenceSolution=essen", vm.GeneratedPrompt);
        Assert.Contains("prompt=tertidur | referenceSolution=einschlafen", vm.GeneratedPrompt);
        Assert.Contains("Do not generate the same word, phrase, question, answer pair", vm.GeneratedPrompt);
        Assert.Contains("CEFR B1", vm.GeneratedPrompt);
        Assert.Contains("Exercise profile: vocabulary flashcards", vm.GeneratedPrompt);
    }

    private static LearningItemView Item(Guid id, string prompt, string solution, Guid deckId) =>
        new(id, prompt, solution, [], LearningItemResponseMode.SelfAssessed, [], [], [], true,
            LearningItemLifecycle.Active, false, DateTimeOffset.UtcNow, [deckId]);

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
