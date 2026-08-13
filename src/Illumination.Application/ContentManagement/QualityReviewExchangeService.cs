using System.Text.Json;
using System.Text.Json.Nodes;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Json.Schema;

namespace Illumination.Application.ContentManagement;

public sealed class QualityReviewExchangeService
{
    public const string Contract = "illumination.quality-review-result";
    public const string Version = "1.0";
    private const string SchemaResourceName = "Illumination.Application.Schemas.illumination-quality-review-result-1.0.schema.json";
    private readonly IContentPersistence _contentPersistence;
    private readonly ContentCurationService _curation;

    public QualityReviewExchangeService(IContentPersistence contentPersistence, IUserFlagDefinitionPersistence flagPersistence)
    {
        _contentPersistence = contentPersistence ?? throw new ArgumentNullException(nameof(contentPersistence));
        _curation = new ContentCurationService(contentPersistence, flagPersistence ?? throw new ArgumentNullException(nameof(flagPersistence)));
    }

    public async Task<GeneratedQualityReviewPrompt> GeneratePromptAsync(
        GenerateQualityReviewPromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var ids = command.LearningItemIds?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(command.LearningItemIds));
        if (ids.Length == 0) throw new ArgumentException("At least one Learning Item is required.", nameof(command));
        if (!Enum.IsDefined(command.Mode)) throw new ArgumentOutOfRangeException(nameof(command.Mode), command.Mode, "Unsupported quality review prompt mode.");
        var snapshots = await _contentPersistence.ListLearningItemsAsync(cancellationToken);
        var selected = ids.Select(id => snapshots.SingleOrDefault(item => item.Id == id) ?? throw new ContentNotFoundException($"Learning Item '{id}' was not found.")).ToArray();
        var modeGuidance = command.Mode switch
        {
            QualityReviewPromptMode.Standard => "Review for correctness, clarity, ambiguity, and self-contained wording.",
            QualityReviewPromptMode.Strict => "Review rigorously for factual correctness, ambiguity, hidden assumptions, and answerability. Be conservative when evidence is insufficient.",
            QualityReviewPromptMode.SourceGrounded => "Review rigorously and provide source/evidence information in findings for every material factual claim. Source-grounded evidence is not a generic Verified state.",
            _ => throw new ArgumentOutOfRangeException(nameof(command.Mode), command.Mode, "Unsupported quality review prompt mode."),
        };
        var requiredEvidenceType = command.Mode == QualityReviewPromptMode.SourceGrounded
            ? "source_grounded_review"
            : "model_review";
        var guidance = string.IsNullOrWhiteSpace(command.AdditionalGuidance) ? string.Empty : $"Additional guidance: {command.AdditionalGuidance}";
        var items = string.Join(Environment.NewLine + Environment.NewLine, selected.Select(item => $"Learning Item ID: {item.Id:D}\nContentRevision: {item.ContentRevision}\nPrompt: {item.Prompt}\nReference Solution: {item.ReferenceSolution}"));
        var prompt = string.Join(Environment.NewLine, new[]
        {
            "You are reviewing existing Illumination Learning Items. This is a review exchange only; do not modify content.",
            string.Empty,
            $"Return JSON only using contract \"{Contract}\" version \"{Version}\". Return one result for each supplied item. Preserve the exact learningItemId and contentRevision. Use outcome pass, warning, or needs_review; emit evidenceType \"{requiredEvidenceType}\" for every result. Include human-readable findings and optionally suggestedCorrection. Do not use a generic Verified state.",
            string.Empty,
            $"Mode guidance: {modeGuidance}",
            guidance,
            string.Empty,
            "Items:",
            items,
            string.Empty,
            "JSON shape example:",
            $"{{\"contract\":\"{Contract}\",\"version\":\"{Version}\",\"results\":[{{\"learningItemId\":\"...\",\"contentRevision\":1,\"outcome\":\"pass\",\"evidenceType\":\"model_review\",\"findings\":\"...\"}}]}}"
        });
        return new GeneratedQualityReviewPrompt(prompt);
    }

    public async Task<QualityReviewExchangePreview> PreviewAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        var parsed = Parse(rawJson);
        if (parsed.Root is null) return new(false, parsed.BundleDiagnostics, []);
        var snapshots = await _contentPersistence.ListLearningItemsAsync(cancellationToken);
        var previews = ValidateResults(parsed, snapshots);
        return new(parsed.BundleDiagnostics.Count == 0 && previews.All(x => x.IsValid), parsed.BundleDiagnostics, previews);
    }

    public async Task<QualityReviewExchangeAcceptanceResult> AcceptSelectedAsync(
        AcceptQualityReviewResultsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var parsed = Parse(command.RawJson);
        var snapshots = await _contentPersistence.ListLearningItemsAsync(cancellationToken);
        var previews = ValidateResults(parsed, snapshots);
        var selected = command.SelectedResultIndices.Distinct().Order().ToArray();
        var diagnostics = new List<QualityReviewResultDiagnostic>(parsed.BundleDiagnostics);
        if (selected.Any(index => index < 0 || index >= previews.Count)) diagnostics.Add(new("selection.index", "Selected result index is outside the result set."));
        foreach (var preview in previews.Where(x => selected.Contains(x.ResultIndex)))
        {
            diagnostics.AddRange(preview.Diagnostics);
            if (!preview.IsValid) diagnostics.Add(new("selection.invalid", "Selected result is not valid.", preview.ResultIndex));
        }
        if (diagnostics.Count > 0) throw new QualityReviewExchangeValidationException("Quality Review Results cannot be accepted.", diagnostics);

        var accepted = new List<CuratedLearningItemView>();
        foreach (var index in selected)
        {
            var result = parsed.Results[index];
            var itemId = Guid.Parse(result.GetProperty("learningItemId").GetString()!);
            var commandResult = new AcceptQualityReviewCommand(
                ToApplicationOutcome(result.GetProperty("outcome").GetString()!),
                ToApplicationEvidence(result.GetProperty("evidenceType").GetString()!),
                result.GetProperty("findings").GetString()!,
                result.TryGetProperty("suggestedCorrection", out var correction) ? correction.GetString() : null);
            accepted.Add(await _curation.AcceptQualityReviewAsync(itemId, commandResult, cancellationToken));
        }
        return new(accepted);
    }

    private static ParsedResults Parse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new(null, [new("json.empty", "JSON text is required.")], []);
        JsonDocument document;
        try { document = JsonDocument.Parse(rawJson); } catch (JsonException) { return new(null, [new("json.malformed", "JSON text is malformed.")], []); }
        using (document)
        {
            var root = document.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object) return new(root, [new("result.root", "Result root must be an object.")], []);
            var diagnostics = new List<QualityReviewResultDiagnostic>();
            if (StringProperty(root, "contract") != Contract) diagnostics.Add(new("result.contract", "Unsupported or missing contract."));
            if (StringProperty(root, "version") != Version) diagnostics.Add(new("result.version", "Unsupported or missing version."));
            var results = root.TryGetProperty("results", out var resultArray) && resultArray.ValueKind == JsonValueKind.Array
                ? resultArray.EnumerateArray().Select(x => x.Clone()).ToArray()
                : [];
            if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array) diagnostics.Add(new("result.results", "Results must be an array."));
            else if (resultsElement.GetArrayLength() == 0) diagnostics.Add(new("result.results", "Results must contain at least one result."));
            var schemaDiagnostic = EvaluateEnvelopeSchema(root, results.Length);
            if (schemaDiagnostic is not null) diagnostics.Add(schemaDiagnostic);
            return new(root, diagnostics, results);
        }
    }

    private static QualityReviewResultDiagnostic? EvaluateEnvelopeSchema(JsonElement root, int count)
    {
        try
        {
            var schema = JsonSchema.FromText(SchemaText());
            var envelope = JsonNode.Parse(root.GetRawText())!.AsObject();
            if (count == 0) return new("result.schema", "Result envelope does not conform to the canonical schema.");
            envelope["results"] = new JsonArray { new JsonObject { ["learningItemId"] = "envelope-check", ["contentRevision"] = 1, ["outcome"] = "pass", ["evidenceType"] = "model_review", ["findings"] = "Envelope check" } };
            return schema.Evaluate(envelope).IsValid ? null : new("result.schema", "Result envelope does not conform to the canonical schema.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return new("result.schema", "Canonical result schema could not be evaluated."); }
    }

    private static List<QualityReviewResultPreview> ValidateResults(ParsedResults parsed, IReadOnlyList<LearningItemSnapshot> snapshots)
    {
        var result = new List<QualityReviewResultPreview>();
        for (var index = 0; index < parsed.Results.Count; index++)
        {
            var element = parsed.Results[index];
            var diagnostics = new List<QualityReviewResultDiagnostic>();
            var itemId = TryGuid(element, "learningItemId");
            var revision = element.TryGetProperty("contentRevision", out var revisionElement) && revisionElement.TryGetInt32(out var parsedRevision) ? parsedRevision : (int?)null;
            var outcome = TryOutcome(element);
            var evidence = TryEvidence(element);
            var findings = StringProperty(element, "findings");
            var correction = StringProperty(element, "suggestedCorrection");
            var operationSchema = EvaluateResultSchema(element);
            if (operationSchema is not null) diagnostics.Add(new(operationSchema.Code, operationSchema.Message, index));
            if (itemId is null) diagnostics.Add(new("target.item", "learningItemId must be a non-empty Guid.", index));
            else if (snapshots.All(item => item.Id != itemId.Value)) diagnostics.Add(new("target.item.missing", "Learning Item was not found.", index));
            if (revision is null || revision < 1) diagnostics.Add(new("target.revision", "contentRevision must be a positive integer.", index));
            else if (itemId is { } id && snapshots.SingleOrDefault(item => item.Id == id)?.ContentRevision != revision) diagnostics.Add(new("target.revision.stale", "Result does not target the Learning Item's current ContentRevision.", index));
            result.Add(new(index, itemId, revision, outcome, evidence, findings, correction, diagnostics.Count == 0, diagnostics));
        }
        return result;
    }

    private static QualityReviewResultDiagnostic? EvaluateResultSchema(JsonElement result)
    {
        try
        {
            var wrapper = new JsonObject { ["contract"] = Contract, ["version"] = Version, ["results"] = new JsonArray { JsonNode.Parse(result.GetRawText())! } };
            return JsonSchema.FromText(SchemaText()).Evaluate(wrapper).IsValid ? null : new("result.schema", "Result does not conform to the canonical Quality Review Result 1.0 operation shape.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return new("result.schema", "Result could not be validated against the canonical schema."); }
    }

    private static Guid? TryGuid(JsonElement element, string property) => Guid.TryParse(StringProperty(element, property), out var id) && id != Guid.Empty ? id : null;
    private static string? StringProperty(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static CurationQualityReviewOutcome? TryOutcome(JsonElement element) => StringProperty(element, "outcome") switch { "pass" => CurationQualityReviewOutcome.Pass, "warning" => CurationQualityReviewOutcome.Warning, "needs_review" => CurationQualityReviewOutcome.NeedsReview, _ => null };
    private static CurationQualityReviewEvidenceType? TryEvidence(JsonElement element) => StringProperty(element, "evidenceType") switch { "model_review" => CurationQualityReviewEvidenceType.ModelReview, "source_grounded_review" => CurationQualityReviewEvidenceType.SourceGroundedReview, "user_review" => CurationQualityReviewEvidenceType.UserReview, _ => null };
    private static CurationQualityReviewOutcome ToApplicationOutcome(string value) => value switch { "pass" => CurationQualityReviewOutcome.Pass, "warning" => CurationQualityReviewOutcome.Warning, "needs_review" => CurationQualityReviewOutcome.NeedsReview, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static CurationQualityReviewEvidenceType ToApplicationEvidence(string value) => value switch { "model_review" => CurationQualityReviewEvidenceType.ModelReview, "source_grounded_review" => CurationQualityReviewEvidenceType.SourceGroundedReview, "user_review" => CurationQualityReviewEvidenceType.UserReview, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static string SchemaText() => typeof(QualityReviewExchangeService).Assembly.GetManifestResourceStream(SchemaResourceName) is { } stream ? new StreamReader(stream).ReadToEnd() : throw new InvalidOperationException("Canonical Quality Review Result schema resource is unavailable.");
    private sealed record ParsedResults(JsonElement? Root, IReadOnlyList<QualityReviewResultDiagnostic> BundleDiagnostics, IReadOnlyList<JsonElement> Results);
}
