namespace Illumination.Application.ContentAcquisition;

public enum ContentUpdateSignificance { Minor, Semantic }

public sealed record GenerateContentPromptCommand(string Subject, int RequestedItemCount, string? NewDeckName = null, Guid? ExistingDeckId = null, string? Guidance = null);
public sealed record GeneratedContentPrompt(string Prompt);
public sealed record ContentBundleDiagnostic(string Code, string Message, int? OperationIndex = null);
public sealed record ContentBundleOperationPreview(int OperationIndex, string? OperationType, string? LocalRef, Guid? TargetId, string Summary, bool IsValid, IReadOnlyList<ContentBundleDiagnostic> Diagnostics, IReadOnlyList<string> Warnings, IReadOnlyList<string> Dependencies, bool IsSelectable);
public sealed record ContentBundlePreview(bool IsValid, IReadOnlyList<ContentBundleDiagnostic> Diagnostics, IReadOnlyList<ContentBundleOperationPreview> Operations, bool CanGenerateRepairPrompt);
public sealed record GenerateRepairPromptCommand(string InvalidJson, IReadOnlyList<ContentBundleDiagnostic> Diagnostics);
public sealed record GeneratedRepairPrompt(string Prompt);
public sealed record CommitContentBundleCommand(string RawJson, IReadOnlyList<int> SelectedOperationIndices);
public sealed record ContentImportResult(Guid ImportBatchId, DateTimeOffset ImportedAt, IReadOnlyList<Guid> CreatedLearningItemIds, IReadOnlyList<Guid> UpdatedLearningItemIds, IReadOnlyList<Guid> CreatedDeckIds, IReadOnlyList<Guid> UpdatedDeckIds, int AppliedMembershipCount, IReadOnlyList<int> CommittedOperationIndices, IReadOnlyList<int> SkippedOperationIndices);

public sealed class ContentAcquisitionValidationException : Exception
{
    public ContentAcquisitionValidationException(string message, IReadOnlyList<ContentBundleDiagnostic> diagnostics) : base(message) => Diagnostics = diagnostics;
    public IReadOnlyList<ContentBundleDiagnostic> Diagnostics { get; }
}
