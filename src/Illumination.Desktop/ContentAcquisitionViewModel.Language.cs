using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.ContentAcquisition;

namespace Illumination.Desktop;

public sealed record LanguageProficiencyOption(string Label, LanguageProficiencyLevel? Level)
{
    public override string ToString() => Label;
}

public sealed record LanguageExerciseProfileOption(string Label, LanguageExerciseProfile? Profile)
{
    public override string ToString() => Label;
}

public sealed partial class ContentAcquisitionViewModel
{
    private bool _applyingLanguageGuidance;

    public IReadOnlyList<LanguageProficiencyOption> LanguageProficiencyOptions { get; } =
    [
        new("Automatic", null),
        new("A1 – Beginner", LanguageProficiencyLevel.A1),
        new("A2 – Elementary", LanguageProficiencyLevel.A2),
        new("B1 – Intermediate", LanguageProficiencyLevel.B1),
        new("B2 – Upper intermediate", LanguageProficiencyLevel.B2),
        new("C1 – Advanced", LanguageProficiencyLevel.C1),
        new("C2 – Proficient", LanguageProficiencyLevel.C2),
    ];

    public IReadOnlyList<LanguageExerciseProfileOption> LanguageExerciseProfileOptions { get; } =
    [
        new("Automatic / general", null),
        new("Vocabulary flashcards", LanguageExerciseProfile.VocabularyFlashcards),
        new("Phrases & chunks", LanguageExerciseProfile.PhrasesAndChunks),
        new("Translation", LanguageExerciseProfile.Translation),
        new("Grammar practice", LanguageExerciseProfile.GrammarPractice),
        new("Comprehension", LanguageExerciseProfile.Comprehension),
        new("Mixed practice", LanguageExerciseProfile.MixedPractice),
    ];

    [ObservableProperty]
    private string _instructionLanguage = string.Empty;

    [ObservableProperty]
    private string _sourceLanguage = string.Empty;

    [ObservableProperty]
    private string _targetLanguage = string.Empty;

    [ObservableProperty]
    private LanguageProficiencyOption _selectedLanguageProficiency = new("Automatic", null);

    [ObservableProperty]
    private LanguageExerciseProfileOption _selectedLanguageExerciseProfile = new("Automatic / general", null);

    public bool HasExplicitLanguageGuidance =>
        !string.IsNullOrWhiteSpace(InstructionLanguage) ||
        !string.IsNullOrWhiteSpace(SourceLanguage) ||
        !string.IsNullOrWhiteSpace(TargetLanguage) ||
        SelectedLanguageProficiency.Level is not null ||
        SelectedLanguageExerciseProfile.Profile is not null;

    partial void OnInstructionLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnSourceLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnTargetLanguageChanged(string value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnSelectedLanguageProficiencyChanged(LanguageProficiencyOption value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));
    partial void OnSelectedLanguageExerciseProfileChanged(LanguageExerciseProfileOption value) => OnPropertyChanged(nameof(HasExplicitLanguageGuidance));

    partial void OnGeneratedPromptChanged(string value)
    {
        if (_applyingLanguageGuidance || string.IsNullOrWhiteSpace(value)) return;

        _applyingLanguageGuidance = true;
        try
        {
            var generated = ApplyExistingDeckInventory(new GeneratedContentPrompt(value));
            if (HasExplicitLanguageGuidance)
            {
                generated = LanguageContentPromptGuidance.Apply(
                    generated,
                    new LanguageGenerationGuidance(
                        InstructionLanguage,
                        SourceLanguage,
                        TargetLanguage,
                        SelectedLanguageProficiency.Level,
                        SelectedLanguageExerciseProfile.Profile,
                        ProgressionMode,
                        HasSourceDeckContext || (UseExistingDeck && SelectedExistingDeck is not null)));
            }
            GeneratedPrompt = generated.Prompt;
        }
        finally
        {
            _applyingLanguageGuidance = false;
        }
    }
}
