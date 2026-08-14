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
        ContentCurationService curation,
        QualityReviewExchangeService qualityExchange,
        TimeProvider timeProvider)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _study = study ?? throw new ArgumentNullException(nameof(study));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ContentAcquisition = new ContentAcquisitionViewModel(
            acquisition,
            () => RefreshContentAsync(),
            message => StatusMessage = message);
        ContentCuration = new ContentCurationViewModel(curation, qualityExchange, message => StatusMessage = message);
    }

    public string Title => "Illumination";
    public ObservableCollection<DeckView> Decks { get; } = [];
    public ObservableCollection<DeckPresentationItem> DeckPresentationItems { get; } = [];
    public ObservableCollection<LearningItemView> LearningItems { get; } = [];
    public ObservableCollection<LearningItemView> SelectedDeckItems { get; } = [];
    public ObservableCollection<LearningItemView> AvailableDeckItems { get; } = [];
    public ObservableCollection<StudyAssessmentPreviewDisplay> AssessmentPreviews { get; } = [];
    public ObservableCollection<StudyQueueEntryDisplay> UpcomingStudyItems { get; } = [];
    public ObservableCollection<string> RevealedHints { get; } = [];
    public ObservableCollection<StudyChoiceDisplay> CurrentDirectChoices { get; } = [];
    public ObservableCollection<StudyChoiceDisplay> CurrentAssistanceChoices { get; } = [];
    public IReadOnlyList<StudyEvaluationModeOption> EvaluationModeOptions { get; } =
    [new("Use global default", null), new("Manual", StudyEvaluationMode.Manual), new("Assisted", StudyEvaluationMode.Assisted)];
    public IReadOnlyList<StudyEvaluationMode> EvaluationModes { get; } = [StudyEvaluationMode.Manual, StudyEvaluationMode.Assisted];
    public ContentAcquisitionViewModel ContentAcquisition { get; }
    public ContentCurationViewModel ContentCuration { get; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateDeckCommand))]
    private string _newDeckName = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateLearningItemCommand))]
    private string _newPrompt = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateLearningItemCommand))]
    private string _newReferenceSolution = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddToDeckCommand)), NotifyCanExecuteChangedFor(nameof(RemoveFromDeckCommand))]
    private DeckView? _selectedDeck;

    [ObservableProperty]
    private DeckPresentationItem? _selectedDeckPresentation;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddToDeckCommand))]
    private LearningItemView? _selectedAvailableDeckItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RemoveFromDeckCommand))]
    private LearningItemView? _selectedDeckItem;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StartSessionCommand))]
    private DeckView? _selectedStudyDeck;

    [ObservableProperty]
    private DeckPresentationItem? _selectedStudyDeckPresentation;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCurrentStudyItem)), NotifyPropertyChangedFor(nameof(IsSelectionMode)), NotifyPropertyChangedFor(nameof(IsShortTextMode)), NotifyPropertyChangedFor(nameof(IsCodeMode)), NotifyPropertyChangedFor(nameof(IsSelfAssessedMode)), NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand)), NotifyCanExecuteChangedFor(nameof(SubmitResponseCommand)), NotifyCanExecuteChangedFor(nameof(GradeNochmalCommand)), NotifyCanExecuteChangedFor(nameof(GradeSchwerCommand)), NotifyCanExecuteChangedFor(nameof(GradeUnsicherCommand)), NotifyCanExecuteChangedFor(nameof(GradeGutCommand)), NotifyCanExecuteChangedFor(nameof(GradeLeichtCommand))]
    private StudySessionItemView? _currentStudyItem;

    [ObservableProperty] private StudyEvaluationModeOption _selectedEvaluationModeOption = new("Use global default", null);
    [ObservableProperty] private StudyEvaluationMode _globalEvaluationMode = StudyEvaluationMode.Manual;
    [ObservableProperty] private bool _considerAssistance;
    [ObservableProperty] private bool _lowInteractionOnly;
    [ObservableProperty] private StudyEvaluationMode _activeEvaluationMode = StudyEvaluationMode.Manual;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RevealAssistanceCommand))] private bool _assistanceAnswerChoicesRevealed;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RevealSolutionCommand))] private bool _isReferenceSolutionRevealed;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SubmitResponseCommand))] private string _shortTextResponse = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SubmitResponseCommand))] private string _codeResponse = string.Empty;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SubmitResponseCommand)), NotifyCanExecuteChangedFor(nameof(GradeNochmalCommand)), NotifyCanExecuteChangedFor(nameof(GradeSchwerCommand)), NotifyCanExecuteChangedFor(nameof(GradeUnsicherCommand)), NotifyCanExecuteChangedFor(nameof(GradeGutCommand)), NotifyCanExecuteChangedFor(nameof(GradeLeichtCommand))] private bool _responseSubmitted;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAutomaticResult))] private bool? _automaticCorrectness;
    [ObservableProperty] private StudyLearningAssessment? _suggestedAssessment;

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
    public bool IsSelfAssessedMode => CurrentStudyItem?.ResponseMode == LearningItemResponseMode.SelfAssessed;
    public bool IsSelectionMode => CurrentStudyItem?.ResponseMode == LearningItemResponseMode.Selection;
    public bool IsShortTextMode => CurrentStudyItem?.ResponseMode == LearningItemResponseMode.ShortText;
    public bool IsCodeMode => CurrentStudyItem?.ResponseMode == LearningItemResponseMode.Code;
    public bool HasAutomaticResult => AutomaticCorrectness.HasValue;

    public async Task InitializeAsync()
    {
        GlobalEvaluationMode = await _study.GetDefaultEvaluationModeAsync();
        await RefreshContentAsync();
    }

    partial void OnSelectedDeckChanged(DeckView? value)
    {
        SelectedDeckPresentation = DeckPresentationItems.FirstOrDefault(x => x.Id == value?.Id);
        RebuildDeckMembershipLists();
    }

    partial void OnSelectedDeckPresentationChanged(DeckPresentationItem? value)
    {
        if (value is not null && SelectedDeck?.Id != value.Id) SelectedDeck = value.Deck;
    }

    partial void OnSelectedStudyDeckPresentationChanged(DeckPresentationItem? value)
    {
        if (value is not null && SelectedStudyDeck?.Id != value.Id) SelectedStudyDeck = value.Deck;
    }

    partial void OnCurrentStudyItemChanged(StudySessionItemView? value)
    {
        ContentCuration.SetStudyItem(value?.Id);
        ResetInteractionState(value);
    }

    partial void OnGlobalEvaluationModeChanged(StudyEvaluationMode value) => _ = PersistGlobalEvaluationModeAsync(value);

    private async Task PersistGlobalEvaluationModeAsync(StudyEvaluationMode mode)
    {
        try { await _study.SetDefaultEvaluationModeAsync(mode); StatusMessage = $"Global evaluation default: {mode}."; }
        catch (Exception exception) { StatusMessage = exception.Message; }
    }

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
            var session = await _study.StartStudySessionAsync(new StartStudySessionCommand(
                [SelectedStudyDeck.Id],
                EvaluationMode: SelectedEvaluationModeOption.Mode,
                ConsiderAssistance: ConsiderAssistance,
                LowInteractionOnly: LowInteractionOnly));
            _activeSessionId = session.Id;
            SessionIsActive = true;
            ActiveEvaluationMode = session.EvaluationMode;
            IsSolutionRevealed = false;
            await RefreshStudyTransparencyAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Session started, but this Deck has no due or new Learning Items."
                : $"Session started with {session.Queue.Count} queue entries.";
        });
    }

    private bool CanStartSession() => SelectedStudyDeck is not null && !SessionIsActive;

    [RelayCommand(CanExecute = nameof(CanRevealSolution))]
    private async Task RevealSolutionAsync()
    {
        if (_activeSessionId is null) return;
        await RunAsync(async () =>
        {
            await _study.RevealReferenceSolutionAsync(_activeSessionId.Value);
            IsSolutionRevealed = true;
            IsReferenceSolutionRevealed = true;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRevealHint))]
    private async Task RevealHintAsync() => await RunInteractionAsync(() => _study.RevealNextHintAsync(_activeSessionId!.Value));

    [RelayCommand(CanExecute = nameof(CanRevealAssistance))]
    private async Task RevealAssistanceAsync() => await RunInteractionAsync(() => _study.RevealAssistanceAnswerChoicesAsync(_activeSessionId!.Value));

    [RelayCommand(CanExecute = nameof(CanSubmitResponse))]
    private async Task SubmitResponseAsync()
    {
        if (_activeSessionId is null || CurrentStudyItem is null) return;
        await RunAsync(async () =>
        {
            var selected = CurrentDirectChoices.Where(x => x.IsSelected).Select(x => x.Id).ToArray();
            var result = await _study.SubmitResponseAsync(new SubmitStudyResponseCommand(
                _activeSessionId.Value, CurrentStudyItem.Id,
                IsSelectionMode ? selected : null,
                IsShortTextMode ? ShortTextResponse : null,
                IsCodeMode ? CodeResponse : null));
            ResponseSubmitted = true;
            AutomaticCorrectness = result.AutomaticCorrectness;
            SuggestedAssessment = result.SuggestedAssessment;
            StatusMessage = result.AutomaticCorrectness is { } correct
                ? $"Response evaluated: {(correct ? "correct" : "incorrect")}. Choose the final grade."
                : "Response recorded. Choose the final grade.";
        });
    }

    [RelayCommand]
    private Task ToggleStudyFlagAsync(Guid flagId) => ContentCuration.ToggleFlagCommand.ExecuteAsync(flagId);

    private bool CanRevealSolution() => HasCurrentStudyItem && !IsSolutionRevealed;
    private bool CanRevealHint() => SessionIsActive && HasCurrentStudyItem && CurrentStudyItem!.Hints is { Count: > 0 } && RevealedHints.Count < CurrentStudyItem.Hints.Count;
    private bool CanRevealAssistance() => SessionIsActive && HasCurrentStudyItem && !AssistanceAnswerChoicesRevealed && CurrentStudyItem!.AssistanceAnswerChoices is { Count: > 0 };
    private bool CanSubmitResponse() => SessionIsActive && HasCurrentStudyItem && !IsSelfAssessedMode && !ResponseSubmitted;

    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeNochmalAsync() => SubmitGradeAsync(StudyLearningAssessment.Nochmal);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeSchwerAsync() => SubmitGradeAsync(StudyLearningAssessment.Schwer);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeUnsicherAsync() => SubmitGradeAsync(StudyLearningAssessment.Unsicher);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeGutAsync() => SubmitGradeAsync(StudyLearningAssessment.Gut);
    [RelayCommand(CanExecute = nameof(CanGrade))] private Task GradeLeichtAsync() => SubmitGradeAsync(StudyLearningAssessment.Leicht);

    private bool CanGrade() => SessionIsActive && CurrentStudyItem is not null && (IsSelfAssessedMode || ResponseSubmitted);

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
        Replace(DeckPresentationItems, DeckPresentationLabeler.Label(decks));
        Replace(LearningItems, items);
        ContentAcquisition.UpdateDecks(decks);
        await ContentCuration.RefreshAsync(items);
        SelectedDeck = Decks.FirstOrDefault(x => x.Id == selectedDeckId) ?? Decks.FirstOrDefault();
        SelectedStudyDeck = Decks.FirstOrDefault(x => x.Id == selectedStudyDeckId) ?? Decks.FirstOrDefault();
        SelectedDeckPresentation = DeckPresentationItems.FirstOrDefault(x => x.Id == SelectedDeck?.Id) ?? DeckPresentationItems.FirstOrDefault();
        SelectedStudyDeckPresentation = DeckPresentationItems.FirstOrDefault(x => x.Id == SelectedStudyDeck?.Id) ?? DeckPresentationItems.FirstOrDefault();
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
        ResetInteractionState(null);
        RemainingQueueEntryCount = 0;
        CurrentItemRequiresReinforcement = false;
        AssessmentPreviews.Clear();
        UpcomingStudyItems.Clear();
    }

    private void ResetInteractionState(StudySessionItemView? item)
    {
        RevealedHints.Clear();
        CurrentDirectChoices.Clear();
        CurrentAssistanceChoices.Clear();
        if (item is not null)
        {
            foreach (var choice in item.DirectAnswerChoices ?? []) CurrentDirectChoices.Add(new StudyChoiceDisplay(choice.Id, choice.Text));
        }
        AssistanceAnswerChoicesRevealed = false;
        IsSolutionRevealed = false;
        IsReferenceSolutionRevealed = false;
        ShortTextResponse = string.Empty;
        CodeResponse = string.Empty;
        ResponseSubmitted = false;
        AutomaticCorrectness = null;
        SuggestedAssessment = null;
        RevealHintCommand.NotifyCanExecuteChanged();
        RevealAssistanceCommand.NotifyCanExecuteChanged();
    }

    private async Task RunInteractionAsync(Func<Task<StudyInteractionStateView>> operation)
    {
        await RunAsync(async () =>
        {
            var state = await operation();
            RevealedHints.Clear();
            foreach (var hint in state.RevealedHintTexts) RevealedHints.Add(hint);
            AssistanceAnswerChoicesRevealed = state.AssistanceAnswerChoicesRevealed;
            CurrentAssistanceChoices.Clear();
            foreach (var choice in state.RevealedAssistanceAnswerChoices ?? []) CurrentAssistanceChoices.Add(new StudyChoiceDisplay(choice.Id, choice.Text));
        });
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
