using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel : ObservableObject
{
    private readonly ContentCurationService _curation;
    private readonly QualityReviewExchangeService _exchange;
    private readonly Action<string> _reportStatus;
    private readonly ContentManagementService? _content;
    private readonly Func<Task>? _refreshContent;
    private IDesktopInteractionService? _desktopInteractions;

    public ContentCurationViewModel(ContentCurationService curation, QualityReviewExchangeService exchange, Action<string> reportStatus, ContentManagementService? content = null, Func<Task>? refreshContent = null)
    {
        _curation = curation ?? throw new ArgumentNullException(nameof(curation));
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));
        _content = content;
        _refreshContent = refreshContent;
    }

    public ObservableCollection<CuratedLearningItemRowViewModel> Items { get; } = [];
    public ObservableCollection<UserFlagDefinitionView> FlagDefinitions { get; } = [];
    public ObservableCollection<QualityReviewExchangeRowViewModel> ReviewResults { get; } = [];
    public ObservableCollection<CurationDiagnosticDisplay> ReviewDiagnostics { get; } = [];
    public IReadOnlyList<QualityReviewPromptMode> ReviewModes { get; } = Enum.GetValues<QualityReviewPromptMode>();

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(GenerateReviewPromptCommand)), NotifyCanExecuteChangedFor(nameof(AddFlagCommand)), NotifyCanExecuteChangedFor(nameof(RemoveFlagCommand)), NotifyCanExecuteChangedFor(nameof(SuspendSelectedCommand)), NotifyCanExecuteChangedFor(nameof(ReactivateSelectedCommand)), NotifyCanExecuteChangedFor(nameof(MarkMasteredSelectedCommand)), NotifyCanExecuteChangedFor(nameof(UnmarkMasteredSelectedCommand))]
    private CuratedLearningItemRowViewModel? _selectedItem;

    [ObservableProperty]
    private UserFlagDefinitionView? _selectedFlag;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateFlagCommand))]
    private string _newFlagName = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CreateFlagCommand))]
    private string _newFlagMeaning = string.Empty;

    [ObservableProperty]
    private UserFlagDefinitionView? _filterFlag;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PreviewReviewResultsCommand))]
    private string _rawReviewJson = string.Empty;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CopyReviewPromptCommand))]
    private string _reviewPrompt = string.Empty;

    [ObservableProperty]
    private QualityReviewPromptMode _reviewMode = QualityReviewPromptMode.Standard;

    [ObservableProperty]
    private string _reviewSummary = string.Empty;

    [ObservableProperty]
    private bool _hasReviewPreview;

    [ObservableProperty]
    private Guid? _studyItemId;

    public bool HasReviewPrompt => !string.IsNullOrWhiteSpace(ReviewPrompt);
    public bool HasReviewResults => ReviewResults.Count > 0;
    public ObservableCollection<UserFlagDefinitionView> StudyFlags { get; } = [];

    public bool CanSuspendSelected => SelectedItem?.Lifecycle == LearningItemLifecycle.Active;
    public bool CanReactivateSelected => SelectedItem?.Lifecycle == LearningItemLifecycle.Suspended;
    public bool CanMarkMasteredSelected => SelectedItem?.Lifecycle == LearningItemLifecycle.Active;
    public bool CanUnmarkMasteredSelected => SelectedItem?.Lifecycle == LearningItemLifecycle.Mastered;

    partial void OnSelectedItemChanged(CuratedLearningItemRowViewModel? value)
    {
        OnPropertyChanged(nameof(CanSuspendSelected));
        OnPropertyChanged(nameof(CanReactivateSelected));
        OnPropertyChanged(nameof(CanMarkMasteredSelected));
        OnPropertyChanged(nameof(CanUnmarkMasteredSelected));
    }

    [RelayCommand(CanExecute = nameof(CanSuspendSelected))]
    private Task SuspendSelectedAsync() => ChangeLifecycleAsync(item => _content!.SuspendLearningItemAsync(item.Id), "Learning Item suspended.");

    [RelayCommand(CanExecute = nameof(CanReactivateSelected))]
    private Task ReactivateSelectedAsync() => ChangeLifecycleAsync(item => _content!.ReactivateLearningItemAsync(item.Id), "Learning Item reactivated and due now.");

    [RelayCommand(CanExecute = nameof(CanMarkMasteredSelected))]
    private Task MarkMasteredSelectedAsync() => ChangeLifecycleAsync(item => _content!.MarkLearningItemMasteredAsync(item.Id), "Learning Item marked as mastered.");

    [RelayCommand(CanExecute = nameof(CanUnmarkMasteredSelected))]
    private Task UnmarkMasteredSelectedAsync() => ChangeLifecycleAsync(item => _content!.UnmarkLearningItemMasteredAsync(item.Id), "Learning Item returned to active and due now.");

    private async Task ChangeLifecycleAsync(Func<CuratedLearningItemRowViewModel, Task> action, string message)
    {
        if (_content is null || SelectedItem is null) return;
        await RunAsync(async () => { await action(SelectedItem); if (_refreshContent is not null) await _refreshContent(); _reportStatus(message); });
    }

    public void AttachDesktopInteractions(IDesktopInteractionService interactions)
    {
        _desktopInteractions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        CopyReviewPromptCommand.NotifyCanExecuteChanged();
        LoadReviewFileCommand.NotifyCanExecuteChanged();
    }

    public async Task RefreshAsync(IReadOnlyList<LearningItemView> learningItems)
    {
        var selectedId = SelectedItem?.Id;
        var definitions = await _curation.ListUserFlagDefinitionsAsync();
        Replace(FlagDefinitions, definitions);
        var curated = new List<CuratedLearningItemView>();
        foreach (var item in learningItems) curated.Add(await _curation.GetLearningItemCurationAsync(item.Id));
        Replace(Items, curated.Select(x => new CuratedLearningItemRowViewModel(x, ReviewSelectionChanged)));
        SelectedItem = Items.FirstOrDefault(x => x.Id == selectedId) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredItems));
        RefreshStudyFlags();
    }

    public IEnumerable<CuratedLearningItemRowViewModel> FilteredItems =>
        FilterFlag is null ? Items : Items.Where(item => item.FlagIds.Contains(FilterFlag.Id));

    partial void OnFilterFlagChanged(UserFlagDefinitionView? value) => OnPropertyChanged(nameof(FilteredItems));
    partial void OnRawReviewJsonChanged(string value) => InvalidateReviewPreview();
    partial void OnReviewModeChanged(QualityReviewPromptMode value) => InvalidateReviewPreview();

    public void SetStudyItem(Guid? itemId)
    {
        StudyItemId = itemId;
        RefreshStudyFlags();
    }

    [RelayCommand(CanExecute = nameof(CanCreateFlag))]
    private async Task CreateFlagAsync() => await RunAsync(async () =>
    {
        var created = await _curation.CreateUserFlagDefinitionAsync(new CreateUserFlagDefinitionCommand(NewFlagName, NewFlagMeaning));
        FlagDefinitions.Add(created);
        NewFlagName = string.Empty;
        NewFlagMeaning = string.Empty;
        _reportStatus($"Flag '{created.Name}' created.");
    });

    private bool CanCreateFlag() => !string.IsNullOrWhiteSpace(NewFlagName) && !string.IsNullOrWhiteSpace(NewFlagMeaning);

    [RelayCommand(CanExecute = nameof(CanChangeFlag))]
    private async Task AddFlagAsync() => await ChangeFlagAsync(add: true);

    [RelayCommand(CanExecute = nameof(CanChangeFlag))]
    private async Task RemoveFlagAsync() => await ChangeFlagAsync(add: false);

    private bool CanChangeFlag() => SelectedItem is not null && SelectedFlag is not null;

    [RelayCommand]
    private async Task ToggleFlagAsync(Guid flagId) => await RunAsync(async () =>
    {
        if (StudyItemId is not { } itemId) return;
        var item = Items.FirstOrDefault(x => x.Id == itemId);
        if (item is null) return;
        var updated = item.FlagIds.Contains(flagId)
            ? await _curation.RemoveFlagFromLearningItemAsync(itemId, flagId)
            : await _curation.AddFlagToLearningItemAsync(itemId, flagId);
        ReplaceItem(updated);
        RefreshStudyFlags();
    });

    private async Task ChangeFlagAsync(bool add) => await RunAsync(async () =>
    {
        if (SelectedItem is null || SelectedFlag is null) return;
        var updated = add
            ? await _curation.AddFlagToLearningItemAsync(SelectedItem.Id, SelectedFlag.Id)
            : await _curation.RemoveFlagFromLearningItemAsync(SelectedItem.Id, SelectedFlag.Id);
        ReplaceItem(updated);
        _reportStatus(add ? "Flag assigned." : "Flag removed.");
    });

    [RelayCommand(CanExecute = nameof(CanGenerateReviewPrompt))]
    private async Task GenerateReviewPromptAsync() => await RunAsync(async () =>
    {
        var ids = Items.Where(x => x.IsSelectedForReview).Select(x => x.Id).ToArray();
        var generated = await _exchange.GeneratePromptAsync(new GenerateQualityReviewPromptCommand(ids, ReviewMode));
        ReviewPrompt = generated.Prompt;
        _reportStatus($"Quality Review prompt generated for {ids.Length} Learning Items.");
    });

    private bool CanGenerateReviewPrompt() => Items.Any(x => x.IsSelectedForReview);

    [RelayCommand(CanExecute = nameof(CanCopyReviewPrompt))]
    private async Task CopyReviewPromptAsync() => await CopyAsync(ReviewPrompt, "Quality Review prompt copied.");

    private bool CanCopyReviewPrompt() => _desktopInteractions is not null && HasReviewPrompt;

    [RelayCommand(CanExecute = nameof(CanLoadReviewFile))]
    private async Task LoadReviewFileAsync() => await RunAsync(async () =>
    {
        var json = await _desktopInteractions!.LoadJsonFileAsync();
        if (json is not null) RawReviewJson = json;
    });

    private bool CanLoadReviewFile() => _desktopInteractions is not null;

    [RelayCommand(CanExecute = nameof(CanPreviewReviewResults))]
    private async Task PreviewReviewResultsAsync() => await RunAsync(async () =>
    {
        ReviewResults.Clear();
        ReviewDiagnostics.Clear();
        var preview = await _exchange.PreviewAsync(RawReviewJson, ReviewMode);
        foreach (var diagnostic in preview.Diagnostics) ReviewDiagnostics.Add(ToDisplay(diagnostic));
        foreach (var result in preview.Results)
        {
            var item = result.LearningItemId is { } itemId ? Items.FirstOrDefault(x => x.Id == itemId) : null;
            ReviewResults.Add(new QualityReviewExchangeRowViewModel(result, item, ReviewSelectionChanged));
        }
        HasReviewPreview = true;
        var valid = preview.Results.Count(x => x.IsValid);
        ReviewSummary = $"{valid} valid review results · {preview.Results.Count - valid} invalid";
        _reportStatus("Quality Review results previewed. Select results explicitly before accepting.");
    });

    private bool CanPreviewReviewResults() => !string.IsNullOrWhiteSpace(RawReviewJson);

    [RelayCommand]
    private async Task AcceptSelectedReviewsAsync() => await RunAsync(async () =>
    {
        var selected = ReviewResults.Where(x => x.IsSelected && x.IsSelectable).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No Quality Review results selected; content remains unchanged.");
            return;
        }
        foreach (var result in selected)
        {
            await _curation.AcceptQualityReviewAsync(result.LearningItemId, new AcceptQualityReviewCommand(
                result.Outcome, result.EvidenceType, result.Findings, result.SuggestedCorrection,
                result.SelectedSupersededReviewIds.ToArray()));
        }
        foreach (var result in selected)
        {
            var updated = await _curation.GetLearningItemCurationAsync(result.LearningItemId);
            ReplaceItem(updated);
        }
        _reportStatus($"Accepted {selected.Length} Quality Review results. Suggested corrections were not applied.");
    });

    [RelayCommand]
    private void SelectAllReviewItems() { foreach (var item in Items) item.IsSelectedForReview = true; }

    [RelayCommand]
    private void ClearReviewItemSelection() { foreach (var item in Items) item.IsSelectedForReview = false; }

    private void ReviewSelectionChanged() => GenerateReviewPromptCommand.NotifyCanExecuteChanged();

    private void InvalidateReviewPreview()
    {
        HasReviewPreview = false;
        ReviewSummary = string.Empty;
        ReviewDiagnostics.Clear();
        ReviewResults.Clear();
    }

    private void ReplaceItem(CuratedLearningItemView updated)
    {
        var replacement = new CuratedLearningItemRowViewModel(updated, ReviewSelectionChanged) { IsSelectedForReview = SelectedItem?.IsSelectedForReview == true };
        var index = Items.IndexOf(Items.First(x => x.Id == updated.Id));
        Items[index] = replacement;
        SelectedItem = replacement;
        OnPropertyChanged(nameof(FilteredItems));
    }

    private void RefreshStudyFlags()
    {
        StudyFlags.Clear();
        if (StudyItemId is not { } id) return;
        var item = Items.FirstOrDefault(x => x.Id == id);
        if (item is null) return;
        foreach (var flag in FlagDefinitions.Where(x => item.FlagIds.Contains(x.Id))) StudyFlags.Add(flag);
    }

    private async Task CopyAsync(string text, string message)
    {
        if (_desktopInteractions is null) return;
        await _desktopInteractions.CopyTextAsync(text);
        _reportStatus(message);
    }

    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception exception) { _reportStatus(exception.Message); }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
    private static CurationDiagnosticDisplay ToDisplay(QualityReviewResultDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Message, diagnostic.ResultIndex is { } index ? $"Result {index + 1}" : "Bundle");
}

