namespace Illumination.Domain.Learning;

public sealed class LearningState
{
    internal LearningState(DateTimeOffset dueAt)
        : this(isNew: true, dueAt: dueAt)
    {
    }

    internal LearningState(bool isNew, DateTimeOffset dueAt)
        : this(isNew, dueAt, difficulty: 5.0, stabilityDays: 0.5, isInShortTermRelearning: false)
    {
    }

    internal LearningState(
        bool isNew,
        DateTimeOffset dueAt,
        double difficulty,
        double stabilityDays,
        bool isInShortTermRelearning)
    {
        ValidateSchedulingState(difficulty, stabilityDays);
        DueAt = dueAt;
        IsNew = isNew;
        Difficulty = difficulty;
        StabilityDays = stabilityDays;
        IsInShortTermRelearning = isInShortTermRelearning;
    }

    public bool IsNew { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public double Difficulty { get; private set; }

    public double StabilityDays { get; private set; }

    public bool IsInShortTermRelearning { get; private set; }

    public bool IsDueAt(DateTimeOffset instant) => DueAt <= instant;

    internal void MarkImmediatelyDue(DateTimeOffset dueAt)
    {
        DueAt = dueAt;
    }

    internal void ResetForSemanticContentChange(DateTimeOffset resetAt)
    {
        IsNew = true;
        DueAt = resetAt;
        Difficulty = 5.0;
        StabilityDays = 0.5;
        IsInShortTermRelearning = false;
    }

    public LearningStateProjection ProjectReview(DateTimeOffset completedAt, LearningAssessment assessment)
    {
        if (!Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        var difficulty = Math.Clamp(Difficulty + DifficultyDelta(assessment), 1.0, 10.0);
        var stabilityDays = StabilityDays;
        var reinforcementRequired = true;
        DateTimeOffset dueAt = completedAt;

        switch (assessment)
        {
            case LearningAssessment.Nochmal:
                stabilityDays = Math.Min(StabilityDays * 0.25, 3.0);
                break;
            case LearningAssessment.Schwer:
                stabilityDays = Math.Min(StabilityDays * 0.55, 7.0);
                break;
            case LearningAssessment.Unsicher:
                break;
            case LearningAssessment.Gut:
                stabilityDays = CalculateGraduatingStability(stabilityDays, difficulty, baseGrowth: 2.20, minimum: 2.0);
                reinforcementRequired = false;
                dueAt = completedAt.AddDays(stabilityDays);
                break;
            case LearningAssessment.Leicht:
                stabilityDays = CalculateGraduatingStability(stabilityDays, difficulty, baseGrowth: 3.60, minimum: 4.0);
                reinforcementRequired = false;
                dueAt = completedAt.AddDays(stabilityDays);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(assessment), assessment, "Unsupported Learning Assessment.");
        }

        return new LearningStateProjection(
            IsNew: false,
            DueAt: dueAt,
            Difficulty: difficulty,
            StabilityDays: stabilityDays,
            IsInShortTermRelearning: reinforcementRequired);
    }

    internal void ApplyReview(DateTimeOffset completedAt, LearningAssessment assessment)
    {
        var projection = ProjectReview(completedAt, assessment);
        IsNew = projection.IsNew;
        Difficulty = projection.Difficulty;
        StabilityDays = projection.StabilityDays;
        IsInShortTermRelearning = projection.IsInShortTermRelearning;
        DueAt = projection.DueAt;
    }

    private static double CalculateGraduatingStability(double oldStability, double difficulty, double baseGrowth, double minimum)
    {
        var difficultyFactor = Math.Clamp(1.15 - (0.07 * difficulty), 0.45, 1.05);
        var effectiveGrowth = 1 + ((baseGrowth - 1) * difficultyFactor);
        return Math.Max(minimum, oldStability * effectiveGrowth);
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

    private static void ValidateSchedulingState(double difficulty, double stabilityDays)
    {
        if (double.IsNaN(difficulty) || double.IsInfinity(difficulty) || difficulty < 1.0 || difficulty > 10.0)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty must be between 1.0 and 10.0.");
        }

        if (double.IsNaN(stabilityDays) || double.IsInfinity(stabilityDays) || stabilityDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stabilityDays), stabilityDays, "Stability must be greater than zero.");
        }
    }
}
