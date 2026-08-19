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
            StatusMessage = result.LearningItemCount == 0
                ? "The Deck is empty; no Learning State changed."
                : $"Restarted learning for {result.LearningItemCount} item(s). They are new and due now; Review history and lifecycle were preserved.";
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
            StatusMessage = "Learning Item restarted. It is new and due now; Review history and lifecycle were preserved.";
        });
    }
}
