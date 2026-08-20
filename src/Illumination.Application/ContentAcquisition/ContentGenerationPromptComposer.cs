namespace Illumination.Application.ContentAcquisition;

public sealed record ContentGenerationInventoryItem(Guid LearningItemId, string Prompt, string ReferenceSolution);

/// <summary>
/// Composes the semantic learner/content guidance around the stable Content Bundle contract prompt.
/// Presentation code supplies typed options and inventory only; it does not append prompt semantics.
/// </summary>
public static class ContentGenerationPromptComposer
{
    private const string ContractMarker = "Canonical Content Bundle 1.0 contract guidance:";
    private const string InventoryMarker = "Existing target Deck anti-duplication inventory:";
    private const int InventoryLimit = 250;

    public static GeneratedContentPrompt Compose(
        ContentAcquisitionService service,
        GenerateContentPromptCommand command,
        LanguageGenerationGuidance? languageGuidance = null,
        IReadOnlyList<ContentGenerationInventoryItem>? existingDeckInventory = null,
        string? existingDeckName = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(command);

        // The legacy base generator still knows how to produce the exact Content Bundle contract
        // appendix. Suppress its raw per-item scheduler dump for learning-aware follow-up because
        // the deterministic profile below is the accepted interpretation of that evidence.
        var baseCommand = command.SourceDeckContext is null
            ? command
            : command with { SourceDeckContext = null, ProgressionMode = null };
        var basePrompt = service.GenerateContentPrompt(baseCommand).Prompt;
        var markerIndex = basePrompt.IndexOf(ContractMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException("The generated Content Bundle prompt is missing its canonical contract guidance marker.");

        var semanticPart = basePrompt[..markerIndex].TrimEnd();
        var contractPart = basePrompt[markerIndex..].Trim();
        var composed = new GeneratedContentPrompt(semanticPart);

        if (command.SourceDeckContext is not null)
            composed = LearningGenerationProfilePromptGuidance.Apply(composed, command.SourceDeckContext, command.ProgressionMode);

        if (languageGuidance is not null)
            composed = LanguageContentPromptGuidance.Apply(composed, languageGuidance);

        if (existingDeckInventory is { Count: > 0 })
            composed = ApplyExistingInventory(composed, existingDeckInventory, existingDeckName, command.ProgressionMode);

        return new GeneratedContentPrompt(
            composed.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine +
            contractPart + Environment.NewLine);
    }

    private static GeneratedContentPrompt ApplyExistingInventory(
        GeneratedContentPrompt prompt,
        IReadOnlyList<ContentGenerationInventoryItem> inventory,
        string? deckName,
        FollowUpProgressionMode? progressionMode)
    {
        if (prompt.Prompt.Contains(InventoryMarker, StringComparison.Ordinal)) return prompt;

        var distinct = inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.Prompt) && !string.IsNullOrWhiteSpace(item.ReferenceSolution))
            .GroupBy(item => item.LearningItemId)
            .Select(group => group.First())
            .OrderBy(item => item.Prompt, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinct.Length == 0) return prompt;

        var included = distinct.Take(InventoryLimit).ToArray();
        var lines = new List<string>
        {
            InventoryMarker,
            $"The existing Deck{(string.IsNullOrWhiteSpace(deckName) ? string.Empty : $" `{deckName}`")} contains {distinct.Length} Learning Item(s). The bounded inventory below contains {included.Length} item(s) for duplicate avoidance.",
            "Never create an exact prompt/referenceSolution duplicate or a cosmetic near-duplicate of listed content.",
            progressionMode == FollowUpProgressionMode.Reinforce
                ? "Reinforce may revisit the same underlying knowledge when learning evidence calls for it, but use a genuinely different cue, example, application or scaffold rather than recreating the same card."
                : "For extension, prefer genuinely new knowledge, useful next material, or new applications rather than paraphrasing cards that already exist.",
        };
        if (distinct.Length > InventoryLimit)
            lines.Add($"The Deck is larger than the prompt inventory limit; {distinct.Length - InventoryLimit} item(s) are omitted from this appendix. Do not infer that omitted content is absent from the Deck.");

        lines.AddRange(included.Select(item => $"- {Compact(item.Prompt)} => {Compact(item.ReferenceSolution)}"));
        return new GeneratedContentPrompt(
            prompt.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static string Compact(string value)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 180 ? normalized : normalized[..177] + "...";
    }
}
