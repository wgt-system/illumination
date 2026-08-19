using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed partial class LearningInsightsViewModel : ObservableObject
{
    private readonly LearningInsightService? _service;
    private readonly ContentManagementService? _content;
    private readonly Func<Task>? _refreshAll;
    private readonly Func<DeckInsight, Task> _generateFollowUp;

    public LearningInsightsViewModel(
        LearningInsightService? service,
        Func<DeckInsight, Task> generateFollowUp,
        ContentManagementService? content = null,
        Func<Task>? refreshAll = null)
    {
        _service = service;
        _generateFollowUp = generateFollowUp;
        _content = content;
        _refreshAll = refreshAll;
    }

    public ObservableCollection<DeckInsight> Decks { get; } = [];
    public ObservableCollection<LearningItemInsight> Items { get; } = [];
    public ObservableCollection<ReviewHistoryEntry> Reviews { get; } = [];
    public ObservableCollection<StudySessionHistoryEntry> Sessions { get; } = [];
    public IReadOnlyList<LearningItemLifecycle?> LifecycleOptions { get; } =
        [null, LearningItemLifecycle.Active, LearningItemLifecycle.Suspended, LearningItemLifecycle.Mastered];
    public IReadOnlyList<FollowUpProgressionMode> ProgressionModes { get; } = Enum.GetValues<FollowUpProgressionMode>();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasLearningData))]
    private LearningInsightOverview? _overview;

    [ObservableProperty]
    private LearningActivitySummary? _activity;

    [ObservableProperty]
    private LearningDueForecast? _dueForecast;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelectedDeck)), NotifyCanExecuteChangedFor(nameof(GenerateFollowUpCommand))]
    private DeckInsight? _selectedDeck;

    [ObservableProperty]
    private string _promptSearch = string.Empty;

    [ObservableProperty]
    private LearningItemLifecycle? _lifecycle;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SuspendCommand)), NotifyCanExecuteChangedFor(nameof(MarkMasteredCommand)), NotifyCanExecuteChangedFor(nameof(ReactivateCommand)), NotifyCanExecuteChangedFor(nameof(UnmarkMasteredCommand)), NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    private LearningItemInsight? _selectedItem;

    [ObservableProperty]
    private bool _newOnly;

    [ObservableProperty]
    private bool _dueNowOnly;

    [ObservableProperty]
    private bool _relearningOnly;

    [ObservableProperty]
    private FollowUpProgressionMode _progressionMode = FollowUpProgressionMode.Continue;

    [ObservableProperty]
    private bool _selfAssessedEnabled;

    [ObservableProperty]
    private bool _selectionEnabled;

    [ObservableProperty]
    private bool _shortTextEnabled;

    [ObservableProperty]
    private bool _codeEnabled;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    public bool HasLearningData => Overview?.TotalLearningItems > 0;
    public bool HasDecks => Decks.Count > 0;
    public bool HasSelectedDeck => SelectedDeck is not null;
    public bool HasItems => Items.Count > 0;
    public bool HasSelectedItem => SelectedItem is not null;
    public bool HasReviews => Reviews.Count > 0;
    public bool HasSessions => Sessions.Count > 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    partial void OnSelectedDeckChanged(DeckInsight? value) => _ = RefreshItemsAsync();
    partial void OnPromptSearchChanged(string value) => _ = RefreshItemsAsync();
    partial void OnLifecycleChanged(LearningItemLifecycle? value) => _ = RefreshItemsAsync();
    partial void OnNewOnlyChanged(bool value) => _ = RefreshItemsAsync();
    partial void OnDueNowOnlyChanged(bool value) => _ = RefreshItemsAsync();
    partial void OnRelearningOnlyChanged(bool value) => _ = RefreshItemsAsync();

    public async Task RefreshAsync()
    {
        if (_service is null) return;
        try
        {
            Overview = await _service.GetOverviewAsync();
            Activity = await _service.GetLearningActivityAsync(30);
            DueForecast = await _service.GetDueForecastAsync(14);

            var decks = await _service.GetDeckInsightsAsync();
            var selectedId = SelectedDeck?.Id;
            Replace(Decks, decks);
            OnPropertyChanged(nameof(HasDecks));
            SelectedDeck = Decks.FirstOrDefault(x => x.Id == selectedId) ?? Decks.FirstOrDefault();

            await RefreshItemsAsync();

            Replace(Reviews, await _service.GetReviewHistoryAsync(limit: 12));
            Replace(Sessions, await _service.GetStudySessionHistoryAsync(limit: 8));
            OnPropertyChanged(nameof(HasReviews));
            OnPropertyChanged(nameof(HasSessions));
            Status = string.Empty;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerateFollowUp))]
    private async Task GenerateFollowUpAsync()
    {
        if (SelectedDeck is not null) await _generateFollowUp(SelectedDeck);
    }

    private bool CanGenerateFollowUp() => SelectedDeck is not null;

    public IReadOnlyList<LearningItemResponseMode> SelectedResponseModes => new[]
    {
        (SelfAssessedEnabled, LearningItemResponseMode.SelfAssessed),
        (SelectionEnabled, LearningItemResponseMode.Selection),
        (ShortTextEnabled, LearningItemResponseMode.ShortText),
        (CodeEnabled, LearningItemResponseMode.Code),
    }.Where(x => x.Item1).Select(x => x.Item2).ToArray();

    public bool CanSuspendAction => SelectedItem?.LifecycleState == LearningItemLifecycle.Active;
    public bool CanMarkMasteredAction => SelectedItem?.LifecycleState == LearningItemLifecycle.Active;
    public bool CanReactivateAction => SelectedItem?.LifecycleState == LearningItemLifecycle.Suspended;
    public bool CanUnmarkMasteredAction => SelectedItem?.LifecycleState == LearningItemLifecycle.Mastered;

    private bool CanSuspend() => CanSuspendAction;
    private bool CanMarkMastered() => CanMarkMasteredAction;
    private bool CanReactivate() => CanReactivateAction;
    private bool CanUnmarkMastered() => CanUnmarkMasteredAction;

    partial void OnSelectedItemChanged(LearningItemInsight? value)
    {
        OnPropertyChanged(nameof(CanSuspendAction));
        OnPropertyChanged(nameof(CanMarkMasteredAction));
        OnPropertyChanged(nameof(CanReactivateAction));
        OnPropertyChanged(nameof(CanUnmarkMasteredAction));
    }

    [RelayCommand(CanExecute = nameof(CanSuspend))]
    private Task SuspendAsync() => ChangeLifecycleAsync(
        item => _content!.SuspendLearningItemAsync(item.LearningItemId),
        "Learning Item suspended.");

    [RelayCommand(CanExecute = nameof(CanReactivate))]
    private Task ReactivateAsync() => ChangeLifecycleAsync(
        item => _content!.ReactivateLearningItemAsync(item.LearningItemId),
        "Learning Item reactivated and due now.");

    [RelayCommand(CanExecute = nameof(CanMarkMastered))]
    private Task MarkMasteredAsync() => ChangeLifecycleAsync(
        item => _content!.MarkLearningItemMasteredAsync(item.LearningItemId),
        "Learning Item marked as mastered.");

    [RelayCommand(CanExecute = nameof(CanUnmarkMastered))]
    private Task UnmarkMasteredAsync() => ChangeLifecycleAsync(
        item => _content!.UnmarkLearningItemMasteredAsync(item.LearningItemId),
        "Learning Item returned to active and due now.");

    private async Task ChangeLifecycleAsync(Func<LearningItemInsight, Task> action, string message)
    {
        if (_content is null || SelectedItem is null) return;
        try
        {
            await action(SelectedItem);
            await (_refreshAll?.Invoke() ?? RefreshAsync());
            Status = message;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task RefreshItemsAsync()
    {
        if (_service is null) return;
        try
        {
            var selectedItemId = SelectedItem?.LearningItemId;
            var items = await _service.GetLearningItemInsightsAsync(new LearningItemInsightQuery(
                SelectedDeck?.Id,
                PromptSearch,
                Lifecycle,
                NewOnly,
                DueNowOnly,
                RelearningOnly));

            Replace(Items, items);
            OnPropertyChanged(nameof(HasItems));
            SelectedItem = Items.FirstOrDefault(x => x.LearningItemId == selectedItemId);
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}

public static class InsightPresentationFormatter
{
    public static string Assessment(StudyLearningAssessment? assessment) => assessment?.ToString() ?? "—";
    public static string Timestamp(DateTimeOffset? timestamp) => timestamp?.ToLocalTime().ToString("g") ?? "—";
    public static string Distribution(AssessmentDistribution distribution) =>
        $"Nochmal {distribution.Nochmal} · Schwer {distribution.Schwer} · Unsicher {distribution.Unsicher} · Gut {distribution.Gut} · Leicht {distribution.Leicht}";
}
