using System.Text.Json;
using Illumination.Application.ContentManagement;

namespace Illumination.Application.ContentAcquisition;

public sealed record ExistingContentImprovementPrompt(string Prompt, int LearningItemCount);

public sealed class ExistingContentImprovementPromptService
{
    private const string SchemaResourceName = "Illumination.Application.Schemas.illumination-content-bundle-1.0.schema.json";
    private readonly ContentManagementService _content;

    public ExistingContentImprovementPromptService(ContentManagementService content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public async Task<ExistingContentImprovementPrompt> GenerateAsync(
        IReadOnlyList<Guid> learningItemIds,
        ContentUpdateSignificance significance,
        string guidance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(learningItemIds);
        if (learningItemIds.Count == 0) throw new ArgumentException("Select at least one Learning Item to improve.", nameof(learningItemIds));
        if (string.IsNullOrWhiteSpace(guidance)) throw new ArgumentException("Improvement guidance is required.", nameof(guidance));

        var ids = learningItemIds.Distinct().ToArray();
        var items = new List<LearningItemView>(ids.Length);
        foreach (var id in ids) items.Add(await _content.GetLearningItemAsync(id, cancellationToken));

        var significanceValue = significance == ContentUpdateSignificance.Semantic ? "semantic" : "minor";
        var significanceExplanation = significance == ContentUpdateSignificance.Semantic
            ? "These are intentional semantic changes. Illumination will preserve immutable Review history and memberships but reset current scheduling to new-item defaults after import."
            : "These are minor content changes. Preserve the intended learning objective; Illumination will preserve current scheduling, lifecycle, memberships, and Review history.";
        var snapshots = string.Join(Environment.NewLine + Environment.NewLine, items.Select(FormatItem));

        var prompt = $"""
You are improving existing Illumination Learning Items.

Return a Content Bundle 1.0 JSON file only. Do not create new Learning Items or Decks. Do not delete, suspend, master, or reassign anything. Return exactly one `update_learning_item` operation for each supplied stable `itemId` and do not change those stable item IDs.

Requested update significance: `{significanceValue}`.
{significanceExplanation}

User improvement guidance:
{guidance.Trim()}

For every update operation:
- use `op`: `update_learning_item`;
- preserve the supplied stable `itemId` exactly;
- use `significance`: `{significanceValue}`;
- return the complete Content Bundle 1.0 `item` payload, not a partial patch;
- keep prompt/referenceSolution aligned;
- preserve valid authored choice IDs when the corresponding choices remain conceptually the same;
- use only response modes supported by Content Bundle 1.0;
- do not invent factual changes merely to make an edit;
- if the requested improvement does not require changing an item, reproduce its current content faithfully rather than forcing a gratuitous change.

Existing Learning Items:
{snapshots}

Canonical Content Bundle 1.0 schema:
{CanonicalSchemaText()}
""";

        return new ExistingContentImprovementPrompt(prompt, items.Count);
    }

    private static string FormatItem(LearningItemView item)
    {
        var payload = new
        {
            prompt = item.Prompt,
            referenceSolution = item.ReferenceSolution,
            responseMode = ToContract(item.ResponseMode),
            hints = item.Hints.Select(x => x.Text).ToArray(),
            directAnswerChoices = item.DirectAnswerChoices.Select(choice => new { id = choice.Id, text = choice.Text, correct = choice.IsCorrect }).ToArray(),
            assistanceAnswerChoices = item.AssistanceAnswerChoices.Select(choice => new { id = choice.Id, text = choice.Text, correct = choice.IsCorrect }).ToArray(),
            acceptedShortAnswers = item.AcceptedShortAnswers,
            lowInteractionEligible = item.LowInteractionEligible,
        };
        return $"itemId: {item.Id:D}{Environment.NewLine}currentItem: {JsonSerializer.Serialize(payload)}";
    }

    private static string ToContract(LearningItemResponseMode mode) => mode switch
    {
        LearningItemResponseMode.SelfAssessed => "self_assessed",
        LearningItemResponseMode.Selection => "selection",
        LearningItemResponseMode.ShortText => "short_text",
        LearningItemResponseMode.Code => "code",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported ResponseMode."),
    };

    private static string CanonicalSchemaText()
    {
        using var stream = typeof(ContentAcquisitionService).Assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException("The canonical Content Bundle 1.0 schema resource is unavailable.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
