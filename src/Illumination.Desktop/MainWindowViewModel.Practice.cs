using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class MainWindowViewModel
{
    private int _practiceIndex = -1;

    public ObservableCollection<LearningItemView> PracticeItems { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCurrentPracticeItem)), NotifyPropertyChangedFor(nameof(PracticeProgress))]
    private LearningItemView? _currentPracticeItem;

    [ObservableProperty]
    private bool _practiceSolutionRevealed;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PracticeProgress))]
    private bool _practiceIsActive;

    public bool HasCurrentPracticeItem => CurrentPracticeItem is not null;
    public string PracticeProgress => PracticeIsActive && CurrentPracticeItem is not null
        ? $"{_practiceIndex + 1} / {PracticeItems.Count}"
        : "No practice active";

    [RelayCommand]
    private void StartSelectedDeckPractice()
    {
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active scheduled Study Session before starting Practice now.";
            return;
        }
        if (SelectedDeck is null)
        {
            StatusMessage = "Select a Deck to practice.";
            return;
        }

        var items = LearningItems.Where(item => item.DeckIds.Contains(SelectedDeck.Id)).ToArray();
        StartPractice(items, $"Practice now started for '{SelectedDeck.Name}'. No Reviews or scheduling state will be changed.");
    }

    [RelayCommand]
    private void StartSelectedItemPractice()
    {
        if (SessionIsActive)
        {
            StatusMessage = "Complete the active scheduled Study Session before starting Practice now.";
            return;
        }
        if (SelectedDeckItem is null)
        {
            StatusMessage = "Select a Learning Item in the Deck to practice.";
            return;
        }

        StartPractice([SelectedDeckItem], "Practice now started for the selected Learning Item. No Review or scheduling state will be changed.");
    }

    [RelayCommand]
    private void RevealPracticeSolution()
    {
        if (CurrentPracticeItem is null) return;
        PracticeSolutionRevealed = true;
    }

    [RelayCommand]
    private void NextPracticeItem()
    {
        if (!PracticeIsActive || CurrentPracticeItem is null) return;
        if (_practiceIndex >= PracticeItems.Count - 1)
        {
            EndPractice("Practice complete. No Reviews or scheduling state were changed.");
            return;
        }

        _practiceIndex++;
        CurrentPracticeItem = PracticeItems[_practiceIndex];
        PracticeSolutionRevealed = false;
        OnPropertyChanged(nameof(PracticeProgress));
    }

    [RelayCommand]
    private void PreviousPracticeItem()
    {
        if (!PracticeIsActive || CurrentPracticeItem is null || _practiceIndex <= 0) return;
        _practiceIndex--;
        CurrentPracticeItem = PracticeItems[_practiceIndex];
        PracticeSolutionRevealed = false;
        OnPropertyChanged(nameof(PracticeProgress));
    }

    [RelayCommand]
    private void ClosePractice() => EndPractice("Practice closed. No Reviews or scheduling state were changed.");

    private void StartPractice(IEnumerable<LearningItemView> items, string status)
    {
        var selected = items.DistinctBy(item => item.Id).ToArray();
        if (selected.Length == 0)
        {
            EndPractice("There are no Learning Items in the selected practice scope.");
            return;
        }

        Replace(PracticeItems, selected);
        _practiceIndex = 0;
        PracticeIsActive = true;
        CurrentPracticeItem = PracticeItems[0];
        PracticeSolutionRevealed = false;
        OnPropertyChanged(nameof(PracticeProgress));
        StatusMessage = status;
    }

    private void EndPractice(string status)
    {
        PracticeIsActive = false;
        PracticeSolutionRevealed = false;
        CurrentPracticeItem = null;
        _practiceIndex = -1;
        PracticeItems.Clear();
        OnPropertyChanged(nameof(PracticeProgress));
        StatusMessage = status;
    }
}
