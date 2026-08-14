using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Xunit;

namespace Illumination.Application.Tests;

public sealed class LearningInsightServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overview_uses_factual_current_state_and_review_windows()
    {
        var data = SampleData();
        var overview = await CreateService(data).GetOverviewAsync();

        Assert.Equal(5, overview.TotalLearningItems);
        Assert.Equal(3, overview.ActiveCount);
        Assert.Equal(1, overview.SuspendedCount);
        Assert.Equal(1, overview.MasteredCount);
        Assert.Equal(1, overview.NewItemCount);
        Assert.Equal(2, overview.DueNowCount);
        Assert.Equal(1, overview.ShortTermRelearningCount);
        Assert.Equal(3, overview.TotalCompletedReviewCount);
        Assert.Equal(2, overview.ReviewsLast7Days);
        Assert.Equal(3, overview.ReviewsLast30Days);
        Assert.Equal(Now.AddDays(-1), overview.MostRecentReviewAt);
    }

    [Fact]
    public async Task Deck_insight_uses_current_membership_and_final_grade_distribution()
    {
        var data = SampleData();
        var deck = Assert.Single((await CreateService(data).GetDeckInsightsAsync()).Where(x => x.Id == data.DeckA));

        Assert.Equal(3, deck.CurrentItemCount);
        Assert.Equal(2, deck.ActiveCount);
        Assert.Equal(1, deck.SuspendedCount);
        Assert.Equal(3, deck.TotalReviewCount);
        Assert.Equal(1, deck.AssessmentDistribution.Gut);
        Assert.Equal(1, deck.AssessmentDistribution.Nochmal);
        Assert.Equal(1, deck.AssessmentDistribution.Leicht);
        Assert.Equal(Now.AddDays(-1), deck.LastReviewAt);
    }

    [Fact]
    public async Task Item_filters_are_composable_and_advisory_facts_do_not_drive_grade_statistics()
    {
        var data = SampleData();
        var service = CreateService(data);
        var result = await service.GetLearningItemInsightsAsync(new LearningItemInsightQuery(DeckId: data.DeckA, PromptContains: "alpha", DueNowOnly: true));

        var item = Assert.Single(result);
        Assert.Equal(data.ReviewedItem, item.LearningItemId);
        Assert.Equal(2, item.ReviewCount);
        Assert.Equal(StudyLearningAssessment.Gut, item.LastConfirmedAssessment);
        Assert.Equal(1, item.AssessmentDistribution.Gut);
        Assert.Equal(1, item.AssessmentDistribution.Nochmal);
    }

    [Fact]
    public async Task Review_history_is_newest_first_and_deck_filter_uses_current_membership()
    {
        var data = SampleData();
        var service = CreateService(data);
        var history = await service.GetReviewHistoryAsync(deckId: data.DeckB, limit: 10);

        Assert.Equal(2, history.Count);
        Assert.Equal(Now.AddDays(-1), history[0].CompletedAt);
        Assert.All(history, x => Assert.Equal(data.ReviewedItem, x.LearningItemId));
        Assert.True(history[0].AutomaticCorrectness);
        Assert.Equal(StudyLearningAssessment.Unsicher, history[0].SuggestedAssessment);
    }

    [Fact]
    public async Task Deck_learning_context_contains_content_and_performance_facts()
    {
        var data = SampleData();
        var context = await CreateService(data).GetDeckLearningContextAsync(data.DeckA);

        var item = Assert.Single(context.Items.Where(x => x.LearningItemId == data.ReviewedItem));
        Assert.Equal("Reference", item.ReferenceSolution);
        Assert.Equal(2, item.ReviewCount);
        Assert.Equal(StudyLearningAssessment.Gut, item.LastConfirmedAssessment);
        Assert.Equal(1, item.AssessmentDistribution.Nochmal);
    }

    [Fact]
    public async Task Study_session_history_exposes_only_persisted_facts()
    {
        var data = SampleData();
        var session = Assert.Single(await CreateService(data).GetStudySessionHistoryAsync());

        Assert.Equal(data.SessionId, session.SessionId);
        Assert.Equal(Now.AddDays(-2), session.StartedAt);
        Assert.Null(session.CompletedAt);
        Assert.Equal(StudyEvaluationMode.Assisted, session.EvaluationMode);
        Assert.True(session.ConsiderAssistance);
        Assert.Equal(2, session.ReviewCount);
    }

    private static LearningInsightService CreateService(FakePersistence persistence) => new(persistence, new FixedTimeProvider(Now));

    private static FakePersistence SampleData()
    {
        var data = new FakePersistence
        {
            DeckA = Guid.NewGuid(), DeckB = Guid.NewGuid(), ReviewedItem = Guid.NewGuid(), SessionId = Guid.NewGuid(),
        };
        var newItem = Guid.NewGuid();
        var suspended = Guid.NewGuid();
        var mastered = Guid.NewGuid();
        var relearning = Guid.NewGuid();
        data.Decks =
        [
            new(data.DeckA, "Alpha", [data.ReviewedItem, newItem, suspended]),
            new(data.DeckB, "Follow-up", [data.ReviewedItem]),
        ];
        data.Items =
        [
            new(data.ReviewedItem, "Alpha prompt", "Reference", LearningItemResponseMode.ShortText, LearningItemLifecycle.Active, false, Now.AddDays(-1), 5.5, 2, false, [data.DeckA, data.DeckB],
                [new(Guid.NewGuid(), data.ReviewedItem, Now.AddDays(-10), StudyLearningAssessment.Nochmal, null, null, null, 0, false, false, []), new(Guid.NewGuid(), data.ReviewedItem, Now.AddDays(-1), StudyLearningAssessment.Gut, "answer", true, StudyLearningAssessment.Unsicher, 2, true, true, [data.SessionId])]),
            new(newItem, "New prompt", "Reference", LearningItemResponseMode.SelfAssessed, LearningItemLifecycle.Active, true, Now, 5, .5, false, [data.DeckA], []),
            new(suspended, "Suspended prompt", "Reference", LearningItemResponseMode.SelfAssessed, LearningItemLifecycle.Suspended, false, Now.AddDays(-1), 5, 2, false, [data.DeckA], [new(Guid.NewGuid(), suspended, Now.AddDays(-2), StudyLearningAssessment.Leicht, null, null, null, 0, false, false, [])]),
            new(mastered, "Mastered prompt", "Reference", LearningItemResponseMode.SelfAssessed, LearningItemLifecycle.Mastered, false, Now.AddDays(-1), 5, 2, false, [], []),
            new(relearning, "Relearning prompt", "Reference", LearningItemResponseMode.SelfAssessed, LearningItemLifecycle.Active, false, Now.AddDays(1), 5, 1, true, [], []),
        ];
        data.Sessions = [new(data.SessionId, Now.AddDays(-2), null, [new(data.DeckA, "Alpha")], data.Items[0].Reviews.Select(x => x.Id).ToArray(), StudyEvaluationMode.Assisted, true, false)];
        return data;
    }

    private sealed class FakePersistence : ILearningInsightPersistence
    {
        public Guid DeckA { get; set; }
        public Guid DeckB { get; set; }
        public Guid ReviewedItem { get; set; }
        public Guid SessionId { get; set; }
        public IReadOnlyList<LearningInsightItemSnapshot> Items { get; set; } = [];
        public IReadOnlyList<InsightDeckSnapshot> Decks { get; set; } = [];
        public IReadOnlyList<InsightStudySessionSnapshot> Sessions { get; set; } = [];

        public Task<IReadOnlyList<LearningInsightItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items);
        public Task<IReadOnlyList<InsightDeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default) => Task.FromResult(Decks);
        public Task<IReadOnlyList<InsightReviewSnapshot>> LoadReviewsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InsightReviewSnapshot>>(Items.SelectMany(x => x.Reviews).ToArray());
        public Task<IReadOnlyList<InsightStudySessionSnapshot>> LoadStudySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sessions);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
