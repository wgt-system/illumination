using CommunityToolkit.Mvvm.Input;
using Illumination.Application.ContentAcquisition;
using Illumination.Application.Insights;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private Func<Guid, CancellationToken, Task<DeckLearningContext>>? _deckLearningContextProvider;
    private EventHandler? _generatePromptCanExecuteRelay;

    public void ConfigureDeckLearningContextProvider(
        Func<Guid, CancellationToken, Task<DeckLearningContext>> provider)
    {
        _deckLearningContextProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        if (_generatePromptCanExecuteRelay is null)
        {
            _generatePromptCanExecuteRelay = (_, _) => GenerateLearningAwarePromptCommand.NotifyCanExecuteChanged();
            GeneratePromptCommand.CanExecuteChanged += _generatePromptCanExecuteRelay;
        }
        GenerateLearningAwarePromptCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGeneratePrompt))]
    private async Task GenerateLearningAwarePromptAsync(CancellationToken cancellationToken) =>
        await RunBusyAsync(async () =>
        {
            var learningContext = _sourceDeckContext;
            var contextWasLoadedFromTargetDeck = false;
            if (learningContext is null &&
                UseExistingDeck &&
                SelectedExistingDeck is { } selectedDeck &&
                _deckLearningContextProvider is not null)
            {
                learningContext = await _deckLearningContextProvider(selectedDeck.Id, cancellationToken);
                contextWasLoadedFromTargetDeck = true;
            }

            var command = new GenerateContentPromptCommand(
                Subject,
                RequestedItemCount,
                NewDeckName: UseNewDeck ? NewDeckName : null,
                ExistingDeckId: UseExistingDeck ? SelectedExistingDeck?.Id : null,
                Guidance: Guidance,
                SourceDeckContext: learningContext,
                ProgressionMode: ProgressionMode,
                AllowedResponseModes: RestrictResponseModes ? SelectedResponseModes() : null);

            var language = HasExplicitLanguageGuidance
                ? new LanguageGenerationGuidance(
                    InstructionLanguage,
                    SourceLanguage,
                    TargetLanguage,
                    SelectedLanguageProficiency.Level,
                    SelectedLanguageExerciseProfile.Profile,
                    ProgressionMode,
                    learningContext is not null || (UseExistingDeck && SelectedExistingDeck is not null))
                : null;

            var composed = ContentGenerationPromptComposer.Compose(
                _service,
                command,
                language,
                BuildExistingDeckInventory(),
                UseExistingDeck ? SelectedExistingDeck?.Name : null);

            // The normal GeneratedPrompt observer remains for legacy/internal callers of
            // GeneratePromptCommand. This user-facing command already has the complete
            // Application-owned composition and must not run it a second time.
            _composingGeneratedPrompt = true;
            try
            {
                GeneratedPrompt = composed.Prompt;
            }
            finally
            {
                _composingGeneratedPrompt = false;
            }

            _reportStatus(contextWasLoadedFromTargetDeck
                ? "Prompt generated with fresh learning evidence from the selected Deck."
                : learningContext is not null
                    ? "Learning-aware prompt generated."
                    : "Prompt generated.");
        });
}