public sealed partial class CuratedLearningItemRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    public CuratedLearningItemRowViewModel(CuratedLearningItemView item, Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        Id = item.Id; Prompt = item.Prompt; ContentRevision = item.ContentRevision; FlagIds = item.UserFlagDefinitionIds.ToHashSet();
        QualityState = item.CurrentQualityState?.Outcome.ToString() ?? "NoAssurance"; Lifecycle = item.Lifecycle;
        History = item.QualityReviews.Select(x => new QualityReviewHistoryRowViewModel(x)).ToArray();
    }
    public Guid Id { get; }
    public string Prompt { get; }
    public int ContentRevision { get; }
    public string QualityState { get; }
    public LearningItemLifecycle Lifecycle { get; }
    public HashSet<Guid> FlagIds { get; }
    public IReadOnlyList<QualityReviewHistoryRowViewModel> History { get; }
    public bool HasHistory => History.Count > 0;
    [ObservableProperty] private bool _isSelectedForReview;
    partial void OnIsSelectedForReviewChanged(bool value) => _selectionChanged();
}

public sealed class QualityReviewHistoryRowViewModel
{
    public QualityReviewHistoryRowViewModel(QualityReviewView review) { Id = review.Id; Outcome = review.Outcome.ToString(); EvidenceType = review.EvidenceType.ToString(); Findings = review.Findings; SuggestedCorrection = review.SuggestedCorrection ?? string.Empty; ContentRevision = review.ContentRevision; IsActive = review.SupersededBy is null; }
    public Guid Id { get; }
    public int ContentRevision { get; }
    public string Outcome { get; }
    public string EvidenceType { get; }
    public string Findings { get; }
    public string SuggestedCorrection { get; }
    public bool IsActive { get; }
}

public sealed partial class QualityReviewExchangeRowViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    public QualityReviewExchangeRowViewModel(QualityReviewResultPreview result, CuratedLearningItemRowViewModel? item, Action selectionChanged)
    {
        _selectionChanged = selectionChanged; ResultIndex = result.ResultIndex; LearningItemId = result.LearningItemId ?? Guid.Empty; Prompt = item?.Prompt ?? "Unknown Learning Item"; Outcome = result.Outcome ?? CurationQualityReviewOutcome.NeedsReview; EvidenceType = result.EvidenceType ?? CurationQualityReviewEvidenceType.ModelReview; Findings = result.Findings ?? string.Empty; SuggestedCorrection = result.SuggestedCorrection; Diagnostics = string.Join(" · ", result.Diagnostics.Select(x => x.Message)); IsSelectable = result.IsValid; SelectedSupersededReviewIds = []; PriorActiveReviews = item is null ? [] : item.History.Where(x => x.IsActive && x.ContentRevision == (result.ContentRevision ?? item.ContentRevision)).ToArray();
    }
    public int ResultIndex { get; }
    public Guid LearningItemId { get; }
    public string Prompt { get; }
    public CurationQualityReviewOutcome Outcome { get; }
    public CurationQualityReviewEvidenceType EvidenceType { get; }
    public string Findings { get; }
    public string? SuggestedCorrection { get; }
    public string Diagnostics { get; }
    public bool IsSelectable { get; }
    public bool HasDiagnostics => !string.IsNullOrWhiteSpace(Diagnostics);
    public bool HasSuggestedCorrection => !string.IsNullOrWhiteSpace(SuggestedCorrection);
    public IReadOnlyList<QualityReviewHistoryRowViewModel> PriorActiveReviews { get; }
    public HashSet<Guid> SelectedSupersededReviewIds { get; }
    [ObservableProperty] private bool _isSelected;
    partial void OnIsSelectedChanged(bool value) { if (!IsSelectable && value) { IsSelected = false; return; } _selectionChanged(); }
    [RelayCommand] private void ToggleSuperseded(Guid reviewId) { if (!SelectedSupersededReviewIds.Add(reviewId)) SelectedSupersededReviewIds.Remove(reviewId); }
}

public sealed record CurationDiagnosticDisplay(string Code, string Message, string Scope);
