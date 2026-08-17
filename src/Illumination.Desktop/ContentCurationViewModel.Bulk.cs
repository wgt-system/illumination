using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel
{
    [ObservableProperty]
    private DeckPresentationItem? _bulkTargetDeckPresentation;

    [ObservableProperty]
    private string _bulkNewDeckName = string.Empty;

    [ObservableProperty]
    private UserFlagDefinitionView? _bulkTargetFlag;

    [RelayCommand]
    private void SelectFilteredForManagement()
    {
        foreach (var item in FilteredItems.ToArray()) item.IsSelectedForManagement = true;
    }

    [RelayCommand]
    private void ClearManagementSelection()
    {
        foreach (var item in Items) item.IsSelectedForManagement = false;
    }

    [RelayCommand]
    private async Task CreateDeckFromSelectedAsync()
    {
        if (_content is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("Select at least one Learning Item for the new Deck.");
            return;
        }
        if (string.IsNullOrWhiteSpace(BulkNewDeckName))
        {
            _reportStatus("Enter a name for the new Deck.");
            return;
        }

        var requestedName = BulkNewDeckName.Trim();
        await RunAsync(async () =>
        {
            var deck = await _content.CreateDeckAsync(new CreateDeckCommand(requestedName));
            foreach (var item in selected) await _content.AddLearningItemToDeckAsync(deck.Id, item.Id);
            BulkNewDeckName = string.Empty;
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"Created Deck '{deck.Name}' from {selected.Length} selected Learning Items. Existing Learning State is shared, not copied.");
        });
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
    private async Task BulkAddFlagAsync()
    {
        if (BulkTargetFlag is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement && !x.FlagIds.Contains(BulkTargetFlag.Id)).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No selected Learning Items need that flag added.");
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var item in selected) await _curation.AddFlagToLearningItemAsync(item.Id, BulkTargetFlag.Id);
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"Added flag '{BulkTargetFlag.Name}' to {selected.Length} Learning Items.");
        });
    }

    [RelayCommand]
    private async Task BulkRemoveFlagAsync()
    {
        if (BulkTargetFlag is null) return;
        var selected = Items.Where(x => x.IsSelectedForManagement && x.FlagIds.Contains(BulkTargetFlag.Id)).ToArray();
        if (selected.Length == 0)
        {
            _reportStatus("No selected Learning Items currently have that flag.");
            return;
        }

        await RunAsync(async () =>
        {
            foreach (var item in selected) await _curation.RemoveFlagFromLearningItemAsync(item.Id, BulkTargetFlag.Id);
            if (_refreshContent is not null) await _refreshContent();
            _reportStatus($"Removed flag '{BulkTargetFlag.Name}' from {selected.Length} Learning Items.");
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
}

public sealed partial class CuratedLearningItemRowViewModel
{
    [ObservableProperty]
    private bool _isSelectedForManagement;
}
