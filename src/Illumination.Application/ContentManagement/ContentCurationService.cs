using Illumination.Domain.Identity;
using Illumination.Domain.Learning;

namespace Illumination.Application.ContentManagement;

public sealed class ContentCurationService
{
    private readonly IContentPersistence _contentPersistence;
    private readonly IUserFlagDefinitionPersistence _flagPersistence;

    public ContentCurationService(IContentPersistence contentPersistence, IUserFlagDefinitionPersistence flagPersistence)
    {
        _contentPersistence = contentPersistence ?? throw new ArgumentNullException(nameof(contentPersistence));
        _flagPersistence = flagPersistence ?? throw new ArgumentNullException(nameof(flagPersistence));
    }

    public async Task<UserFlagDefinitionView> CreateUserFlagDefinitionAsync(
        CreateUserFlagDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var definition = ExecuteDomain(() => UserFlagDefinition.Create(command.Name, command.Meaning));
        var snapshot = new UserFlagDefinitionSnapshot(definition.Id.Value, definition.Name, definition.Meaning);
        await _flagPersistence.SaveUserFlagDefinitionAsync(snapshot, cancellationToken);
        return ToView(snapshot);
    }

    public async Task<IReadOnlyList<UserFlagDefinitionView>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _flagPersistence.ListUserFlagDefinitionsAsync(cancellationToken);
        return definitions.Select(ToView).ToArray();
    }

    public async Task<CuratedLearningItemView> GetLearningItemCurationAsync(Guid learningItemId, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadItemAsync(learningItemId, cancellationToken);
        var item = loaded.Item;
        return ToView(item);
    }

    public async Task<IReadOnlyList<CuratedLearningItemView>> ListLearningItemsByFlagsAsync(
        IReadOnlyList<Guid> flagDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flagDefinitionIds);
        var requestedIds = flagDefinitionIds.Distinct().ToArray();
        var definitions = await _flagPersistence.ListUserFlagDefinitionsAsync(cancellationToken);
        if (requestedIds.Any(id => id == Guid.Empty || definitions.All(definition => definition.Id != id)))
        {
            throw new ContentValidationException("One or more User Flag Definitions were not found.", new ArgumentException("Unknown User Flag Definition."));
        }

        var items = await _contentPersistence.ListLearningItemsAsync(cancellationToken);
        return items
            .Where(item => requestedIds.All(id => (item.UserFlagDefinitionIds ?? []).Contains(id)))
            .Select(snapshot => ToView(ExecuteDomain(() => ToDomain(snapshot))))
            .ToArray();
    }

    public async Task<CuratedLearningItemView> AddFlagToLearningItemAsync(
        Guid learningItemId,
        Guid flagDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadItemAsync(learningItemId, cancellationToken);
        var item = loaded.Item;
        await RequireFlagDefinitionAsync(flagDefinitionId, cancellationToken);
        item.AddUserFlag(UserFlagDefinitionId.From(flagDefinitionId));
        await SaveItemAsync(item, loaded.Snapshot.DeckIds, cancellationToken);
        return ToView(item);
    }

    public async Task<CuratedLearningItemView> RemoveFlagFromLearningItemAsync(
        Guid learningItemId,
        Guid flagDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadItemAsync(learningItemId, cancellationToken);
        var item = loaded.Item;
        await RequireFlagDefinitionAsync(flagDefinitionId, cancellationToken);
        item.RemoveUserFlag(UserFlagDefinitionId.From(flagDefinitionId));
        await SaveItemAsync(item, loaded.Snapshot.DeckIds, cancellationToken);
        return ToView(item);
    }

    public async Task<CuratedLearningItemView> AcceptQualityReviewAsync(
        Guid learningItemId,
        AcceptQualityReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var loaded = await LoadItemAsync(learningItemId, cancellationToken);
        var item = loaded.Item;
        var review = ExecuteDomain(() => QualityReview.Create(
            item.Id,
            item.ContentRevision,
            ToDomain(command.Outcome),
            ToDomain(command.EvidenceType),
            command.Findings,
            command.SuggestedCorrection));
        var supersededIds = command.SupersededReviewIds?.Select(QualityReviewId.From).ToArray();
        ExecuteDomain(() => item.AcceptQualityReview(review, supersededIds));
        await SaveItemAsync(item, loaded.Snapshot.DeckIds, cancellationToken);
        return ToView(item);
    }

    private async Task<(LearningItem Item, LearningItemSnapshot Snapshot)> LoadItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _contentPersistence.FindLearningItemAsync(id, cancellationToken)
            ?? throw new ContentNotFoundException($"Learning Item '{id}' was not found.");
        return (ExecuteDomain(() => ToDomain(snapshot)), snapshot);
    }

    private async Task RequireFlagDefinitionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || (await _flagPersistence.ListUserFlagDefinitionsAsync(cancellationToken)).All(definition => definition.Id != id))
        {
            throw new ContentValidationException("User Flag Definition was not found.", new ArgumentException("Unknown User Flag Definition."));
        }
    }

    private async Task SaveItemAsync(LearningItem item, IReadOnlyList<Guid> deckIds, CancellationToken cancellationToken) =>
        await _contentPersistence.SaveLearningItemAsync(ToSnapshot(item) with { DeckIds = deckIds }, cancellationToken);

    private static LearningItem ToDomain(LearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id), snapshot.Prompt, snapshot.ReferenceSolution, snapshot.DueAt, snapshot.IsNew,
        ToDomain(snapshot.ResponseMode), snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)), snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible, ToDomain(snapshot.Lifecycle), snapshot.Difficulty, snapshot.StabilityDays,
        snapshot.IsInShortTermRelearning, snapshot.ContentRevision,
        (snapshot.QualityReviews ?? []).Select(x => QualityReview.Restore(
            QualityReviewId.From(x.Id), LearningItemId.From(x.LearningItemId), x.ContentRevision,
            ToDomain(x.Outcome), ToDomain(x.EvidenceType), x.Findings, x.SuggestedCorrection,
            x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)),
        (snapshot.UserFlagDefinitionIds ?? []).Select(UserFlagDefinitionId.From));

    private static LearningItemSnapshot ToSnapshot(LearningItem item) => new(
        item.Id.Value, item.Prompt, item.ReferenceSolution.Content, ToApplication(item.ResponseMode),
        item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(),
        item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(),
        item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(),
        item.AcceptedShortAnswers.ToArray(), item.LowInteractionEligible, ToApplication(item.LifecycleState),
        item.LearningState.IsNew, item.LearningState.DueAt, item.LearningState.Difficulty, item.LearningState.StabilityDays,
        item.LearningState.IsInShortTermRelearning, [], item.ContentRevision,
        item.QualityReviews.Select(x => new QualityReviewSnapshot(
            x.Id.Value, x.LearningItemId.Value, x.ContentRevision, ToApplication(x.Outcome), ToApplication(x.EvidenceType),
            x.Findings, x.SuggestedCorrection, x.SupersededBy?.Value)).ToArray(),
        item.UserFlagDefinitionIds.Select(x => x.Value).ToArray());

    private static CuratedLearningItemView ToView(LearningItem item) => new(
        item.Id.Value, item.Prompt, ToApplication(item.LifecycleState), item.ContentRevision, item.UserFlagDefinitionIds.Select(x => x.Value).ToArray(),
        item.QualityReviews.Select(ToView).ToArray(),
        item.CurrentQualityState is null ? null : new CurrentQualityStateView(ToCuration(item.CurrentQualityState.Outcome)));

    private static QualityReviewView ToView(QualityReview review) => new(
        review.Id.Value, review.LearningItemId.Value, review.ContentRevision, ToCuration(review.Outcome), ToCuration(review.EvidenceType),
        review.Findings, review.SuggestedCorrection, review.SupersededBy?.Value);

    private static UserFlagDefinitionView ToView(UserFlagDefinitionSnapshot definition) => new(definition.Id, definition.Name, definition.Meaning);

    private static T ExecuteDomain<T>(Func<T> action)
    {
        try { return action(); }
        catch (ArgumentException exception) { throw new ContentValidationException(exception.Message, exception); }
        catch (InvalidOperationException exception) { throw new ContentValidationException(exception.Message, exception); }
    }

    private static void ExecuteDomain(Action action) => ExecuteDomain(() => { action(); return true; });

    private static ResponseMode ToDomain(LearningItemResponseMode value) => value switch
    {
        LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed,
        LearningItemResponseMode.Selection => ResponseMode.Selection,
        LearningItemResponseMode.ShortText => ResponseMode.ShortText,
        LearningItemResponseMode.Code => ResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported response mode."),
    };

    private static LearningItemLifecycleState ToDomain(LearningItemLifecycle value) => value switch
    {
        LearningItemLifecycle.Active => LearningItemLifecycleState.Active,
        LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended,
        LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported lifecycle."),
    };

    private static QualityReviewOutcome ToDomain(QualityReviewOutcomeSnapshot value) => value switch
    {
        QualityReviewOutcomeSnapshot.Pass => QualityReviewOutcome.Pass,
        QualityReviewOutcomeSnapshot.Warning => QualityReviewOutcome.Warning,
        QualityReviewOutcomeSnapshot.NeedsReview => QualityReviewOutcome.NeedsReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported quality outcome."),
    };

    private static QualityReviewEvidenceType ToDomain(QualityReviewEvidenceTypeSnapshot value) => value switch
    {
        QualityReviewEvidenceTypeSnapshot.ModelReview => QualityReviewEvidenceType.ModelReview,
        QualityReviewEvidenceTypeSnapshot.SourceGroundedReview => QualityReviewEvidenceType.SourceGroundedReview,
        QualityReviewEvidenceTypeSnapshot.UserReview => QualityReviewEvidenceType.UserReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported evidence type."),
    };

    private static QualityReviewOutcome ToDomain(CurationQualityReviewOutcome value) => value switch
    {
        CurationQualityReviewOutcome.Pass => QualityReviewOutcome.Pass,
        CurationQualityReviewOutcome.Warning => QualityReviewOutcome.Warning,
        CurationQualityReviewOutcome.NeedsReview => QualityReviewOutcome.NeedsReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported quality outcome."),
    };

    private static QualityReviewEvidenceType ToDomain(CurationQualityReviewEvidenceType value) => value switch
    {
        CurationQualityReviewEvidenceType.ModelReview => QualityReviewEvidenceType.ModelReview,
        CurationQualityReviewEvidenceType.SourceGroundedReview => QualityReviewEvidenceType.SourceGroundedReview,
        CurationQualityReviewEvidenceType.UserReview => QualityReviewEvidenceType.UserReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported evidence type."),
    };

    private static LearningItemResponseMode ToApplication(ResponseMode value) => value switch
    {
        ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed,
        ResponseMode.Selection => LearningItemResponseMode.Selection,
        ResponseMode.ShortText => LearningItemResponseMode.ShortText,
        ResponseMode.Code => LearningItemResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported response mode."),
    };

    private static LearningItemLifecycle ToApplication(LearningItemLifecycleState value) => value switch
    {
        LearningItemLifecycleState.Active => LearningItemLifecycle.Active,
        LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended,
        LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported lifecycle."),
    };

    private static QualityReviewOutcomeSnapshot ToApplication(QualityReviewOutcome value) => value switch
    {
        QualityReviewOutcome.Pass => QualityReviewOutcomeSnapshot.Pass,
        QualityReviewOutcome.Warning => QualityReviewOutcomeSnapshot.Warning,
        QualityReviewOutcome.NeedsReview => QualityReviewOutcomeSnapshot.NeedsReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported quality outcome."),
    };

    private static QualityReviewEvidenceTypeSnapshot ToApplication(QualityReviewEvidenceType value) => value switch
    {
        QualityReviewEvidenceType.ModelReview => QualityReviewEvidenceTypeSnapshot.ModelReview,
        QualityReviewEvidenceType.SourceGroundedReview => QualityReviewEvidenceTypeSnapshot.SourceGroundedReview,
        QualityReviewEvidenceType.UserReview => QualityReviewEvidenceTypeSnapshot.UserReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported evidence type."),
    };

    private static CurationQualityReviewOutcome ToCuration(QualityReviewOutcome value) => value switch
    {
        QualityReviewOutcome.Pass => CurationQualityReviewOutcome.Pass,
        QualityReviewOutcome.Warning => CurationQualityReviewOutcome.Warning,
        QualityReviewOutcome.NeedsReview => CurationQualityReviewOutcome.NeedsReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported quality outcome."),
    };

    private static CurationQualityReviewEvidenceType ToCuration(QualityReviewEvidenceType value) => value switch
    {
        QualityReviewEvidenceType.ModelReview => CurationQualityReviewEvidenceType.ModelReview,
        QualityReviewEvidenceType.SourceGroundedReview => CurationQualityReviewEvidenceType.SourceGroundedReview,
        QualityReviewEvidenceType.UserReview => CurationQualityReviewEvidenceType.UserReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported evidence type."),
    };

}
