using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Xunit;

namespace Illumination.Domain.Tests;

public sealed class ContentQualityTests
{
    private static readonly DateTimeOffset DueAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_learning_items_start_at_content_revision_one()
    {
        Assert.Equal(1, CreateItem().ContentRevision);
    }

    [Fact]
    public void One_logical_quality_content_update_increments_revision_once_and_no_op_does_not()
    {
        var item = CreateItem();

        Assert.True(item.UpdateContent("Changed", "Changed solution", ResponseMode.ShortText, acceptedShortAnswers: ["changed"]));
        Assert.Equal(2, item.ContentRevision);
        Assert.False(item.UpdateContent("Changed", "Changed solution", ResponseMode.ShortText, acceptedShortAnswers: ["changed"]));
        Assert.Equal(2, item.ContentRevision);
    }

    [Fact]
    public void Only_quality_relevant_content_changes_revision()
    {
        var item = CreateItem();
        var definition = UserFlagDefinition.Create("Later", "Review later");

        item.ChangeLowInteractionEligibility(true);
        item.Suspend();
        item.AddUserFlag(definition);

        Assert.Equal(1, item.ContentRevision);
        Assert.False(item.UpdateContent("Question", "Solution", ResponseMode.SelfAssessed));
        Assert.Equal(1, item.ContentRevision);
    }

    [Fact]
    public void Quality_review_is_immutable_history_bound_to_item_and_revision()
    {
        var item = CreateItem();
        var review = QualityReview.Create(item.Id, item.ContentRevision, QualityReviewOutcome.Pass, QualityReviewEvidenceType.ModelReview, "Clear and correct.", "No correction.");
        var promptBefore = item.Prompt;

        item.AcceptQualityReview(review);

        Assert.Equal(item.Id, review.LearningItemId);
        Assert.Equal(1, review.ContentRevision);
        Assert.False(review.IsSuperseded);
        Assert.Equal(promptBefore, item.Prompt);
        Assert.NotNull(item.CurrentQualityState);
        Assert.Equal(review.Id, item.CurrentQualityState!.ReviewId);
    }

    [Fact]
    public void Current_quality_state_uses_precedence_and_ignores_old_revision_reviews()
    {
        var item = CreateItem();
        var pass = QualityReview.Create(item.Id, 1, QualityReviewOutcome.Pass, QualityReviewEvidenceType.ModelReview, "Pass finding.");
        var warning = QualityReview.Create(item.Id, 1, QualityReviewOutcome.Warning, QualityReviewEvidenceType.UserReview, "Warning finding.");
        var needsReview = QualityReview.Create(item.Id, 1, QualityReviewOutcome.NeedsReview, QualityReviewEvidenceType.SourceGroundedReview, "Needs review finding.");

        item.AcceptQualityReview(pass);
        item.AcceptQualityReview(warning);
        item.AcceptQualityReview(needsReview);

        Assert.Equal(QualityReviewOutcome.NeedsReview, item.CurrentQualityState!.Outcome);
        Assert.Equal(QualityReviewEvidenceType.SourceGroundedReview, item.CurrentQualityState.EvidenceType);

        item.UpdateContent("Changed", "Solution", ResponseMode.SelfAssessed);

        Assert.Null(item.CurrentQualityState);
        Assert.Equal(3, item.QualityReviews.Count);
    }

    [Fact]
    public void Supersession_is_explicit_and_only_applies_to_same_item_and_revision()
    {
        var item = CreateItem();
        var pass = QualityReview.Create(item.Id, 1, QualityReviewOutcome.Pass, QualityReviewEvidenceType.ModelReview, "Pass finding.");
        var warning = QualityReview.Create(item.Id, 1, QualityReviewOutcome.Warning, QualityReviewEvidenceType.UserReview, "Warning finding.");
        var replacement = QualityReview.Create(item.Id, 1, QualityReviewOutcome.Pass, QualityReviewEvidenceType.SourceGroundedReview, "Replacement finding.");

        item.AcceptQualityReview(pass);
        item.AcceptQualityReview(warning);
        Assert.Equal(QualityReviewOutcome.Warning, item.CurrentQualityState!.Outcome);

        item.AcceptQualityReview(replacement, [warning.Id]);

        Assert.True(item.QualityReviews.Single(review => review.Id == warning.Id).IsSuperseded);
        Assert.Equal(QualityReviewOutcome.Pass, item.CurrentQualityState!.Outcome);
        Assert.False(item.QualityReviews.Single(review => review.Id == pass.Id).IsSuperseded);

        var otherItem = CreateItem();
        var otherReview = QualityReview.Create(otherItem.Id, 1, QualityReviewOutcome.Pass, QualityReviewEvidenceType.UserReview, "Other item.");
        Assert.Throws<InvalidOperationException>(() => item.AcceptQualityReview(
            QualityReview.Create(item.Id, 1, QualityReviewOutcome.Warning, QualityReviewEvidenceType.UserReview, "Invalid supersession."),
            [otherReview.Id]));
    }

    [Fact]
    public void Multiple_user_defined_flags_are_independent_and_do_not_affect_revision()
    {
        var item = CreateItem();
        var later = UserFlagDefinition.Create("Later", "Review later");
        var wording = UserFlagDefinition.Create("Wording", "Improve wording");

        item.AddUserFlag(later);
        item.AddUserFlag(wording);

        Assert.Equal(1, item.ContentRevision);
        Assert.True(item.HasUserFlag(later.Id));
        Assert.True(item.HasUserFlag(wording.Id));
        Assert.False(item.RemoveUserFlag(UserFlagDefinitionId.New()));
        Assert.True(item.RemoveUserFlag(later.Id));
        Assert.False(item.HasUserFlag(later.Id));
        Assert.True(item.HasUserFlag(wording.Id));
    }

    [Fact]
    public void Review_requires_human_readable_findings_and_supported_values()
    {
        var item = CreateItem();

        Assert.Throws<ArgumentException>(() => QualityReview.Create(item.Id, 1, QualityReviewOutcome.Pass, QualityReviewEvidenceType.ModelReview, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => QualityReview.Create(item.Id, 0, QualityReviewOutcome.Pass, QualityReviewEvidenceType.ModelReview, "Finding"));
    }

    private static LearningItem CreateItem() => LearningItem.Create("Question", "Solution", DueAt);
}
