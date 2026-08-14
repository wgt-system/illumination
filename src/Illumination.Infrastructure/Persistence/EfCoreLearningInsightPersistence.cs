using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreLearningInsightPersistence : ILearningInsightPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreLearningInsightPersistence(IDbContextFactory<IlluminationDbContext> contextFactory) =>
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<LearningInsightItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.LearningItems.AsNoTracking().Include(x => x.DeckMemberships).ToArrayAsync(cancellationToken);
        var reviews = await context.Reviews.AsNoTracking().Include(x => x.StudySessionAssociations).ToArrayAsync(cancellationToken);
        var reviewsByItem = reviews.ToLookup(x => x.LearningItemId);
        return records.Select(record => ToItemSnapshot(record, reviewsByItem[record.LearningItemId])).ToArray();
    }

    public async Task<IReadOnlyList<InsightDeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return (await context.Decks.AsNoTracking().Include(x => x.Memberships).ToArrayAsync(cancellationToken))
            .Select(x => new InsightDeckSnapshot(x.DeckId, x.Name, x.Memberships.Select(y => y.LearningItemId).Distinct().ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<InsightReviewSnapshot>> LoadReviewsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return (await context.Reviews.AsNoTracking().Include(x => x.StudySessionAssociations).ToArrayAsync(cancellationToken))
            .Select(ToReviewSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<InsightStudySessionSnapshot>> LoadStudySessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var decks = await context.Decks.AsNoTracking().ToDictionaryAsync(x => x.DeckId, cancellationToken);
        var sessions = await context.StudySessions.AsNoTracking().Include(x => x.SelectedDecks).Include(x => x.Reviews).ToArrayAsync(cancellationToken);
        return sessions.Select(session => new InsightStudySessionSnapshot(
            session.StudySessionId,
            session.StartedAt,
            session.CompletedAt,
            session.SelectedDecks.Select(x => new InsightDeckIdentity(x.DeckId, decks.TryGetValue(x.DeckId, out var deck) ? deck.Name : string.Empty)).ToArray(),
            session.Reviews.OrderBy(x => x.Position).Select(x => x.ReviewId).ToArray(),
            ToApplication(session.EvaluationMode),
            session.ConsiderAssistance,
            session.LowInteractionOnly)).ToArray();
    }

    private static LearningInsightItemSnapshot ToItemSnapshot(LearningItemRecord record, IEnumerable<ReviewRecord> reviews) => new(
        record.LearningItemId,
        record.Prompt,
        record.ReferenceSolutionContent,
        ToApplication(record.ResponseMode),
        ToApplication(record.LifecycleState),
        record.IsNew,
        record.DueAt,
        record.Difficulty,
        record.StabilityDays,
        record.IsInShortTermRelearning,
        record.DeckMemberships.Select(x => x.DeckId).Distinct().ToArray(),
        reviews.Select(ToReviewSnapshot).ToArray());

    private static InsightReviewSnapshot ToReviewSnapshot(ReviewRecord record) => new(
        record.ReviewId,
        record.LearningItemId,
        record.CompletedAt,
        ToApplication(record.Assessment),
        record.SubmittedResponse,
        record.AutomaticCorrectness,
        record.SuggestedAssessment is { } suggested ? ToApplication(suggested) : null,
        record.HintCount,
        record.AssistanceAnswerChoicesRevealed,
        record.ReferenceSolutionRevealed,
        record.StudySessionAssociations.Select(x => x.StudySessionId).Distinct().ToArray());

    private static LearningItemResponseMode ToApplication(ResponseMode mode) => mode switch
    {
        ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed,
        ResponseMode.Selection => LearningItemResponseMode.Selection,
        ResponseMode.ShortText => LearningItemResponseMode.ShortText,
        ResponseMode.Code => LearningItemResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported response mode."),
    };

    private static LearningItemLifecycle ToApplication(LearningItemLifecycleState state) => state switch
    {
        LearningItemLifecycleState.Active => LearningItemLifecycle.Active,
        LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended,
        LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported lifecycle state."),
    };

    private static StudyLearningAssessment ToApplication(LearningAssessment assessment) => assessment switch
    {
        LearningAssessment.Nochmal => StudyLearningAssessment.Nochmal,
        LearningAssessment.Schwer => StudyLearningAssessment.Schwer,
        LearningAssessment.Unsicher => StudyLearningAssessment.Unsicher,
        LearningAssessment.Gut => StudyLearningAssessment.Gut,
        LearningAssessment.Leicht => StudyLearningAssessment.Leicht,
        _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported assessment."),
    };

    private static StudyEvaluationMode ToApplication(StudyEvaluationMode mode) => mode switch
    {
        StudyEvaluationMode.Manual => StudyEvaluationMode.Manual,
        StudyEvaluationMode.Assisted => StudyEvaluationMode.Assisted,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported evaluation mode."),
    };
}
