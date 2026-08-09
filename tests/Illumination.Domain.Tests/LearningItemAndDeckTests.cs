using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Xunit;

namespace Illumination.Domain.Tests;

public class LearningItemAndDeckTests
{
    private static readonly DateTimeOffset InitialDueAt = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_a_valid_basic_learning_item_with_a_new_due_state()
    {
        var item = LearningItem.Create("Question", "Reference solution", InitialDueAt);

        Assert.NotEqual(Guid.Empty, item.Id.Value);
        Assert.Equal("Question", item.Prompt);
        Assert.Equal("Reference solution", item.ReferenceSolution.Content);
        Assert.Empty(item.Hints);
        Assert.Equal(ResponseMode.SelfAssessed, item.ResponseMode);
        Assert.Equal(LearningItemLifecycleState.Active, item.LifecycleState);
        Assert.True(item.LearningState.IsNew);
        Assert.Equal(InitialDueAt, item.LearningState.DueAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_missing_or_blank_reference_solution(string? referenceSolution)
    {
        Assert.Throws<ArgumentException>(() => LearningItem.Create("Question", referenceSolution!, InitialDueAt));
    }

    [Fact]
    public void Accepts_zero_hints_and_preserves_multiple_hints_in_order()
    {
        var noHints = LearningItem.Create("Question", "Solution", InitialDueAt);
        var hints = new[] { new Hint("First hint"), new Hint("Second hint") };
        var item = LearningItem.Create("Question", "Solution", InitialDueAt, hints: hints);

        Assert.Empty(noHints.Hints);
        Assert.Equal(new[] { "First hint", "Second hint" }, item.Hints.Select(hint => hint.Text));
    }

    [Fact]
    public void Selection_requires_two_choices_and_a_correct_choice()
    {
        Assert.Throws<ArgumentException>(() => LearningItem.Create(
            "Question",
            "Solution",
            InitialDueAt,
            ResponseMode.Selection,
            directAnswerChoices: [new AnswerChoice("Only choice", true)]));

        Assert.Throws<ArgumentException>(() => LearningItem.Create(
            "Question",
            "Solution",
            InitialDueAt,
            ResponseMode.Selection,
            directAnswerChoices: [new AnswerChoice("First"), new AnswerChoice("Second")]));
    }

    [Fact]
    public void ShortText_requires_an_accepted_short_answer()
    {
        Assert.Throws<ArgumentException>(() => LearningItem.Create(
            "Question",
            "Solution",
            InitialDueAt,
            ResponseMode.ShortText));

        var item = LearningItem.Create(
            "Question",
            "Solution",
            InitialDueAt,
            ResponseMode.ShortText,
            acceptedShortAnswers: ["accepted"]);

        Assert.Equal(new[] { "accepted" }, item.AcceptedShortAnswers);
    }

    [Fact]
    public void Assistance_choices_remain_distinct_from_direct_choices()
    {
        var direct = new[] { new AnswerChoice("Direct wrong"), new AnswerChoice("Direct correct", true) };
        var assistance = new[] { new AnswerChoice("Help one"), new AnswerChoice("Help two") };
        var item = LearningItem.Create(
            "Question",
            "Solution",
            InitialDueAt,
            ResponseMode.Selection,
            directAnswerChoices: direct,
            assistanceAnswerChoices: assistance);

        Assert.Equal(direct, item.DirectAnswerChoices);
        Assert.Equal(assistance, item.AssistanceAnswerChoices);
        Assert.NotSame(item.DirectAnswerChoices, item.AssistanceAnswerChoices);
    }

    [Fact]
    public void Low_interaction_eligibility_is_retained_and_changeable()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt, lowInteractionEligible: true);

        item.ChangeLowInteractionEligibility(false);

        Assert.False(item.LowInteractionEligible);
    }

