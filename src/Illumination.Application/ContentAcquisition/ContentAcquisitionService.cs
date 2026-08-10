using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Illumination.Application.ContentManagement;
using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Json.Schema;

namespace Illumination.Application.ContentAcquisition;

public sealed class ContentAcquisitionService
{
    public const string Contract = "illumination.content-bundle";
    public const string Version = "1.0";
    private const string SchemaResourceName = "Illumination.Application.Schemas.illumination-content-bundle-1.0.schema.json";
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
        return new GeneratedContentPrompt($@"You are generating Illumination learning content.

Return JSON only. The root contract must be ""{Contract}"" and version must be ""{Version}"".
Generate exactly {command.RequestedItemCount} independent concise question or mini-task Learning Items about:
{command.Subject}

Every item must include a non-empty prompt, exactly one non-empty referenceSolution, lowInteractionEligible, and responseMode ""self_assessed"". Use supported operations only, consistent localRefs, and assign every generated item to the target Deck.
{target}
{guidance}

Canonical Content Bundle 1.0 contract guidance:
{CanonicalSchemaText()}
");
    }

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

        var plan = BuildPlan(parsed, selected, items, decks, operations);
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
            if (schemaDiagnostic is not null) diagnostics.Add(schemaDiagnostic);
            return new(root, diagnostics, true, operations, contract, version, bundleId, generatedFor);
        }
    }

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

    private ImportPlan BuildPlan(ParsedBundle parsed, IReadOnlyList<int> selected, IReadOnlyList<LearningItemSnapshot> items, IReadOnlyList<DeckSnapshot> decks, IReadOnlyList<ContentBundleOperationPreview> previews)
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
            if (op == "create_learning_item") { var id = LearningItemId.New().Value; localItems[StringProperty(element, "localRef")!] = id; var snapshot = CreateItemSnapshot(id, element, _timeProvider.GetUtcNow()); changedItems[id] = snapshot; createdItems.Add(id); }
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
        var item = LearningItem.Restore(LearningItemId.From(current.Id), current.Prompt, current.ReferenceSolution, current.DueAt, current.IsNew, PayloadMode(current.ResponseMode), current.Hints.Select(x => new Hint(x.Text)), current.DirectAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)), current.AssistanceAnswerChoices.Select(x => new AnswerChoice(x.Text, x.IsCorrect)), current.AcceptedShortAnswers, current.LowInteractionEligible, PayloadLifecycle(current.Lifecycle), current.Difficulty, current.StabilityDays, current.IsInShortTermRelearning);
        item.ChangePrompt(PayloadPrompt(operation)); item.ChangeReferenceSolution(PayloadReference(operation)); item.ReplaceHints(PayloadHints(operation)); item.ChangeInteractionConfiguration(PayloadMode(operation), PayloadChoices(operation, "directAnswerChoices"), PayloadChoices(operation, "assistanceAnswerChoices"), PayloadStrings(operation, "acceptedShortAnswers")); item.ChangeLowInteractionEligibility(PayloadBool(operation, "lowInteractionEligible"));
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
    private static IEnumerable<AnswerChoice> PayloadChoices(JsonElement operation, string name) => operation.GetProperty("item").TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Select(x => new AnswerChoice(x.GetProperty("text").GetString()!, x.TryGetProperty("correct", out var correct) && correct.GetBoolean())) : [];
    private static IEnumerable<string> PayloadStrings(JsonElement operation, string name) => operation.GetProperty("item").TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Select(x => x.GetString()!) : [];
    private static Guid ResolveTargetId(JsonElement element, string idName, string localName, IReadOnlyDictionary<string, Guid> locals, IEnumerable<Guid> existing) => element.TryGetProperty(idName, out var id) ? Guid.Parse(id.GetString()!) : locals[localName == "itemLocalRef" ? element.GetProperty(localName).GetString()! : element.GetProperty(localName).GetString()!];
    private static LearningItemSnapshot ToSnapshot(LearningItem item) => new(item.Id.Value, item.Prompt, item.ReferenceSolution.Content, item.ResponseMode switch { ResponseMode.SelfAssessed => LearningItemResponseMode.SelfAssessed, ResponseMode.Selection => LearningItemResponseMode.Selection, ResponseMode.ShortText => LearningItemResponseMode.ShortText, ResponseMode.Code => LearningItemResponseMode.Code, _ => throw new ArgumentOutOfRangeException() }, item.Hints.Select(x => new HintSnapshot(x.Text)).ToArray(), item.DirectAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(), item.AssistanceAnswerChoices.Select(x => new AnswerChoiceSnapshot(x.Text, x.IsCorrect)).ToArray(), item.AcceptedShortAnswers.ToArray(), item.LowInteractionEligible, item.LifecycleState switch { LearningItemLifecycleState.Active => LearningItemLifecycle.Active, LearningItemLifecycleState.Suspended => LearningItemLifecycle.Suspended, LearningItemLifecycleState.Mastered => LearningItemLifecycle.Mastered, _ => throw new ArgumentOutOfRangeException() }, item.LearningState.IsNew, item.LearningState.DueAt, item.LearningState.Difficulty, item.LearningState.StabilityDays, item.LearningState.IsInShortTermRelearning, []);
    private static string CanonicalSchemaText() => typeof(ContentAcquisitionService).Assembly.GetManifestResourceStream(SchemaResourceName) is { } stream ? new StreamReader(stream).ReadToEnd() : throw new InvalidOperationException("Canonical Content Bundle schema resource is unavailable.");
    private sealed record ParsedBundle(JsonElement? Root, IReadOnlyList<ContentBundleDiagnostic> BundleDiagnostics, bool CanRepair, IReadOnlyList<JsonElement> Operations, string? Contract, string? Version, string? BundleId, string? GeneratedFor);
    private sealed record ImportPlan(ContentAcquisitionCommitSnapshot Snapshot, IReadOnlyList<Guid> CreatedItems, IReadOnlyList<Guid> UpdatedItems, IReadOnlyList<Guid> CreatedDecks, IReadOnlyList<Guid> UpdatedDecks, int AssignmentCount);
}
