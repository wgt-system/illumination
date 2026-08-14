using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed record StudyEvaluationModeOption(string Name, StudyEvaluationMode? Mode);

public sealed partial class StudyChoiceDisplay(string id, string text) : ObservableObject
{
    public string Id { get; } = id;
    public string Text { get; } = text;

    [ObservableProperty]
    private bool _isSelected;
}
