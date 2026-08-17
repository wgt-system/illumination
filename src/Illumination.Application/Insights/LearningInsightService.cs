using Illumination.Application.ContentManagement;
using Illumination.Application.Study;

namespace Illumination.Application.Insights;

public sealed class LearningInsightService
{
    private readonly ILearningInsightPersistence _persistence;
    private readonly TimeProvider _timeProvider;

    public LearningInsightService(ILearningInsightPersistence persistence, TimeProvider timeProvider)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<LearningInsightOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var reviews = await _persistence.LoadReviewsAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var active = items.Where(x => x.Lifecycle == LearningItemLifecycle.Active).ToArray();
        return new(
            items.Count,
            active.Length,
            items.Count(x => x.Lifecycle == LearningItemLifecycle.Suspended),
            items.Count(x => x.Lifecycle == LearningItemLifecycle.Mastered),
            active.Count(x => x.IsNew),
            active.Count(x => x.DueAt <= now),
            active.Count(x => x.IsInShortTermRelearning),
            reviews.Count,
            reviews.Count(x => x.CompletedAt >= now.AddDays(-7)),
            reviews.Count(x => x.CompletedAt >= now.AddDays(-30)),
            reviews.OrderByDescending(x => x.CompletedAt).Select(x => (DateTimeOffset?)x.CompletedAt).FirstOrDefault());
    }

    public async Task<LearningActivitySummary> GetLearningActivityAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        if (days is <= 0 or > 366) throw new ArgumentOutOfRangeException(nameof(days), "Activity range must be between 1 and 366 days.");

        var today = LocalDate(_timeProvider.GetUtcNow());
        var start = today.AddDays(-(days - 1));
        var reviews = await _persistence.LoadReviewsAsync(cancellationToken);
        var sessions = await _persistence.LoadStudySessionsAsync(cancellationToken);

        var reviewsByDay = reviews
            .Select(review => (Date: LocalDate(review.CompletedAt), Review: review))
            .Where(x => x.Date >= start && x.Date <= today)
            .GroupBy(x => x.Date)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Review).ToArray());
        var sessionsByDay = sessions
            .Select(session => LocalDate(session.StartedAt))
            .Where(date => date >= start && date <= today)
            .GroupBy(date => date)
            .ToDictionary(group => group.Key, group => group.Count());

        var result = Enumerable.Range(0, days)
            .Select(offset => start.AddDays(offset))
            .Select(date =>
            {
                var dayReviews = reviewsByDay.GetValueOrDefault(date) ?? [];
                var sessionCount = sessionsByDay.GetValueOrDefault(date);
                return new LearningActivityDay(date, dayReviews.Length, sessionCount, Distribution(dayReviews));
            })
            .ToArray();

        return new LearningActivitySummary(
            start,
            today,
            result.Count(x => x.ReviewCount > 0 || x.StudySessionCount > 0),
            result.Sum(x => x.ReviewCount),
            result.Sum(x => x.StudySessionCount),
            result);
    }

    public async Task<LearningDueForecast> GetDueForecastAsync(int days = 14, CancellationToken cancellationToken = default)
    {
        if (days is <= 0 or > 366) throw new ArgumentOutOfRangeException(nameof(days), "Due forecast range must be between 1 and 366 days.");

        var now = _timeProvider.GetUtcNow();
        var start = LocalDate(now);
        var end = start.AddDays(days - 1);
        var active = (await _persistence.LoadLearningItemsAsync(cancellationToken))
            .Where(x => x.Lifecycle == LearningItemLifecycle.Active)
            .ToArray();
        var upcomingByDay = active
            .Where(x => x.DueAt > now)
            .Select(x => LocalDate(x.DueAt))
            .Where(date => date >= start && date <= end)
            .GroupBy(date => date)
            .ToDictionary(group => group.Key, group => group.Count());

        var upcoming = Enumerable.Range(0, days)
            .Select(offset => start.AddDays(offset))
            .Select(date => new DueForecastDay(date, upcomingByDay.GetValueOrDefault(date)))
            .ToArray();

        return new LearningDueForecast(active.Count(x => x.DueAt <= now), start, end, upcoming);
    }

    public async Task<IReadOnlyList<DeckInsight>> GetDeckInsightsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var decks = await _persistence.LoadDecksAsync(cancellationToken);
        var reviews = await _persistence.LoadReviewsAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        return decks.Select(deck =>
        {
            var members = items.Where(item => deck.CurrentLearningItemIds.Contains(item.Id)).ToArray();
            var memberIds = members.Select(x => x.Id).ToHashSet();
            var memberReviews = reviews.Where(review => memberIds.Contains(review.LearningItemId)).ToArray();
            return new DeckInsight(deck.Id, deck.Name, members.Length,
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Active),
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Suspended),
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Mastered),
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Active && x.IsNew),
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Active && x.DueAt <= now),
                members.Count(x => x.Lifecycle == LearningItemLifecycle.Active && x.IsInShortTermRelearning),
                memberReviews.Length,
                memberReviews.OrderByDescending(x => x.CompletedAt).Select(x => (DateTimeOffset?)x.CompletedAt).FirstOrDefault(),
                Distribution(memberReviews));
        }).ToArray();
    }

    public async Task<IReadOnlyList<LearningItemInsight>> GetLearningItemInsightsAsync(LearningItemInsightQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new();
        if (query.Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(query), "The insight limit must be positive.");
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var filtered = items.Where(item => query.DeckId is null || item.CurrentDeckIds.Contains(query.DeckId.Value))
            .Where(item => string.IsNullOrWhiteSpace(query.PromptContains) || item.Prompt.Contains(query.PromptContains, StringComparison.OrdinalIgnoreCase))
            .Where(item => query.Lifecycle is null || item.Lifecycle == query.Lifecycle)
            .Where(item => !query.NewOnly || item.IsNew)
            .Where(item => !query.DueNowOnly || item.DueAt <= now)
            .Where(item => !query.RelearningOnly || item.IsInShortTermRelearning)
            .Select(ToInsight)
            .OrderBy(x => x.Prompt, StringComparer.OrdinalIgnoreCase);
        return (query.Limit is { } limit ? filtered.Take(limit) : filtered).ToArray();
    }

    public async Task<IReadOnlyList<ReviewHistoryEntry>> GetReviewHistoryAsync(Guid? deckId = null, Guid? learningItemId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var reviews = await _persistence.LoadReviewsAsync(cancellationToken);
        var itemLookup = items.ToDictionary(x => x.Id);
        var allowed = items.Where(x => (deckId is null || x.CurrentDeckIds.Contains(deckId.Value)) && (learningItemId is null || x.Id == learningItemId.Value)).Select(x => x.Id).ToHashSet();
        return reviews.Where(x => allowed.Contains(x.LearningItemId)).OrderByDescending(x => x.CompletedAt).Take(limit).Select(x =>
        {
            if (!itemLookup.TryGetValue(x.LearningItemId, out var item)) throw new InvalidOperationException("Review references an unknown Learning Item.");
            return new ReviewHistoryEntry(x.Id, x.LearningItemId, item.Prompt, x.CompletedAt, x.Assessment, x.StudySessionIds, x.SubmittedResponse, x.AutomaticCorrectness, x.SuggestedAssessment, x.HintCount, x.AssistanceAnswerChoicesRevealed, x.ReferenceSolutionRevealed);
        }).ToArray();
    }

    public async Task<IReadOnlyList<StudySessionHistoryEntry>> GetStudySessionHistoryAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var sessions = await _persistence.LoadStudySessionsAsync(cancellationToken);
        return sessions.OrderByDescending(x => x.StartedAt).Take(limit).Select(x => new StudySessionHistoryEntry(x.Id, x.SelectedDecks, x.StartedAt, x.CompletedAt, x.EvaluationMode, x.ConsiderAssistance, x.LowInteractionOnly, x.ReviewIds.Count)).ToArray();
    }

    public async Task<DeckLearningContext> GetDeckLearningContextAsync(Guid deckId, CancellationToken cancellationToken = default)
    {
        var decks = await _persistence.LoadDecksAsync(cancellationToken);
        var deck = decks.SingleOrDefault(x => x.Id == deckId) ?? throw new KeyNotFoundException($"Deck '{deckId}' was not found.");
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        return new(deck.Id, deck.Name, items.Where(x => deck.CurrentLearningItemIds.Contains(x.Id)).Select(x => new DeckLearningContextItem(x.Id, x.Prompt, x.ReferenceSolution, x.ResponseMode, x.Lifecycle, x.IsNew, x.DueAt, x.Difficulty, x.StabilityDays, x.IsInShortTermRelearning, x.Reviews.Count, LastAssessment(x.Reviews), Distribution(x.Reviews))).ToArray());
    }

    private DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, _timeProvider.LocalTimeZone).DateTime);

    private static LearningItemInsight ToInsight(LearningInsightItemSnapshot item) => new(item.Id, item.Prompt, item.ResponseMode, item.Lifecycle, item.IsNew, item.DueAt, item.Difficulty, item.StabilityDays, item.IsInShortTermRelearning, item.Reviews.Count, item.Reviews.OrderByDescending(x => x.CompletedAt).Select(x => (DateTimeOffset?)x.CompletedAt).FirstOrDefault(), LastAssessment(item.Reviews), Distribution(item.Reviews));

    private static StudyLearningAssessment? LastAssessment(IEnumerable<InsightReviewSnapshot> reviews) => reviews.OrderByDescending(x => x.CompletedAt).Select(x => (StudyLearningAssessment?)x.Assessment).FirstOrDefault();

    private static AssessmentDistribution Distribution(IEnumerable<InsightReviewSnapshot> reviews) => new(
        reviews.Count(x => x.Assessment == StudyLearningAssessment.Nochmal), reviews.Count(x => x.Assessment == StudyLearningAssessment.Schwer), reviews.Count(x => x.Assessment == StudyLearningAssessment.Unsicher), reviews.Count(x => x.Assessment == StudyLearningAssessment.Gut), reviews.Count(x => x.Assessment == StudyLearningAssessment.Leicht));
}
