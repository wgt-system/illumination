using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;

namespace Illumination.Infrastructure.Persistence;

public sealed class LearningItemRecord
{
    public Guid LearningItemId { get; set; }
    public string Prompt { get; set; } = null!;
    public string ReferenceSolutionContent { get; set; } = null!;
    public ResponseMode ResponseMode { get; set; }
    public bool LowInteractionEligible { get; set; }
    public LearningItemLifecycleState LifecycleState { get; set; }
    public bool IsNew { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public double Difficulty { get; set; }
    public double StabilityDays { get; set; }
    public bool IsInShortTermRelearning { get; set; }
    public int? InterveningCardTarget { get; set; }
    public List<HintRecord> Hints { get; } = [];
    public List<AnswerChoiceRecord> AnswerChoices { get; } = [];
    public List<AcceptedShortAnswerRecord> AcceptedShortAnswers { get; } = [];
    public List<DeckLearningItemRecord> DeckMemberships { get; } = [];
}

public sealed class HintRecord
{
    public Guid LearningItemId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = null!;
    public LearningItemRecord LearningItem { get; set; } = null!;
}

public enum AnswerChoiceRole
{
    Direct,
    Assistance,
}

public sealed class AnswerChoiceRecord
{
    public Guid LearningItemId { get; set; }
    public AnswerChoiceRole Role { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public LearningItemRecord LearningItem { get; set; } = null!;
}

public sealed class AcceptedShortAnswerRecord
{
    public Guid LearningItemId { get; set; }
    public int Position { get; set; }
    public string Value { get; set; } = null!;
    public LearningItemRecord LearningItem { get; set; } = null!;
}

public sealed class DeckRecord
{
    public Guid DeckId { get; set; }
    public string Name { get; set; } = null!;
    public List<DeckLearningItemRecord> Memberships { get; } = [];
}

public sealed class DeckLearningItemRecord
{
    public Guid DeckId { get; set; }
    public Guid LearningItemId { get; set; }
    public DeckRecord Deck { get; set; } = null!;
    public LearningItemRecord LearningItem { get; set; } = null!;
}

public sealed class ReviewRecord
{
    public Guid ReviewId { get; set; }
    public Guid LearningItemId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public LearningAssessment Assessment { get; set; }
    public string? SubmittedResponse { get; set; }
    public LearningItemRecord LearningItem { get; set; } = null!;
    public List<StudySessionReviewRecord> StudySessionAssociations { get; } = [];
}

public sealed class StudySessionRecord
{
    public Guid StudySessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<StudySessionDeckRecord> SelectedDecks { get; } = [];
    public List<StudySessionQueueRecord> Queue { get; } = [];
    public List<StudySessionReviewRecord> Reviews { get; } = [];
}

public sealed class StudySessionDeckRecord
{
    public Guid StudySessionId { get; set; }
    public Guid DeckId { get; set; }
    public StudySessionRecord StudySession { get; set; } = null!;
}

public sealed class StudySessionQueueRecord
{
    public Guid StudySessionId { get; set; }
    public int Position { get; set; }
    public Guid LearningItemId { get; set; }
    public StudySessionRecord StudySession { get; set; } = null!;
}

public sealed class StudySessionReviewRecord
{
    public Guid StudySessionId { get; set; }
    public int Position { get; set; }
    public Guid ReviewId { get; set; }
    public StudySessionRecord StudySession { get; set; } = null!;
    public ReviewRecord Review { get; set; } = null!;
}
