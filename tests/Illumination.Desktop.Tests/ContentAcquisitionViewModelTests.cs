using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ContentAcquisitionViewModelTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Generates_prompts_for_new_and_existing_Deck_targets()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        viewModel.Subject = "Java records";
        viewModel.RequestedItemCount = 10;
        viewModel.NewDeckName = "Java Basics";

        await viewModel.GeneratePromptCommand.ExecuteAsync(null);

        Assert.Contains("Java Basics", viewModel.GeneratedPrompt);
        Assert.Contains("exactly 10", viewModel.GeneratedPrompt);

        var deckId = Guid.NewGuid();
        viewModel.UpdateDecks([new DeckView(deckId, "Existing", [])]);
        viewModel.UseExistingDeck = true;
        await viewModel.GeneratePromptCommand.ExecuteAsync(null);

        Assert.Contains(deckId.ToString("D"), viewModel.GeneratedPrompt);
        Assert.DoesNotContain("name `Java Basics`", viewModel.GeneratedPrompt);
    }

    [Fact]
    public async Task Raw_json_change_invalidates_the_current_preview_and_selection()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        viewModel.RawJson = Bundle(CreateDeck("deck", "Deck"));
        await viewModel.ValidateCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasCurrentPreview);
        Assert.Single(viewModel.Operations);

        viewModel.RawJson += " ";

        Assert.False(viewModel.HasCurrentPreview);
        Assert.Empty(viewModel.Operations);
        Assert.False(viewModel.ImportSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Mixed_preview_selects_valid_and_warning_rows_but_never_invalid_rows()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        viewModel.RawJson = Bundle(
            CreateDeck("deck", "Deck"),
            """{"op":"create_deck","localRef":"bad","deck":{}}""",
            CreateItem("first", "Same prompt"),
            CreateItem("second", " same   prompt "));

        await viewModel.ValidateCommand.ExecuteAsync(null);

        Assert.Equal("3 valid · 1 invalid", viewModel.PreviewSummary);
        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.False(viewModel.Operations[1].IsSelectable);
        Assert.False(viewModel.Operations[1].IsSelected);
        Assert.True(viewModel.Operations[3].IsSelectable);
        Assert.True(viewModel.Operations[3].IsSelected);
        Assert.True(viewModel.Operations[3].HasWarnings);
    }

    [Fact]
    public async Task Clear_and_select_all_valid_preserve_invalid_selection_rules()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        viewModel.RawJson = Bundle(CreateDeck("deck", "Deck"), """{"op":"create_deck","localRef":"bad","deck":{}}""");
        await viewModel.ValidateCommand.ExecuteAsync(null);

        viewModel.ClearSelectionCommand.Execute(null);
        Assert.All(viewModel.Operations, operation => Assert.False(operation.IsSelected));

        viewModel.SelectAllValidCommand.Execute(null);
        Assert.True(viewModel.Operations[0].IsSelected);
        Assert.False(viewModel.Operations[1].IsSelected);
    }

    [Fact]
    public async Task Repair_prompt_is_disabled_for_valid_preview_and_enabled_for_malformed_json()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        var desktop = new FakeDesktopInteractions();
        viewModel.AttachDesktopInteractions(desktop);

        viewModel.RawJson = Bundle(CreateDeck("valid", "Valid"));
        await viewModel.ValidateCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCurrentPreview);
        Assert.False(viewModel.CanGenerateRepairPrompt);
        Assert.False(viewModel.GenerateRepairPromptCommand.CanExecute(null));

        viewModel.RawJson = "{ malformed";
        await viewModel.ValidateCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanGenerateRepairPrompt);
        Assert.NotEmpty(viewModel.BundleDiagnostics);

        await viewModel.GenerateRepairPromptCommand.ExecuteAsync(null);
        await viewModel.CopyRepairPromptCommand.ExecuteAsync(null);

        Assert.Contains("Repair only", viewModel.RepairPrompt);
        Assert.Equal(viewModel.RepairPrompt, desktop.CopiedText);
    }

    [Fact]
    public async Task Successful_import_commits_selected_operations_reports_result_and_refreshes_content()
    {
        var persistence = new FakePersistence();
        var viewModel = CreateViewModel(persistence, out _, out var refreshCount);
        viewModel.RawJson = ValidDependentBundle();
        await viewModel.ValidateCommand.ExecuteAsync(null);

        await viewModel.ImportSelectedCommand.ExecuteAsync(null);

        var commit = Assert.Single(persistence.Commits);
        Assert.Equal(3, commit.Provenance.AcceptedOperationCount);
        Assert.Contains("1 Learning Items created", viewModel.ImportResult);
        Assert.Contains("1 Decks created", viewModel.ImportResult);
        Assert.Contains("1 memberships applied", viewModel.ImportResult);
        Assert.Equal(1, refreshCount.Value);
        Assert.True(viewModel.HasImportResult);
        Assert.False(viewModel.HasCurrentPreview);
        Assert.False(viewModel.ImportSelectedCommand.CanExecute(null));

        await viewModel.ImportSelectedCommand.ExecuteAsync(null);

        Assert.Single(persistence.Commits);

        await viewModel.ValidateCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCurrentPreview);
        Assert.True(viewModel.ImportSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Missing_selected_dependency_shows_structured_validation_without_success()
    {
        var persistence = new FakePersistence();
        var viewModel = CreateViewModel(persistence, out _, out _);
        viewModel.RawJson = ValidDependentBundle();
        await viewModel.ValidateCommand.ExecuteAsync(null);
        viewModel.Operations[0].IsSelected = false;

        await viewModel.ImportSelectedCommand.ExecuteAsync(null);

        Assert.Empty(persistence.Commits);
        Assert.Empty(viewModel.ImportResult);
        Assert.Contains(viewModel.BundleDiagnostics, diagnostic => diagnostic.Code == "selection.dependency");

        viewModel.Operations[0].IsSelected = true;
        await viewModel.ImportSelectedCommand.ExecuteAsync(null);

        Assert.Single(persistence.Commits);
        Assert.DoesNotContain(viewModel.BundleDiagnostics, diagnostic => diagnostic.Code == "selection.dependency");
        Assert.True(viewModel.HasImportResult);
    }

    [Fact]
    public async Task Clipboard_and_file_loading_are_presentation_operations_and_loaded_json_stays_uncommitted()
    {
        var viewModel = CreateViewModel(new FakePersistence(), out _, out _);
        var desktop = new FakeDesktopInteractions { LoadedJson = Bundle(CreateDeck("loaded", "Loaded")) };
        viewModel.AttachDesktopInteractions(desktop);
        viewModel.Subject = "Topic";
        viewModel.NewDeckName = "Deck";
        await viewModel.GeneratePromptCommand.ExecuteAsync(null);
        await viewModel.CopyPromptCommand.ExecuteAsync(null);
        await viewModel.LoadJsonFileCommand.ExecuteAsync(null);

        Assert.Equal(viewModel.GeneratedPrompt, desktop.CopiedText);
        Assert.Equal(desktop.LoadedJson, viewModel.RawJson);
        Assert.False(viewModel.HasCurrentPreview);
    }

    private static ContentAcquisitionViewModel CreateViewModel(
        FakePersistence persistence,
        out List<string> statuses,
        out Counter refreshCount)
    {
        statuses = [];
        refreshCount = new Counter();
        var capturedStatuses = statuses;
        var capturedCounter = refreshCount;
        return new ContentAcquisitionViewModel(
            new ContentAcquisitionService(persistence, new FixedTimeProvider(Now)),
            () =>
            {
                capturedCounter.Value++;
                return Task.CompletedTask;
            },
            capturedStatuses.Add);
    }

    private static string ValidDependentBundle() => Bundle(
        CreateDeck("deck", "Deck"),
        CreateItem("item", "Prompt"),
        """{"op":"assign_item_to_decks","item":{"itemLocalRef":"item"},"decks":[{"deckLocalRef":"deck"}]}""");

    private static string CreateDeck(string localRef, string name) =>
        $"{{\"op\":\"create_deck\",\"localRef\":\"{localRef}\",\"deck\":{{\"name\":\"{name}\"}}}}";

    private static string CreateItem(string localRef, string prompt) =>
        $"{{\"op\":\"create_learning_item\",\"localRef\":\"{localRef}\",\"item\":{{\"prompt\":\"{prompt}\",\"referenceSolution\":\"Solution\",\"responseMode\":\"self_assessed\",\"lowInteractionEligible\":false}}}}";

    private static string Bundle(params string[] operations) =>
        $"{{\"contract\":\"{ContentAcquisitionService.Contract}\",\"version\":\"1.0\",\"operations\":[{string.Join(',', operations)}]}}";

    private sealed class Counter { public int Value { get; set; } }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class FakeDesktopInteractions : IDesktopInteractionService
    {
        public string? CopiedText { get; private set; }
        public string? LoadedJson { get; init; }
        public Task CopyTextAsync(string text) { CopiedText = text; return Task.CompletedTask; }
        public Task<string?> LoadJsonFileAsync() => Task.FromResult(LoadedJson);
    }

    private sealed class FakePersistence : IContentAcquisitionPersistence
    {
        public Dictionary<Guid, LearningItemSnapshot> Items { get; } = [];
        public Dictionary<Guid, DeckSnapshot> Decks { get; } = [];
        public List<ContentAcquisitionCommitSnapshot> Commits { get; } = [];

        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Items.Values.ToArray());

        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>(Decks.Values.ToArray());

        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Commits.Add(snapshot);
            foreach (var item in snapshot.LearningItems) Items[item.Id] = item;
            foreach (var deck in snapshot.Decks) Decks[deck.Id] = deck;
            return Task.CompletedTask;
        }
    }
}
