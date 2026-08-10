using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ContentManagementService _content;
    private readonly StudySessionService _study;
    private readonly TimeProvider _timeProvider;
    private Guid? _activeSessionId;

    public MainWindowViewModel(
        ContentManagementService content,
        StudySessionService study,
        ContentAcquisitionService acquisition,
        TimeProvider timeProvider)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _study = study ?? throw new ArgumentNullException(nameof(study));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ContentAcquisition = new ContentAcquisitionViewModel(
            acquisition,
            () => RefreshContentAsync(),
            message => StatusMessage = message);
    }

    public string Title => "Illumination";
    public ObservableCollection<DeckView> Decks { get; } = [];
    public ObservableCollection<LearningItemView> LearningItems { get; } = [];
    public ObservableCollection<LearningItemView> SelectedDeckItems { get; } = [];
    public ObservableCollection<LearningItemView> AvailableDeckItems { get; } = [];
    public ObservableCollection<StudyAssessmentPreviewDisplay> AssessmentPreviews { get; } = [];
    public ObservableCollection<StudyQueueEntryDisplay> UpcomingStudyItems { get; } = [];
    public ContentAcquisitionViewModel ContentAcquisition { get; }

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

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCurrentStudyItem)), NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand)), NotifyCanExecuteChangedFor(nameof(GradeNochmalCommand)), NotifyCanExecuteChangedFor(nameof(GradeSchwerCommand)), NotifyCanExecuteChangedFor(nameof(GradeUnsicherCommand)), NotifyCanExecuteChangedFor(nameof(GradeGutCommand)), NotifyCanExecuteChangedFor(nameof(GradeLeichtCommand))]
    private StudySessionItemView? _currentStudyItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand))]
    private bool _isSolutionRevealed;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasActiveSession)), NotifyCanExecuteChangedFor(nameof(StartSessionCommand)), NotifyCanExecuteChangedFor(nameof(CompleteSessionCommand)), NotifyCanExecuteChangedFor(nameof(GradeNochmalCommand)), NotifyCanExecuteChangedFor(nameof(GradeSchwerCommand)), NotifyCanExecuteChangedFor(nameof(GradeUnsicherCommand)), NotifyCanExecuteChangedFor(nameof(GradeGutCommand)), NotifyCanExecuteChangedFor(nameof(GradeLeichtCommand))]
    private bool _sessionIsActive;

    [ObservableProperty]
    private int _remainingQueueEntryCount;

    [ObservableProperty]
    private bool _currentItemRequiresReinforcement;

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
            await RefreshStudyTransparencyAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Session started, but this Deck has no due or new Learning Items."
                : $"Session started with {session.Queue.Count} queue entries.";
        });
    }

    private bool CanStartSession() => SelectedStudyDeck is not null && !SessionIsActive;

    [RelayCommand(CanExecute = nameof(CanRevealSolution))]
    private void RevealSolution() => IsSolutionRevealed = true;

    private bool CanRevealSolution() => HasCurrentStudyItem && !IsSolutionRevealed;

    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeNochmalAsync() => SubmitGradeAsync(StudyLearningAssessment.Nochmal);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeSchwerAsync() => SubmitGradeAsync(StudyLearningAssessment.Schwer);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeUnsicherAsync() => SubmitGradeAsync(StudyLearningAssessment.Unsicher);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeGutAsync() => SubmitGradeAsync(StudyLearningAssessment.Gut);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeLeichtAsync() => SubmitGradeAsync(StudyLearningAssessment.Leicht);

    private bool CanGrade() => SessionIsActive && CurrentStudyItem is not null;

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
            await _study.SubmitReviewAsync(new SubmitStudyReviewCommand(_activeSessionId.Value, itemId, assessment));
            IsSolutionRevealed = false;
            await RefreshStudyTransparencyAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Grade saved. The queue is empty; complete the session when ready."
                : $"Grade saved. {RemainingQueueEntryCount} entries remain after the current card.";
        });
    }

    private async Task RefreshStudyTransparencyAsync()
    {
        if (_activeSessionId is null)
        {
            ClearStudyPresentation();
            return;
        }

        var transparency = await _study.GetStudySessionTransparencyAsync(_activeSessionId.Value, maxUpcomingEntries: 5);
        CurrentStudyItem = await _study.GetNextStudySessionItemAsync(_activeSessionId.Value);
        RemainingQueueEntryCount = transparency.RemainingQueueEntryCount;
        CurrentItemRequiresReinforcement = transparency.CurrentItem?.ReinforcementRequired == true;
        Replace(UpcomingStudyItems, transparency.UpcomingItems.Select(item => new StudyQueueEntryDisplay(
            item.Prompt,
            item.ReinforcementRequired,
            item.ReinforcementRequired ? "Reinforcement" : "Upcoming")));

        var now = _timeProvider.GetUtcNow();
        Replace(AssessmentPreviews, transparency.AssessmentPreviews.Select(preview => new StudyAssessmentPreviewDisplay(
            preview.Assessment,
            preview.Assessment.ToString(),
            StudyPresentationFormatter.FormatPreview(preview, now),
            CommandFor(preview.Assessment))));
    }

    private IAsyncRelayCommand CommandFor(StudyLearningAssessment assessment) => assessment switch
    {
        StudyLearningAssessment.Nochmal => GradeNochmalCommand,
        StudyLearningAssessment.Schwer => GradeSchwerCommand,
        StudyLearningAssessment.Unsicher => GradeUnsicherCommand,
        StudyLearningAssessment.Gut => GradeGutCommand,
        StudyLearningAssessment.Leicht => GradeLeichtCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported assessment."),
    };

    private async Task RefreshContentAsync(Guid? preferredDeckId = null)
    {
        var selectedDeckId = preferredDeckId ?? SelectedDeck?.Id;
        var selectedStudyDeckId = SelectedStudyDeck?.Id;
        var decks = await _content.ListDecksAsync();
        var items = await _content.ListLearningItemsAsync();
        Replace(Decks, decks);
        Replace(LearningItems, items);
        ContentAcquisition.UpdateDecks(decks);
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
        IsSolutionRevealed = false;
        ClearStudyPresentation();
    }

    private void ClearStudyPresentation()
    {
        CurrentStudyItem = null;
        RemainingQueueEntryCount = 0;
        CurrentItemRequiresReinforcement = false;
        AssessmentPreviews.Clear();
        UpcomingStudyItems.Clear();
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
