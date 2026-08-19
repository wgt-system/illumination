using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class FreshExistingDeckLearningContextTests
{
    [Fact]
    public async Task Normal_existing_deck_generate_loads_fresh_learning_context_at_click_time()
    {
        var deckId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var providerCalls = 0;
        var status = string.Empty;
        var vm = CreateViewModel(message => status = message);
        vm.UpdateDecks([new DeckView(deckId, "Indo", [itemId])]);
        vm.ConfigureExistingDeckContent(() => [Item(itemId, "makan", "essen", deckId)]);
        vm.ConfigureDeckLearningContextProvider((requestedDeckId, _) =>
        {
            providerCalls++;
            Assert.Equal(deckId, requestedDeckId);
            return Task.FromResult(Context(deckId, itemId, "makan", "essen", StudyLearningAssessment.Schwer));
        });

        vm.Subject = "Indonesian vocabulary";
        vm.UseExistingDeck = true;
        vm.SelectedExistingDeck = vm.ExistingDecks.Single();
        vm.ProgressionMode = FollowUpProgressionMode.Continue;

        await vm.GenerateLearningAwarePromptCommand.ExecuteAsync(null);

        Assert.Equal(1, providerCalls);
        Assert.Contains("Illumination-derived learning generation profile:", vm.GeneratedPrompt);
        Assert.Contains("makan => essen", vm.GeneratedPrompt);
        Assert.Contains("Schwer=1", vm.GeneratedPrompt);
        Assert.Contains("Existing target Deck anti-duplication inventory:", vm.GeneratedPrompt);
        Assert.Contains("fresh learning evidence", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Explicit_follow_up_source_context_wins_over_target_deck_provider()
    {
        var targetDeckId = Guid.NewGuid();
        var targetItemId = Guid.NewGuid();
        var sourceDeckId = Guid.NewGuid();
        var sourceItemId = Guid.NewGuid();
        var providerCalls = 0;
        var vm = CreateViewModel(_ => { });
        vm.UpdateDecks([new DeckView(targetDeckId, "Target", [targetItemId])]);
        vm.ConfigureExistingDeckContent(() => [Item(targetItemId, "target-card", "target-answer", targetDeckId)]);
        vm.ConfigureDeckLearningContextProvider((_, _) =>
        {
            providerCalls++;
            return Task.FromResult(Context(targetDeckId, targetItemId, "stale-target", "stale", StudyLearningAssessment.Leicht));
        });
        vm.ConfigureFollowUp(
            Context(sourceDeckId, sourceItemId, "source-weak", "source-answer", StudyLearningAssessment.Nochmal),
            FollowUpProgressionMode.Reinforce,
            [LearningItemResponseMode.SelfAssessed]);

        vm.Subject = "Follow-up";
        vm.UseExistingDeck = true;
        vm.SelectedExistingDeck = vm.ExistingDecks.Single();

        await vm.GenerateLearningAwarePromptCommand.ExecuteAsync(null);

        Assert.Equal(0, providerCalls);
        Assert.Contains("Source Deck: Source", vm.GeneratedPrompt);
        Assert.Contains("source-weak => source-answer", vm.GeneratedPrompt);
        Assert.DoesNotContain("stale-target", vm.GeneratedPrompt);
    }

    private static ContentAcquisitionViewModel CreateViewModel(Action<string> reportStatus) =>
        new(
            new ContentAcquisitionService(new FakePersistence(), TimeProvider.System),
            () => Task.CompletedTask,
            reportStatus);

    private static LearningItemView Item(Guid id, string prompt, string solution, Guid deckId) =>
        new(
            id,
            prompt,
            solution,
            [],
            LearningItemResponseMode.SelfAssessed,
            [],
            [],
            [],
            true,
            LearningItemLifecycle.Active,
            false,
            DateTimeOffset.UtcNow,
            [deckId]);

    private static DeckLearningContext Context(
        Guid deckId,
        Guid itemId,
        string prompt,
        string solution,
        StudyLearningAssessment lastAssessment) =>
        new(
            deckId,
            deckId == Guid.Empty ? "Deck" : (prompt.StartsWith("source", StringComparison.Ordinal) ? "Source" : "Indo"),
            [
                new DeckLearningContextItem(
                    itemId,
                    prompt,
                    solution,
                    LearningItemResponseMode.SelfAssessed,
                    LearningItemLifecycle.Active,
                    false,
                    DateTimeOffset.UtcNow,
                    lastAssessment == StudyLearningAssessment.Leicht ? 3 : 7,
                    lastAssessment == StudyLearningAssessment.Leicht ? 30 : 2,
                    lastAssessment is StudyLearningAssessment.Nochmal or StudyLearningAssessment.Schwer,
                    1,
                    lastAssessment,
                    Distribution(lastAssessment)),
            ]);

    private static AssessmentDistribution Distribution(StudyLearningAssessment assessment) => assessment switch
    {
        StudyLearningAssessment.Nochmal => new AssessmentDistribution(1, 0, 0, 0, 0),
        StudyLearningAssessment.Schwer => new AssessmentDistribution(0, 1, 0, 0, 0),
        StudyLearningAssessment.Unsicher => new AssessmentDistribution(0, 0, 1, 0, 0),
        StudyLearningAssessment.Gut => new AssessmentDistribution(0, 0, 0, 1, 0),
        StudyLearningAssessment.Leicht => new AssessmentDistribution(0, 0, 0, 0, 1),
        _ => AssessmentDistribution.Empty,
    };

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
