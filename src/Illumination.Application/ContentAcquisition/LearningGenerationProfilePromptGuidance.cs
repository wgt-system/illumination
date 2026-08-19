using Illumination.Application.Insights;
using Illumination.Application.Study;

namespace Illumination.Application.ContentAcquisition;

public sealed record LearningGenerationProfile(
    int TotalItemCount,
    int ReviewedItemCount,
    int UnreviewedItemCount,
    int NewItemCount,
    int RelearningItemCount,
    AssessmentDistribution AssessmentDistribution,
    IReadOnlyList<LearningGenerationEvidenceItem> ReinforcementCandidates,
    IReadOnlyList<LearningGenerationEvidenceItem> EstablishedCandidates,
    IReadOnlyList<LearningGenerationEvidenceItem> UnreviewedExamples);

public sealed record LearningGenerationEvidenceItem(
    Guid LearningItemId,
    string Prompt,
    string ReferenceSolution,
    int ReviewCount,
    StudyLearningAssessment? LastConfirmedAssessment,
    int WeakAssessmentCount,
    int PositiveAssessmentCount,
    bool IsNew,
    bool IsInShortTermRelearning,
    double Difficulty,
    double StabilityDays);

/// <summary>
/// Converts existing Illumination learning evidence into a compact prompt-facing brief.
/// The profile is derived guidance only; it does not create a second Learning State or a mastery score.
/// </summary>
public static class LearningGenerationProfilePromptGuidance
{
    private const string Marker = "Illumination-derived learning generation profile:";
    private const int EvidenceExampleLimit = 12;

    public static LearningGenerationProfile Build(DeckLearningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = context.Items;
        var distribution = new AssessmentDistribution(
            items.Sum(item => item.AssessmentDistribution.Nochmal),
            items.Sum(item => item.AssessmentDistribution.Schwer),
            items.Sum(item => item.AssessmentDistribution.Unsicher),
            items.Sum(item => item.AssessmentDistribution.Gut),
            items.Sum(item => item.AssessmentDistribution.Leicht));

        var evidence = items.Select(ToEvidence).ToArray();
        var reinforcement = evidence
            .Where(IsReinforcementCandidate)
            .OrderByDescending(item => item.IsInShortTermRelearning)
            .ThenByDescending(item => LastAssessmentWeakness(item.LastConfirmedAssessment))
            .ThenByDescending(item => item.WeakAssessmentCount)
            .ThenByDescending(item => item.Difficulty)
            .ThenBy(item => item.StabilityDays)
            .ThenBy(item => item.Prompt, StringComparer.OrdinalIgnoreCase)
            .Take(EvidenceExampleLimit)
            .ToArray();

        var established = evidence
            .Where(item => item.ReviewCount > 0 && !item.IsNew && !item.IsInShortTermRelearning)
            .Where(item => item.LastConfirmedAssessment is StudyLearningAssessment.Gut or StudyLearningAssessment.Leicht)
            .Where(item => !IsReinforcementCandidate(item))
            .OrderByDescending(item => item.StabilityDays)
            .ThenByDescending(item => item.ReviewCount)
            .ThenBy(item => item.Prompt, StringComparer.OrdinalIgnoreCase)
            .Take(EvidenceExampleLimit)
            .ToArray();

        var unreviewed = evidence
            .Where(item => item.ReviewCount == 0)
            .OrderBy(item => item.Prompt, StringComparer.OrdinalIgnoreCase)
            .Take(EvidenceExampleLimit)
            .ToArray();

        return new LearningGenerationProfile(
            items.Count,
            items.Count(item => item.ReviewCount > 0),
            items.Count(item => item.ReviewCount == 0),
            items.Count(item => item.IsNew),
            items.Count(item => item.IsInShortTermRelearning),
            distribution,
            reinforcement,
            established,
            unreviewed);
    }

    public static GeneratedContentPrompt Apply(
        GeneratedContentPrompt prompt,
        DeckLearningContext context,
        FollowUpProgressionMode? progressionMode)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        if (prompt.Prompt.Contains(Marker, StringComparison.Ordinal)) return prompt;

