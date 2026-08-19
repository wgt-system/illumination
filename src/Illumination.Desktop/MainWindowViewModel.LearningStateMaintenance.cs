using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    private LearningStateMaintenanceService? _learningStateMaintenance;
    private Guid? _restartDeckConfirmationId;
    private Guid? _restartItemConfirmationId;

    public void ConfigureLearningStateMaintenance(LearningStateMaintenanceService service) =>
        _learningStateMaintenance = service ?? throw new ArgumentNullException(nameof(service));

    [RelayCommand]
    private async Task RestartSelectedDeckLearningAsync()
    {
        if (_learningStateMaintenance is null)
        {
            StatusMessage = "Learning State maintenance is not configured.";
            return;
        }
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active Study Session before restarting learning state.";
            return;
        }
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck to restart its learning state.";
            return;
        }

        if (_restartDeckConfirmationId != SelectedDeck.Id)
        {
            _restartDeckConfirmationId = SelectedDeck.Id;
            _restartItemConfirmationId = null;
            StatusMessage = $"Select 'Restart Deck learning' again to reset scheduling for every item in '{SelectedDeck.Name}'. Review history, content, Deck membership and lifecycle are preserved.";
            return;
        }

        var deckId = SelectedDeck.Id;
        _restartDeckConfirmationId = null;
        await RunAsync(async () =>
        {
            var result = await _learningStateMaintenance.RestartDeckAsync(deckId);
            await RefreshContentAsync(deckId);
            if (result.LearningItemCount == 0)
            {
                StatusMessage = "The Deck is empty; no Learning State changed.";
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var refreshed = LearningItems.Where(item => item.DeckIds.Contains(deckId)).ToArray();
            var resetVisible = refreshed.Count(item => item.IsNew && item.DueAt <= now);
            if (resetVisible != result.LearningItemCount)
                throw new InvalidOperationException($"Restart wrote {result.LearningItemCount} item(s), but the refreshed view reports only {resetVisible} as new and due now.");

            var active = refreshed.Count(item => item.Lifecycle == LearningItemLifecycle.Active);
            var excludedByLifecycle = refreshed.Length - active;
            var studySelection = StudyAllNew
                ? "Scheduled Study is currently set to include all new Active cards."
                : $"Scheduled Study still applies the current new-card limit ({StudyNewItemLimitText}) and its filters; choose All new to include every reset Active card in one session.";
            var lifecycle = excludedByLifecycle == 0
                ? string.Empty
                : $" {excludedByLifecycle} item(s) remain Suspended/Mastered and therefore stay outside normal Study until reactivated/unmastered.";

            StatusMessage = $"Restarted learning for {result.LearningItemCount} item(s); the refreshed state confirms all are New and due now. {studySelection}{lifecycle} Review history is preserved.";
        });
    }

    [RelayCommand]
    private async Task RestartSelectedDeckItemLearningAsync()
    {
        if (_learningStateMaintenance is null)
        {
            StatusMessage = "Learning State maintenance is not configured.";
            return;
        }
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active Study Session before restarting learning state.";
            return;
        }
        if (SelectedDeckItem is null)
        {
            StatusMessage = "Select a Learning Item in the Deck first.";
            return;
        }

        if (_restartItemConfirmationId != SelectedDeckItem.Id)
        {
            _restartItemConfirmationId = SelectedDeckItem.Id;
            _restartDeckConfirmationId = null;
            StatusMessage = $"Select 'Restart selected item' again to reset scheduling for '{SelectedDeckItem.Prompt}'. Review history, content, Deck membership and lifecycle are preserved.";
            return;
        }

        var itemId = SelectedDeckItem.Id;
        var deckId = SelectedDeck?.Id;
        _restartItemConfirmationId = null;
        await RunAsync(async () =>
        {
            await _learningStateMaintenance.RestartLearningItemAsync(itemId);
            await RefreshContentAsync(deckId);
            SelectedDeckItem = SelectedDeckItems.FirstOrDefault(item => item.Id == itemId);

            var restarted = LearningItems.FirstOrDefault(item => item.Id == itemId)
                ?? throw new InvalidOperationException("The restarted Learning Item disappeared from the refreshed content view.");
            if (!restarted.IsNew || restarted.DueAt > _timeProvider.GetUtcNow())
                throw new InvalidOperationException("Restart completed, but the refreshed Learning Item is not reported as New and due now.");

            var studyEligibility = restarted.Lifecycle switch
            {
                LearningItemLifecycle.Active => StudyAllNew
                    ? "It is eligible for scheduled Study under the current all-new setting."
                    : $"It is eligible as a new card, subject to the current Study new-card limit ({StudyNewItemLimitText}) and other session filters.",
                LearningItemLifecycle.Suspended => "Its Suspended lifecycle was preserved, so normal Study still excludes it until Reactivate.",
                LearningItemLifecycle.Mastered => "Its Mastered lifecycle was preserved, so normal Study still excludes it until Unmark mastered.",
                _ => string.Empty,
            };

            StatusMessage = $"Learning Item restarted; the refreshed state confirms it is New and due now. {studyEligibility} Review history is preserved.";
        });
    }
}
