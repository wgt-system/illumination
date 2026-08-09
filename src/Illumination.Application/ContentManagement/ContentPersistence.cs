namespace Illumination.Application.ContentManagement;

public interface IContentPersistence
{
    Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveLearningItemAsync(LearningItemSnapshot item, CancellationToken cancellationToken = default);

    Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveDeckAsync(DeckSnapshot deck, CancellationToken cancellationToken = default);

    Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record LearningItemSnapshot(
    Guid Id,
    string Prompt,
    string ReferenceSolution,
    LearningItemResponseMode ResponseMode,
    IReadOnlyList<HintSnapshot> Hints,
    IReadOnlyList<AnswerChoiceSnapshot> DirectAnswerChoices,
    IReadOnlyList<AnswerChoiceSnapshot> AssistanceAnswerChoices,
    IReadOnlyList<string> AcceptedShortAnswers,
    bool LowInteractionEligible,
    LearningItemLifecycle Lifecycle,
    bool IsNew,
    DateTimeOffset DueAt,
    IReadOnlyList<Guid> DeckIds);

public sealed record HintSnapshot(string Text);

public sealed record AnswerChoiceSnapshot(string Text, bool IsCorrect);

public sealed record DeckSnapshot(Guid Id, string Name, IReadOnlyList<Guid> LearningItemIds);
