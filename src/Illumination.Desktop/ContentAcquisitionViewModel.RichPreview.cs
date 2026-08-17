using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Illumination.Application.ContentManagement;

namespace Illumination.Desktop;

public sealed partial class ContentAcquisitionViewModel
{
    private ContentManagementService? _previewContent;
    private int _richPreviewGeneration;

    public ObservableCollection<LearningItemContentPreviewDisplay> RichContentPreviews { get; } = [];
    public bool HasRichContentPreviews => RichContentPreviews.Count > 0;

    public void ConfigureContentPreview(ContentManagementService content)
    {
        _previewContent = content ?? throw new ArgumentNullException(nameof(content));
    }

    partial void OnHasCurrentPreviewChanged(bool value)
    {
        var generation = ++_richPreviewGeneration;
        if (!value)
        {
            RichContentPreviews.Clear();
            OnPropertyChanged(nameof(HasRichContentPreviews));
            return;
        }
        _ = RefreshRichContentPreviewAsync(generation);
    }

    private async Task RefreshRichContentPreviewAsync(int generation)
    {
        if (_previewContent is null || string.IsNullOrWhiteSpace(RawJson)) return;
        var rawJson = RawJson;
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty("operations", out var operations) || operations.ValueKind != JsonValueKind.Array) return;

            var rows = new List<LearningItemContentPreviewDisplay>();
            var index = 0;
            foreach (var operation in operations.EnumerateArray())
            {
                var operationRow = Operations.FirstOrDefault(x => x.OperationIndex == index);
                if (operationRow is null || !operationRow.IsValid ||
                    !operation.TryGetProperty("op", out var opElement) ||
                    !operation.TryGetProperty("item", out var itemElement))
                {
                    index++;
                    continue;
                }

                var op = opElement.GetString();
                if (op is not ("create_learning_item" or "update_learning_item"))
                {
                    index++;
                    continue;
                }

                var proposed = ReadAuthoredContent(itemElement);
                LearningItemView? current = null;
                string significance = string.Empty;
                if (op == "update_learning_item")
                {
                    if (operation.TryGetProperty("significance", out var significanceElement)) significance = significanceElement.GetString() ?? string.Empty;
                    if (operation.TryGetProperty("itemId", out var idElement) && Guid.TryParse(idElement.GetString(), out var itemId))
                    {
                        try { current = await _previewContent.GetLearningItemAsync(itemId); }
                        catch (ContentNotFoundException) { }
                    }
                }

                rows.Add(new LearningItemContentPreviewDisplay(
                    operationRow,
                    op == "update_learning_item" ? "Update Learning Item" : "Create Learning Item",
                    significance,
                    current is null ? null : ToPreview(current),
                    proposed));
                index++;
            }

            if (generation != _richPreviewGeneration || !HasCurrentPreview || !string.Equals(rawJson, RawJson, StringComparison.Ordinal)) return;
            RichContentPreviews.Clear();
            foreach (var row in rows) RichContentPreviews.Add(row);
            OnPropertyChanged(nameof(HasRichContentPreviews));
        }
        catch (JsonException)
        {
            // Normal bundle validation already owns JSON diagnostics.
        }
    }

    private static AuthoredLearningItemPreview ReadAuthoredContent(JsonElement item)
    {
        var prompt = item.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() ?? string.Empty : string.Empty;
        var solution = item.TryGetProperty("referenceSolution", out var solutionElement) ? solutionElement.GetString() ?? string.Empty : string.Empty;
        var responseMode = item.TryGetProperty("responseMode", out var modeElement) ? modeElement.GetString() ?? string.Empty : string.Empty;
        var lowInteraction = item.TryGetProperty("lowInteractionEligible", out var lowElement) && lowElement.ValueKind is JsonValueKind.True or JsonValueKind.False && lowElement.GetBoolean();
        return new AuthoredLearningItemPreview(
            prompt,
            solution,
            responseMode,
            lowInteraction,
            ArrayCount(item, "hints"),
            ArrayCount(item, "directAnswerChoices"),
            ArrayCount(item, "assistanceAnswerChoices"),
            ArrayCount(item, "acceptedShortAnswers"));
    }

    private static AuthoredLearningItemPreview ToPreview(LearningItemView item) => new(
        item.Prompt,
        item.ReferenceSolution,
        item.ResponseMode switch
        {
            LearningItemResponseMode.SelfAssessed => "self_assessed",
            LearningItemResponseMode.Selection => "selection",
            LearningItemResponseMode.ShortText => "short_text",
            LearningItemResponseMode.Code => "code",
            _ => item.ResponseMode.ToString(),
        },
        item.LowInteractionEligible,
        item.Hints.Count,
        item.DirectAnswerChoices.Count,
        item.AssistanceAnswerChoices.Count,
        item.AcceptedShortAnswers.Count);

    private static int ArrayCount(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array ? property.GetArrayLength() : 0;
}

public sealed record AuthoredLearningItemPreview(
    string Prompt,
    string ReferenceSolution,
    string ResponseMode,
    bool LowInteractionEligible,
    int HintCount,
    int DirectChoiceCount,
    int AssistanceChoiceCount,
    int AcceptedAnswerCount)
{
    public string Shape => $"{ResponseMode} · hints {HintCount} · direct choices {DirectChoiceCount} · assistance {AssistanceChoiceCount} · accepted answers {AcceptedAnswerCount}";
}

public sealed record LearningItemContentPreviewDisplay(
    ContentOperationRowViewModel Operation,
    string Action,
    string Significance,
    AuthoredLearningItemPreview? Current,
    AuthoredLearningItemPreview Proposed)
{
    public bool IsUpdate => Current is not null;
    public bool HasSignificance => !string.IsNullOrWhiteSpace(Significance);
    public string Heading => HasSignificance ? $"{Action} · {Significance}" : Action;
    public string ChangeSummary
    {
        get
        {
            if (Current is null) return "New authored content";
            var changes = new List<string>();
            if (!string.Equals(Current.Prompt, Proposed.Prompt, StringComparison.Ordinal)) changes.Add("prompt");
            if (!string.Equals(Current.ReferenceSolution, Proposed.ReferenceSolution, StringComparison.Ordinal)) changes.Add("solution");
            if (!string.Equals(Current.ResponseMode, Proposed.ResponseMode, StringComparison.Ordinal)) changes.Add("response mode");
            if (Current.LowInteractionEligible != Proposed.LowInteractionEligible) changes.Add("low-interaction eligibility");
            if (Current.HintCount != Proposed.HintCount) changes.Add("hints");
            if (Current.DirectChoiceCount != Proposed.DirectChoiceCount) changes.Add("direct choices");
            if (Current.AssistanceChoiceCount != Proposed.AssistanceChoiceCount) changes.Add("assistance choices");
            if (Current.AcceptedAnswerCount != Proposed.AcceptedAnswerCount) changes.Add("accepted answers");
            return changes.Count == 0 ? "No headline field/count changes detected" : "Changes: " + string.Join(", ", changes);
        }
    }
}
