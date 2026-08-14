using Illumination.Application.Study;
using Illumination.Application.ContentManagement;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreStudySessionPersistence : IStudySessionPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreStudySessionPersistence(IDbContextFactory<IlluminationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<StudyDeckSnapshot>> LoadDecksAsync(IReadOnlyList<Guid> deckIds, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.Decks.AsNoTracking().Include(x => x.Memberships)
            .Where(x => deckIds.Contains(x.DeckId)).ToArrayAsync(cancellationToken);
        return records.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<StudyLearningItemSnapshot>> LoadLearningItemsAsync(IReadOnlyList<Guid> learningItemIds, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await LoadItems(context, learningItemIds).ToArrayAsync(cancellationToken);
        return records.Select(ToSnapshot).ToArray();
    }

    public async Task<StudyLearningItemSnapshot?> FindLearningItemAsync(Guid learningItemId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await LoadItems(context, [learningItemId]).SingleOrDefaultAsync(cancellationToken);
        return record is null ? null : ToSnapshot(record);
    }

    public async Task<StudySessionSnapshot?> FindStudySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.StudySessions.AsNoTracking()
            .Include(x => x.SelectedDecks).Include(x => x.Queue).Include(x => x.Reviews)
            .SingleOrDefaultAsync(x => x.StudySessionId == sessionId, cancellationToken);
        return record is null ? null : ToSnapshot(record);
    }

    public async Task SaveStartedStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.StudySessions.Add(ToRecord(session));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitReviewAsync(StudyLearningItemSnapshot learningItem, StudyReviewSnapshot review, StudySessionSnapshot session, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var item = await context.LearningItems.SingleOrDefaultAsync(x => x.LearningItemId == learningItem.Id, cancellationToken)
            ?? throw new InvalidOperationException("The Learning Item for the Review was not found.");
        if (review.LearningItemId != learningItem.Id)
        {
            throw new InvalidOperationException("The Review does not belong to the Learning Item.");
        }

        ApplySchedulingState(item, learningItem);
        context.Reviews.Add(ToRecord(review));

        var storedSession = await context.StudySessions
            .Include(x => x.Queue).Include(x => x.Reviews)
            .SingleOrDefaultAsync(x => x.StudySessionId == session.Id, cancellationToken)
            ?? throw new InvalidOperationException("The Study Session was not found.");
        storedSession.CompletedAt = session.CompletedAt;
        context.StudySessionQueue.RemoveRange(storedSession.Queue);
        context.StudySessionReviews.RemoveRange(storedSession.Reviews);
        storedSession.Queue.Clear();
        storedSession.Reviews.Clear();
        storedSession.Queue.AddRange(session.Queue.Select((id, position) => new StudySessionQueueRecord
        {
            StudySessionId = session.Id, Position = position, LearningItemId = id,
        }));
        storedSession.Reviews.AddRange(session.ReviewIds.Select((id, position) => new StudySessionReviewRecord
        {
            StudySessionId = session.Id, Position = position, ReviewId = id,
        }));

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await context.StudySessions.SingleOrDefaultAsync(x => x.StudySessionId == session.Id, cancellationToken)
            ?? throw new InvalidOperationException("The Study Session was not found.");
        stored.CompletedAt = session.CompletedAt;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<LearningItemRecord> LoadItems(IlluminationDbContext context, IReadOnlyList<Guid> ids) =>
        context.LearningItems.AsNoTracking().Include(x => x.Hints).Include(x => x.AnswerChoices)
            .Include(x => x.AcceptedShortAnswers).Include(x => x.DeckMemberships)
            .Where(x => ids.Contains(x.LearningItemId));

    private static void ApplySchedulingState(LearningItemRecord record, StudyLearningItemSnapshot snapshot)
    {
        record.IsNew = snapshot.IsNew;
        record.DueAt = snapshot.DueAt;
        record.Difficulty = snapshot.Difficulty;
        record.StabilityDays = snapshot.StabilityDays;
        record.IsInShortTermRelearning = snapshot.IsInShortTermRelearning;
    }

    private static StudyLearningItemSnapshot ToSnapshot(LearningItemRecord record) => new(
        record.LearningItemId, record.Prompt, record.ReferenceSolutionContent, ToApplication(record.ResponseMode),
        record.Hints.OrderBy(x => x.Position).Select(x => new HintSnapshot(x.Text)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AnswerChoices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.ChoiceId ?? string.Empty)).ToArray(),
        record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value).ToArray(), record.LowInteractionEligible,
        ToApplication(record.LifecycleState), record.IsNew, record.DueAt, record.Difficulty, record.StabilityDays,
        record.IsInShortTermRelearning, record.DeckMemberships.Select(x => x.DeckId).Distinct().ToArray());

    private static StudyDeckSnapshot ToSnapshot(DeckRecord record) =>
        new(record.DeckId, record.Memberships.Select(x => x.LearningItemId).Distinct().ToArray());

    private static StudySessionSnapshot ToSnapshot(StudySessionRecord record) => new(
        record.StudySessionId, record.StartedAt, record.CompletedAt,
        record.SelectedDecks.OrderBy(x => x.DeckId).Select(x => x.DeckId).ToArray(),
        record.Queue.OrderBy(x => x.Position).Select(x => x.LearningItemId).ToArray(),
        record.Reviews.OrderBy(x => x.Position).Select(x => x.ReviewId).ToArray(),
        (StudyEvaluationMode)record.EvaluationMode, record.ConsiderAssistance, record.LowInteractionOnly);

    private static StudySessionRecord ToRecord(StudySessionSnapshot snapshot)
    {
        var record = new StudySessionRecord
        {
            StudySessionId = snapshot.Id, StartedAt = snapshot.StartedAt, CompletedAt = snapshot.CompletedAt,
            EvaluationMode = (StudyEvaluationMode)snapshot.EvaluationMode,
            ConsiderAssistance = snapshot.ConsiderAssistance,
            LowInteractionOnly = snapshot.LowInteractionOnly,
        };
        record.SelectedDecks.AddRange(snapshot.SelectedDeckIds.Distinct().Select(id => new StudySessionDeckRecord { StudySessionId = snapshot.Id, DeckId = id }));
        record.Queue.AddRange(snapshot.Queue.Select((id, position) => new StudySessionQueueRecord { StudySessionId = snapshot.Id, Position = position, LearningItemId = id }));
        record.Reviews.AddRange(snapshot.ReviewIds.Select((id, position) => new StudySessionReviewRecord { StudySessionId = snapshot.Id, Position = position, ReviewId = id }));
        return record;
    }

    private static ReviewRecord ToRecord(StudyReviewSnapshot snapshot) => new()
    {
        ReviewId = snapshot.Id, LearningItemId = snapshot.LearningItemId, CompletedAt = snapshot.CompletedAt,
        Assessment = ToDomain(snapshot.Assessment), SubmittedResponse = snapshot.SubmittedResponse,
        AutomaticCorrectness = snapshot.AutomaticCorrectness,
        SuggestedAssessment = snapshot.SuggestedAssessment is { } suggested ? ToDomain(suggested) : null,
        HintCount = snapshot.HintCount,
        AssistanceAnswerChoicesRevealed = snapshot.AssistanceAnswerChoicesRevealed,
        ReferenceSolutionRevealed = snapshot.ReferenceSolutionRevealed,
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

    private static LearningAssessment ToDomain(StudyLearningAssessment assessment) => assessment switch
    {
        StudyLearningAssessment.Nochmal => LearningAssessment.Nochmal,
        StudyLearningAssessment.Schwer => LearningAssessment.Schwer,
        StudyLearningAssessment.Unsicher => LearningAssessment.Unsicher,
        StudyLearningAssessment.Gut => LearningAssessment.Gut,
        StudyLearningAssessment.Leicht => LearningAssessment.Leicht,
        _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported assessment."),
    };

    private static LearningItemResponseMode ToApplication(ResponseMode mode) => mode switch
    {
        ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed,
        ResponseMode.Selection => LearningItemResponseMode.Selection,
        ResponseMode.ShortText => LearningItemResponseMode.ShortText,
        ResponseMode.Code => LearningItemResponseMode.Code,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported response mode."),
    };

    private static LearningItemLifecycle ToApplication(LearningItemLifecycleState lifecycle) => lifecycle switch
    {
        LearningItemLifecycleState.Active => LearningItemLifecycle.Active,
        LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended,
        LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unsupported lifecycle."),
    };
}
