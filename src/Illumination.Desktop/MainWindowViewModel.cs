using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ContentManagementService _content;
    private readonly StudySessionService _study;
    private Guid? _activeSessionId;

    public MainWindowViewModel(ContentManagementService content, StudySessionService study)
    {
        _content = content;
        _study = study;
    }

    public string Title => "Illumination";
    public ObservableCollection<DeckView> Decks { get; } = [];
    public ObservableCollection<LearningItemView> LearningItems { get; } = [];
    public ObservableCollection<LearningItemView> SelectedDeckItems { get; } = [];
    public ObservableCollection<LearningItemView> AvailableDeckItems { get; } = [];

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateDeckCommand))]
    private string _newDeckName = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateLearningItemCommand))]
    private string _newPrompt = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateLearningItemCommand))]
    private string _newReferenceSolution = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddToDeckCommand)), NotifyCanExecuteChangedFor(nameof(RemoveFromDeckCommand))]
    private DeckView? _selectedDeck;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddToDeckCommand))]
    private LearningItemView? _selectedAvailableDeckItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RemoveFromDeckCommand))]
    private LearningItemView? _selectedDeckItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StartSessionCommand))]
    private DeckView? _selectedStudyDeck;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCurrentStudyItem)), NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand))]
    private StudySessionItemView? _currentStudyItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand))]
    private bool _isSolutionRevealed;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasActiveSession)), NotifyCanExecuteChangedFor(nameof(StartSessionCommand)), NotifyCanExecuteChangedFor(nameof(CompleteSessionCommand))]
    private bool _sessionIsActive;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    public bool HasCurrentStudyItem => CurrentStudyItem is not null;
    public bool HasActiveSession => SessionIsActive;

    public Task InitializeAsync() => RefreshContentAsync();

    partial void OnSelectedDeckChanged(DeckView? value) => RebuildDeckMembershipLists();

    [RelayCommand(CanExecute = nameof(CanCreateDeck))]
    private async Task CreateDeckAsync() => await RunAsync(async () =>
    {
        var created = await _content.CreateDeckAsync(new CreateDeckCommand(NewDeckName));
        NewDeckName = string.Empty;
        await RefreshContentAsync(created.Id);
        StatusMessage = $"Created Deck '{created.Name}'.";
    });

    private bool CanCreateDeck() => !string.IsNullOrWhiteSpace(NewDeckName);

    [RelayCommand(CanExecute = nameof(CanCreateLearningItem))]
    private async Task CreateLearningItemAsync() => await RunAsync(async () =>
    {
        var created = await _content.CreateLearningItemAsync(new CreateLearningItemCommand(
            NewPrompt, NewReferenceSolution, LearningItemResponseMode.SelfAssessed));
        NewPrompt = string.Empty;
        NewReferenceSolution = string.Empty;
        await RefreshContentAsync();
        StatusMessage = $"Created Learning Item '{created.Prompt}'.";
    });

    private bool CanCreateLearningItem() =>
        !string.IsNullOrWhiteSpace(NewPrompt) && !string.IsNullOrWhiteSpace(NewReferenceSolution);

    [RelayCommand(CanExecute = nameof(CanAddToDeck))]
    private async Task AddToDeckAsync()
    {
        if (SelectedDeck is null || SelectedAvailableDeckItem is null) return;
        var deckId = SelectedDeck.Id;
        var itemId = SelectedAvailableDeckItem.Id;
        await RunAsync(async () =>
        {
            await _content.AddLearningItemToDeckAsync(deckId, itemId);
            await RefreshContentAsync(deckId);
            StatusMessage = "Added the Learning Item to the Deck.";
        });
    }

    private bool CanAddToDeck() => SelectedDeck is not null && SelectedAvailableDeckItem is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromDeck))]
    private async Task RemoveFromDeckAsync()
    {
        if (SelectedDeck is null || SelectedDeckItem is null) return;
        var deckId = SelectedDeck.Id;
        var itemId = SelectedDeckItem.Id;
        await RunAsync(async () =>
        {
            await _content.RemoveLearningItemFromDeckAsync(deckId, itemId);
            await RefreshContentAsync(deckId);
            StatusMessage = "Removed the Learning Item from the Deck.";
        });
    }

    private bool CanRemoveFromDeck() => SelectedDeck is not null && SelectedDeckItem is not null;

    [RelayCommand(CanExecute = nameof(CanStartSession))]
    private async Task StartSessionAsync()
    {
        if (SelectedStudyDeck is null) return;
        await RunAsync(async () =>
        {
            var session = await _study.StartStudySessionAsync(new StartStudySessionCommand([SelectedStudyDeck.Id]));
            _activeSessionId = session.Id;
            SessionIsActive = true;
            IsSolutionRevealed = false;
            await LoadNextStudyItemAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Session started, but this Deck has no due or new Learning Items."
                : $"Session started with {session.Queue.Count} queued item(s).";
        });
    }

    private bool CanStartSession() => SelectedStudyDeck is not null && !SessionIsActive;

    [RelayCommand(CanExecute = nameof(CanRevealSolution))]
    private void RevealSolution() => IsSolutionRevealed = true;

    private bool CanRevealSolution() => HasCurrentStudyItem && !IsSolutionRevealed;

    [RelayCommand] private Task GradeNochmalAsync() => SubmitGradeAsync(StudyLearningAssessment.Nochmal);
    [RelayCommand] private Task GradeSchwerAsync() => SubmitGradeAsync(StudyLearningAssessment.Schwer);
    [RelayCommand] private Task GradeUnsicherAsync() => SubmitGradeAsync(StudyLearningAssessment.Unsicher);
    [RelayCommand] private Task GradeGutAsync() => SubmitGradeAsync(StudyLearningAssessment.Gut);
    [RelayCommand] private Task GradeLeichtAsync() => SubmitGradeAsync(StudyLearningAssessment.Leicht);

    [RelayCommand(CanExecute = nameof(CanCompleteSession))]
    private async Task CompleteSessionAsync()
    {
        if (_activeSessionId is null) return;
        await RunAsync(async () =>
        {
            await _study.CompleteStudySessionAsync(_activeSessionId.Value);
            ClearStudyState();
            await RefreshContentAsync();
            StatusMessage = "Study Session completed.";
        });
    }

    private bool CanCompleteSession() => SessionIsActive;

    private async Task SubmitGradeAsync(StudyLearningAssessment assessment)
    {
        if (_activeSessionId is null || CurrentStudyItem is null) return;
        var itemId = CurrentStudyItem.Id;
        await RunAsync(async () =>
        {
            var result = await _study.SubmitReviewAsync(new SubmitStudyReviewCommand(_activeSessionId.Value, itemId, assessment));
            IsSolutionRevealed = false;
            await LoadNextStudyItemAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Grade saved. The queue is empty; complete the session when ready."
                : $"Grade saved. {result.Session.Queue.Count} queued item(s) remain.";
        });
    }

    private async Task LoadNextStudyItemAsync() => CurrentStudyItem = _activeSessionId is null
        ? null
        : await _study.GetNextStudySessionItemAsync(_activeSessionId.Value);

    private async Task RefreshContentAsync(Guid? preferredDeckId = null)
    {
        var selectedDeckId = preferredDeckId ?? SelectedDeck?.Id;
        var selectedStudyDeckId = SelectedStudyDeck?.Id;
        var decks = await _content.ListDecksAsync();
        var items = await _content.ListLearningItemsAsync();
        Replace(Decks, decks);
        Replace(LearningItems, items);
        SelectedDeck = Decks.FirstOrDefault(x => x.Id == selectedDeckId) ?? Decks.FirstOrDefault();
        SelectedStudyDeck = Decks.FirstOrDefault(x => x.Id == selectedStudyDeckId) ?? Decks.FirstOrDefault();
        RebuildDeckMembershipLists();
    }

    private void RebuildDeckMembershipLists()
    {
        var memberIds = SelectedDeck?.LearningItemIds.ToHashSet() ?? [];
        Replace(SelectedDeckItems, LearningItems.Where(x => memberIds.Contains(x.Id)));
        Replace(AvailableDeckItems, LearningItems.Where(x => !memberIds.Contains(x.Id)));
        SelectedDeckItem = null;
        SelectedAvailableDeckItem = null;
    }

    private void ClearStudyState()
    {
        _activeSessionId = null;
        SessionIsActive = false;
        CurrentStudyItem = null;
        IsSolutionRevealed = false;
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { StatusMessage = exception.Message; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}
