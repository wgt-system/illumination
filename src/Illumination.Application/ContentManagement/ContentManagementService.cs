using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;

namespace Illumination.Application.ContentManagement;

public sealed class ContentManagementService
{
    private readonly IContentPersistence _persistence;
    private readonly TimeProvider _timeProvider;

    public ContentManagementService(IContentPersistence persistence, TimeProvider timeProvider)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LearningItemView> CreateLearningItemAsync(
        CreateLearningItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = ExecuteDomain(() => LearningItem.Create(
            command.Prompt,
            command.ReferenceSolution,
            _timeProvider.GetUtcNow(),
            ToDomain(command.ResponseMode),
            ToDomainHints(command.Hints),
            ToDomainChoices(command.DirectAnswerChoices),
            ToDomainChoices(command.AssistanceAnswerChoices),
            command.AcceptedShortAnswers,
            command.LowInteractionEligible));

        await _persistence.SaveLearningItemAsync(ToSnapshot(item), cancellationToken);
        return ToView(item, []);
    }

    public async Task<IReadOnlyList<LearningItemView>> ListLearningItemsAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _persistence.ListLearningItemsAsync(cancellationToken);
        return snapshots.Select(ToView).ToArray();
    }

    public async Task<LearningItemView> GetLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _persistence.FindLearningItemAsync(id, cancellationToken)
            ?? throw NotFound("Learning Item", id);
        return ToView(snapshot);
    }

    public async Task<LearningItemView> UpdateLearningItemAsync(
        Guid id,
        UpdateLearningItemCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var snapshot = await LoadLearningItemSnapshotAsync(id, cancellationToken);
        var item = ExecuteDomain(() => ToDomain(snapshot));

        ExecuteDomain(() =>
        {
            item.UpdateContent(
                command.Prompt,
                command.ReferenceSolution,
                ToDomain(command.ResponseMode),
                ToDomainHints(command.Hints),
                ToDomainChoices(command.DirectAnswerChoices),
                ToDomainChoices(command.AssistanceAnswerChoices),
                command.AcceptedShortAnswers);
            item.ChangeLowInteractionEligibility(command.LowInteractionEligible);
        });

        await _persistence.SaveLearningItemAsync(ToSnapshot(item) with { DeckIds = snapshot.DeckIds }, cancellationToken);
        return ToView(item, itemSnapshotDeckIds: snapshot.DeckIds);
    }

    public Task SuspendLearningItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, item => item.Suspend(), cancellationToken);

    public Task ReactivateLearningItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, item => item.Reactivate(_timeProvider.GetUtcNow()), cancellationToken);

    public Task MarkLearningItemMasteredAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, item => item.MarkMastered(), cancellationToken);

    public Task UnmarkLearningItemMasteredAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, item => item.UnmarkMastered(_timeProvider.GetUtcNow()), cancellationToken);

    public async Task<DeckView> CreateDeckAsync(
        CreateDeckCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var deck = ExecuteDomain(() => Deck.Create(command.Name));
        await _persistence.SaveDeckAsync(ToSnapshot(deck), cancellationToken);
        return ToView(deck);
    }

    public async Task<IReadOnlyList<DeckView>> ListDecksAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _persistence.ListDecksAsync(cancellationToken);
        return snapshots.Select(ToView).ToArray();
    }

    public async Task<DeckView> GetDeckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _persistence.FindDeckAsync(id, cancellationToken)
            ?? throw NotFound("Deck", id);
        return ToView(snapshot);
    }

    public async Task<DeckView> RenameDeckAsync(
        Guid id,
        RenameDeckCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var deck = await LoadDeckAsync(id, cancellationToken);
        ExecuteDomain(() => deck.Rename(command.Name));
        await _persistence.SaveDeckAsync(ToSnapshot(deck), cancellationToken);
        return ToView(deck);
    }

    public async Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = await LoadDeckAsync(id, cancellationToken);
        await _persistence.DeleteDeckAsync(id, cancellationToken);
    }

    public async Task<DeckView> AddLearningItemToDeckAsync(
        Guid deckId,
        Guid learningItemId,
        CancellationToken cancellationToken = default)
    {
        _ = await LoadLearningItemAsync(learningItemId, cancellationToken);
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        deck.AddLearningItem(LearningItemId.From(learningItemId));
        await _persistence.SaveDeckAsync(ToSnapshot(deck), cancellationToken);
        return ToView(deck);
    }

    public async Task<DeckView> RemoveLearningItemFromDeckAsync(
        Guid deckId,
        Guid learningItemId,
        CancellationToken cancellationToken = default)
    {
        _ = await LoadLearningItemAsync(learningItemId, cancellationToken);
        var deck = await LoadDeckAsync(deckId, cancellationToken);
        deck.RemoveLearningItem(LearningItemId.From(learningItemId));
        await _persistence.SaveDeckAsync(ToSnapshot(deck), cancellationToken);
        return ToView(deck);
    }

    private async Task<LearningItem> LoadLearningItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await LoadLearningItemSnapshotAsync(id, cancellationToken);
        return ExecuteDomain(() => ToDomain(snapshot));
    }

    private async Task<LearningItemSnapshot> LoadLearningItemSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _persistence.FindLearningItemAsync(id, cancellationToken)
            ?? throw NotFound("Learning Item", id);
    }

    private async Task<Deck> LoadDeckAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _persistence.FindDeckAsync(id, cancellationToken)
            ?? throw NotFound("Deck", id);
        return ExecuteDomain(() => ToDomain(snapshot));
    }

    private async Task ChangeLifecycleAsync(
        Guid id,
        Action<LearningItem> transition,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadLearningItemSnapshotAsync(id, cancellationToken);
        var item = ExecuteDomain(() => ToDomain(snapshot));
        ExecuteDomain(() => transition(item));
        await _persistence.SaveLearningItemAsync(ToSnapshot(item) with { DeckIds = snapshot.DeckIds }, cancellationToken);
    }

    private static T ExecuteDomain<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException exception)
        {
            throw new ContentValidationException(exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new ContentValidationException(exception.Message, exception);
        }
    }

    private static void ExecuteDomain(Action action) => ExecuteDomain(() =>
    {
        action();
        return true;
    });

    private static ContentNotFoundException NotFound(string type, Guid id) =>
        new($"{type} '{id}' was not found.");

    private static LearningItem ToDomain(LearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id),
        snapshot.Prompt,
        snapshot.ReferenceSolution,
        snapshot.DueAt,
        snapshot.IsNew,
        ToDomain(snapshot.ResponseMode),
        snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
        snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible,
        ToDomain(snapshot.Lifecycle), snapshot.Difficulty, snapshot.StabilityDays, snapshot.IsInShortTermRelearning,
        snapshot.ContentRevision,
        (snapshot.QualityReviews ?? []).Select(x => QualityReview.Restore(
            QualityReviewId.From(x.Id), LearningItemId.From(x.LearningItemId), x.ContentRevision,
            (QualityReviewOutcome)x.Outcome, (QualityReviewEvidenceType)x.EvidenceType,
            x.Findings, x.SuggestedCorrection, x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)),
        (snapshot.UserFlagDefinitionIds ?? []).Select(UserFlagDefinitionId.From));

    private static Deck ToDomain(DeckSnapshot snapshot)
    {
        var deck = Deck.Create(DeckId.From(snapshot.Id), snapshot.Name);
        foreach (var learningItemId in snapshot.LearningItemIds)
        {
            deck.AddLearningItem(LearningItemId.From(learningItemId));
        }

        return deck;
    }

    private static LearningItemSnapshot ToSnapshot(LearningItem item) => new(
        item.Id.Value,
        item.Prompt,
        item.ReferenceSolution.Content,
        ToApplication(item.ResponseMode),
        item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(),
        item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        item.AcceptedShortAnswers.ToArray(),
        item.LowInteractionEligible,
        ToApplication(item.LifecycleState),
        item.LearningState.IsNew,
        item.LearningState.DueAt,
        item.LearningState.Difficulty,
        item.LearningState.StabilityDays,
        item.LearningState.IsInShortTermRelearning,
        [], item.ContentRevision,
        item.QualityReviews.Select(x => new QualityReviewSnapshot(
            x.Id.Value, x.LearningItemId.Value, x.ContentRevision, (QualityReviewOutcomeSnapshot)x.Outcome,
            (QualityReviewEvidenceTypeSnapshot)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy?.Value)).ToArray(),
        item.UserFlagDefinitionIds.Select(x => x.Value).ToArray());

    private static DeckSnapshot ToSnapshot(Deck deck) => new(
        deck.Id.Value,
        deck.Name,
        deck.LearningItemIds.Select(x => x.Value).ToArray());

    private static LearningItemView ToView(LearningItemSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Prompt,
        snapshot.ReferenceSolution,
        snapshot.Hints.Select(x => new HintView(x.Text)).ToArray(),
        snapshot.ResponseMode,
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoiceView(x.Text, x.IsCorrect)).ToArray(),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoiceView(x.Text, x.IsCorrect)).ToArray(),
        snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible,
        snapshot.Lifecycle,
        snapshot.IsNew,
        snapshot.DueAt,
        snapshot.DeckIds);

    private static LearningItemView ToView(LearningItem item, IReadOnlyList<Guid> itemSnapshotDeckIds) => ToView(ToSnapshot(item) with { DeckIds = itemSnapshotDeckIds });

    private static DeckView ToView(DeckSnapshot snapshot) => new(snapshot.Id, snapshot.Name, snapshot.LearningItemIds);

    private static DeckView ToView(Deck deck) => new(deck.Id.Value, deck.Name, deck.LearningItemIds.Select(x => x.Value).ToArray());

    private static ResponseMode ToDomain(LearningItemResponseMode mode) => mode switch
    {
        LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed,
        LearningItemResponseMode.Selection => ResponseMode.Selection,
        LearningItemResponseMode.ShortText => ResponseMode.ShortText,
        LearningItemResponseMode.Code => ResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Learning Item response mode."),
    };

    private static LearningItemLifecycleState ToDomain(LearningItemLifecycle lifecycle) => lifecycle switch
    {
        LearningItemLifecycle.Active => LearningItemLifecycleState.Active,
        LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended,
        LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unsupported Learning Item lifecycle."),
    };

    private static LearningItemResponseMode ToApplication(ResponseMode mode) => mode switch
    {
        ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed,
        ResponseMode.Selection => LearningItemResponseMode.Selection,
        ResponseMode.ShortText => LearningItemResponseMode.ShortText,
        ResponseMode.Code => LearningItemResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Domain response mode."),
    };

    private static LearningItemLifecycle ToApplication(LearningItemLifecycleState lifecycle) => lifecycle switch
    {
        LearningItemLifecycleState.Active => LearningItemLifecycle.Active,
        LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended,
        LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unsupported Domain lifecycle."),
    };

    private static IEnumerable<Hint>? ToDomainHints(IReadOnlyList<HintInput>? hints) => hints?.Select(x => new Hint(x.Text));

    private static IEnumerable<AnswerChoice>? ToDomainChoices(IReadOnlyList<AnswerChoiceInput>? choices) => choices?.Select(x => new AnswerChoice(x.Text, x.IsCorrect));
}
