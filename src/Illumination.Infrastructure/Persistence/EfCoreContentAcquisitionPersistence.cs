using Illumination.Application.ContentAcquisition;
using Illumination.Application.ContentManagement;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreContentAcquisitionPersistence : IContentAcquisitionPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;
    public EfCoreContentAcquisitionPersistence(IDbContextFactory<IlluminationDbContext> contextFactory) => _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.LearningItems.AsNoTracking().Include(x => x.Hints).Include(x => x.AnswerChoices).Include(x => x.AcceptedShortAnswers).Include(x => x.DeckMemberships).Include(x => x.QualityReviews).Include(x => x.UserFlagAssignments).OrderBy(x => x.LearningItemId).ToArrayAsync(cancellationToken);
        return records.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.Decks.AsNoTracking().Include(x => x.Memberships).OrderBy(x => x.DeckId).ToArrayAsync(cancellationToken);
        return records.Select(x => new DeckSnapshot(x.DeckId, x.Name, x.Memberships.Select(m => m.LearningItemId).ToArray())).ToArray();
    }

    public async Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        foreach (var itemSnapshot in snapshot.LearningItems) await SaveItemAsync(context, itemSnapshot, cancellationToken);
        foreach (var deckSnapshot in snapshot.Decks) await SaveDeckAsync(context, deckSnapshot, cancellationToken);
        var provenance = snapshot.Provenance;
        context.ImportProvenance.Add(new ImportProvenanceRecord { ImportBatchId = provenance.ImportBatchId, ImportedAt = provenance.ImportedAt, Contract = provenance.Contract, Version = provenance.Version, ExternalBundleId = provenance.ExternalBundleId, GeneratedFor = provenance.GeneratedFor, AcceptedOperationCount = provenance.AcceptedOperationCount, CreatedLearningItemCount = provenance.CreatedLearningItemCount, UpdatedLearningItemCount = provenance.UpdatedLearningItemCount, CreatedDeckCount = provenance.CreatedDeckCount, UpdatedDeckCount = provenance.UpdatedDeckCount, AssignmentCount = provenance.AssignmentCount });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SaveItemAsync(IlluminationDbContext context, LearningItemSnapshot snapshot, CancellationToken cancellationToken)
    {
        var domain = ToDomain(snapshot); var replacement = DomainPersistenceMapper.ToRecord(domain);
        var existing = await context.LearningItems.Include(x => x.Hints).Include(x => x.AnswerChoices).Include(x => x.AcceptedShortAnswers).Include(x => x.QualityReviews).Include(x => x.UserFlagAssignments).SingleOrDefaultAsync(x => x.LearningItemId == snapshot.Id, cancellationToken);
        if (existing is null) context.LearningItems.Add(replacement);
        else
        {
            existing.Prompt = replacement.Prompt; existing.ReferenceSolutionContent = replacement.ReferenceSolutionContent; existing.ResponseMode = replacement.ResponseMode; existing.LowInteractionEligible = replacement.LowInteractionEligible; existing.LifecycleState = replacement.LifecycleState; existing.IsNew = replacement.IsNew; existing.DueAt = replacement.DueAt; existing.Difficulty = replacement.Difficulty; existing.StabilityDays = replacement.StabilityDays; existing.IsInShortTermRelearning = replacement.IsInShortTermRelearning;
            existing.ContentRevision = replacement.ContentRevision;
            context.Hints.RemoveRange(existing.Hints); context.AnswerChoices.RemoveRange(existing.AnswerChoices); context.AcceptedShortAnswers.RemoveRange(existing.AcceptedShortAnswers); existing.Hints.Clear(); existing.AnswerChoices.Clear(); existing.AcceptedShortAnswers.Clear(); existing.Hints.AddRange(replacement.Hints); existing.AnswerChoices.AddRange(replacement.AnswerChoices); existing.AcceptedShortAnswers.AddRange(replacement.AcceptedShortAnswers);
            context.QualityReviews.RemoveRange(existing.QualityReviews); context.LearningItemUserFlags.RemoveRange(existing.UserFlagAssignments); existing.QualityReviews.Clear(); existing.UserFlagAssignments.Clear(); existing.QualityReviews.AddRange(replacement.QualityReviews); existing.UserFlagAssignments.AddRange(replacement.UserFlagAssignments);
        }
    }

    private static async Task SaveDeckAsync(IlluminationDbContext context, DeckSnapshot snapshot, CancellationToken cancellationToken)
    {
        var existing = await context.Decks.Include(x => x.Memberships).SingleOrDefaultAsync(x => x.DeckId == snapshot.Id, cancellationToken);
        if (existing is null) { var deck = new DeckRecord { DeckId = snapshot.Id, Name = snapshot.Name }; deck.Memberships.AddRange(snapshot.LearningItemIds.Distinct().Select(id => new DeckLearningItemRecord { DeckId = snapshot.Id, LearningItemId = id })); context.Decks.Add(deck); }
        else { existing.Name = snapshot.Name; context.DeckLearningItems.RemoveRange(existing.Memberships); existing.Memberships.Clear(); existing.Memberships.AddRange(snapshot.LearningItemIds.Distinct().Select(id => new DeckLearningItemRecord { DeckId = snapshot.Id, LearningItemId = id })); }
    }

    private static LearningItemSnapshot ToSnapshot(LearningItemRecord record) => new(
        record.LearningItemId, record.Prompt, record.ReferenceSolutionContent,
        record.ResponseMode switch { ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed, ResponseMode.Selection => LearningItemResponseMode.Selection, ResponseMode.ShortText => LearningItemResponseMode.ShortText, ResponseMode.Code => LearningItemResponseMode.Code, _ => throw new ArgumentOutOfRangeException() },
        record.Hints.OrderBy(x => x.Position).Select(x => new HintSnapshot(x.Text)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value).ToArray(), record.LowInteractionEligible,
        record.LifecycleState switch { LearningItemLifecycleState.Active => LearningItemLifecycle.Active, LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended, LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered, _ => throw new ArgumentOutOfRangeException() },
        record.IsNew, record.DueAt, record.Difficulty, record.StabilityDays, record.IsInShortTermRelearning,
        record.DeckMemberships.Select(x => x.DeckId).Distinct().ToArray(), record.ContentRevision,
        record.QualityReviews.Select(x => new QualityReviewSnapshot(x.QualityReviewId, x.LearningItemId, x.ContentRevision, (QualityReviewOutcomeSnapshot)x.Outcome, (QualityReviewEvidenceTypeSnapshot)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy)).ToArray(),
        record.UserFlagAssignments.Select(x => x.UserFlagDefinitionId).Distinct().ToArray());

    private static LearningItem ToDomain(LearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id), snapshot.Prompt, snapshot.ReferenceSolution, snapshot.DueAt, snapshot.IsNew,
        snapshot.ResponseMode switch { LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed, LearningItemResponseMode.Selection => ResponseMode.Selection, LearningItemResponseMode.ShortText => ResponseMode.ShortText, LearningItemResponseMode.Code => ResponseMode.Code, _ => throw new ArgumentOutOfRangeException() },
        snapshot.Hints.Select(x => new Hint(x.Text)), snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)), snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)), snapshot.AcceptedShortAnswers, snapshot.LowInteractionEligible,
        snapshot.Lifecycle switch { LearningItemLifecycle.Active => LearningItemLifecycleState.Active, LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended, LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered, _ => throw new ArgumentOutOfRangeException() },
        snapshot.Difficulty, snapshot.StabilityDays, snapshot.IsInShortTermRelearning, snapshot.ContentRevision,
        (snapshot.QualityReviews ?? []).Select(x => QualityReview.Restore(QualityReviewId.From(x.Id), LearningItemId.From(x.LearningItemId), x.ContentRevision, (Illumination.Domain.Learning.QualityReviewOutcome)x.Outcome, (Illumination.Domain.Learning.QualityReviewEvidenceType)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)),
        (snapshot.UserFlagDefinitionIds ?? []).Select(UserFlagDefinitionId.From));
}
