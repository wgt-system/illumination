using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Illumination.Desktop;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ContentManagementHardeningTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Short_text_editor_persists_edits_and_reordered_answers()
    {
        var store = new FakeContentPersistence();
        var content = new ContentManagementService(store, new FixedTimeProvider(Now));
        var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt", "Solution", LearningItemResponseMode.ShortText,
            AcceptedShortAnswers: ["first", "second"]));
        var editor = new LearningItemEditorViewModel(content, _ => { }, () => Task.CompletedTask);

        await editor.BeginEditAsync(item.Id);
        editor.AcceptedAnswers[0].Text = "changed";
        editor.MoveAcceptedAnswerDownCommand.Execute(editor.AcceptedAnswers[0]);
        await editor.SaveCommand.ExecuteAsync(null);

        var updated = await content.GetLearningItemAsync(item.Id);
        Assert.Equal(["second", "changed"], updated.AcceptedShortAnswers);
    }

    [Fact]
    public async Task Editor_reorders_authored_collections_without_changing_choice_identity()
    {
        var store = new FakeContentPersistence();
        var content = new ContentManagementService(store, new FixedTimeProvider(Now));
        var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt", "Solution", LearningItemResponseMode.Selection,
            Hints: [new HintInput("h1"), new HintInput("h2")],
            DirectAnswerChoices:
            [
                new AnswerChoiceInput("A", true, "choice-a"),
                new AnswerChoiceInput("B", false, "choice-b")
            ],
            AssistanceAnswerChoices:
            [
                new AnswerChoiceInput("X", false, "assist-x"),
                new AnswerChoiceInput("Y", false, "assist-y")
            ]));
        var editor = new LearningItemEditorViewModel(content, _ => { }, () => Task.CompletedTask);

        await editor.BeginEditAsync(item.Id);
        editor.MoveHintUpCommand.Execute(editor.Hints[1]);
        editor.MoveDirectChoiceUpCommand.Execute(editor.DirectChoices[1]);
        editor.MoveAssistanceChoiceUpCommand.Execute(editor.AssistanceChoices[1]);
        await editor.SaveCommand.ExecuteAsync(null);

        var updated = await content.GetLearningItemAsync(item.Id);
        Assert.Equal(["h2", "h1"], updated.Hints.Select(x => x.Text));
        Assert.Equal(["choice-b", "choice-a"], updated.DirectAnswerChoices.Select(x => x.Id));
        Assert.Equal(["assist-y", "assist-x"], updated.AssistanceAnswerChoices.Select(x => x.Id));
    }

    [Fact]
    public async Task Response_mode_switch_does_not_save_hidden_incompatible_fields()
    {
        var store = new FakeContentPersistence();
        var content = new ContentManagementService(store, new FixedTimeProvider(Now));
        var item = await content.CreateLearningItemAsync(new CreateLearningItemCommand(
            "Prompt", "Solution", LearningItemResponseMode.Selection,
            DirectAnswerChoices:
            [
                new AnswerChoiceInput("A", true, "choice-a"),
                new AnswerChoiceInput("B", false, "choice-b")
            ]));
        var editor = new LearningItemEditorViewModel(content, _ => { }, () => Task.CompletedTask);

        await editor.BeginEditAsync(item.Id);
        editor.ResponseMode = LearningItemResponseMode.ShortText;
        editor.AddAcceptedAnswerCommand.Execute(null);
        editor.AcceptedAnswers[0].Text = "accepted";
        await editor.SaveCommand.ExecuteAsync(null);

        var updated = await content.GetLearningItemAsync(item.Id);
        Assert.Equal(LearningItemResponseMode.ShortText, updated.ResponseMode);
        Assert.Empty(updated.DirectAnswerChoices);
        Assert.Equal(["accepted"], updated.AcceptedShortAnswers);
    }

    [Fact]
    public async Task Deck_filter_uses_duplicate_safe_presentation_but_filters_by_stable_identity()
    {
        var firstDeckId = Guid.NewGuid();
        var secondDeckId = Guid.NewGuid();
        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var store = new FakeContentPersistence();
        store.Items[firstItemId] = Snapshot(firstItemId, "First", [firstDeckId]);
        store.Items[secondItemId] = Snapshot(secondItemId, "Second", [secondDeckId]);
        var curation = new ContentCurationViewModel(
            new ContentCurationService(store, store),
            new QualityReviewExchangeService(store, store),
            _ => { });
        await curation.RefreshAsync(
        [
            View(firstItemId, "First", [firstDeckId]),
            View(secondItemId, "Second", [secondDeckId])
        ]);
        var presentations = DeckPresentationLabeler.Label(
        [
            new DeckView(firstDeckId, "Indo", [firstItemId]),
            new DeckView(secondDeckId, "Indo", [secondItemId])
        ]);

        curation.FilterDeckPresentation = presentations[1];

        Assert.Equal("Indo (2)", presentations[1].DisplayName);
        Assert.Equal(secondItemId, Assert.Single(curation.FilteredItems).Id);

        curation.ClearLibraryFiltersCommand.Execute(null);
        Assert.Equal(2, curation.FilteredItems.Count());
    }

    [Fact]
    public async Task Delete_confirmation_is_reset_when_deck_or_item_target_changes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var first = await content.CreateLearningItemAsync(new CreateLearningItemCommand("First", "One"));
        var second = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Second", "Two"));
        var third = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Third", "Three"));
        var deckA = await content.CreateDeckAsync(new CreateDeckCommand("Deck"));
        var deckB = await content.CreateDeckAsync(new CreateDeckCommand("Deck"));
        await content.AddLearningItemToDeckAsync(deckA.Id, first.Id);
        await content.AddLearningItemToDeckAsync(deckA.Id, third.Id);
        await content.AddLearningItemToDeckAsync(deckB.Id, second.Id);

        var vm = new MainWindowViewModel(
            content,
            study,
            new ContentAcquisitionService(new FakeAcquisitionPersistence(), time),
            new ContentCurationService(persistence, persistence),
            new QualityReviewExchangeService(persistence, persistence),
            time);
        await vm.InitializeAsync();

        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deckA.Id);
        await vm.DeleteSelectedDeckCommand.ExecuteAsync(null);
        vm.SelectedDeck = vm.Decks.Single(x => x.Id == deckB.Id);
        await vm.DeleteSelectedDeckCommand.ExecuteAsync(null);
        Assert.Equal(deckB.Id, (await content.GetDeckAsync(deckB.Id)).Id);
        await vm.DeleteSelectedDeckCommand.ExecuteAsync(null);
        await Assert.ThrowsAsync<ContentNotFoundException>(() => content.GetDeckAsync(deckB.Id));
        Assert.Equal(deckA.Id, (await content.GetDeckAsync(deckA.Id)).Id);

        vm.ContentCuration.SelectedItem = vm.ContentCuration.Items.Single(x => x.Id == first.Id);
        await vm.DeleteSelectedLearningItemCommand.ExecuteAsync(null);
        vm.ContentCuration.SelectedItem = vm.ContentCuration.Items.Single(x => x.Id == third.Id);
        await vm.DeleteSelectedLearningItemCommand.ExecuteAsync(null);
        Assert.Equal(third.Id, (await content.GetLearningItemAsync(third.Id)).Id);
        await vm.DeleteSelectedLearningItemCommand.ExecuteAsync(null);
        await Assert.ThrowsAsync<ContentNotFoundException>(() => content.GetLearningItemAsync(third.Id));
        Assert.Equal(first.Id, (await content.GetLearningItemAsync(first.Id)).Id);
    }

    [Fact]
    public async Task Permanent_item_delete_is_blocked_for_any_active_study_session()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var factory = new FixedDbContextFactory(connection);
        await using (var setup = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var time = new FixedTimeProvider(Now);
        var persistence = new EfCoreContentPersistence(factory);
        var content = new ContentManagementService(persistence, time);
        var study = new StudySessionService(new EfCoreStudySessionPersistence(factory), time, new IdentityOrdering());
        var first = await content.CreateLearningItemAsync(new CreateLearningItemCommand("First", "One"));
        var second = await content.CreateLearningItemAsync(new CreateLearningItemCommand("Second", "Two"));
        var deck = await content.CreateDeckAsync(new CreateDeckCommand("Deck"));
        await content.AddLearningItemToDeckAsync(deck.Id, first.Id);
        await content.AddLearningItemToDeckAsync(deck.Id, second.Id);

        var vm = new MainWindowViewModel(
            content,
            study,
            new ContentAcquisitionService(new FakeAcquisitionPersistence(), time),
            new ContentCurationService(persistence, persistence),
            new QualityReviewExchangeService(persistence, persistence),
            time);
        await vm.InitializeAsync();
        vm.SelectedStudyDeck = vm.Decks.Single(x => x.Id == deck.Id);
        await vm.StartSessionCommand.ExecuteAsync(null);
        var nonCurrent = vm.ContentCuration.Items.Single(x => x.Id != vm.CurrentStudyItem!.Id);
        vm.ContentCuration.SelectedItem = nonCurrent;

        await vm.DeleteSelectedLearningItemCommand.ExecuteAsync(null);
        await vm.DeleteSelectedLearningItemCommand.ExecuteAsync(null);

        Assert.Equal(nonCurrent.Id, (await content.GetLearningItemAsync(nonCurrent.Id)).Id);
        Assert.True(vm.SessionIsActive);
    }

    private static LearningItemSnapshot Snapshot(Guid id, string prompt, IReadOnlyList<Guid> deckIds) =>
        new(id, prompt, "Solution", LearningItemResponseMode.SelfAssessed, [], [], [], [], false,
            LearningItemLifecycle.Active, true, Now, 5, .5, false, deckIds);

    private static LearningItemView View(Guid id, string prompt, IReadOnlyList<Guid> deckIds) =>
        new(id, prompt, "Solution", [], LearningItemResponseMode.SelfAssessed, [], [], [], false,
            LearningItemLifecycle.Active, true, Now, deckIds);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class IdentityOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.ToArray();
    }

    private sealed class FakeAcquisitionPersistence : IContentAcquisitionPersistence
    {
        public Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>([]);
        public Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>([]);
        public Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedDbContextFactory(SqliteConnection connection) : IDbContextFactory<IlluminationDbContext>, IAsyncDisposable
    {
        public IlluminationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeContentPersistence : IContentPersistence, IUserFlagDefinitionPersistence
    {
        public Dictionary<Guid, LearningItemSnapshot> Items { get; } = [];
        public Dictionary<Guid, DeckSnapshot> Decks { get; } = [];
        public Dictionary<Guid, UserFlagDefinitionSnapshot> Flags { get; } = [];

        public Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LearningItemSnapshot>>(Items.Values.ToArray());
        public Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(id));
        public Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default)
        {
            Items[item.Id] = item;
            return Task.CompletedTask;
        }
        public Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.Remove(id);
            foreach (var deck in Decks.Values.ToArray())
                Decks[deck.Id] = deck with { LearningItemIds = deck.LearningItemIds.Where(x => x != id).ToArray() };
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeckSnapshot>>(Decks.Values.ToArray());
        public Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Decks.GetValueOrDefault(id));
        public Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default)
        {
            Decks[deck.Id] = deck;
            return Task.CompletedTask;
        }
        public Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Decks.Remove(id);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserFlagDefinitionSnapshot>>(Flags.Values.ToArray());
        public Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default)
        {
            Flags[definition.Id] = definition;
            return Task.CompletedTask;
        }
    }
}