    [Fact]
    public void Learning_state_is_created_once_per_item()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt);
        var state = item.LearningState;

        item.Suspend();

        Assert.Same(state, item.LearningState);
    }

    [Fact]
    public void Suspend_preserves_the_item_and_state()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt);

        item.Suspend();

        Assert.Equal(LearningItemLifecycleState.Suspended, item.LifecycleState);
        Assert.NotNull(item.LearningState);
    }

    [Fact]
    public void Reactivate_returns_to_active_and_marks_state_immediately_due()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt.AddDays(10));
        var dueAt = new DateTimeOffset(2030, 2, 1, 12, 0, 0, TimeSpan.Zero);
        var state = item.LearningState;
        item.Suspend();

        item.Reactivate(dueAt);

        Assert.Equal(LearningItemLifecycleState.Active, item.LifecycleState);
        Assert.Same(state, item.LearningState);
        Assert.Equal(dueAt, item.LearningState.DueAt);
        Assert.True(item.LearningState.IsDueAt(dueAt));
    }

    [Fact]
    public void Active_item_cannot_be_reactivated()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt);

        Assert.Throws<InvalidOperationException>(() => item.Reactivate(InitialDueAt));
    }

    [Fact]
    public void Mastered_item_cannot_be_reactivated()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt);
        item.MarkMastered();

        Assert.Throws<InvalidOperationException>(() => item.Reactivate(InitialDueAt));
        Assert.Equal(LearningItemLifecycleState.Mastered, item.LifecycleState);
    }

    [Fact]
    public void Mark_mastered_and_unmark_mastered_return_to_active_and_due()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt.AddDays(10));
        var dueAt = new DateTimeOffset(2030, 2, 2, 12, 0, 0, TimeSpan.Zero);
        var state = item.LearningState;

        item.MarkMastered();
        item.UnmarkMastered(dueAt);

        Assert.Equal(LearningItemLifecycleState.Active, item.LifecycleState);
        Assert.Same(state, item.LearningState);
        Assert.Equal(dueAt, item.LearningState.DueAt);
    }

    [Fact]
    public void Suspended_item_cannot_be_unmarked_as_mastered()
    {
        var item = LearningItem.Create("Question", "Solution", InitialDueAt);
        item.Suspend();

        Assert.Throws<InvalidOperationException>(() => item.UnmarkMastered(InitialDueAt));
        Assert.Equal(LearningItemLifecycleState.Suspended, item.LifecycleState);
    }

    [Fact]
    public void Authored_content_can_be_changed_without_replacing_identity()
    {
        var id = LearningItemId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var item = LearningItem.Create(id, "Question", "Solution", InitialDueAt);

        item.ChangePrompt("Changed question");
        item.ChangeReferenceSolution("Changed solution");
        item.ReplaceHints([new Hint("New hint")]);
        item.ChangeInteractionConfiguration(ResponseMode.Code);

        Assert.Equal(id, item.Id);
        Assert.Equal("Changed question", item.Prompt);
        Assert.Equal("Changed solution", item.ReferenceSolution.Content);
        Assert.Single(item.Hints);
        Assert.Equal(ResponseMode.Code, item.ResponseMode);
    }

    [Fact]
    public void A_deck_accepts_one_item_id_without_duplicating_membership()
    {
        var id = LearningItemId.New();
        var deck = Deck.Create("Deck");

        deck.AddLearningItem(id);
        deck.AddLearningItem(id);

        Assert.Single(deck.LearningItemIds);
        Assert.Contains(id, deck.LearningItemIds);
    }

    [Fact]
    public void One_item_id_can_belong_to_multiple_decks_and_removal_is_local()
    {
        var id = LearningItemId.New();
        var firstDeck = Deck.Create("First");
        var secondDeck = Deck.Create("Second");
        firstDeck.AddLearningItem(id);
        secondDeck.AddLearningItem(id);

        firstDeck.RemoveLearningItem(id);

        Assert.DoesNotContain(id, firstDeck.LearningItemIds);
        Assert.Contains(id, secondDeck.LearningItemIds);
    }

    [Fact]
    public void Deck_renaming_validates_and_preserves_identity()
    {
        var id = DeckId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var deck = Deck.Create(id, "Original");

        deck.Rename("Renamed");

        Assert.Equal(id, deck.Id);
        Assert.Equal("Renamed", deck.Name);
        Assert.Throws<ArgumentException>(() => deck.Rename(" "));
    }
}
