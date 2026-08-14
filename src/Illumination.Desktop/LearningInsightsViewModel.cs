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
    private readonly Func<DeckInsight, Task> _generateFollowUp;

    public LearningInsightsViewModel(LearningInsightService? service, Func<DeckInsight, Task> generateFollowUp)
    {
        _service = service;
        _generateFollowUp = generateFollowUp;
    }

    public ObservableCollection<DeckInsight> Decks { get; } = [];
    public ObservableCollection<LearningItemInsight> Items { get; } = [];
    public ObservableCollection<ReviewHistoryEntry> Reviews { get; } = [];
    public ObservableCollection<StudySessionHistoryEntry> Sessions { get; } = [];
    public IReadOnlyList<LearningItemLifecycle?> LifecycleOptions { get; } = [null, LearningItemLifecycle.Active, LearningItemLifecycle.Suspended, LearningItemLifecycle.Mastered];
    public IReadOnlyList<FollowUpProgressionMode> ProgressionModes { get; } = Enum.GetValues<FollowUpProgressionMode>();

    [ObservableProperty] private LearningInsightOverview? _overview;
    [ObservableProperty] private DeckInsight? _selectedDeck;
    [ObservableProperty] private string _promptSearch = string.Empty;
    [ObservableProperty] private LearningItemLifecycle? _lifecycle;
    [ObservableProperty] private bool _newOnly;
    [ObservableProperty] private bool _dueNowOnly;
    [ObservableProperty] private bool _relearningOnly;
    [ObservableProperty] private FollowUpProgressionMode _progressionMode = FollowUpProgressionMode.Continue;
    [ObservableProperty] private bool _selfAssessedEnabled;
    [ObservableProperty] private bool _selectionEnabled;
    [ObservableProperty] private bool _shortTextEnabled;
    [ObservableProperty] private bool _codeEnabled;
    [ObservableProperty] private string _status = string.Empty;

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
            var decks = await _service.GetDeckInsightsAsync();
            var selectedId = SelectedDeck?.Id;
            Replace(Decks, decks);
            SelectedDeck = Decks.FirstOrDefault(x => x.Id == selectedId) ?? Decks.FirstOrDefault();
            await RefreshItemsAsync();
            Replace(Reviews, await _service.GetReviewHistoryAsync(limit: 12));
            Replace(Sessions, await _service.GetStudySessionHistoryAsync(limit: 8));
            Status = string.Empty;
        }
        catch (Exception exception) { Status = exception.Message; }
    }

    [RelayCommand]
    private async Task GenerateFollowUpAsync()
    {
        if (SelectedDeck is not null) await _generateFollowUp(SelectedDeck);
    }

    public IReadOnlyList<LearningItemResponseMode> SelectedResponseModes => new[]
    {
        (SelfAssessedEnabled, LearningItemResponseMode.SelfAssessed),
        (SelectionEnabled, LearningItemResponseMode.Selection),
        (ShortTextEnabled, LearningItemResponseMode.ShortText),
        (CodeEnabled, LearningItemResponseMode.Code),
    }.Where(x => x.Item1).Select(x => x.Item2).ToArray();

    private async Task RefreshItemsAsync()
    {
        if (_service is null) return;
        try
        {
            var items = await _service.GetLearningItemInsightsAsync(new LearningItemInsightQuery(
                SelectedDeck?.Id,
                PromptSearch,
                Lifecycle,
                NewOnly,
                DueNowOnly,
                RelearningOnly));
            Replace(Items, items);
        }
        catch (Exception exception) { Status = exception.Message; }
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
