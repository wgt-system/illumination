using System.Text.Json;
using System.Text.Json.Serialization;
using Illumination.Application.ContentManagement;

namespace Illumination.Application.ContentAcquisition;

public sealed record ExportedContentBundle(string Json, string SuggestedFileName, int LearningItemCount);

public sealed class ContentBundleExportService
{
    private readonly ContentManagementService _content;

    public ContentBundleExportService(ContentManagementService content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public async Task<ExportedContentBundle> ExportDeckAsync(Guid deckId, CancellationToken cancellationToken = default)
    {
        var deck = await _content.GetDeckAsync(deckId, cancellationToken);
        var allItems = await _content.ListLearningItemsAsync(cancellationToken);
        var memberIds = deck.LearningItemIds.ToHashSet();
        var items = allItems
            .Where(x => memberIds.Contains(x.Id))
            .OrderBy(x => x.Prompt, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .ToArray();

        var operations = new List<object>
        {
            new
            {
                op = "create_deck",
                localRef = "target-deck",
                deck = new { name = deck.Name },
            },
        };

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var localRef = $"item-{index + 1:000}";
            operations.Add(new
            {
                op = "create_learning_item",
                localRef,
                item = BuildItemPayload(item),
            });
            operations.Add(new
            {
                op = "assign_item_to_decks",
                item = new { itemLocalRef = localRef },
                decks = new[] { new { deckLocalRef = "target-deck" } },
            });
        }

        var bundle = new
        {
            contract = "illumination.content-bundle",
            version = "1.0",
            bundleId = $"deck-export-{deck.Id:N}",
            generatedFor = $"Portable content export of Deck '{deck.Name}'. Learning state and Review history are intentionally not included.",
            operations,
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        return new ExportedContentBundle(json, $"{SafeFileName(deck.Name)}.illumination.json", items.Length);
    }

    private static object BuildItemPayload(LearningItemView item)
    {
        if (item.ResponseMode == LearningItemResponseMode.Selection && item.DirectAnswerChoices.Count < 2)
            throw new InvalidOperationException($"Selection item '{item.Prompt}' cannot be exported because it has fewer than two direct choices.");
        if (item.AssistanceAnswerChoices.Count == 1)
            throw new InvalidOperationException($"Learning Item '{item.Prompt}' cannot be exported because Content Bundle 1.0 requires at least two assistance choices when present.");

        return new
        {
            prompt = item.Prompt,
            referenceSolution = item.ReferenceSolution,
            responseMode = ToContract(item.ResponseMode),
            hints = item.Hints.Count == 0 ? null : item.Hints.Select(x => x.Text).ToArray(),
            directAnswerChoices = item.DirectAnswerChoices.Count == 0
                ? null
                : item.DirectAnswerChoices.Select((choice, index) => new
                {
                    id = $"choice-{index + 1:000}",
                    text = choice.Text,
                    correct = choice.IsCorrect,
                }).ToArray(),
            assistanceAnswerChoices = item.AssistanceAnswerChoices.Count == 0
                ? null
                : item.AssistanceAnswerChoices.Select((choice, index) => new
                {
                    id = $"assistance-{index + 1:000}",
                    text = choice.Text,
                    correct = choice.IsCorrect,
                }).ToArray(),
            acceptedShortAnswers = item.AcceptedShortAnswers.Count == 0 ? null : item.AcceptedShortAnswers.ToArray(),
            lowInteractionEligible = item.LowInteractionEligible,
        };
    }

    private static string ToContract(LearningItemResponseMode mode) => mode switch
    {
        LearningItemResponseMode.SelfAssessed => "self_assessed",
        LearningItemResponseMode.Selection => "selection",
        LearningItemResponseMode.ShortText => "short_text",
        LearningItemResponseMode.Code => "code",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported ResponseMode."),
    };

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "illumination-deck" : cleaned;
    }
}
