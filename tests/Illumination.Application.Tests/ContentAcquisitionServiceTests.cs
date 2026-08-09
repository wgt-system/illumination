using System.Reflection;
using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class ContentAcquisitionServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void Prompt_generation_contains_the_canonical_contract_and_target_guidance()
    {
        var service = new ContentAcquisitionService(new FakePersistence(), new FixedTimeProvider(Now));
        var prompt = service.GenerateContentPrompt(new GenerateContentPromptCommand("C# async", 3, NewDeckName: "Async Deck")).Prompt;
        Assert.Contains("illumination.content-bundle", prompt);
        Assert.Contains("1.0", prompt);
        Assert.Contains("Return JSON only", prompt);
        Assert.Contains("self_assessed", prompt);
        Assert.Contains("referenceSolution", prompt);
        Assert.Contains("target-deck", prompt);
        Assert.Contains("C# async", prompt);
        Assert.Contains("3", prompt);
    }

    [Fact]
    public async Task Preview_keeps_valid_siblings_when_one_operation_is_malformed_and_is_side_effect_free()
    {
        var store = new FakePersistence();
        var service = new ContentAcquisitionService(store, new FixedTimeProvider(Now));
        var preview = await service.PreviewContentBundleAsync(Bundle(
            """{"op":"create_deck","localRef":"bad","deck":{}}""",
            """{"op":"create_learning_item","localRef":"item","item":{"prompt":"Prompt","referenceSolution":"Solution","responseMode":"self_assessed","lowInteractionEligible":false}}"""));

        Assert.False(preview.IsValid);
        Assert.Equal(2, preview.Operations.Count);
        Assert.False(preview.Operations[0].IsSelectable);
        Assert.True(preview.Operations[1].IsSelectable);
        Assert.Empty(store.Commits);
    }

    [Fact]
    public async Task Selected_valid_subset_is_dependency_checked_and_committed_atomically()
    {
        var store = new FakePersistence();
        var service = new ContentAcquisitionService(store, new FixedTimeProvider(Now));
        var bundle = Bundle(
            """{"op":"create_deck","localRef":"deck","deck":{"name":"Deck"}}""",
            """{"op":"create_learning_item","localRef":"item","item":{"prompt":"Prompt","referenceSolution":"Solution","responseMode":"self_assessed","lowInteractionEligible":false}}""",
            """{"op":"assign_item_to_decks","item":{"itemLocalRef":"item"},"decks":[{"deckLocalRef":"deck"}]}"""
#if false
            "{\"op\":\"assign_item_to_decks\",\"item\":{"itemLocalRef":"item"},\"decks\":[{\"deckLocalRef":"deck"}]} ");

 #endif
        );
        var result = await service.CommitContentBundleAsync(new CommitContentBundleCommand(bundle, [0, 1, 2]));
        Assert.Equal(3, result.CommittedOperationIndices.Count);
        Assert.Single(result.CreatedDeckIds);
        Assert.Single(result.CreatedLearningItemIds);
        Assert.Equal(1, result.AppliedMembershipCount);
        Assert.Single(store.Commits);
        Assert.Equal(3, store.Commits[0].Provenance.AcceptedOperationCount);
    }

    [Fact]
    public async Task Duplicate_prompts_warn_deterministically_without_invalidating_or_merging()
    {
        var service = new ContentAcquisitionService(new FakePersistence(), new FixedTimeProvider(Now));
        var preview = await service.PreviewContentBundleAsync(Bundle(
            """{"op":"create_learning_item","localRef":"a","item":{"prompt":"  Same   Prompt ","referenceSolution":"S1","responseMode":"self_assessed","lowInteractionEligible":false}}""",
            """{"op":"create_learning_item","localRef":"b","item":{"prompt":"same prompt","referenceSolution":"S2","responseMode":"self_assessed","lowInteractionEligible":false}}"""));
        Assert.True(preview.Operations.All(x => x.IsSelectable));
        Assert.Contains(preview.Operations[1].Warnings, warning => warning.Contains("earlier", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Malformed_json_exposes_repair_capability_without_mutation()
    {
        var store = new FakePersistence();
        var service = new ContentAcquisitionService(store, new FixedTimeProvider(Now));
        var preview = await service.PreviewContentBundleAsync("{ malformed");
        var repair = service.GenerateRepairPrompt(new GenerateRepairPromptCommand("{ malformed", preview.Diagnostics));
        Assert.True(preview.CanGenerateRepairPrompt);
        Assert.Contains("repair only", repair.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{ malformed", repair.Prompt);
        Assert.Empty(store.Commits);
    }

    [Fact]
    public async Task Minor_update_preserves_complete_learning_state_and_semantic_update_resets_it()
    {
        var itemId = Guid.NewGuid();
        var store = new FakePersistence { Items = { [itemId] = Item(itemId, "Old", isNew: false, difficulty: 8.25, stability: 4.5, relearning: true) } };
        var service = new ContentAcquisitionService(store, new FixedTimeProvider(Now));
        var minor = Bundle($"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"minor\",\"item\":{{\"prompt\":\"Minor\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}");
        await service.CommitContentBundleAsync(new CommitContentBundleCommand(minor, [0]));
        Assert.Equal(8.25, store.CommittedItems[itemId].Difficulty);
        Assert.True(store.CommittedItems[itemId].IsInShortTermRelearning);

        var semantic = Bundle($"{{\"op\":\"update_learning_item\",\"itemId\":\"{itemId}\",\"significance\":\"semantic\",\"item\":{{\"prompt\":\"Semantic\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}");
        await service.CommitContentBundleAsync(new CommitContentBundleCommand(semantic, [0]));
        Assert.True(store.CommittedItems[itemId].IsNew);
        Assert.Equal(Now, store.CommittedItems[itemId].DueAt);
        Assert.Equal(5.0, store.CommittedItems[itemId].Difficulty);
        Assert.False(store.CommittedItems[itemId].IsInShortTermRelearning);
    }

    [Fact]
    public void Public_acquisition_contracts_expose_no_domain_types()
    {
        var types = new[] { typeof(ContentAcquisitionService), typeof(IContentAcquisitionPersistence), typeof(GenerateContentPromptCommand), typeof(ContentBundlePreview), typeof(ContentImportResult) };
        var exposed = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).SelectMany(method => method.GetParameters().Select(x => x.ParameterType).Append(method.ReturnType))).SelectMany(Flatten).ToArray();
        Assert.DoesNotContain(exposed, type => type.FullName?.StartsWith("Illumination.Domain.", StringComparison.Ordinal) == true);
    }

    private static string Bundle(params string[] operations) => $"{{\"contract\":\"{ContentAcquisitionService.Contract}\",\"version\":\"1.0\",\"operations\":[{string.Join(',', operations)}]}}";
    private static LearningItemSnapshot Item(Guid id, string prompt, bool isNew = true, double difficulty = 5.0, double stability = 0.5, bool relearning = false) => new(id, prompt, "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false, LearningItemLifecycle.Active, isNew, Now, difficulty, stability, relearning, []);
    private static IEnumerable<Type> Flatten(Type type) { yield return type; if (type.IsArray) foreach (var nested in Flatten(type.GetElementType()!)) yield return nested; if (type.IsGenericType) foreach (var nested in type.GetGenericArguments().SelectMany(Flatten)) yield return nested; }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FakePersistence : IContentAcquisitionPersistence
    {
        public Dictionary<Guid, LearningItemSnapshot> Items { get; } = [];
        public Dictionary<Guid, LearningItemSnapshot> CommittedItems { get; } = [];
        public List<ContentAcquisitionCommitSnapshot> Commits { get; } = [];
        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Items.Values.ToArray());
        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);
        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default) { Commits.Add(snapshot); foreach (var item in snapshot.LearningItems) CommittedItems[item.Id] = item; return Task.CompletedTask; }
    }
}
