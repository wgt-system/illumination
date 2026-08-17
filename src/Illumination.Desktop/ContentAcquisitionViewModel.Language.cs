using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.ContentAcquisition;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private bool _applyingLanguageGuidance;

    [ObservableProperty]
    private string _instructionLanguage = string.Empty;

    [ObservableProperty]
    private string _sourceLanguage = string.Empty;

    [ObservableProperty]
    private string _targetLanguage = string.Empty;

    public bool HasExplicitLanguageGuidance =>
        !string.IsNullOrWhiteSpace(InstructionLanguage) ||
        !string.IsNullOrWhiteSpace(SourceLanguage) ||
        !string.IsNullOrWhiteSpace(TargetLanguage);

    partial void OnInstructionLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnSourceLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnTargetLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));

    partial void OnGeneratedPromptChanged(string value)
    {
        if (_applyingLanguageGuidance || string.IsNullOrWhiteSpace(value) || !HasExplicitLanguageGuidance) return;

        _applyingLanguageGuidance = true;
        try
        {
            GeneratedPrompt = LanguageContentPromptGuidance.Apply(
                new GeneratedContentPrompt(value),
                new LanguageGenerationGuidance(InstructionLanguage, SourceLanguage, TargetLanguage)).Prompt;
        }
        finally
        {
            _applyingLanguageGuidance = false;
        }
    }
}
