namespace Illumination.Domain.Learning;

public sealed class LearningState
{
    internal LearningState(DateTimeOffset dueAt)
        : this(isNew: true, dueAt: dueAt)
    {
    }

    internal LearningState(bool isNew, DateTimeOffset dueAt)
        : this(isNew, dueAt, difficulty: 5.0, stabilityDays: 0.5, isInShortTermRelearning: false, interveningCardTarget: null)
    {
    }

    internal LearningState(
        bool isNew,
        DateTimeOffset dueAt,
        double difficulty,
        double stabilityDays,
        bool isInShortTermRelearning,
        int? interveningCardTarget)
    {
        ValidateSchedulingState(difficulty, stabilityDays, isInShortTermRelearning, interveningCardTarget);
        DueAt = dueAt;
        IsNew = isNew;
        Difficulty = difficulty;
        StabilityDays = stabilityDays;
        IsInShortTermRelearning = isInShortTermRelearning;
        InterveningCardTarget = interveningCardTarget;
    }

    public bool IsNew { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public double Difficulty { get; private set; }

    public double StabilityDays { get; private set; }

    public bool IsInShortTermRelearning { get; private set; }

    public int? InterveningCardTarget { get; private set; }

    public bool IsDueAt(DateTimeOffset instant) => DueAt <= instant;

    internal void MarkImmediatelyDue(DateTimeOffset dueAt)
    {
        DueAt = dueAt;
    }

    internal void ApplyReview(DateTimeOffset completedAt, LearningAssessment assessment)
    {
        if (!Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        var difficulty = Math.Clamp(Difficulty + DifficultyDelta(assessment), 1.0, 10.0);
        var stabilityDays = StabilityDays;
        var isInShortTermRelearning = false;
        int? interveningCardTarget = null;
        DateTimeOffset dueAt;

        switch (assessment)
        {
            case LearningAssessment.Nochmal:
                stabilityDays = Math.Min(StabilityDays * 0.25, 3.0);
                isInShortTermRelearning = true;
                interveningCardTarget = 3;
                dueAt = completedAt;
                break;
            case LearningAssessment.Schwer:
                stabilityDays = Math.Min(StabilityDays * 0.55, 7.0);
                isInShortTermRelearning = true;
                interveningCardTarget = 10;
                dueAt = completedAt;
                break;
            case LearningAssessment.Unsicher:
            case LearningAssessment.Gut:
            case LearningAssessment.Leicht:
                var baseGrowth = assessment switch
                {
                    LearningAssessment.Unsicher => 1.20,
                    LearningAssessment.Gut => 2.20,
                    LearningAssessment.Leicht => 3.60,
                    _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment."),
                };
                var minimumStability = assessment switch
                {
                    LearningAssessment.Unsicher => 1.0,
                    LearningAssessment.Gut => 2.0,
                    LearningAssessment.Leicht => 4.0,
                    _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment."),
                };
                var difficultyFactor = Math.Clamp(1.15 - (0.07 * difficulty), 0.45, 1.05);
                var effectiveGrowth = 1 + ((baseGrowth - 1) * difficultyFactor);
                stabilityDays = Math.Max(minimumStability, StabilityDays * effectiveGrowth);
                dueAt = completedAt.AddDays(stabilityDays);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        IsNew = false;
        Difficulty = difficulty;
        StabilityDays = stabilityDays;
        IsInShortTermRelearning = isInShortTermRelearning;
        InterveningCardTarget = interveningCardTarget;
        DueAt = dueAt;
    }

    private static double DifficultyDelta(LearningAssessment assessment) => assessment switch
    {
        LearningAssessment.Nochmal => 1.20,
        LearningAssessment.Schwer => 0.60,
        LearningAssessment.Unsicher => 0.15,
        LearningAssessment.Gut => -0.20,
        LearningAssessment.Leicht => -0.45,
        _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment."),
    };

    private static void ValidateSchedulingState(
        double difficulty,
        double stabilityDays,
        bool isInShortTermRelearning,
        int? interveningCardTarget)
    {
        if (double.IsNaN(difficulty) || double.IsInfinity(difficulty) || difficulty < 1.0 || difficulty > 10.0)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty must be between 1.0 and 10.0.");
        }

        if (double.IsNaN(stabilityDays) || double.IsInfinity(stabilityDays) || stabilityDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stabilityDays), stabilityDays, "Stability must be greater than zero.");
        }

        if (!isInShortTermRelearning && interveningCardTarget is not null)
        {
            throw new ArgumentException("A relearning target requires short-term relearning.", nameof(interveningCardTarget));
        }

        if (isInShortTermRelearning && interveningCardTarget is not (3 or 10))
        {
            throw new ArgumentException("Short-term relearning requires a target of 3 or 10.", nameof(interveningCardTarget));
        }
    }
}
