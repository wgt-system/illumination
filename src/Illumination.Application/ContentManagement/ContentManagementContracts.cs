namespace Illumination.Application.ContentManagement;

public enum LearningItemResponseMode
{
    SelfAssessed,
    Selection,
    ShortText,
    Code,
}

public enum LearningItemLifecycle
{
    Active,
    Suspended,
    Mastered,
}

public sealed record HintInput(string Text);

public sealed record AnswerChoiceInput(string Text, bool IsCorrect = false);

public sealed record CreateLearningItemCommand(
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode = LearningItemResponseMode.SelfAssessed,
    IReadOnlyList<HintInput>? Hints = null,
    IReadOnlyList<AnswerChoiceInput>? DirectAnswerChoices = null,
    IReadOnlyList<AnswerChoiceInput>? AssistanceAnswerChoices = null,
    IReadOnlyList<string>? AcceptedShortAnswers = null,
    bool LowInteractionEligible = false);

public sealed record UpdateLearningItemCommand(
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode = LearningItemResponseMode.SelfAssessed,
    IReadOnlyList<HintInput>? Hints = null,
    IReadOnlyList<AnswerChoiceInput>? DirectAnswerChoices = null,
    IReadOnlyList<AnswerChoiceInput>? AssistanceAnswerChoices = null,
    IReadOnlyList<string>? AcceptedShortAnswers = null,
    bool LowInteractionEligible = false);

public sealed record CreateDeckCommand(string Name);

public sealed record RenameDeckCommand(string Name);

public sealed record LearningItemView(
    Guid Id,
    string Prompt,
    string ReferenceSolution,
    IReadOnlyList<HintView> Hints,
    LearningItemResponseMode ResponseMode,
    IReadOnlyList<AnswerChoiceView> DirectAnswerChoices,
    IReadOnlyList<AnswerChoiceView> AssistanceAnswerChoices,
    IReadOnlyList<string> AcceptedShortAnswers,
    bool LowInteractionEligible,
    LearningItemLifecycle Lifecycle,
    bool IsNew,
    DateTimeOffset DueAt,
    IReadOnlyList<Guid> DeckIds);

public sealed record HintView(string Text);

public sealed record AnswerChoiceView(string Text, bool IsCorrect);

public sealed record DeckView(Guid Id, string Name, IReadOnlyList<Guid> LearningItemIds);

public sealed class ContentNotFoundException : Exception
{
    public ContentNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class ContentValidationException : Exception
{
    public ContentValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
