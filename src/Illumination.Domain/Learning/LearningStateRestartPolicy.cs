namespace Illumination.Domain.Learning;

public sealed record LearningStateRestartProjection(
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning);

public static class LearningStateRestartPolicy
{
    public static LearningStateRestartProjection Project(DateTimeOffset restartedAt) =>
        new(
            IsNew: true,
            DueAt: restartedAt,
            Difficulty: 5.0,
            StabilityDays: 0.5,
            IsInShortTermRelearning: false);
}
