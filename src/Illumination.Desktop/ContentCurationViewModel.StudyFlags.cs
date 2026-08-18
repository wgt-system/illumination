using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentCurationViewModel
{
    [ObservableProperty]
    private UserFlagDefinitionView? _selectedStudyFlag;

    public bool SelectedStudyFlagIsAssigned =>
        SelectedStudyFlag is not null &&
        StudyItemId is { } itemId &&
        Items.FirstOrDefault(x => x.Id == itemId)?.FlagIds.Contains(SelectedStudyFlag.Id) == true;

    public string StudyFlagActionLabel => SelectedStudyFlagIsAssigned ? "Remove flag" : "Add flag";

    partial void OnSelectedStudyFlagChanged(UserFlagDefinitionView? value)
    {
        OnPropertyChanged(nameof(SelectedStudyFlagIsAssigned));
        OnPropertyChanged(nameof(StudyFlagActionLabel));
    }

    partial void OnStudyItemIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(SelectedStudyFlagIsAssigned));
        OnPropertyChanged(nameof(StudyFlagActionLabel));
    }

    [RelayCommand]
    private async Task ToggleSelectedStudyFlagAsync()
    {
        if (SelectedStudyFlag is null || StudyItemId is null) return;
        await ToggleFlagAsync(SelectedStudyFlag.Id);
        OnPropertyChanged(nameof(SelectedStudyFlagIsAssigned));
        OnPropertyChanged(nameof(StudyFlagActionLabel));
    }
}
