using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class LearningAwareGenerationProfileTests
{
    [Fact]
    public async Task Follow_up_generation_includes_derived_learning_profile()
    {
        var deckId = Guid.NewGuid();
        var context = new DeckLearningContext(
            deckId,
            "Indo",
            [
                new DeckLearningContextItem(
                    Guid.NewGuid(),
                    "makan",
                    "essen",
                    LearningItemResponseMode.SelfAssessed,
                    LearningItemLifecycle.Active,
                    false,
                    DateTimeOffset.UtcNow.AddDays(-1),
                    7.5,
                    2,
                    true,
                    4,
                    StudyLearningAssessment.Schwer,
                    new AssessmentDistribution(1, 2, 1, 0, 0)),
                new DeckLearningContextItem(
                    Guid.NewGuid(),
                    "tertidur",
                    "einschlafen",
                    LearningItemResponseMode.SelfAssessed,
                    LearningItemLifecycle.Active,
                    false,
                    DateTimeOffset.UtcNow.AddDays(30),
                    3,
                    35,
                    false,
                    5,
                    StudyLearningAssessment.Leicht,
                    new AssessmentDistribution(0, 0, 0, 1, 4)),
            ]);

        var vm = new ContentAcquisitionViewModel(
            new ContentAcquisitionService(new FakePersistence(), TimeProvider.System),
            () => Task.CompletedTask,
            _ => { });
        vm.UpdateDecks([new DeckView(deckId, "Indo", context.Items.Select(item => item.LearningItemId).ToArray())]);
        vm.Subject = "Indonesian vocabulary";
        vm.UseExistingDeck = true;
        vm.SelectedExistingDeck = vm.ExistingDecks.Single();
        vm.ConfigureFollowUp(context, FollowUpProgressionMode.Continue, [LearningItemResponseMode.SelfAssessed]);

        await vm.GeneratePromptCommand.ExecuteAsync(null);

        Assert.Contains("Illumination-derived learning generation profile:", vm.GeneratedPrompt);
        Assert.Contains("Reinforcement candidates", vm.GeneratedPrompt);
        Assert.Contains("makan => essen", vm.GeneratedPrompt);
        Assert.Contains("Comparatively established material", vm.GeneratedPrompt);
        Assert.Contains("tertidur => einschlafen", vm.GeneratedPrompt);
        Assert.Contains("Continue should introduce genuinely new material", vm.GeneratedPrompt);
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
