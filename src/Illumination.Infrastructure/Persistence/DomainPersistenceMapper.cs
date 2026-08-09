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
        return record;
    }

    public static LearningItem ToDomain(LearningItemRecord record)
    {
        var choices = record.AnswerChoices.OrderBy(x => x.Role).ThenBy(x => x.Position);
        return LearningItem.Rehydrate(
            LearningItemId.From(record.LearningItemId), record.Prompt, record.ReferenceSolutionContent,
            record.DueAt, record.IsNew, record.ResponseMode,
            record.Hints.OrderBy(x => x.Position).Select(x => new Hint(x.Text)),
            choices.Where(x => x.Role == AnswerChoiceRole.Direct).OrderBy(x => x.Position).Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
            choices.Where(x => x.Role == AnswerChoiceRole.Assistance).OrderBy(x => x.Position).Select(x => new AnswerChoice(x.Text, x.IsCorrect)),
            record.AcceptedShortAnswers.OrderBy(x => x.Position).Select(x => x.Value),
            record.LowInteractionEligible, record.LifecycleState);
    }

    public static DeckRecord ToRecord(Deck deck)
    {
        var record = new DeckRecord { DeckId = deck.Id.Value, Name = deck.Name };
        record.Memberships.AddRange(deck.LearningItemIds.Select(id => new DeckLearningItemRecord
        {
            DeckId = record.DeckId, LearningItemId = id.Value,
        }));
        return record;
    }

    public static Deck ToDomain(DeckRecord record)
    {
        var deck = Deck.Create(DeckId.From(record.DeckId), record.Name);
        foreach (var membership in record.Memberships.OrderBy(x => x.LearningItemId))
        {
            deck.AddLearningItem(LearningItemId.From(membership.LearningItemId));
        }
        return deck;
    }

    private static AnswerChoiceRecord ToChoice(Guid itemId, AnswerChoiceRole role, int position, AnswerChoice choice) => new()
    {
        LearningItemId = itemId, Role = role, Position = position, Text = choice.Text, IsCorrect = choice.IsCorrect,
    };
}
