using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel
{
    [ObservableProperty]
    private DeckPresentationItem? _bulkTargetDeckPresentation;

    public int ManagementSelectionCount => Items.Count(x => x.IsSelectedForManagement);
    public bool HasManagementSelection => ManagementSelectionCount > 0;

    [RelayCommand]
    private void SelectFilteredForManagement()
    {
        foreach (var item in FilteredItems.ToArray()) item.IsSelectedForManagement = true;
        ManagementSelectionChanged();
    }

    [RelayCommand]
    private void ClearManagementSelection()
    {
        foreach (var item in Items) item.IsSelectedForManagement = false;
        ManagementSelectionChanged();
    }

    [RelayCommand]
    private async Task BulkAddSelectedToDeckAsync()
    {
        if (_content is null || BulkTargetDeckPresentation is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement && !x.DeckIds.Contains(BulkTargetDeckPresentation.Id)).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No selected Learning Items need to be added to that Deck.");
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var item in selected) await _content.AddLearningItemToDeckAsync(BulkTargetDeckPresentation.Id, item.Id);
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"Added {selected.Length} Learning Items to '{BulkTargetDeckPresentation.DisplayName}'.");
        });
    }

    [RelayCommand]
    private async Task BulkRemoveSelectedFromDeckAsync()
    {
        if (_content is null || BulkTargetDeckPresentation is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement && x.DeckIds.Contains(BulkTargetDeckPresentation.Id)).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No selected Learning Items belong to that Deck.");
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var item in selected) await _content.RemoveLearningItemFromDeckAsync(BulkTargetDeckPresentation.Id, item.Id);
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"Removed {selected.Length} Learning Items from '{BulkTargetDeckPresentation.DisplayName}'. The Learning Items were not deleted.");
        });
    }

    [RelayCommand]
    private Task BulkSuspendSelectedAsync() => BulkLifecycleAsync(
        item => item.Lifecycle == LearningItemLifecycle.Active,
        id => _content!.SuspendLearningItemAsync(id),
        "suspended");

    [RelayCommand]
    private Task BulkReactivateSelectedAsync() => BulkLifecycleAsync(
        item => item.Lifecycle == LearningItemLifecycle.Suspended,
        id => _content!.ReactivateLearningItemAsync(id),
        "reactivated and made due now");

    [RelayCommand]
    private Task BulkMarkMasteredSelectedAsync() => BulkLifecycleAsync(
        item => item.Lifecycle == LearningItemLifecycle.Active,
        id => _content!.MarkLearningItemMasteredAsync(id),
        "marked mastered");

    [RelayCommand]
    private Task BulkUnmarkMasteredSelectedAsync() => BulkLifecycleAsync(
        item => item.Lifecycle == LearningItemLifecycle.Mastered,
        id => _content!.UnmarkLearningItemMasteredAsync(id),
        "returned to active and made due now");

    private async Task BulkLifecycleAsync(
        Func<CuratedLearningItemRowViewModel, bool> applicable,
        Func<Guid, Task> operation,
        string actionLabel)
    {
        if (_content is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement && applicable(x)).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No selected Learning Items support that lifecycle action.");
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var item in selected) await operation(item.Id);
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"{selected.Length} Learning Items {actionLabel}.");
        });
    }

    internal void ManagementSelectionChanged()
    {
        OnPropertyChanged(nameof(ManagementSelectionCount));
        OnPropertyChanged(nameof(HasManagementSelection));
    }
}

public sealed partial class CuratedLearningItemRowViewModel
{
    [ObservableProperty]
    private bool _isSelectedForManagement;

    partial void OnIsSelectedForManagementChanged(bool value)
    {
        // The containing view model also refreshes the aggregate after select-all/clear.
        // Individual toggles intentionally remain local to keep row construction simple.
    }
}
