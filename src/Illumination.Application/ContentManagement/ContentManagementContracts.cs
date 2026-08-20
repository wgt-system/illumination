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

public enum DeckLearningActivityProfile
{
    GeneralRecall,
    LanguageLearning,
    CodingProblemSolving,
    Geospatial,
}

public sealed record HintInput(string Text);

public sealed record AnswerChoiceInput(string Text, bool IsCorrect = false, string? Id = null);

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

public sealed record CreateDeckCommand(
    string Name,
    IReadOnlyList<string>? TopicLabels = null,
    IReadOnlyList<DeckLearningActivityProfile>? LearningActivityProfiles = null);

public sealed record RenameDeckCommand(string Name);

public sealed record SetDeckTopicLabelsCommand(IReadOnlyList<string> TopicLabels);

public sealed record SetDeckLearningActivityProfilesCommand(IReadOnlyList<DeckLearningActivityProfile> Profiles);

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

public sealed record AnswerChoiceView(string Text, bool IsCorrect, string Id = "");

public sealed record DeckView(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> LearningItemIds,
    IReadOnlyList<string> TopicLabels,
    IReadOnlyList<DeckLearningActivityProfile> LearningActivityProfiles)
{
    public DeckView(Guid id, string name, IReadOnlyList<Guid> learningItemIds)
        : this(id, name, learningItemIds, [], [])
    {
    }

    public DeckView(Guid id, string name, IReadOnlyList<Guid> learningItemIds, IReadOnlyList<string> topicLabels)
        : this(id, name, learningItemIds, topicLabels, [])
    {
    }
}

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
