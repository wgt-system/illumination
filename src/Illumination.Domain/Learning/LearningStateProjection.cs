namespace Illumination.Domain.Learning;

public sealed record LearningStateProjection(
    bool IsNew,
    DateTimeOffset DueAt,
    double Difficulty,
    double StabilityDays,
    bool IsInShortTermRelearning);
