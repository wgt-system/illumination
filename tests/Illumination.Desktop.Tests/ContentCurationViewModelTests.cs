using Illumination.Application.ContentManagement;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ContentCurationViewModelTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Existing_items_expose_quality_state_revision_and_history_activity()
    {
        var itemId = Guid.NewGuid();
        var oldReviewId = Guid.NewGuid();
        var store = new FakePersistence
        {
            Items =
            {
                [itemId] = Item(itemId, "Prompt", revision: 3, reviews:
                [
                    new QualityReviewSnapshot(oldReviewId, itemId, 3, QualityReviewOutcomeSnapshot.Pass, QualityReviewEvidenceTypeSnapshot.ModelReview, "Old pass", null, Guid.NewGuid()),
                    new QualityReviewSnapshot(Guid.NewGuid(), itemId, 3, QualityReviewOutcomeSnapshot.Warning, QualityReviewEvidenceTypeSnapshot.SourceGroundedReview, "Check source", "Clarify source", null)
                ])
            }
        };
        var vm = CreateViewModel(store);

        await vm.RefreshAsync([View(itemId, "Prompt")]);

        var row = Assert.Single(vm.Items);
        Assert.Equal("Warning", row.QualityState);
        Assert.Equal(3, row.ContentRevision);
        Assert.Contains(row.History, history => history.IsActive && history.Outcome == "Warning");
        Assert.Contains(row.History, history => !history.IsActive && history.Outcome == "Pass");
    }

    [Fact]
    public async Task Review_results_require_explicit_selection_and_allow_explicit_supersession()
    {
        var itemId = Guid.NewGuid();
        var oldReviewId = Guid.NewGuid();
        var store = new FakePersistence
        {
            Items =
            {
                [itemId] = Item(itemId, "Prompt", revision: 1, reviews:
                [new QualityReviewSnapshot(oldReviewId, itemId, 1, QualityReviewOutcomeSnapshot.Warning, QualityReviewEvidenceTypeSnapshot.ModelReview, "Old warning", null, null)])
            }
        };
        var vm = CreateViewModel(store);
        await vm.RefreshAsync([View(itemId, "Prompt")]);
        vm.Items.Single().IsSelectedForReview = true;
        await vm.GenerateReviewPromptCommand.ExecuteAsync(null);
        vm.RawReviewJson = ReviewResult(itemId, "pass", "Replaces the old warning.");
        await vm.PreviewReviewResultsCommand.ExecuteAsync(null);

        var result = Assert.Single(vm.ReviewResults);
        Assert.False(result.IsSelected);
        Assert.True(result.IsSelectable);
        result.SelectedSupersededReviewIds.Add(oldReviewId);

        await vm.AcceptSelectedReviewsCommand.ExecuteAsync(null);
        Assert.Empty(store.SavedItems);

        result.IsSelected = true;
        await vm.AcceptSelectedReviewsCommand.ExecuteAsync(null);

        var saved = Assert.Single(store.SavedItems);
        Assert.Contains(saved.QualityReviews ?? [], review => review.SupersededBy is not null);
        Assert.Contains(saved.QualityReviews ?? [], review => review.Findings == "Replaces the old warning.");
    }

    [Fact]
    public async Task Flags_are_user_defined_assignable_and_filterable_independently()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [firstId] = Item(firstId, "First"), [secondId] = Item(secondId, "Second") } };
        var vm = CreateViewModel(store);
        await vm.RefreshAsync([View(firstId, "First"), View(secondId, "Second")]);
        vm.NewFlagName = "Needs source";
        vm.NewFlagMeaning = "User wants a source checked";
        await vm.CreateFlagCommand.ExecuteAsync(null);
        vm.SelectedItem = vm.Items.First(x => x.Id == firstId);
        vm.SelectedFlag = Assert.Single(vm.FlagDefinitions);
        await vm.AddFlagCommand.ExecuteAsync(null);
        vm.FilterFlag = vm.SelectedFlag;

        Assert.Single(vm.FilteredItems);
        Assert.Equal(firstId, vm.FilteredItems.Single().Id);
        Assert.Equal("User wants a source checked", vm.FlagDefinitions.Single().Meaning);
    }

    private static ContentCurationViewModel CreateViewModel(FakePersistence store) =>
        new(new ContentCurationService(store, store), new QualityReviewExchangeService(store, store), _ => { });

    private static LearningItemView View(Guid id, string prompt) => new(id, prompt, "Solution", [], LearningItemResponseMode.SelfAssessed, [], [], [], false, LearningItemLifecycle.Active, true, Now, []);
    private static LearningItemSnapshot Item(Guid id, string prompt, int revision = 1, IReadOnlyList<QualityReviewSnapshot>? reviews = null) => new(id, prompt, "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false, LearningItemLifecycle.Active, true, Now, 5, .5, false, [], revision, reviews, []);
    private static string ReviewResult(Guid itemId, string outcome, string findings) => $"{{\"contract\":\"{QualityReviewExchangeService.Contract}\",\"version\":\"{QualityReviewExchangeService.Version}\",\"results\":[{{\"learningItemId\":\"{itemId:D}\",\"contentRevision\":1,\"outcome\":\"{outcome}\",\"evidenceType\":\"model_review\",\"findings\":\"{findings}\"}}]}}";

    private sealed class FakePersistence : IContentPersistence, IUserFlagDefinitionPersistence
    {
        public Dictionary<Guid, LearningItemSnapshot> Items { get; set; } = [];
        public Dictionary<Guid, UserFlagDefinitionSnapshot> Flags { get; } = [];
        public List<LearningItemSnapshot> SavedItems { get; } = [];
        public Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Items.Values.ToArray());
        public Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default) { Items[item.Id] = item; SavedItems.Add(item); return Task.CompletedTask; }
        public Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);
        public Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<DeckSnapshot?>(null);
        public Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserFlagDefinitionSnapshot>>(Flags.Values.ToArray());
        public Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default) { Flags[definition.Id] = definition; return Task.CompletedTask; }
    }
}