        var profile = Build(context);
        var lines = new List<string>
        {
            Marker,
            $"Source Deck: {context.DeckName} ({profile.TotalItemCount} current Learning Items).",
            $"- Coverage: reviewed={profile.ReviewedItemCount}, unreviewed={profile.UnreviewedItemCount}, currently new={profile.NewItemCount}, short-term relearning={profile.RelearningItemCount}.",
            $"- Confirmed assessment history: Nochmal={profile.AssessmentDistribution.Nochmal}, Schwer={profile.AssessmentDistribution.Schwer}, Unsicher={profile.AssessmentDistribution.Unsicher}, Gut={profile.AssessmentDistribution.Gut}, Leicht={profile.AssessmentDistribution.Leicht}.",
            "- This profile is deterministically derived by Illumination from existing learning evidence. Treat it as the learner-context interpretation; do not invent a mastery score or reinterpret scheduler formulas from raw difficulty/stability fields elsewhere in the prompt.",
            ProgressionDirective(progressionMode),
        };

        AppendEvidence(lines, "Reinforcement candidates", profile.ReinforcementCandidates, reinforcement: true);
        AppendEvidence(lines, "Comparatively established material", profile.EstablishedCandidates, reinforcement: false);
        AppendEvidence(lines, "Unreviewed/new examples", profile.UnreviewedExamples, reinforcement: false);

        lines.Add("Generate against the pattern of evidence, not merely the most memorable source example. Preserve prerequisite coherence and prefer fewer high-value items over filler.");

        return new GeneratedContentPrompt(
            prompt.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static LearningGenerationEvidenceItem ToEvidence(DeckLearningContextItem item) =>
        new(
            item.LearningItemId,
            item.Prompt,
            item.ReferenceSolution,
            item.ReviewCount,
            item.LastConfirmedAssessment,
            item.AssessmentDistribution.Nochmal + item.AssessmentDistribution.Schwer + item.AssessmentDistribution.Unsicher,
            item.AssessmentDistribution.Gut + item.AssessmentDistribution.Leicht,
            item.IsNew,
            item.IsInShortTermRelearning,
            item.Difficulty,
            item.StabilityDays);

    private static bool IsReinforcementCandidate(LearningGenerationEvidenceItem item) =>
        item.IsInShortTermRelearning ||
        item.LastConfirmedAssessment is StudyLearningAssessment.Nochmal or StudyLearningAssessment.Schwer or StudyLearningAssessment.Unsicher ||
        (item.ReviewCount > 0 && item.WeakAssessmentCount > item.PositiveAssessmentCount);

    private static int LastAssessmentWeakness(StudyLearningAssessment? assessment) => assessment switch
    {
        StudyLearningAssessment.Nochmal => 3,
        StudyLearningAssessment.Schwer => 2,
        StudyLearningAssessment.Unsicher => 1,
        _ => 0,
    };

    private static string ProgressionDirective(FollowUpProgressionMode? progressionMode) => progressionMode switch
    {
        FollowUpProgressionMode.Reinforce =>
            "- Progression use: Reinforce should concentrate on the reinforcement pattern below, vary the wording/application enough to avoid cosmetic duplicates, and scaffold weak prerequisites rather than restarting the entire Deck from trivial material.",
        FollowUpProgressionMode.Continue =>
            "- Progression use: Continue should introduce genuinely new material at the current level while respecting weak prerequisites; established material is a foundation, not a request to regenerate it.",
        FollowUpProgressionMode.Advance =>
            "- Progression use: Advance should build from comparatively established material, but weak/relearning evidence below is a prerequisite caution. Do not jump beyond prerequisites merely because some items are stable.",
        _ =>
            "- Progression use: balance new material with explicit weak prerequisites; do not infer that unreviewed items are already learned.",
    };

    private static void AppendEvidence(
        ICollection<string> lines,
        string heading,
        IReadOnlyList<LearningGenerationEvidenceItem> items,
        bool reinforcement)
    {
        if (items.Count == 0)
        {
            lines.Add($"- {heading}: none identified from current evidence.");
            return;
        }

        lines.Add($"- {heading} ({items.Count} representative item(s), bounded):");
        foreach (var item in items)
        {
            var evidence = reinforcement
                ? $"reviews={item.ReviewCount}, weakAssessments={item.WeakAssessmentCount}, last={item.LastConfirmedAssessment?.ToString() ?? "none"}, relearning={item.IsInShortTermRelearning}"
                : $"reviews={item.ReviewCount}, last={item.LastConfirmedAssessment?.ToString() ?? "none"}, new={item.IsNew}";
            lines.Add($"  - {Compact(item.Prompt)} => {Compact(item.ReferenceSolution)} [{evidence}]");
        }
    }

    private static string Compact(string value)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 160 ? normalized : normalized[..157] + "...";
    }
}
