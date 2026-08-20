using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;

namespace Illumination.Infrastructure.Persistence;

public static class DomainPersistenceMapper
{
    public static LearningItemRecord ToRecord(LearningItem item)
    {
        var record = new LearningItemRecord
        {
            LearningItemId = item.Id.Value,
            Prompt = item.Prompt,
            ReferenceSolutionContent = item.ReferenceSolution.Content,
            ResponseMode = item.ResponseMode,
            LowInteractionEligible = item.LowInteractionEligible,
            LifecycleState = item.LifecycleState,
            IsNew = item.LearningState.IsNew,
            DueAt = item.LearningState.DueAt,
            Difficulty = item.LearningState.Difficulty,
            StabilityDays = item.LearningState.StabilityDays,
            IsInShortTermRelearning = item.LearningState.IsInShortTermRelearning,
            ContentRevision = item.ContentRevision,
        };

        record.Hints.AddRange(item.Hints.Select((hint, position) => new HintRecord
        {
            LearningItemId = record.LearningItemId, Position = position, Text = hint.Text,
        }));
        record.AnswerChoices.AddRange(item.DirectAnswerChoices.Select((choice, position) => ToChoice(record.LearningItemId, AnswerChoiceRole.Direct, position, choice)));
        record.AnswerChoices.AddRange(item.AssistanceAnswerChoices.Select((choice, position) => ToChoice(record.LearningItemId, AnswerChoiceRole.Assistance, position, choice)));
        record.AcceptedShortAnswers.AddRange(item.AcceptedShortAnswers.Select((value, position) => new AcceptedShortAnswerRecord
        {
            LearningItemId = record.LearningItemId, Position = position, Value = value,
        }));
        record.QualityReviews.AddRange(item.QualityReviews.Select(review => new QualityReviewRecord
        {
            QualityReviewId = review.Id.Value, LearningItemId = review.LearningItemId.Value,
            ContentRevision = review.ContentRevision, Outcome = (QualityReviewOutcome)review.Outcome,
            EvidenceType = (QualityReviewEvidenceType)review.EvidenceType, Findings = review.Findings,
            SuggestedCorrection = review.SuggestedCorrection, SupersededBy = review.SupersededBy?.Value,
        }));
        record.UserFlagAssignments.AddRange(item.UserFlagDefinitionIds.Select(id => new LearningItemUserFlagRecord
        { LearningItemId = item.Id.Value, UserFlagDefinitionId = id.Value }));
        return record;
    }

    public static LearningItem ToDomain(LearningItemRecord record)
    {
        var choices = record.AnswerChoices.OrderBy(x => x.Role).ThenBy(x => x.Position);
        return LearningItem.Restore(
        LearningItemId.From(record.LearningItemId), record.Prompt, record.ReferenceSolutionContent,
            record.DueAt, record.IsNew, record.ResponseMode,
            record.Hints.OrderBy(x => x.Position).Select(x => new Hint(x.Text)),
            choices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.ChoiceId)),
            choices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.ChoiceId)),
            record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value),
            record.LowInteractionEligible, record.LifecycleState, record.Difficulty, record.StabilityDays,
            record.IsInShortTermRelearning, record.ContentRevision,
            record.QualityReviews.OrderBy(x => x.QualityReviewId).Select(x => QualityReview.Restore(
                QualityReviewId.From(x.QualityReviewId), LearningItemId.From(x.LearningItemId), x.ContentRevision,
                (Illumination.Domain.Learning.QualityReviewOutcome)x.Outcome, (Illumination.Domain.Learning.QualityReviewEvidenceType)x.EvidenceType, x.Findings,
                x.SuggestedCorrection, x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)),
            record.UserFlagAssignments.Select(x => UserFlagDefinitionId.From(x.UserFlagDefinitionId)));
    }

    public static DeckRecord ToRecord(Deck deck)
    {
        var record = new DeckRecord { DeckId = deck.Id.Value, Name = deck.Name };
        record.Memberships.AddRange(deck.LearningItemIds.Select(id => new DeckLearningItemRecord
        {
            DeckId = record.DeckId, LearningItemId = id.Value,
        }));
        record.TopicLabels.AddRange(deck.TopicLabels.Select(label => new DeckTopicLabelRecord
        {
            DeckId = record.DeckId,
            Label = label,
        }));
        record.LearningActivityProfiles.AddRange(deck.LearningActivityProfiles.Select(profile => new DeckLearningActivityProfileRecord
        {
            DeckId = record.DeckId,
            Profile = profile,
        }));
        return record;
    }

    public static Deck ToDomain(DeckRecord record)
    {
        var deck = Deck.Create(
            DeckId.From(record.DeckId),
            record.Name,
            record.TopicLabels.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Select(x => x.Label),
            record.LearningActivityProfiles.OrderBy(x => x.Profile).Select(x => x.Profile));
        foreach (var membership in record.Memberships.OrderBy(x => x.LearningItemId))
        {
            deck.AddLearningItem(LearningItemId.From(membership.LearningItemId));
        }
        return deck;
    }

    private static AnswerChoiceRecord ToChoice(Guid itemId, AnswerChoiceRole role, int position, AnswerChoice choice) => new()
    {
            LearningItemId = itemId, Role = role, Position = position, ChoiceId = string.IsNullOrWhiteSpace(choice.Id) ? null : choice.Id, Text = choice.Text, IsCorrect = choice.IsCorrect,
    };
}
