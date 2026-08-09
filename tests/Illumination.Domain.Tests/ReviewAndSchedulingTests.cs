using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Xunit;

namespace Illumination.Domain.Tests;

public class ReviewAndSchedulingTests
{
    private static readonly DateTimeOffset InitialDueAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = new(2030, 1, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Review_has_stable_identity_and_captures_its_historical_values()
    {
        var itemId = LearningItemId.New();
        var reviewId = ReviewId.New();
        var review = Review.Create(reviewId, itemId, CompletedAt, LearningAssessment.Gut, "  opaque response  ");

        Assert.NotEqual(Guid.Empty, review.Id.Value);
        Assert.Equal(reviewId, review.Id);
        Assert.Equal(itemId, review.LearningItemId);
        Assert.Equal(CompletedAt, review.CompletedAt);
        Assert.Equal(LearningAssessment.Gut, review.Assessment);
        Assert.Equal("  opaque response  ", review.SubmittedResponse);
        Assert.Throws<ArgumentException>(() => ReviewId.From(Guid.Empty));
    }

    [Fact]
    public void New_learning_state_uses_the_initial_scheduling_defaults()
    {
        var item = CreateItem();

        Assert.True(item.LearningState.IsNew);
        Assert.Equal(InitialDueAt, item.LearningState.DueAt);
        Assert.Equal(5.0, item.LearningState.Difficulty);
        Assert.Equal(0.5, item.LearningState.StabilityDays);
        Assert.False(item.LearningState.IsInShortTermRelearning);
    }

    [Fact]
    public void Review_is_rejected_for_suspended_and_mastered_items()
    {
        var suspended = CreateItem();
        suspended.Suspend();
        Assert.Throws<InvalidOperationException>(() => suspended.CompleteReview(CompletedAt, LearningAssessment.Gut));

        var mastered = CreateItem();
        mastered.MarkMastered();
        Assert.Throws<InvalidOperationException>(() => mastered.CompleteReview(CompletedAt, LearningAssessment.Gut));
    }

    [Theory]
    [InlineData(LearningAssessment.Nochmal, 6.20)]
    [InlineData(LearningAssessment.Schwer, 5.60)]
    [InlineData(LearningAssessment.Unsicher, 5.15)]
    [InlineData(LearningAssessment.Gut, 4.80)]
    [InlineData(LearningAssessment.Leicht, 4.55)]
    public void Every_assessment_applies_its_difficulty_delta_and_marks_item_seen(
        LearningAssessment assessment,
        double expectedDifficulty)
    {
        var item = CreateItem();

        var review = item.CompleteReview(CompletedAt, assessment);

        Assert.Equal(item.Id, review.LearningItemId);
        Assert.False(item.LearningState.IsNew);
        Assert.Equal(expectedDifficulty, item.LearningState.Difficulty, precision: 10);
    }

    [Fact]
    public void Difficulty_is_clamped_at_both_bounds()
    {
        var lower = CreateItem(difficulty: 1.0);
        lower.CompleteReview(CompletedAt, LearningAssessment.Leicht);
        Assert.Equal(1.0, lower.LearningState.Difficulty);

        var upper = CreateItem(difficulty: 10.0);
        upper.CompleteReview(CompletedAt, LearningAssessment.Nochmal);
        Assert.Equal(10.0, upper.LearningState.Difficulty);
    }

    [Fact]
    public void Gut_growth_uses_the_post_clamp_difficulty()
    {
        var item = CreateItem(difficulty: 9.0, stabilityDays: 10.0);

        item.CompleteReview(CompletedAt, LearningAssessment.Gut);

        Assert.Equal(8.80, item.LearningState.Difficulty, precision: 10);
        Assert.Equal(16.408, item.LearningState.StabilityDays, precision: 10);
    }

    [Fact]
    public void Worse_assessments_are_never_scheduled_later_than_better_assessments()
    {
        var dueAtByAssessment = Enum.GetValues<LearningAssessment>()
            .ToDictionary(assessment => assessment, assessment =>
            {
                var item = CreateItem(stabilityDays: 10.0);
                item.CompleteReview(CompletedAt, assessment);
                return item.LearningState.DueAt;
            });

        Assert.True(dueAtByAssessment[LearningAssessment.Nochmal] <= dueAtByAssessment[LearningAssessment.Schwer]);
        Assert.True(dueAtByAssessment[LearningAssessment.Schwer] <= dueAtByAssessment[LearningAssessment.Unsicher]);
        Assert.True(dueAtByAssessment[LearningAssessment.Unsicher] <= dueAtByAssessment[LearningAssessment.Gut]);
        Assert.True(dueAtByAssessment[LearningAssessment.Gut] <= dueAtByAssessment[LearningAssessment.Leicht]);
    }

    [Fact]
    public void Nochmal_reduces_stability_requires_reinforcement_and_is_immediately_due()
    {
        var item = CreateItem(stabilityDays: 60.0);

        item.CompleteReview(CompletedAt, LearningAssessment.Nochmal);

        Assert.Equal(3.0, item.LearningState.StabilityDays);
        Assert.True(item.LearningState.IsInShortTermRelearning);
        Assert.Equal(CompletedAt, item.LearningState.DueAt);
    }

    [Fact]
    public void Schwer_reduces_stability_requires_reinforcement_and_is_immediately_due()
    {
        var item = CreateItem(stabilityDays: 60.0);

        item.CompleteReview(CompletedAt, LearningAssessment.Schwer);

        Assert.Equal(7.0, item.LearningState.StabilityDays);
        Assert.True(item.LearningState.IsInShortTermRelearning);
        Assert.Equal(CompletedAt, item.LearningState.DueAt);
    }

    [Theory]
    [InlineData(LearningAssessment.Gut, 2.0)]
    [InlineData(LearningAssessment.Leicht, 4.0)]
    public void Graduating_assessments_respect_minimum_stability(LearningAssessment assessment, double minimum)
    {
        var item = CreateItem(stabilityDays: 0.5);

        item.CompleteReview(CompletedAt, assessment);

        Assert.Equal(minimum, item.LearningState.StabilityDays);
        Assert.Equal(CompletedAt.AddDays(minimum), item.LearningState.DueAt);
    }

    [Theory]
    [InlineData(LearningAssessment.Gut)]
    [InlineData(LearningAssessment.Leicht)]
    public void Successful_post_relearning_review_clears_relearning_and_grows_retained_stability(
        LearningAssessment successfulAssessment)
    {
        var item = CreateItem(stabilityDays: 60.0);
        item.CompleteReview(CompletedAt, LearningAssessment.Nochmal);

        var nextCompletedAt = CompletedAt.AddDays(1);
        item.CompleteReview(nextCompletedAt, successfulAssessment);

        Assert.False(item.LearningState.IsInShortTermRelearning);
        Assert.True(item.LearningState.StabilityDays > 3.0);
        Assert.Equal(nextCompletedAt.AddDays(item.LearningState.StabilityDays), item.LearningState.DueAt);
    }

    [Fact]
    public void Unsicher_preserves_stability_requires_reinforcement_and_is_immediately_due()
    {
        var item = CreateItem(stabilityDays: 12.0);

        item.CompleteReview(CompletedAt, LearningAssessment.Unsicher);

        Assert.Equal(12.0, item.LearningState.StabilityDays);
        Assert.True(item.LearningState.IsInShortTermRelearning);
        Assert.Equal(CompletedAt, item.LearningState.DueAt);
        Assert.False(item.LearningState.IsNew);
    }

    [Fact]
    public void Review_projection_is_side_effect_free_and_matches_actual_transition()
    {
        var item = CreateItem(difficulty: 6.0, stabilityDays: 4.0);
        var before = item.LearningState;

        var projection = item.PreviewReview(CompletedAt, LearningAssessment.Gut);

        Assert.Same(before, item.LearningState);
        Assert.Equal(6.0, item.LearningState.Difficulty);
        var review = item.CompleteReview(CompletedAt, LearningAssessment.Gut);
        Assert.Equal(LearningAssessment.Gut, review.Assessment);
        Assert.Equal(projection.Difficulty, item.LearningState.Difficulty);
        Assert.Equal(projection.StabilityDays, item.LearningState.StabilityDays);
        Assert.Equal(projection.DueAt, item.LearningState.DueAt);
        Assert.Equal(projection.IsInShortTermRelearning, item.LearningState.IsInShortTermRelearning);
    }

    [Fact]
    public void Repeated_successful_reviews_increase_intervals()
    {
        var item = CreateItem();

        item.CompleteReview(CompletedAt, LearningAssessment.Gut);
        var firstInterval = item.LearningState.StabilityDays;
        item.CompleteReview(item.LearningState.DueAt, LearningAssessment.Gut);

        Assert.True(item.LearningState.StabilityDays > firstInterval);
    }

    [Fact]
    public void Scheduling_is_deterministic_for_equal_input()
    {
        var first = CreateItem(difficulty: 6.0, stabilityDays: 4.0);
        var second = CreateItem(difficulty: 6.0, stabilityDays: 4.0);

        first.CompleteReview(CompletedAt, LearningAssessment.Unsicher);
        second.CompleteReview(CompletedAt, LearningAssessment.Unsicher);

        Assert.Equal(first.LearningState.Difficulty, second.LearningState.Difficulty);
        Assert.Equal(first.LearningState.StabilityDays, second.LearningState.StabilityDays);
        Assert.Equal(first.LearningState.DueAt, second.LearningState.DueAt);
        Assert.Equal(first.LearningState.IsNew, second.LearningState.IsNew);
    }

    [Fact]
    public void Lifecycle_actions_preserve_scheduling_state_except_immediate_due_time()
    {
        var suspended = CreateItem(difficulty: 6.0, stabilityDays: 12.0);
        suspended.CompleteReview(CompletedAt, LearningAssessment.Gut);
        var suspendedDifficulty = suspended.LearningState.Difficulty;
        var suspendedStability = suspended.LearningState.StabilityDays;
        var suspendedIsNew = suspended.LearningState.IsNew;
        var suspendedRelearning = suspended.LearningState.IsInShortTermRelearning;
        suspended.Suspend();
        var reactivatedAt = CompletedAt.AddDays(3);
        suspended.Reactivate(reactivatedAt);

        Assert.Equal(suspendedDifficulty, suspended.LearningState.Difficulty);
        Assert.Equal(suspendedStability, suspended.LearningState.StabilityDays);
        Assert.Equal(suspendedIsNew, suspended.LearningState.IsNew);
        Assert.Equal(suspendedRelearning, suspended.LearningState.IsInShortTermRelearning);
        Assert.Equal(reactivatedAt, suspended.LearningState.DueAt);

        var mastered = CreateItem(difficulty: 4.0, stabilityDays: 8.0);
        mastered.CompleteReview(CompletedAt, LearningAssessment.Nochmal);
        var masteredDifficulty = mastered.LearningState.Difficulty;
        var masteredStability = mastered.LearningState.StabilityDays;
        mastered.MarkMastered();
        var unmarkedAt = CompletedAt.AddDays(4);
        mastered.UnmarkMastered(unmarkedAt);

        Assert.Equal(masteredDifficulty, mastered.LearningState.Difficulty);
        Assert.Equal(masteredStability, mastered.LearningState.StabilityDays);
        Assert.Equal(unmarkedAt, mastered.LearningState.DueAt);
    }

    [Fact]
    public void Assessments_do_not_change_lifecycle_automatically()
    {
        var item = CreateItem();

        item.CompleteReview(CompletedAt, LearningAssessment.Leicht);

        Assert.Equal(LearningItemLifecycleState.Active, item.LifecycleState);
    }

    private static LearningItem CreateItem(
        double difficulty = 5.0,
        double stabilityDays = 0.5,
        LearningItemLifecycleState lifecycleState = LearningItemLifecycleState.Active) =>
        LearningItem.Restore(
            LearningItemId.New(),
            "Question",
            "Solution",
            InitialDueAt,
            isNew: true,
            ResponseMode.SelfAssessed,
            hints: null,
            directAnswerChoices: null,
            assistanceAnswerChoices: null,
            acceptedShortAnswers: null,
            lowInteractionEligible: false,
            lifecycleState,
            difficulty,
            stabilityDays,
            isInShortTermRelearning: false);
}
