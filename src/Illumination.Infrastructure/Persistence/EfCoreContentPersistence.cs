using Illumination.Application.ContentManagement;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreContentPersistence : IContentPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreContentPersistence(IDbContextFactory<IlluminationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<LearningItemSnapshot?> FindLearningItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.LearningItems
            .AsNoTracking()
            .Include(x => x.Hints)
            .Include(x => x.AnswerChoices)
            .Include(x => x.AcceptedShortAnswers)
            .Include(x => x.DeckMemberships)
            .SingleOrDefaultAsync(x => x.LearningItemId == id, cancellationToken);

        return record is null ? null : ToSnapshot(record);
    }

    public async Task SaveLearningItemAsync(LearningItemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var domainItem = ToDomain(snapshot);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.LearningItems
            .Include(x => x.Hints)
            .Include(x => x.AnswerChoices)
            .Include(x => x.AcceptedShortAnswers)
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

            context.Hints.RemoveRange(existing.Hints);
            context.AnswerChoices.RemoveRange(existing.AnswerChoices);
            context.AcceptedShortAnswers.RemoveRange(existing.AcceptedShortAnswers);
            existing.Hints.Clear();
            existing.AnswerChoices.Clear();
            existing.AcceptedShortAnswers.Clear();
            existing.Hints.AddRange(replacement.Hints);
            existing.AnswerChoices.AddRange(replacement.AnswerChoices);
            existing.AcceptedShortAnswers.AddRange(replacement.AcceptedShortAnswers);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeckSnapshot?> FindDeckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.Decks
            .AsNoTracking()
            .Include(x => x.Memberships)
            .SingleOrDefaultAsync(x => x.DeckId == id, cancellationToken);

        return record is null ? null : ToSnapshot(record);
    }

    public async Task SaveDeckAsync(DeckSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var domainDeck = ToDomain(snapshot);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Decks
            .Include(x => x.Memberships)
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
            existing.Memberships.Clear();
            existing.Memberships.AddRange(replacement.Memberships);
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

    private static LearningItemSnapshot ToSnapshot(LearningItemRecord record) => new(
        record.LearningItemId,
        record.Prompt,
        record.ReferenceSolutionContent,
        (LearningItemResponseMode)record.ResponseMode,
        record.Hints.OrderBy(x => x.Position).Select(x => new HintSnapshot(x.Text)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(),
        record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value).ToArray(),
        record.LowInteractionEligible,
        (LearningItemLifecycle)record.LifecycleState,
        record.IsNew,
        record.DueAt,
        record.DeckMemberships.Select(x => x.DeckId).Distinct().ToArray());

    private static DeckSnapshot ToSnapshot(DeckRecord record) => new(
        record.DeckId,
        record.Name,
        record.Memberships.Select(x => x.LearningItemId).Distinct().ToArray());

    private static LearningItem ToDomain(LearningItemSnapshot snapshot) => LearningItem.Restore(
        LearningItemId.From(snapshot.Id),
        snapshot.Prompt,
        snapshot.ReferenceSolution,
        snapshot.DueAt,
        snapshot.IsNew,
        (ResponseMode)snapshot.ResponseMode,
        snapshot.Hints.Select(x => new Hint(x.Text)),
        snapshot.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
        snapshot.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
        snapshot.AcceptedShortAnswers,
        snapshot.LowInteractionEligible,
        (LearningItemLifecycleState)snapshot.Lifecycle);

    private static Deck ToDomain(DeckSnapshot snapshot)
    {
        var deck = Deck.Create(DeckId.From(snapshot.Id), snapshot.Name);
        foreach (var learningItemId in snapshot.LearningItemIds)
        {
            deck.AddLearningItem(LearningItemId.From(learningItemId));
        }

        return deck;
    }
}
