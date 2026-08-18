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

    [Theory]
    [InlineData(QualityReviewPromptMode.Standard, "model_review")]
    [InlineData(QualityReviewPromptMode.Strict, "model_review")]
    [InlineData(QualityReviewPromptMode.SourceGrounded, "source_grounded_review")]
    public async Task Generates_mode_specific_prompt_with_required_evidence_type(QualityReviewPromptMode mode, string evidenceType)
    {
        var itemId = Guid.NewGuid();
        var service = new QualityReviewExchangeService(
            new FakePersistence { Items = { [itemId] = Item(itemId) } },
            new FakePersistence());

        var prompt = await service.GeneratePromptAsync(new GenerateQualityReviewPromptCommand([itemId], mode));

        Assert.Contains(itemId.ToString("D"), prompt.Prompt);
        Assert.Contains("ContentRevision: 1", prompt.Prompt);
        Assert.Contains($"emit evidenceType \"{evidenceType}\"", prompt.Prompt);
        Assert.DoesNotContain("emit evidenceType \"user_review\"", prompt.Prompt);
        if (mode == QualityReviewPromptMode.SourceGrounded) Assert.Contains("source/evidence information", prompt.Prompt);
        Assert.Contains("illumination.quality-review-result", prompt.Prompt);
    }

    [Fact]
    public async Task Preview_keeps_mixed_validity_distinguishable_without_persistence()
    {
        var validId = Guid.NewGuid();
        var invalidId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [validId] = Item(validId), [invalidId] = Item(invalidId) } };
        var service = new QualityReviewExchangeService(store, store);
        var raw = Bundle(Result(validId, 1, "pass", "model_review", "Clear."), Result(invalidId, 1, "unsupported", "model_review", "Bad outcome."));

        var preview = await service.PreviewAsync(raw);

        Assert.False(preview.IsValid);
        Assert.Equal(2, preview.Results.Count);
        Assert.True(preview.Results[0].IsValid);
        Assert.False(preview.Results[1].IsValid);
        Assert.Empty(store.SavedItems);
    }

    [Theory]
    [InlineData(QualityReviewPromptMode.Standard, "user_review")]
    [InlineData(QualityReviewPromptMode.Strict, "source_grounded_review")]
    [InlineData(QualityReviewPromptMode.SourceGrounded, "model_review")]
    public async Task Preview_rejects_evidence_type_that_does_not_match_exchange_mode(QualityReviewPromptMode mode, string evidenceType)
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new QualityReviewExchangeService(store, store);
        var raw = Bundle(Result(itemId, 1, "pass", evidenceType, "Finding."));

        var preview = await service.PreviewAsync(raw, mode);
        await Assert.ThrowsAsync<QualityReviewExchangeValidationException>(() =>
            service.AcceptSelectedAsync(new AcceptQualityReviewResultsCommand(raw, [0], mode)));

        Assert.False(preview.IsValid);
        Assert.False(Assert.Single(preview.Results).IsValid);
        Assert.Contains(preview.Results[0].Diagnostics, x => x.Code == "result.evidence_type");
        Assert.Empty(store.SavedItems);
    }

    [Fact]
    public async Task Stale_result_is_rejected_without_saving()
    {
        var itemId = Guid.NewGuid();
        var current = Item(itemId) with { ContentRevision = 2 };
        var store = new FakePersistence { Items = { [itemId] = current } };
        var service = new QualityReviewExchangeService(store, store);
        var raw = Bundle(Result(itemId, 1, "warning", "model_review", "Stale."));

        var preview = await service.PreviewAsync(raw);
        await Assert.ThrowsAsync<QualityReviewExchangeValidationException>(() => service.AcceptSelectedAsync(new AcceptQualityReviewResultsCommand(raw, [0])));

        Assert.False(preview.IsValid);
        Assert.Contains(preview.Results[0].Diagnostics, x => x.Code == "target.revision.stale");
        Assert.Empty(store.SavedItems);
    }

    [Fact]
    public async Task Accepts_selected_result_without_applying_suggested_correction()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId) } };
        var service = new QualityReviewExchangeService(store, store);
        var raw = Bundle(Result(itemId, 1, "warning", "source_grounded_review", "Needs a source.", "Rewrite it."));

        var accepted = await service.AcceptSelectedAsync(new AcceptQualityReviewResultsCommand(raw, [0], QualityReviewPromptMode.SourceGrounded));

        var item = Assert.Single(accepted.AcceptedItems);
        Assert.Equal("Prompt", item.Prompt);
        Assert.Equal(1, item.ContentRevision);
        Assert.Equal("Rewrite it.", Assert.Single(item.QualityReviews).SuggestedCorrection);
        Assert.Single(store.SavedItems);
    }

    [Fact]
    public async Task Exchange_public_contracts_expose_no_domain_types()
    {
        var contracts = new[]
        {
            typeof(QualityReviewExchangeService), typeof(QualityReviewPromptMode),
            typeof(GenerateQualityReviewPromptCommand), typeof(GeneratedQualityReviewPrompt),
            typeof(QualityReviewExchangePreview), typeof(QualityReviewResultPreview),
            typeof(AcceptQualityReviewResultsCommand), typeof(QualityReviewExchangeAcceptanceResult),
        };

        Assert.DoesNotContain(PublicContractTypes(contracts), type => type.FullName?.StartsWith("Illumination.Domain.", StringComparison.Ordinal) == true);
    }

    private static string Bundle(params string[] results) =>
        $"{{\"contract\":\"illumination.quality-review-result\",\"version\":\"1.0\",\"results\":[{string.Join(',', results)}]}}";

    private static string Result(Guid itemId, int revision, string outcome, string evidenceType, string findings, string? suggestedCorrection = null)
    {
        var correction = suggestedCorrection is null ? string.Empty : $",\"suggestedCorrection\":\"{suggestedCorrection}\"";
        return $"{{\"learningItemId\":\"{itemId:D}\",\"contentRevision\":{revision},\"outcome\":\"{outcome}\",\"evidenceType\":\"{evidenceType}\",\"findings\":\"{findings}\"{correction}}}";
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
        public Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserFlagDefinitionSnapshot>>(Definitions.Values.ToArray());
        public Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default) { Definitions[definition.Id] = definition; return Task.CompletedTask; }
    }
}
