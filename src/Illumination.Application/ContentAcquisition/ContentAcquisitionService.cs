using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Globalization;
using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Json.Schema;

namespace Illumination.Application.ContentAcquisition;

public sealed class ContentAcquisitionService
{
    public const string Contract = "illumination.content-bundle";
    public const string Version = "1.0";
    public const string PreImportQualityReviewContract = "illumination.preimport-quality-review-result";
    public const string PreImportQualityReviewVersion = "1.0";
    private const string SchemaResourceName = "Illumination.Application.Schemas.illumination-content-bundle-1.0.schema.json";
    private const string PreImportSchemaResourceName = "Illumination.Application.Schemas.illumination-preimport-quality-review-result-1.0.schema.json";
    private readonly IContentAcquisitionPersistence _persistence;
    private readonly TimeProvider _timeProvider;

    public ContentAcquisitionService(IContentAcquisitionPersistence persistence, TimeProvider timeProvider)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public GeneratedContentPrompt GenerateContentPrompt(GenerateContentPromptCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Subject)) throw new ArgumentException("Subject is required.", nameof(command));
        if (command.RequestedItemCount <= 0) throw new ArgumentException("Requested item count must be positive.", nameof(command));
        if ((!string.IsNullOrWhiteSpace(command.NewDeckName)) == command.ExistingDeckId.HasValue)
            throw new ArgumentException("Specify exactly one new Deck name or existing Deck ID.", nameof(command));

        var target = command.ExistingDeckId is { } id
            ? $"Use the existing Illumination Deck stable ID `{id:D}`; do not recreate it. Assign items with deckId."
            : $"Create one Deck with localRef `target-deck` and name `{command.NewDeckName}`. Assign items with deckLocalRef `target-deck`.";
        var guidance = string.IsNullOrWhiteSpace(command.Guidance) ? string.Empty : $"Additional guidance: {command.Guidance}";
        var responseModes = command.AllowedResponseModes is { Count: > 0 }
            ? $"Use only these existing response modes: {string.Join(", ", command.AllowedResponseModes.Select(x => x.ToString()))}. Do not force variety or create unsuitable tasks."
            : "Choose the simplest appropriate existing responseMode for each item.";
        var progression = command.ProgressionMode switch
        {
            FollowUpProgressionMode.Reinforce => "Reinforce / Easier: focus on weak, due, relearning, uncertain or insufficiently learned material; use more scaffolding and simpler applications.",
            FollowUpProgressionMode.Continue => "Continue / Balanced: consolidate weaker prerequisites while introducing reasonable next material; do not simply duplicate the source Deck.",
            FollowUpProgressionMode.Advance => "Advance / Harder: build on comparatively well-established material with harder applications, combinations or next concepts while respecting weak prerequisites.",
            _ => string.Empty,
        };
        var sourceContext = command.SourceDeckContext is null ? string.Empty : $"""

Learning-aware follow-up context from source Deck `{command.SourceDeckContext.DeckName}` (stable ID `{command.SourceDeckContext.DeckId:D}`):
{FormatLearningContext(command.SourceDeckContext)}
Use these actual facts. Do not simply regenerate source Learning Items. Repetition is appropriate only when intentionally reinforcing weak material; otherwise build on existing knowledge and preserve prerequisite coherence. Return fewer items rather than filler; the requested count is a target, not a quota.
Requested progression: {progression}
""";
        return new GeneratedContentPrompt($@"You are generating Illumination learning content.

Create the Content Bundle as a downloadable UTF-8 JSON file named `illumination-content-bundle.json`.
Do not print the full JSON inline when file creation is available. If file creation is unavailable, return JSON only inline.
The root contract must be ""{Contract}"" and version must be ""{Version}"".
Aim for {command.RequestedItemCount} independent concise question or mini-task Learning Items about:
{command.Subject}
The requested count is a target, not a quota: return fewer items rather than inventing uncertain, repetitive, low-value, or filler content.

Before producing the final bundle, perform a quality pass. Remove or correct items that are factually uncertain, internally inconsistent, ambiguous without sufficient context, mismatched between prompt and referenceSolution, duplicate or near-duplicate, unnatural for the requested language/domain/register, or dependent on unstated assumptions. Do not request or provide numeric confidence scores, and do not claim that generated content is verified.

For every item provide a non-empty prompt, exactly one non-empty referenceSolution, an explicit lowInteractionEligible value, and the simplest appropriate responseMode. Use self_assessed for recall or tasks requiring learner judgment. Use selection only when authored directAnswerChoices are suitable; give every choice a stable content-local id and mark one or more correct choices explicitly. Use short_text only when deterministic checking is genuinely suitable and provide acceptedShortAnswers for all genuinely valid answers. Use code only for small code-response tasks; do not assume execution or automatic correctness. When multiple answers are genuinely valid, represent them explicitly in acceptedShortAnswers or as multiple correct selection choices; otherwise make the reference solution and context clear rather than implying a unique answer incorrectly. Do not force variety merely to use every mode.
{responseModes}

If this is language learning, keep instructional/meta language in the natural instruction language established by Subject and Guidance unless the user explicitly requests immersion. Distinguish instructional language, source language, and target/answer language. State translation direction explicitly; never rely on the target language merely because it is being learned.

Use supported operations only, consistent localRefs, and assign every generated item to the target Deck.
{target}
{guidance}
{sourceContext}
Progression intent: {progression}

User guidance is additional authoritative generation guidance, but it must not weaken Content Bundle 1.0 validity, factual-quality/self-review requirements, or the explicit responseMode authoring requirements above.

Canonical Content Bundle 1.0 contract guidance:
{CanonicalSchemaText()}
");
    }

    private static string FormatLearningContext(DeckLearningContext context) => string.Join(Environment.NewLine, context.Items.Select(item =>
        $"- item {item.LearningItemId:D}: lifecycle={item.LifecycleState}, mode={item.ResponseMode}, new={item.IsNew}, dueAt={item.DueAt:O}, relearning={item.IsInShortTermRelearning}, difficulty={item.Difficulty.ToString("0.##", CultureInfo.InvariantCulture)}, stabilityDays={item.StabilityDays.ToString("0.##", CultureInfo.InvariantCulture)}, reviews={item.ReviewCount}, lastAssessment={item.LastConfirmedAssessment?.ToString() ?? "none"}, distribution=[Nochmal {item.AssessmentDistribution.Nochmal}, Schwer {item.AssessmentDistribution.Schwer}, Unsicher {item.AssessmentDistribution.Unsicher}, Gut {item.AssessmentDistribution.Gut}, Leicht {item.AssessmentDistribution.Leicht}], prompt={item.Prompt}, referenceSolution={item.ReferenceSolution}"));

    public GeneratedRepairPrompt GenerateRepairPrompt(GenerateRepairPromptCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.InvalidJson)) throw new ArgumentException("Invalid JSON is required.", nameof(command));
        var diagnostics = string.Join(Environment.NewLine, command.Diagnostics.Select(x => $"- {x.Code}: {x.Message}"));
        return new GeneratedRepairPrompt($@"Repair only the JSON below. Do not redesign, add, remove, or reinterpret content.
Return JSON only and make it valid against contract ""{Contract}"" version ""{Version}"".
Diagnostics:
{diagnostics}

Original invalid JSON:
{command.InvalidJson}
");
    }

    public async Task<GeneratedPreImportQualityReviewPrompt> GeneratePreImportQualityReviewPromptAsync(
        GeneratePreImportQualityReviewPromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidatePromptMode(command.Mode);
        var bundlePreview = await PreviewContentBundleAsync(command.RawBundleJson, cancellationToken);
        if (!bundlePreview.IsValid) throw new ContentAcquisitionValidationException("Content Bundle must be valid before generating a pre-import review prompt.", bundlePreview.Diagnostics.Concat(bundlePreview.Operations.SelectMany(x => x.Diagnostics)).ToArray());
        var parsed = Parse(command.RawBundleJson);
        var requested = command.OperationIndices?.Distinct().Order().ToArray() ?? parsed.Operations.Select((operation, index) => (operation, index)).Where(x => StringProperty(x.operation, "op") == "create_learning_item").Select(x => x.index).ToArray();
        var items = requested.Select(index => CreatePreImportPromptItem(parsed.Operations, index)).ToArray();
        if (items.Length == 0) throw new ArgumentException("At least one valid create_learning_item operation is required.", nameof(command));
        var evidence = RequiredEvidenceType(command.Mode);
        var promptItems = string.Join(Environment.NewLine + Environment.NewLine, items.Select(item => $"localRef: {item.LocalRef}\noperationIndex: {item.OperationIndex}\ncontentRevision: 1\ncontentFingerprint: {item.ContentFingerprint}\nPrompt: {item.Prompt}\nReference Solution: {item.ReferenceSolution}"));
        var prompt = $"You are reviewing Learning Items before import into Illumination. Review only the supplied create_learning_item content; do not modify it.\n\nReturn JSON only using contract \"{PreImportQualityReviewContract}\" version \"{PreImportQualityReviewVersion}\". Return one result per supplied localRef. Preserve each exact localRef and contentFingerprint. Emit evidenceType \"{evidence}\" for every result. Use outcome pass, warning, or needs_review; include human-readable findings and optionally suggestedCorrection. Do not use user_review or a generic Verified state. A suggested correction is informational and will never be applied automatically.\n\nItems:\n{promptItems}\n\nJSON shape example:\n{{\"contract\":\"{PreImportQualityReviewContract}\",\"version\":\"{PreImportQualityReviewVersion}\",\"results\":[{{\"localRef\":\"{items[0].LocalRef}\",\"contentFingerprint\":\"{items[0].ContentFingerprint}\",\"outcome\":\"pass\",\"evidenceType\":\"{evidence}\",\"findings\":\"...\"}}]}}";
        return new GeneratedPreImportQualityReviewPrompt(prompt, items);
    }

    public async Task<PreImportQualityReviewPreview> PreviewPreImportQualityReviewAsync(
        PreviewPreImportQualityReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidatePromptMode(command.Mode);
        var bundle = await PreviewContentBundleAsync(command.RawBundleJson, cancellationToken);
        var parsedBundle = Parse(command.RawBundleJson);
        var parsedResults = ParsePreImportResults(command.RawResultJson);
        var results = ValidatePreImportResults(parsedBundle, parsedResults, bundle.Operations, command.Mode);
        var diagnostics = parsedResults.BundleDiagnostics.Concat(bundle.Diagnostics.Select(x => new PreImportQualityReviewResultDiagnostic(x.Code, x.Message, x.OperationIndex))).ToArray();
        return new(diagnostics.Length == 0 && results.All(x => x.IsValid), diagnostics, results);
    }

    public async Task<ContentBundlePreview> PreviewContentBundleAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        var parsed = Parse(rawJson);
        if (parsed.Root is null) return new(false, parsed.BundleDiagnostics, [], parsed.CanRepair);
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var decks = await _persistence.LoadDecksAsync(cancellationToken);
        var operations = ValidateOperations(parsed, items, decks);
        return new(parsed.BundleDiagnostics.Count == 0 && operations.All(x => x.IsValid), parsed.BundleDiagnostics, operations, parsed.CanRepair);
    }

    public async Task<ContentImportResult> CommitContentBundleAsync(CommitContentBundleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var parsed = Parse(command.RawJson);
        var items = await _persistence.LoadLearningItemsAsync(cancellationToken);
        var decks = await _persistence.LoadDecksAsync(cancellationToken);
        var operations = ValidateOperations(parsed, items, decks);
        var selected = command.SelectedOperationIndices.Distinct().Order().ToArray();
        var diagnostics = new List<ContentBundleDiagnostic>(parsed.BundleDiagnostics);
        if (selected.Length == 0) diagnostics.Add(new("selection.empty", "At least one operation must be selected."));
        if (selected.Any(index => index < 0 || index >= operations.Count)) diagnostics.Add(new("selection.index", "Selected operation index is outside the bundle."));
        foreach (var operation in operations.Where(x => selected.Contains(x.OperationIndex)))
        {
            diagnostics.AddRange(operation.Diagnostics);
            if (!operation.IsSelectable) diagnostics.Add(new("selection.invalid", "Selected operation is not valid or selectable.", operation.OperationIndex));
            foreach (var dependency in operation.Dependencies)
            {
                var dependencyOperation = operations.FirstOrDefault(x => string.Equals(x.LocalRef, dependency, StringComparison.Ordinal));
                if (dependencyOperation is null || !selected.Contains(dependencyOperation.OperationIndex))
                    diagnostics.Add(new("selection.dependency", $"Dependency localRef '{dependency}' must be selected too.", operation.OperationIndex));
            }
        }
        if (diagnostics.Count > 0) throw new ContentAcquisitionValidationException("Content Bundle cannot be committed.", diagnostics);

        IReadOnlyDictionary<string, PreImportAcceptedReview> acceptedReviews = new Dictionary<string, PreImportAcceptedReview>(StringComparer.Ordinal);
        if (command.AcceptedQualityReview is { } reviewSelection)
        {
            var review = ValidatePreImportSelection(parsed, reviewSelection, selected, operations);
            if (review.Diagnostics.Count > 0) throw new ContentAcquisitionValidationException("Pre-import Quality Review cannot be accepted.", review.Diagnostics.Select(x => new ContentBundleDiagnostic(x.Code, x.Message, x.ResultIndex)).ToArray());
            acceptedReviews = review.Accepted;
        }

        var plan = BuildPlan(parsed, selected, items, decks, operations, acceptedReviews);
        await _persistence.CommitAsync(plan.Snapshot, cancellationToken);
        return new(plan.Snapshot.Provenance.ImportBatchId, plan.Snapshot.Provenance.ImportedAt, plan.CreatedItems, plan.UpdatedItems, plan.CreatedDecks, plan.UpdatedDecks, plan.AssignmentCount, selected, operations.Select(x => x.OperationIndex).Where(x => !selected.Contains(x)).ToArray());
    }

    private static ParsedBundle Parse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new(null, [new("json.empty", "JSON text is required.")], false, [], null, null, null, null);
        JsonDocument document;
        try { document = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return new(null, [new("json.malformed", "The JSON text is malformed.")], true, [], null, null, null, null); }
        using (document)
        {
            var root = document.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object) return new(root, [new("bundle.root", "Bundle root must be an object.")], true, [], null, null, null, null);
            var contract = StringProperty(root, "contract");
            var version = StringProperty(root, "version");
            var bundleId = StringProperty(root, "bundleId");
            var generatedFor = StringProperty(root, "generatedFor");
            var diagnostics = new List<ContentBundleDiagnostic>();
            if (!string.Equals(contract, Contract, StringComparison.Ordinal)) diagnostics.Add(new("bundle.contract", "Unsupported or missing contract."));
            if (!string.Equals(version, Version, StringComparison.Ordinal)) diagnostics.Add(new("bundle.version", "Unsupported or missing version."));
            if (!root.TryGetProperty("operations", out var operationElement) || operationElement.ValueKind != JsonValueKind.Array)
                diagnostics.Add(new("bundle.operations", "Operations must be an array."));
            var operations = operationElement.ValueKind == JsonValueKind.Array ? operationElement.EnumerateArray().Select(x => x.Clone()).ToArray() : [];
            var schemaDiagnostic = EvaluateEnvelopeSchema(root);
            if (schemaDiagnostic is not null) diagnostics.Add(new(schemaDiagnostic.Code, schemaDiagnostic.Message));
            return new(root, diagnostics, true, operations, contract, version, bundleId, generatedFor);
        }
    }

    private static PreImportQualityReviewPromptItem CreatePreImportPromptItem(IReadOnlyList<JsonElement> operations, int index)
    {
        if (index < 0 || index >= operations.Count || StringProperty(operations[index], "op") != "create_learning_item") throw new ArgumentException($"Operation index {index} is not a create_learning_item operation.");
        var operation = operations[index];
        var item = operation.GetProperty("item");
        return new(StringProperty(operation, "localRef")!, index, 1, ComputeContentFingerprint(operation), item.GetProperty("prompt").GetString()!, item.GetProperty("referenceSolution").GetString()!);
    }

    private static ParsedPreImportResults ParsePreImportResults(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new(null, [new("json.empty", "Quality Review Result JSON is required.")], []);
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement.Clone();
            if (root.ValueKind != JsonValueKind.Object) return new(root, [new("result.root", "Quality Review Result root must be an object.")], []);
            var diagnostics = new List<PreImportQualityReviewResultDiagnostic>();
            if (StringProperty(root, "contract") != PreImportQualityReviewContract) diagnostics.Add(new("result.contract", "Unsupported or missing pre-import review contract."));
            if (StringProperty(root, "version") != PreImportQualityReviewVersion) diagnostics.Add(new("result.version", "Unsupported or missing pre-import review version."));
            var results = root.TryGetProperty("results", out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().Select(x => x.Clone()).ToArray() : [];
            if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array) diagnostics.Add(new("result.results", "Results must be an array."));
            else if (resultsElement.GetArrayLength() == 0) diagnostics.Add(new("result.results", "Results must contain at least one result."));
            var schemaDiagnostic = EvaluatePreImportEnvelopeSchema(root, results.Length);
            if (schemaDiagnostic is not null) diagnostics.Add(new(schemaDiagnostic.Code, schemaDiagnostic.Message));
            return new(root, diagnostics, results);
        }
        catch (JsonException) { return new(null, [new("json.malformed", "Quality Review Result JSON is malformed.")], []); }
    }

    private static List<PreImportQualityReviewResultPreview> ValidatePreImportResults(ParsedBundle bundle, ParsedPreImportResults results, IReadOnlyList<ContentBundleOperationPreview> operations, QualityReviewPromptMode mode)
    {
        var result = new List<PreImportQualityReviewResultPreview>();
        var operationMap = operations.Where(x => x.OperationType == "create_learning_item" && x.LocalRef is not null).GroupBy(x => x.LocalRef!, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var expectedEvidence = mode == QualityReviewPromptMode.SourceGrounded ? CurationQualityReviewEvidenceType.SourceGroundedReview : CurationQualityReviewEvidenceType.ModelReview;
        for (var index = 0; index < results.Results.Count; index++)
        {
            var element = results.Results[index];
            var diagnostics = new List<PreImportQualityReviewResultDiagnostic>();
            var localRef = StringProperty(element, "localRef");
            var fingerprint = StringProperty(element, "contentFingerprint");
            var outcome = TryCurationOutcome(element);
            var evidence = TryCurationEvidence(element);
            var findings = StringProperty(element, "findings");
            var correction = StringProperty(element, "suggestedCorrection");
            var schemaDiagnostic = EvaluatePreImportResultSchema(element);
            if (schemaDiagnostic is not null) diagnostics.Add(new(schemaDiagnostic.Code, schemaDiagnostic.Message, index));
            if (string.IsNullOrWhiteSpace(localRef)) diagnostics.Add(new("target.localRef", "localRef is required.", index));
            else if (!seen.Add(localRef)) diagnostics.Add(new("target.duplicate", "Each localRef may be reviewed at most once.", index));
            ContentBundleOperationPreview? operation = null;
            if (localRef is not null && !operationMap.TryGetValue(localRef, out operation)) diagnostics.Add(new("target.localRef.unknown", "localRef does not identify a valid create_learning_item operation.", index));
            else if (operation is { IsValid: false }) diagnostics.Add(new("target.operation.invalid", "Target create_learning_item operation is invalid.", index));
            if (operation is { IsValid: true } && fingerprint is not null && !string.Equals(fingerprint, ComputeContentFingerprint(bundle.Operations[operation.OperationIndex]), StringComparison.Ordinal)) diagnostics.Add(new("target.fingerprint", "Content fingerprint does not match the reviewed bundle content.", index));
            if (evidence is { } actual && actual != expectedEvidence) diagnostics.Add(new("result.evidence_type", $"This {mode} exchange requires {EvidenceName(expectedEvidence)}.", index));
            result.Add(new(index, localRef, operation?.OperationIndex, fingerprint, outcome, evidence, findings, correction, diagnostics.Count == 0, diagnostics));
        }
        return result;
    }

    private static PreImportSelectionValidation ValidatePreImportSelection(ParsedBundle bundle, PreImportQualityReviewSelection selection, IReadOnlyList<int> selectedOperations, IReadOnlyList<ContentBundleOperationPreview> operations)
    {
        ValidatePromptMode(selection.Mode);
        var parsed = ParsePreImportResults(selection.RawResultJson);
        var previews = ValidatePreImportResults(bundle, parsed, operations, selection.Mode);
        var diagnostics = new List<PreImportQualityReviewResultDiagnostic>(parsed.BundleDiagnostics);
        var selected = selection.SelectedResultIndices.Distinct().Order().ToArray();
        if (selected.Length == 0) diagnostics.Add(new("selection.empty", "At least one pre-import review result must be selected."));
        if (selected.Any(x => x < 0 || x >= previews.Count)) diagnostics.Add(new("selection.index", "Selected review result index is outside the result set."));
        var accepted = new Dictionary<string, PreImportAcceptedReview>(StringComparer.Ordinal);
        foreach (var index in selected.Where(x => x >= 0 && x < previews.Count))
        {
            var preview = previews[index];
            diagnostics.AddRange(preview.Diagnostics);
            if (!preview.IsValid) diagnostics.Add(new("selection.invalid", "Selected pre-import review result is invalid.", index));
            if (preview.LocalRef is { } localRef)
            {
                var operationIndex = preview.OperationIndex;
                if (operationIndex is null || !selectedOperations.Contains(operationIndex.Value)) diagnostics.Add(new("selection.operation", "The reviewed create operation must also be selected for import.", index));
                else if (preview.Outcome is { } outcome && preview.EvidenceType is { } evidence && preview.Findings is { } findings) accepted[localRef] = new(outcome switch { CurationQualityReviewOutcome.Pass => QualityReviewOutcomeSnapshot.Pass, CurationQualityReviewOutcome.Warning => QualityReviewOutcomeSnapshot.Warning, CurationQualityReviewOutcome.NeedsReview => QualityReviewOutcomeSnapshot.NeedsReview, _ => throw new ArgumentOutOfRangeException() }, evidence switch { CurationQualityReviewEvidenceType.ModelReview => QualityReviewEvidenceTypeSnapshot.ModelReview, CurationQualityReviewEvidenceType.SourceGroundedReview => QualityReviewEvidenceTypeSnapshot.SourceGroundedReview, CurationQualityReviewEvidenceType.UserReview => QualityReviewEvidenceTypeSnapshot.UserReview, _ => throw new ArgumentOutOfRangeException() }, findings, preview.SuggestedCorrection);
            }
        }
        return new(accepted, diagnostics);
    }

    private static ContentBundleDiagnostic? EvaluatePreImportEnvelopeSchema(JsonElement root, int resultCount)
    {
        try
        {
            var schema = JsonSchema.FromText(PreImportSchemaText());
            var envelope = JsonNode.Parse(root.GetRawText())!.AsObject();
            envelope["results"] = new JsonArray { new JsonObject { ["localRef"] = "schema-check", ["contentFingerprint"] = new string('0', 64), ["outcome"] = "pass", ["evidenceType"] = "model_review", ["findings"] = "Schema check" } };
            return schema.Evaluate(envelope).IsValid ? null : new("result.schema", "Pre-import Quality Review Result envelope does not conform to schema 1.0.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return new("result.schema", "Pre-import Quality Review Result schema could not be evaluated."); }
    }

    private static PreImportQualityReviewResultDiagnostic? EvaluatePreImportResultSchema(JsonElement result)
    {
        try
        {
            var wrapper = new JsonObject { ["contract"] = PreImportQualityReviewContract, ["version"] = PreImportQualityReviewVersion, ["results"] = new JsonArray { JsonNode.Parse(result.GetRawText())! } };
            return JsonSchema.FromText(PreImportSchemaText()).Evaluate(wrapper).IsValid ? null : new("result.schema", "Result does not conform to the pre-import Quality Review Result 1.0 operation shape.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return new("result.schema", "Result could not be validated against the pre-import Quality Review Result schema."); }
    }

    private static string ComputeContentFingerprint(JsonElement operation)
    {
        var item = operation.GetProperty("item");
        var canonical = new JsonObject
        {
            ["prompt"] = item.GetProperty("prompt").GetString(),
            ["referenceSolution"] = item.GetProperty("referenceSolution").GetString(),
            ["hints"] = CanonicalStrings(item, "hints"),
            ["responseMode"] = item.GetProperty("responseMode").GetString(),
            ["directAnswerChoices"] = CanonicalChoices(item, "directAnswerChoices"),
            ["assistanceAnswerChoices"] = CanonicalChoices(item, "assistanceAnswerChoices"),
            ["acceptedShortAnswers"] = CanonicalStrings(item, "acceptedShortAnswers")
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString()))).ToLowerInvariant();
    }

    private static JsonArray CanonicalStrings(JsonElement item, string property) => item.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array ? new JsonArray(values.EnumerateArray().Select(x => (JsonNode?)x.GetString()).ToArray()) : [];
    private static JsonArray CanonicalChoices(JsonElement item, string property) => item.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array ? new JsonArray(values.EnumerateArray().Select(x => (JsonNode?)new JsonObject { ["id"] = x.TryGetProperty("id", out var id) ? id.GetString() : null, ["text"] = x.GetProperty("text").GetString(), ["correct"] = x.TryGetProperty("correct", out var correct) && correct.ValueKind == JsonValueKind.True }).ToArray()) : [];
    private static CurationQualityReviewOutcome? TryCurationOutcome(JsonElement element) => StringProperty(element, "outcome") switch { "pass" => CurationQualityReviewOutcome.Pass, "warning" => CurationQualityReviewOutcome.Warning, "needs_review" => CurationQualityReviewOutcome.NeedsReview, _ => null };
    private static CurationQualityReviewEvidenceType? TryCurationEvidence(JsonElement element) => StringProperty(element, "evidenceType") switch { "model_review" => CurationQualityReviewEvidenceType.ModelReview, "source_grounded_review" => CurationQualityReviewEvidenceType.SourceGroundedReview, "user_review" => CurationQualityReviewEvidenceType.UserReview, _ => null };
    private static string RequiredEvidenceType(QualityReviewPromptMode mode) => mode switch { QualityReviewPromptMode.SourceGrounded => "source_grounded_review", QualityReviewPromptMode.Standard or QualityReviewPromptMode.Strict => "model_review", _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    private static string EvidenceName(CurationQualityReviewEvidenceType evidence) => evidence switch { CurationQualityReviewEvidenceType.ModelReview => "model_review", CurationQualityReviewEvidenceType.SourceGroundedReview => "source_grounded_review", CurationQualityReviewEvidenceType.UserReview => "user_review", _ => throw new ArgumentOutOfRangeException(nameof(evidence)) };
    private static void ValidatePromptMode(QualityReviewPromptMode mode) { if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported quality review prompt mode."); }
    private static string PreImportSchemaText() => typeof(ContentAcquisitionService).Assembly.GetManifestResourceStream(PreImportSchemaResourceName) is { } stream ? new StreamReader(stream).ReadToEnd() : throw new InvalidOperationException("Pre-import Quality Review schema resource is unavailable.");

    private static ContentBundleDiagnostic? EvaluateEnvelopeSchema(JsonElement root)
    {
        try
        {
            var schema = JsonSchema.FromText(CanonicalSchemaText());
            var envelope = JsonNode.Parse(root.GetRawText())!.AsObject();
            if (!root.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
                return new("bundle.schema", "Content Bundle envelope does not conform to the canonical schema.");
            if (operations.GetArrayLength() == 0)
                return new("bundle.operations", "Operations must contain at least one operation.");

            envelope["operations"] = new JsonArray
            {
                new JsonObject
                {
                    ["op"] = "create_deck",
                    ["localRef"] = "envelope-check",
                    ["deck"] = new JsonObject { ["name"] = "Envelope check" }
                }
            };
            var result = schema.Evaluate(envelope);
            return result.IsValid ? null : new("bundle.schema", "Content Bundle envelope does not conform to the canonical schema.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new("bundle.schema", "Canonical Content Bundle schema could not be evaluated.");
        }
    }

    private List<ContentBundleOperationPreview> ValidateOperations(ParsedBundle parsed, IReadOnlyList<LearningItemSnapshot> items, IReadOnlyList<DeckSnapshot> decks)
    {
        var result = new List<ContentBundleOperationPreview>();
        var localRefs = new Dictionary<string, (int Index, string Type)>(StringComparer.Ordinal);
        for (var index = 0; index < parsed.Operations.Count; index++)
        {
            var operation = parsed.Operations[index];
            var diagnostics = new List<ContentBundleDiagnostic>();
            var warnings = new List<string>();
            var dependencies = new List<string>();
            var op = StringProperty(operation, "op");
            var operationSchemaDiagnostic = EvaluateOperationSchema(operation);
            if (operationSchemaDiagnostic is not null) diagnostics.Add(new(operationSchemaDiagnostic.Code, operationSchemaDiagnostic.Message, index));
            var localRef = StringProperty(operation, "localRef");
            Guid? targetId = null;
            if (op is "create_learning_item" or "create_deck")
            {
                if (!string.IsNullOrWhiteSpace(localRef) && !localRefs.TryAdd(localRef, (index, op))) diagnostics.Add(new("localRef.duplicate", $"Duplicate localRef '{localRef}'.", index));
            }
            if (op is "create_learning_item" && operationSchemaDiagnostic is null) ValidateItemPayload(operation, index, diagnostics, out _);
            else if (op is "create_deck" && operationSchemaDiagnostic is null) ValidateDeckPayload(operation, index, diagnostics);
            else if (op is "update_learning_item")
            {
                targetId = ParseId(operation, "itemId", index, diagnostics);
                if (operationSchemaDiagnostic is null) ValidateItemPayload(operation, index, diagnostics, out _);
                if (targetId is { } id && items.All(x => x.Id != id)) diagnostics.Add(new("target.item.missing", "Target Learning Item does not exist.", index));
            }
            else if (op is "update_deck")
            {
                targetId = ParseId(operation, "deckId", index, diagnostics);
                if (operationSchemaDiagnostic is null) ValidateDeckPayload(operation, index, diagnostics);
                if (targetId is { } id && decks.All(x => x.Id != id)) diagnostics.Add(new("target.deck.missing", "Target Deck does not exist.", index));
            }
            else if (op is "assign_item_to_decks")
            {
                if (operation.TryGetProperty("item", out var itemTarget)) ParseTarget(itemTarget, "item", "itemId", "itemLocalRef", index, diagnostics, dependencies, out targetId, expectedType: "create_learning_item");
                else diagnostics.Add(new("dependency.missing", "item target is required.", index));
                if (operation.TryGetProperty("decks", out var deckArray) && deckArray.ValueKind == JsonValueKind.Array)
                    foreach (var deck in deckArray.EnumerateArray()) ParseTarget(deck, "deck", "deckId", "deckLocalRef", index, diagnostics, dependencies, out _, expectedType: "create_deck");
            }
            else diagnostics.Add(new("operation.type", "Unsupported or missing operation type.", index));
            if (op == "create_learning_item" && operationSchemaDiagnostic is null && TryGetPrompt(operation, out var prompt)) AddDuplicateWarnings(prompt, items, parsed.Operations, index, warnings);
            var valid = diagnostics.Count == 0;
            result.Add(new(index, op, localRef, targetId, OperationSummary(operation, op), valid, diagnostics, warnings, dependencies, valid));
        }
        for (var i = 0; i < result.Count; i++)
        {
            var operation = result[i];
            var extra = operation.Dependencies.Where(dependency => !localRefs.ContainsKey(dependency)).Select(dependency => new ContentBundleDiagnostic("dependency.missing", $"Dependency localRef '{dependency}' does not resolve to a create operation.", operation.OperationIndex)).ToList();
            if (operation.LocalRef is { } operationLocalRef && result.Count(x => string.Equals(x.LocalRef, operationLocalRef, StringComparison.Ordinal)) > 1) extra.Add(new("localRef.duplicate", $"Duplicate localRef '{operationLocalRef}'.", operation.OperationIndex));
            var element = parsed.Operations[operation.OperationIndex];
            if (StringProperty(element, "op") == "assign_item_to_decks")
            {
                if (element.TryGetProperty("item", out var itemTarget) && itemTarget.TryGetProperty("itemId", out var itemId) && Guid.TryParse(itemId.GetString(), out var parsedItemId) && !items.Any(x => x.Id == parsedItemId)) extra.Add(new("target.item.missing", "Assigned Learning Item does not exist.", operation.OperationIndex));
                if (element.TryGetProperty("item", out itemTarget) && itemTarget.TryGetProperty("itemLocalRef", out var itemLocal) && (!localRefs.TryGetValue(itemLocal.GetString()!, out var itemReference) || itemReference.Type != "create_learning_item")) extra.Add(new("dependency.type", "itemLocalRef must resolve to create_learning_item.", operation.OperationIndex));
                if (element.TryGetProperty("decks", out var deckTargets) && deckTargets.ValueKind == JsonValueKind.Array)
                    foreach (var deckTarget in deckTargets.EnumerateArray())
                    {
                        if (deckTarget.TryGetProperty("deckId", out var deckId) && Guid.TryParse(deckId.GetString(), out var parsedDeckId) && !decks.Any(x => x.Id == parsedDeckId)) extra.Add(new("target.deck.missing", "Assigned Deck does not exist.", operation.OperationIndex));
                        if (deckTarget.TryGetProperty("deckLocalRef", out var deckLocal) && (!localRefs.TryGetValue(deckLocal.GetString()!, out var deckReference) || deckReference.Type != "create_deck")) extra.Add(new("dependency.type", "deckLocalRef must resolve to create_deck.", operation.OperationIndex));
                    }
            }
            if (extra.Count > 0) result[i] = operation with { IsValid = false, IsSelectable = false, Diagnostics = operation.Diagnostics.Concat(extra).ToArray() };
        }
        return result;
    }

    private static ContentBundleDiagnostic? EvaluateOperationSchema(JsonElement operation)
    {
        try
        {
            var wrapper = new JsonObject { ["contract"] = Contract, ["version"] = Version, ["operations"] = new JsonArray(JsonNode.Parse(operation.GetRawText())!) };
            var result = JsonSchema.FromText(CanonicalSchemaText()).Evaluate(wrapper);
            return result.IsValid ? null : new("operation.schema", "Operation does not conform to the canonical Content Bundle 1.0 operation shape.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new("operation.schema", "Operation could not be validated against the canonical schema.");
        }
    }

    private ImportPlan BuildPlan(ParsedBundle parsed, IReadOnlyList<int> selected, IReadOnlyList<LearningItemSnapshot> items, IReadOnlyList<DeckSnapshot> decks, IReadOnlyList<ContentBundleOperationPreview> previews, IReadOnlyDictionary<string, PreImportAcceptedReview> acceptedReviews)
    {
        var itemMap = items.ToDictionary(x => x.Id);
        var deckMap = decks.ToDictionary(x => x.Id);
        var localItems = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var localDecks = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var changedItems = new Dictionary<Guid, LearningItemSnapshot>();
        var changedDecks = new Dictionary<Guid, DeckSnapshot>();
        var createdItems = new List<Guid>(); var updatedItems = new List<Guid>(); var createdDecks = new List<Guid>(); var updatedDecks = new List<Guid>(); var assignments = 0;
        foreach (var index in selected)
        {
            var element = parsed.Operations[index]; var op = StringProperty(element, "op");
            if (op == "create_learning_item") { var localRef = StringProperty(element, "localRef")!; var id = LearningItemId.New().Value; localItems[localRef] = id; var snapshot = CreateItemSnapshot(id, element, _timeProvider.GetUtcNow()); if (acceptedReviews.TryGetValue(localRef, out var review)) snapshot = snapshot with { QualityReviews = [new QualityReviewSnapshot(QualityReviewId.New().Value, id, 1, review.Outcome, review.EvidenceType, review.Findings, review.SuggestedCorrection, null)] }; changedItems[id] = snapshot; createdItems.Add(id); }
            else if (op == "create_deck") { var id = DeckId.New().Value; localDecks[StringProperty(element, "localRef")!] = id; var snapshot = new DeckSnapshot(id, StringProperty(element.GetProperty("deck"), "name")!, []); changedDecks[id] = snapshot; createdDecks.Add(id); }
        }
        foreach (var index in selected)
        {
            var element = parsed.Operations[index]; var op = StringProperty(element, "op");
            if (op == "update_learning_item") { var id = Guid.Parse(StringProperty(element, "itemId")!); var current = changedItems.GetValueOrDefault(id) ?? itemMap[id]; var updated = UpdateItemSnapshot(current, element, _timeProvider.GetUtcNow()); changedItems[id] = updated; updatedItems.Add(id); }
            else if (op == "update_deck") { var id = Guid.Parse(StringProperty(element, "deckId")!); var current = changedDecks.GetValueOrDefault(id) ?? deckMap[id]; changedDecks[id] = current with { Name = StringProperty(element.GetProperty("deck"), "name")! }; updatedDecks.Add(id); }
            else if (op == "assign_item_to_decks")
            {
                var itemId = ResolveTargetId(element.GetProperty("item"), "itemId", "itemLocalRef", localItems, itemMap.Keys);
                foreach (var target in element.GetProperty("decks").EnumerateArray())
                {
                    var deckId = ResolveTargetId(target, "deckId", "deckLocalRef", localDecks, deckMap.Keys);
                    var current = changedDecks.GetValueOrDefault(deckId) ?? deckMap[deckId];
                    var membershipAlreadyExists = current.LearningItemIds.Contains(itemId);
                    changedDecks[deckId] = membershipAlreadyExists ? current : current with { LearningItemIds = current.LearningItemIds.Append(itemId).ToArray() };
                    if (!membershipAlreadyExists) assignments++;
                }
            }
        }
        var provenance = new ContentAcquisitionProvenanceSnapshot(Guid.NewGuid(), _timeProvider.GetUtcNow(), Contract, Version, parsed.BundleId, parsed.GeneratedFor, selected.Count, createdItems.Count, updatedItems.Count, createdDecks.Count, updatedDecks.Count, assignments);
        return new(new ContentAcquisitionCommitSnapshot(changedItems.Values.ToArray(), changedDecks.Values.ToArray(), provenance), createdItems, updatedItems, createdDecks, updatedDecks, assignments);
    }

    private static LearningItemSnapshot CreateItemSnapshot(Guid id, JsonElement operation, DateTimeOffset now) => ToSnapshot(LearningItem.Create(LearningItemId.From(id), PayloadPrompt(operation), PayloadReference(operation), now, PayloadMode(operation), PayloadHints(operation), PayloadChoices(operation, "directAnswerChoices"), PayloadChoices(operation, "assistanceAnswerChoices"), PayloadStrings(operation, "acceptedShortAnswers"), PayloadBool(operation, "lowInteractionEligible")));
    private static LearningItemSnapshot UpdateItemSnapshot(LearningItemSnapshot current, JsonElement operation, DateTimeOffset now)
    {
        var item = LearningItem.Restore(LearningItemId.From(current.Id), current.Prompt, current.ReferenceSolution, current.DueAt, current.IsNew, PayloadMode(current.ResponseMode), current.Hints.Select(x => new Hint(x.Text)), current.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)), current.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect, x.Id)), current.AcceptedShortAnswers, current.LowInteractionEligible, PayloadLifecycle(current.Lifecycle), current.Difficulty, current.StabilityDays, current.IsInShortTermRelearning, current.ContentRevision, (current.QualityReviews ?? []).Select(x => QualityReview.Restore(QualityReviewId.From(x.Id), LearningItemId.From(x.LearningItemId), x.ContentRevision, (QualityReviewOutcome)x.Outcome, (QualityReviewEvidenceType)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy.HasValue ? QualityReviewId.From(x.SupersededBy.Value) : null)), (current.UserFlagDefinitionIds ?? []).Select(UserFlagDefinitionId.From));
        item.UpdateContent(PayloadPrompt(operation), PayloadReference(operation), PayloadMode(operation), PayloadHints(operation), PayloadChoices(operation, "directAnswerChoices"), PayloadChoices(operation, "assistanceAnswerChoices"), PayloadStrings(operation, "acceptedShortAnswers"));
        item.ChangeLowInteractionEligibility(PayloadBool(operation, "lowInteractionEligible"));
        if (StringProperty(operation, "significance") == "semantic") item.ResetSchedulingForSemanticContentChange(now);
        return ToSnapshot(item) with { DeckIds = current.DeckIds };
    }

    private static void ValidateItemPayload(JsonElement operation, int index, List<ContentBundleDiagnostic> diagnostics, out JsonElement payload)
    {
        if (!operation.TryGetProperty("item", out payload) || payload.ValueKind != JsonValueKind.Object) { diagnostics.Add(new("item.payload", "Item payload is required.", index)); return; }
        try { _ = LearningItem.Create(PayloadPrompt(operation), PayloadReference(operation), DateTimeOffset.UtcNow, PayloadMode(operation), PayloadHints(operation), PayloadChoices(operation, "directAnswerChoices"), PayloadChoices(operation, "assistanceAnswerChoices"), PayloadStrings(operation, "acceptedShortAnswers"), PayloadBool(operation, "lowInteractionEligible")); }
        catch (Exception) { diagnostics.Add(new("item.domain", "Learning Item payload violates domain invariants.", index)); }
    }
    private static void ValidateDeckPayload(JsonElement operation, int index, List<ContentBundleDiagnostic> diagnostics) { if (!operation.TryGetProperty("deck", out var deck) || deck.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(StringProperty(deck, "name"))) diagnostics.Add(new("deck.payload", "Deck name is required.", index)); }
    private static void ParseTarget(JsonElement operation, string property, string idName, string localName, int index, List<ContentBundleDiagnostic> diagnostics, List<string> dependencies, out Guid? id, string expectedType)
    {
        id = null;
        if (operation.TryGetProperty(idName, out _)) { id = ParseId(operation, idName, index, diagnostics); return; }
        if (operation.TryGetProperty(localName, out var local))
        {
            var value = local.ValueKind == JsonValueKind.String ? local.GetString() : null;
            if (string.IsNullOrWhiteSpace(value)) diagnostics.Add(new("dependency.localRef", "Dependency localRef is invalid.", index)); else dependencies.Add(value);
            return;
        }
        diagnostics.Add(new("dependency.missing", $"{property} target is required.", index));
    }
    private static Guid? ParseId(JsonElement element, string property, int index, List<ContentBundleDiagnostic> diagnostics) { var value = StringProperty(element, property); if (!Guid.TryParse(value, out var id) || id == Guid.Empty) { diagnostics.Add(new("target.id", $"{property} must be a non-empty Guid.", index)); return null; } return id; }
    private static void AddDuplicateWarnings(string prompt, IReadOnlyList<LearningItemSnapshot> existing, IReadOnlyList<JsonElement> all, int index, List<string> warnings) { var normalized = Normalize(prompt); if (existing.Any(x => Normalize(x.Prompt) == normalized)) warnings.Add("Prompt matches an existing Learning Item."); if (all.Take(index).Where(x => StringProperty(x, "op") == "create_learning_item").Select(x => TryGetPrompt(x, out var siblingPrompt) ? siblingPrompt : null).Where(x => x is not null).Any(x => Normalize(x!) == normalized)) warnings.Add("Prompt duplicates an earlier create operation."); }
    private static bool TryGetPrompt(JsonElement operation, out string prompt) { prompt = string.Empty; if (!operation.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("prompt", out var value) || value.ValueKind != JsonValueKind.String) return false; prompt = value.GetString()!; return !string.IsNullOrWhiteSpace(prompt); }
    private static string OperationSummary(JsonElement operation, string? operationType)
    {
        if (operationType is "create_learning_item" or "update_learning_item" && TryGetPrompt(operation, out var prompt))
            return prompt;
        if (operationType is "create_deck" or "update_deck"
            && operation.TryGetProperty("deck", out var deck)
            && deck.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(StringProperty(deck, "name")))
            return StringProperty(deck, "name")!;
        if (operationType == "assign_item_to_decks"
            && operation.TryGetProperty("decks", out var decks)
            && decks.ValueKind == JsonValueKind.Array)
            return $"Assign Learning Item to {decks.GetArrayLength()} {(decks.GetArrayLength() == 1 ? "Deck" : "Decks")}";
        return operationType ?? "Invalid operation";
    }
    private static string Normalize(string text) => string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string? StringProperty(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool PayloadBool(JsonElement operation, string name) => operation.GetProperty("item").GetProperty(name).GetBoolean();
    private static string PayloadPrompt(JsonElement operation) => operation.GetProperty("item").GetProperty("prompt").GetString()!;
    private static string PayloadReference(JsonElement operation) => operation.GetProperty("item").GetProperty("referenceSolution").GetString()!;
    private static ResponseMode PayloadMode(JsonElement operation) => PayloadMode(operation.GetProperty("item").GetProperty("responseMode").GetString()!);
    private static ResponseMode PayloadMode(LearningItemResponseMode mode) => mode switch { LearningItemResponseMode.SelfAssessed => ResponseMode.SelfAssessed, LearningItemResponseMode.Selection => ResponseMode.Selection, LearningItemResponseMode.ShortText => ResponseMode.ShortText, LearningItemResponseMode.Code => ResponseMode.Code, _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    private static ResponseMode PayloadMode(string mode) => mode switch { "self_assessed" => ResponseMode.SelfAssessed, "selection" => ResponseMode.Selection, "short_text" => ResponseMode.ShortText, "code" => ResponseMode.Code, _ => throw new ArgumentException("Unsupported response mode.") };
    private static LearningItemLifecycleState PayloadLifecycle(LearningItemLifecycle lifecycle) => lifecycle switch { LearningItemLifecycle.Active => LearningItemLifecycleState.Active, LearningItemLifecycle.Suspended => LearningItemLifecycleState.Suspended, LearningItemLifecycle.Mastered => LearningItemLifecycleState.Mastered, _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)) };
    private static IEnumerable<Hint> PayloadHints(JsonElement operation) => operation.GetProperty("item").TryGetProperty("hints", out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Select(x => new Hint(x.GetString()!)) : [];
    private static IEnumerable<AnswerChoice> PayloadChoices(JsonElement operation, string name) => operation.GetProperty("item").TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Select(x => new AnswerChoice(x.GetProperty("text").GetString()!, x.TryGetProperty("correct", out var correct) && correct.GetBoolean(), x.TryGetProperty("id", out var id) ? id.GetString() : null)) : [];
    private static IEnumerable<string> PayloadStrings(JsonElement operation, string name) => operation.GetProperty("item").TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Select(x => x.GetString()!) : [];
    private static Guid ResolveTargetId(JsonElement element, string idName, string localName, IReadOnlyDictionary<string, Guid> locals, IEnumerable<Guid> existing) => element.TryGetProperty(idName, out var id) ? Guid.Parse(id.GetString()!) : locals[localName == "itemLocalRef" ? element.GetProperty(localName).GetString()! : element.GetProperty(localName).GetString()!];
    private static LearningItemSnapshot ToSnapshot(LearningItem item) => new(item.Id.Value, item.Prompt, item.ReferenceSolution.Content, item.ResponseMode switch { ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed, ResponseMode.Selection => LearningItemResponseMode.Selection, ResponseMode.ShortText => LearningItemResponseMode.ShortText, ResponseMode.Code => LearningItemResponseMode.Code, _ => throw new ArgumentOutOfRangeException() }, item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(), item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(), item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect, x.Id)).ToArray(), item.AcceptedShortAnswers.ToArray(), item.LowInteractionEligible, item.LifecycleState switch { LearningItemLifecycleState.Active => LearningItemLifecycle.Active, LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended, LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered, _ => throw new ArgumentOutOfRangeException() }, item.LearningState.IsNew, item.LearningState.DueAt, item.LearningState.Difficulty, item.LearningState.StabilityDays, item.LearningState.IsInShortTermRelearning, [], item.ContentRevision, item.QualityReviews.Select(x => new QualityReviewSnapshot(x.Id.Value, x.LearningItemId.Value, x.ContentRevision, (QualityReviewOutcomeSnapshot)x.Outcome, (QualityReviewEvidenceTypeSnapshot)x.EvidenceType, x.Findings, x.SuggestedCorrection, x.SupersededBy?.Value)).ToArray(), item.UserFlagDefinitionIds.Select(x => x.Value).ToArray());
    private static string CanonicalSchemaText() => typeof(ContentAcquisitionService).Assembly.GetManifestResourceStream(SchemaResourceName) is { } stream ? new StreamReader(stream).ReadToEnd() : throw new InvalidOperationException("Canonical Content Bundle schema resource is unavailable.");
    private sealed record ParsedBundle(JsonElement? Root, IReadOnlyList<ContentBundleDiagnostic> BundleDiagnostics, bool CanRepair, IReadOnlyList<JsonElement> Operations, string? Contract, string? Version, string? BundleId, string? GeneratedFor);
    private sealed record ParsedPreImportResults(JsonElement? Root, IReadOnlyList<PreImportQualityReviewResultDiagnostic> BundleDiagnostics, IReadOnlyList<JsonElement> Results);
    private sealed record PreImportSelectionValidation(IReadOnlyDictionary<string, PreImportAcceptedReview> Accepted, IReadOnlyList<PreImportQualityReviewResultDiagnostic> Diagnostics);
    private sealed record PreImportAcceptedReview(QualityReviewOutcomeSnapshot Outcome, QualityReviewEvidenceTypeSnapshot EvidenceType, string Findings, string? SuggestedCorrection);
    private sealed record ImportPlan(ContentAcquisitionCommitSnapshot Snapshot, IReadOnlyList<Guid> CreatedItems, IReadOnlyList<Guid> UpdatedItems, IReadOnlyList<Guid> CreatedDecks, IReadOnlyList<Guid> UpdatedDecks, int AssignmentCount);
}
