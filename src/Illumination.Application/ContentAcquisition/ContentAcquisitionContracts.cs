using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;

namespace Illumination.Application.ContentAcquisition;

public enum ContentUpdateSignificance { Minor, Semantic }

public enum FollowUpProgressionMode { Reinforce, Continue, Advance }

public sealed record GenerateContentPromptCommand(
    string Subject,
    int RequestedItemCount,
    string? NewDeckName = null,
    Guid? ExistingDeckId = null,
    string? Guidance = null,
    DeckLearningContext? SourceDeckContext = null,
    FollowUpProgressionMode? ProgressionMode = null,
    IReadOnlyList<LearningItemResponseMode>? AllowedResponseModes = null);
public sealed record GeneratedContentPrompt(string Prompt);
public sealed record ContentBundleDiagnostic(string Code, string Message, int? OperationIndex = null);
public sealed record ContentBundleOperationPreview(int OperationIndex, string? OperationType, string? LocalRef, Guid? TargetId, string Summary, bool IsValid, IReadOnlyList<ContentBundleDiagnostic> Diagnostics, IReadOnlyList<string> Warnings, IReadOnlyList<string> Dependencies, bool IsSelectable);
public sealed record ContentBundlePreview(bool IsValid, IReadOnlyList<ContentBundleDiagnostic> Diagnostics, IReadOnlyList<ContentBundleOperationPreview> Operations, bool CanGenerateRepairPrompt);
public sealed record GenerateRepairPromptCommand(string InvalidJson, IReadOnlyList<ContentBundleDiagnostic> Diagnostics);
public sealed record GeneratedRepairPrompt(string Prompt);
public sealed record GeneratePreImportQualityReviewPromptCommand(string RawBundleJson, QualityReviewPromptMode Mode = QualityReviewPromptMode.Standard, IReadOnlyList<int>? OperationIndices = null);
public sealed record PreImportQualityReviewPromptItem(string LocalRef, int OperationIndex, int ContentRevision, string ContentFingerprint, string Prompt, string ReferenceSolution);
public sealed record GeneratedPreImportQualityReviewPrompt(string Prompt, IReadOnlyList<PreImportQualityReviewPromptItem> Items);
public sealed record PreImportQualityReviewResultDiagnostic(string Code, string Message, int? ResultIndex = null);
public sealed record PreImportQualityReviewResultPreview(int ResultIndex, string? LocalRef, int? OperationIndex, string? ContentFingerprint, CurationQualityReviewOutcome? Outcome, CurationQualityReviewEvidenceType? EvidenceType, string? Findings, string? SuggestedCorrection, bool IsValid, IReadOnlyList<PreImportQualityReviewResultDiagnostic> Diagnostics);
public sealed record PreImportQualityReviewPreview(bool IsValid, IReadOnlyList<PreImportQualityReviewResultDiagnostic> Diagnostics, IReadOnlyList<PreImportQualityReviewResultPreview> Results);
public sealed record PreviewPreImportQualityReviewCommand(string RawBundleJson, string RawResultJson, QualityReviewPromptMode Mode = QualityReviewPromptMode.Standard);
public sealed record PreImportQualityReviewSelection(string RawResultJson, QualityReviewPromptMode Mode, IReadOnlyList<int> SelectedResultIndices);
public sealed record CommitContentBundleCommand(string RawJson, IReadOnlyList<int> SelectedOperationIndices, PreImportQualityReviewSelection? AcceptedQualityReview = null);
public sealed record ContentImportResult(Guid ImportBatchId, DateTimeOffset ImportedAt, IReadOnlyList<Guid> CreatedLearningItemIds, IReadOnlyList<Guid> UpdatedLearningItemIds, IReadOnlyList<Guid> CreatedDeckIds, IReadOnlyList<Guid> UpdatedDeckIds, int AppliedMembershipCount, IReadOnlyList<int> CommittedOperationIndices, IReadOnlyList<int> SkippedOperationIndices);

public sealed class ContentAcquisitionValidationException : Exception
{
    public ContentAcquisitionValidationException(string message, IReadOnlyList<ContentBundleDiagnostic> diagnostics) : base(message) => Diagnostics = diagnostics;
    public IReadOnlyList<ContentBundleDiagnostic> Diagnostics { get; }
}
