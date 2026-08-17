using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.Insights;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    private bool _studyDeckSelectionInitialized;

    public ObservableCollection<DeckPresentationItem> SelectedStudyDecks { get; } = [];
    public ObservableCollection<UnfinishedStudySessionDisplay> UnfinishedStudySessions { get; } = [];

    [ObservableProperty]
    private string _studyNewItemLimitText = "20";

    [ObservableProperty]
    private bool _studyAllNew;

    public string SelectedStudyDeckSummary => SelectedStudyDecks.Count switch
    {
        0 => "No Decks selected",
        1 => SelectedStudyDecks[0].DisplayName,
        _ => $"{SelectedStudyDecks.Count} Decks selected",
    };

    public bool HasUnfinishedStudySessions => UnfinishedStudySessions.Count > 0;
    public string UnfinishedStudySessionsHeader => $"Unfinished sessions ({UnfinishedStudySessions.Count})";

    [RelayCommand]
    private void AddStudyDeck()
    {
        if (SelectedStudyDeckPresentation is null || SelectedStudyDecks.Any(x => x.Id == SelectedStudyDeckPresentation.Id)) return;
        SelectedStudyDecks.Add(SelectedStudyDeckPresentation);
        StudyDeckSelectionChanged();
    }

    [RelayCommand]
    private void RemoveStudyDeck(DeckPresentationItem deck)
    {
        var existing = SelectedStudyDecks.FirstOrDefault(x => x.Id == deck.Id);
        if (existing is null) return;
        SelectedStudyDecks.Remove(existing);
        StudyDeckSelectionChanged();
    }

    [RelayCommand]
    private void AddAllStudyDecks()
    {
        foreach (var deck in DeckPresentationItems)
        {
            if (SelectedStudyDecks.All(x => x.Id != deck.Id)) SelectedStudyDecks.Add(deck);
        }
        StudyDeckSelectionChanged();
    }

    [RelayCommand]
    private void ClearStudyDecks()
    {
        SelectedStudyDecks.Clear();
        _studyDeckSelectionInitialized = true;
        StudyDeckSelectionChanged();
    }

    [RelayCommand]
    private async Task StartConfiguredSessionAsync()
    {
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active Study Session before starting another one.";
            return;
        }

        NormalizeSelectedStudyDecks();
        if (SelectedStudyDecks.Count == 0 && SelectedStudyDeckPresentation is not null)
        {
            SelectedStudyDecks.Add(SelectedStudyDeckPresentation);
            StudyDeckSelectionChanged();
        }

        var deckIds = SelectedStudyDecks.Select(x => x.Id).Distinct().ToArray();
        if (deckIds.Length == 0)
        {
            StatusMessage = "Select at least one Deck for the Study Session.";
            return;
        }

        int? newItemLimit = null;
        if (!StudyAllNew)
        {
            if (!int.TryParse(StudyNewItemLimitText, out var parsed) || parsed <= 0)
            {
                StatusMessage = "New item limit must be a positive whole number, or choose All new.";
                return;
            }
            newItemLimit = parsed;
        }

        await RunAsync(async () =>
        {
            var session = await _study.StartStudySessionAsync(new StartStudySessionCommand(
                deckIds,
                NewItemLimit: newItemLimit,
                AllNew: StudyAllNew,
                EvaluationMode: SelectedEvaluationModeOption.Mode,
                ConsiderAssistance: ConsiderAssistance,
                LowInteractionOnly: LowInteractionOnly));

            _activeSessionId = session.Id;
            SessionIsActive = true;
            ActiveEvaluationMode = session.EvaluationMode;
            IsSolutionRevealed = false;
            await RefreshStudyTransparencyAsync();
            await RefreshUnfinishedStudySessionsAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Session started, but the selected Decks have no due or new Learning Items."
                : $"Session started from {deckIds.Length} Deck{(deckIds.Length == 1 ? string.Empty : "s")} with {session.Queue.Count} queue entries.";
        });
    }

    [RelayCommand]
    private async Task CompleteConfiguredSessionAsync()
    {
        if (_activeSessionId is null || !SessionIsActive) return;

        await RunAsync(async () =>
        {
            await _study.CompleteStudySessionAsync(_activeSessionId.Value);
            ClearStudyState();
            await RefreshContentAsync();
            await RefreshUnfinishedStudySessionsAsync();
            StatusMessage = "Study Session completed.";
        });
    }

    [RelayCommand]
    private async Task ResumeStudySessionAsync(UnfinishedStudySessionDisplay entry)
    {
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active Study Session before resuming another one.";
            return;
        }

        await RunAsync(async () =>
        {
            _activeSessionId = entry.SessionId;
            SessionIsActive = true;
            ActiveEvaluationMode = entry.EvaluationMode;
            ConsiderAssistance = entry.ConsiderAssistance;
            LowInteractionOnly = entry.LowInteractionOnly;
            SelectedEvaluationModeOption = EvaluationModeOptions.FirstOrDefault(x => x.Mode == entry.EvaluationMode)
                ?? new StudyEvaluationModeOption(entry.EvaluationMode.ToString(), entry.EvaluationMode);
            SetSelectedStudyDeckIds(entry.SelectedDeckIds);
            await RefreshStudyTransparencyAsync();
            await RefreshUnfinishedStudySessionsAsync();
            StatusMessage = CurrentStudyItem is null
                ? "Unfinished Study Session resumed; its queue is empty and can be completed."
                : $"Resumed Study Session from {entry.StartedLabel}.";
        });
    }

    [RelayCommand]
    private async Task FinishStoredStudySessionAsync(UnfinishedStudySessionDisplay entry)
    {
        if (SessionIsActive && _activeSessionId == entry.SessionId)
        {
            await CompleteConfiguredSessionAsync();
            return;
        }

        await RunAsync(async () =>
        {
            await _study.CompleteStudySessionAsync(entry.SessionId);
            await RefreshUnfinishedStudySessionsAsync();
            StatusMessage = "Unfinished Study Session marked complete.";
        });
    }

    public async Task RefreshStudyContinuityAsync()
    {
        NormalizeSelectedStudyDecks();
        if (!_studyDeckSelectionInitialized && SelectedStudyDecks.Count == 0 && SelectedStudyDeckPresentation is not null)
        {
            SelectedStudyDecks.Add(SelectedStudyDeckPresentation);
            _studyDeckSelectionInitialized = true;
            StudyDeckSelectionChanged();
        }
        await RefreshUnfinishedStudySessionsAsync();
    }

    private async Task RefreshUnfinishedStudySessionsAsync()
    {
        if (_insightService is null)
        {
            UnfinishedStudySessions.Clear();
            StudySessionListChanged();
            return;
        }

        var history = await _insightService.GetStudySessionHistoryAsync(limit: 200);
        var unfinished = history
            .Where(x => x.CompletedAt is null && x.SessionId != _activeSessionId)
            .OrderByDescending(x => x.StartedAt)
            .Select(ToUnfinishedDisplay)
            .ToArray();
        Replace(UnfinishedStudySessions, unfinished);
        StudySessionListChanged();
    }

    private void NormalizeSelectedStudyDecks()
    {
        var ids = SelectedStudyDecks.Select(x => x.Id).ToHashSet();
        var current = DeckPresentationItems.Where(x => ids.Contains(x.Id)).ToArray();
        if (current.Length == SelectedStudyDecks.Count && current.Select(x => x.Id).SequenceEqual(SelectedStudyDecks.Select(x => x.Id))) return;
        Replace(SelectedStudyDecks, current);
        StudyDeckSelectionChanged();
    }

    private void SetSelectedStudyDeckIds(IEnumerable<Guid> deckIds)
    {
        var ids = deckIds.ToHashSet();
        Replace(SelectedStudyDecks, DeckPresentationItems.Where(x => ids.Contains(x.Id)));
        _studyDeckSelectionInitialized = true;
        StudyDeckSelectionChanged();
    }

    private void StudyDeckSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedStudyDeckSummary));
    }

    private void StudySessionListChanged()
    {
        OnPropertyChanged(nameof(HasUnfinishedStudySessions));
        OnPropertyChanged(nameof(UnfinishedStudySessionsHeader));
    }

    private static UnfinishedStudySessionDisplay ToUnfinishedDisplay(StudySessionHistoryEntry entry)
    {
        var decks = entry.SelectedDecks.Count == 0
            ? "No current Deck identity"
            : string.Join(", ", entry.SelectedDecks.Select(x => x.Name));
        return new UnfinishedStudySessionDisplay(
            entry.SessionId,
            entry.SelectedDecks.Select(x => x.Id).ToArray(),
            decks,
            entry.StartedAt.ToLocalTime().ToString("g"),
            entry.EvaluationMode,
            entry.ConsiderAssistance,
            entry.LowInteractionOnly,
            entry.ReviewCount);
    }
}

public sealed record UnfinishedStudySessionDisplay(
    Guid SessionId,
    IReadOnlyList<Guid> SelectedDeckIds,
    string DecksLabel,
    string StartedLabel,
    StudyEvaluationMode EvaluationMode,
    bool ConsiderAssistance,
    bool LowInteractionOnly,
    int ReviewCount);
