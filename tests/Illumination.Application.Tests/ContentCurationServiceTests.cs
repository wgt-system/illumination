using System.Reflection;
using Illumination.Application.ContentManagement;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class ContentCurationServiceTests
{
    private static readonly DateTimeOffset DueAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Creates_lists_assigns_removes_and_filters_user_defined_flags()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new ContentCurationService(store, store);
        var later = await service.CreateUserFlagDefinitionAsync(new CreateUserFlagDefinitionCommand("Later", "Review later"));
        var wording = await service.CreateUserFlagDefinitionAsync(new CreateUserFlagDefinitionCommand("Wording", "Improve wording"));

        await service.AddFlagToLearningItemAsync(itemId, later.Id);
        await service.AddFlagToLearningItemAsync(itemId, wording.Id);
        var filtered = await service.ListLearningItemsByFlagsAsync([later.Id, wording.Id]);

        Assert.Equal(2, (await service.ListUserFlagDefinitionsAsync()).Count);
        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].ContentRevision);
        Assert.Equal(new[] { later.Id, wording.Id }.OrderBy(x => x), filtered[0].UserFlagDefinitionIds.OrderBy(x => x));

        var removed = await service.RemoveFlagFromLearningItemAsync(itemId, later.Id);
        Assert.DoesNotContain(later.Id, removed.UserFlagDefinitionIds);
        Assert.Contains(wording.Id, removed.UserFlagDefinitionIds);
        Assert.Empty(await service.ListLearningItemsByFlagsAsync([later.Id, wording.Id]));
    }

    [Fact]
    public async Task Accepts_current_revision_review_without_changing_content_and_exposes_aggregate_state()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new ContentCurationService(store, store);

        var result = await service.AcceptQualityReviewAsync(itemId, new AcceptQualityReviewCommand(
            CurationQualityReviewOutcome.Warning,
            CurationQualityReviewEvidenceType.SourceGroundedReview,
            "Ambiguous wording.",
            "Clarify the second sentence."));

        Assert.Equal("Prompt", result.Prompt);
        Assert.Equal(1, result.ContentRevision);
        Assert.Equal(CurationQualityReviewOutcome.Warning, result.CurrentQualityState!.Outcome);
        var review = Assert.Single(result.QualityReviews);
        Assert.Equal(CurationQualityReviewEvidenceType.SourceGroundedReview, review.EvidenceType);
        Assert.Equal("Ambiguous wording.", review.Findings);
        Assert.Equal("Clarify the second sentence.", review.SuggestedCorrection);
        Assert.Equal(DueAt, store.SavedItems.Single().DueAt);
        Assert.Equal(1, store.SavedItems.Single().ContentRevision);
    }

    [Fact]
    public async Task Supersession_is_explicit_same_revision_and_history_remains_available()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new ContentCurationService(store, store);
        var first = await service.AcceptQualityReviewAsync(itemId, new AcceptQualityReviewCommand(
            CurationQualityReviewOutcome.Warning, CurationQualityReviewEvidenceType.ModelReview, "First"));
        var firstId = Assert.Single(first.QualityReviews).Id;

        var second = await service.AcceptQualityReviewAsync(itemId, new AcceptQualityReviewCommand(
            CurationQualityReviewOutcome.Pass, CurationQualityReviewEvidenceType.UserReview, "Resolved", SupersededReviewIds: [firstId]));

        Assert.Equal(2, second.QualityReviews.Count);
        Assert.True(second.QualityReviews.Single(x => x.Id == firstId).SupersededBy.HasValue);
        Assert.Equal(CurationQualityReviewOutcome.Pass, second.CurrentQualityState!.Outcome);
        var contracts = new[]
        {
            typeof(ContentCurationService),
            typeof(CreateUserFlagDefinitionCommand),
            typeof(UserFlagDefinitionView),
            typeof(QualityReviewView),
            typeof(CurrentQualityStateView),
            typeof(CuratedLearningItemView),
            typeof(AcceptQualityReviewCommand),
        };
        Assert.DoesNotContain(PublicContractTypes(contracts), type => type.FullName?.StartsWith("Illumination.Domain.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Unknown_flag_and_invalid_supersession_fail_without_saving()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new ContentCurationService(store, store);

        await Assert.ThrowsAsync<ContentValidationException>(() => service.AddFlagToLearningItemAsync(itemId, Guid.NewGuid()));
        await Assert.ThrowsAsync<ContentValidationException>(() => service.AcceptQualityReviewAsync(itemId, new AcceptQualityReviewCommand(
            CurationQualityReviewOutcome.Pass, CurationQualityReviewEvidenceType.UserReview, "Invalid", SupersededReviewIds: [Guid.NewGuid()])));
        Assert.Empty(store.SavedItems);
    }

    private static LearningItemSnapshot Item(Guid id) => new(
        id, "Prompt", "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false,
        LearningItemLifecycle.Active, true, DueAt, 5.0, 0.5, false, [], 1, [], []);

    private static IEnumerable<Type> PublicContractTypes(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(method => method.GetParameters().Select(x => x.ParameterType).Append(method.ReturnType))
            .SelectMany(Flatten);

    private static IEnumerable<Type> PublicContractTypes(IEnumerable<Type> types) => types.SelectMany(PublicContractTypes);

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsArray) foreach (var nested in Flatten(type.GetElementType()!)) yield return nested;
        if (type.IsGenericType) foreach (var nested in type.GetGenericArguments().SelectMany(Flatten)) yield return nested;
    }

    private sealed class FakePersistence : IContentPersistence, IUserFlagDefinitionPersistence
    {
        public Dictionary<Guid, LearningItemSnapshot> Items { get; } = [];
        public Dictionary<Guid, UserFlagDefinitionSnapshot> Definitions { get; } = [];
        public List<LearningItemSnapshot> SavedItems { get; } = [];

        public Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Items.Values.ToArray());
        public Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default) { Items[item.Id] = item; SavedItems.Add(item); return Task.CompletedTask; }
        public Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);
        public Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<DeckSnapshot?>(null);
        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserFlagDefinitionSnapshot>>(Definitions.Values.ToArray());
        public Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default) { Definitions[definition.Id] = definition; return Task.CompletedTask; }
    }
}
