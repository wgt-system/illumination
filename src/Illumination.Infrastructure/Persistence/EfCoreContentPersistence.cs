using Illumination.Application.ContentManagement;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreContentPersistence : IContentPersistence, IUserFlagDefinitionPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreContentPersistence(IDbContextFactory<IlluminationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<LearningItemSnapshot>> ListLearningItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await LoadLearningItems(context)
            .OrderBy(x => x.Prompt)
            .ThenBy(x => x.LearningItemId)
            .ToArrayAsync(cancellationToken);
        return records.Select(ToSnapshot).ToArray();
    }

    public async Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await LoadLearningItems(context)
            .SingleOrDefaultAsync(x => x.LearningItemId == id, cancellationToken);

        return record is null ? null : ToSnapshot(record);
    }

    public async Task<IReadOnlyList<UserFlagDefinitionSnapshot>> ListUserFlagDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.UserFlagDefinitions.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.UserFlagDefinitionId)
            .Select(x => new UserFlagDefinitionSnapshot(x.UserFlagDefinitionId, x.Name, x.Meaning)).ToArrayAsync(cancellationToken);
    }

    public async Task SaveUserFlagDefinitionAsync(UserFlagDefinitionSnapshot definition, CancellationToken cancellationToken = default)
    {
        var domain = UserFlagDefinition.Create(UserFlagDefinitionId.From(definition.Id), definition.Name, definition.Meaning);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.UserFlagDefinitions.SingleOrDefaultAsync(x => x.UserFlagDefinitionId == domain.Id.Value, cancellationToken);
        if (existing is null)
            context.UserFlagDefinitions.Add(new UserFlagDefinitionRecord { UserFlagDefinitionId = domain.Id.Value, Name = domain.Name, Meaning = domain.Meaning });
        else { existing.Name = domain.Name; existing.Meaning = domain.Meaning; }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeckSnapshot>> ListDecksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.Decks
            .AsNoTracking()
            .Include(x => x.Memberships)
            .Include(x => x.TopicLabels)
            .Include(x => x.LearningActivityProfiles)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.DeckId)
            .ToArrayAsync(cancellationToken);
        return records.Select(ToSnapshot).ToArray();
    }

    public async Task SaveLearningItemAsync(LearningItemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var domainItem = ToDomain(snapshot);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.LearningItems
            .Include(x => x.Hints)
            .Include(x => x.AnswerChoices)
            .Include(x => x.AcceptedShortAnswers)
            .Include(x => x.QualityReviews)
            .Include(x => x.UserFlagAssignments)
            .SingleOrDefaultAsync(x => x.LearningItemId == snapshot.Id, cancellationToken);

        var replacement = DomainPersistenceMapper.ToRecord(domainItem);
        if (existing is null)
        {
            context.LearningItems.Add(replacement);
        }
        else
        {
            existing.Prompt = replacement.Prompt;
            existing.ReferenceSolutionContent = replacement.ReferenceSolutionContent;
            existing.ResponseMode = replacement.ResponseMode;
            existing.LowInteractionEligible = replacement.LowInteractionEligible;
            existing.LifecycleState = replacement.LifecycleState;
            existing.IsNew = replacement.IsNew;
            existing.DueAt = replacement.DueAt;
            existing.Difficulty = replacement.Difficulty;
            existing.StabilityDays = replacement.StabilityDays;
            existing.IsInShortTermRelearning = replacement.IsInShortTermRelearning;
            existing.ContentRevision = replacement.ContentRevision;

            context.Hints.RemoveRange(existing.Hints);
            context.AnswerChoices.RemoveRange(existing.AnswerChoices);
            context.AcceptedShortAnswers.RemoveRange(existing.AcceptedShortAnswers);
            existing.Hints.Clear();
            existing.AnswerChoices.Clear();
            existing.AcceptedShortAnswers.Clear();
            existing.Hints.AddRange(replacement.Hints);
            existing.AnswerChoices.AddRange(replacement.AnswerChoices);
            existing.AcceptedShortAnswers.AddRange(replacement.AcceptedShortAnswers);
            context.QualityReviews.RemoveRange(existing.QualityReviews);
            context.LearningItemUserFlags.RemoveRange(existing.UserFlagAssignments);
            existing.QualityReviews.Clear();
            existing.UserFlagAssignments.Clear();
            existing.QualityReviews.AddRange(replacement.QualityReviews);
            existing.UserFlagAssignments.AddRange(replacement.UserFlagAssignments);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var item = await context.LearningItems.SingleOrDefaultAsync(x => x.LearningItemId == id, cancellationToken);
        if (item is null) return;
        context.StudySessionQueue.RemoveRange(await context.StudySessionQueue.Where(x => x.LearningItemId == id).ToArrayAsync(cancellationToken));
        context.DeckLearningItems.RemoveRange(await context.DeckLearningItems.Where(x => x.LearningItemId == id).ToArrayAsync(cancellationToken));
        context.Reviews.RemoveRange(await context.Reviews.Where(x => x.LearningItemId == id).ToArrayAsync(cancellationToken));
        context.QualityReviews.RemoveRange(await context.QualityReviews.Where(x => x.LearningItemId == id).ToArrayAsync(cancellationToken));
        context.LearningItemUserFlags.RemoveRange(await context.LearningItemUserFlags.Where(x => x.LearningItemId == id).ToArrayAsync(cancellationToken));
        context.LearningItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.Decks
            .AsNoTracking()
            .Include(x => x.Memberships)
            .Include(x => x.TopicLabels)
            .Include(x => x.LearningActivityProfiles)
            .SingleOrDefaultAsync(x => x.DeckId == id, cancellationToken);

        return record is null ? null : ToSnapshot(record);
    }

    public async Task SaveDeckAsync(DeckSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var domainDeck = ToDomain(snapshot);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Decks
            .Include(x => x.Memberships)
            .Include(x => x.TopicLabels)
            .Include(x => x.LearningActivityProfiles)
            .SingleOrDefaultAsync(x => x.DeckId == snapshot.Id, cancellationToken);

        var replacement = DomainPersistenceMapper.ToRecord(domainDeck);
        if (existing is null)
        {
            context.Decks.Add(replacement);
        }
        else
        {
            existing.Name = replacement.Name;
            context.DeckLearningItems.RemoveRange(existing.Memberships);
            context.DeckTopicLabels.RemoveRange(existing.TopicLabels);
            context.DeckLearningActivityProfiles.RemoveRange(existing.LearningActivityProfiles);
            existing.Memberships.Clear();
            existing.TopicLabels.Clear();
            existing.LearningActivityProfiles.Clear();
            existing.Memberships.AddRange(replacement.Memberships);
            existing.TopicLabels.AddRange(replacement.TopicLabels);
            existing.LearningActivityProfiles.AddRange(replacement.LearningActivityProfiles);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDeckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var deck = await context.Decks.SingleOrDefaultAsync(x => x.DeckId == id, cancellationToken);
        if (deck is not null)
        {
            context.Decks.Remove(deck);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static IQueryable<LearningItemRecord> LoadLearningItems(IlluminationDbContext context) =>
        context.LearningItems
            .AsNoTracking()
            .Include(x => x.Hints)
            .Include(x => x.AnswerChoices)
            .Include(x => x.AcceptedShortAnswers)
            .Include(x => x.DeckMemberships)
            .Include(x => x.QualityReviews)
            .Include(x => x.UserFlagAssignments);

    private static LearningItemSnapshot ToSnapshot(LearningItemRecord record) => new(
        record.LearningItemId,
        record.Prompt,
        record.ReferenceSolutionContent,
        ToApplication(record.ResponseMode),
        record.Hints.OrderBy(x => x.Position).Select(x => new HintSnapshot(x.Text)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value).ToArray(),
        record.LowInteractionEligible,
        ToApplication(record.LifecycleState),
        record.IsNew,
        record.DueAt,
        record.Difficulty,
        record.StabilityDays,
        record.IsInShortTermRelearning,
        record.DeckMemberships.Select(x => x.DeckId).Distinct().ToArray(), record.ContentRevision,
        record.QualityReviews.OrderBy(x => x.QualityReviewId).Select(x => new QualityReviewSnapshot(
            x.QualityReviewId, x.LearningItemId, x.ContentRevision, (QualityReviewOutcomeSnapshot)x.Outcome,
            (QualityReviewEvidenceTypeSnapshot)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy)).ToArray(),
        record.UserFlagAssignments.Select(x => x.UserFlagDefinitionId).Distinct().ToArray());

    private static DeckSnapshot ToSnapshot(DeckRecord record) => new(
        record.DeckId,
        record.Name,
        record.Memberships.Select(x => x.LearningItemId).Distinct().ToArray(),
        record.TopicLabels
            .Select(x => x.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        record.LearningActivityProfiles
            .Select(x => ToApplication(x.Profile))
            .Distinct()
            .OrderBy(x => x)
            .ToArray());

    private static LearningItem ToDomain(LearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id),
        snapshot.Prompt,
        snapshot.ReferenceSolution,
        snapshot.DueAt,
        snapshot.IsNew,
        ToDomain(snapshot.ResponseMode),
        snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)),
        snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible,
        ToDomain(snapshot.Lifecycle), snapshot.Difficulty, snapshot.StabilityDays, snapshot.IsInShortTermRelearning,
        snapshot.ContentRevision,
        (snapshot.QualityReviews ?? []).Select(x => QualityReview.Restore(
            QualityReviewId.From(x.Id), LearningItemId.From(x.LearningItemId), x.ContentRevision,
            (Illumination.Domain.Learning.QualityReviewOutcome)x.Outcome, (Illumination.Domain.Learning.QualityReviewEvidenceType)x.EvidenceType,
            x.Findings, x.SuggestedCorrection, x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)),
        (snapshot.UserFlagDefinitionIds ?? []).Select(UserFlagDefinitionId.From));

    private static Deck ToDomain(DeckSnapshot snapshot)
    {
        var deck = Deck.Create(
            DeckId.From(snapshot.Id),
            snapshot.Name,
            snapshot.TopicLabels,
            snapshot.LearningActivityProfiles.Select(ToDomain));
        foreach (var learningItemId in snapshot.LearningItemIds)
        {
            deck.AddLearningItem(LearningItemId.From(learningItemId));
        }

        return deck;
    }

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

    private static DeckLearningActivityProfile ToApplication(LearningActivityProfile profile) => profile switch
    {
        LearningActivityProfile.GeneralRecall => DeckLearningActivityProfile.GeneralRecall,
        LearningActivityProfile.LanguageLearning => DeckLearningActivityProfile.LanguageLearning,
        LearningActivityProfile.CodingProblemSolving => DeckLearningActivityProfile.CodingProblemSolving,
        LearningActivityProfile.Geospatial => DeckLearningActivityProfile.Geospatial,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported Domain Deck learning activity profile."),
    };

    private static ResponseMode ToDomain(LearningItemResponseMode mode) => mode switch
    {
        LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed,
        LearningItemResponseMode.Selection => ResponseMode.Selection,
        LearningItemResponseMode.ShortText => ResponseMode.ShortText,
        LearningItemResponseMode.Code => ResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Application response mode."),
    };

    private static LearningItemLifecycleState ToDomain(LearningItemLifecycle lifecycle) => lifecycle switch
    {
        LearningItemLifecycle.Active => LearningItemLifecycleState.Active,
        LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended,
        LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unsupported Application lifecycle."),
    };

    private static LearningActivityProfile ToDomain(DeckLearningActivityProfile profile) => profile switch
    {
        DeckLearningActivityProfile.GeneralRecall => LearningActivityProfile.GeneralRecall,
        DeckLearningActivityProfile.LanguageLearning => LearningActivityProfile.LanguageLearning,
        DeckLearningActivityProfile.CodingProblemSolving => LearningActivityProfile.CodingProblemSolving,
        DeckLearningActivityProfile.Geospatial => LearningActivityProfile.Geospatial,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported Application Deck learning activity profile."),
    };
}
