namespace Illumination.Application.ContentManagement;

public enum QualityReviewPromptMode
{
    Standard,
    Strict,
    SourceGrounded,
}

public sealed record GenerateQualityReviewPromptCommand(
    IReadOnlyList<Guid> LearningItemIds,
    QualityReviewPromptMode Mode = QualityReviewPromptMode.Standard,
    string? AdditionalGuidance = null);

public sealed record GeneratedQualityReviewPrompt(string Prompt);

public sealed record QualityReviewResultDiagnostic(string Code, string Message, int? ResultIndex = null);

public sealed record QualityReviewResultPreview(
    int ResultIndex,
    Guid? LearningItemId,
    int? ContentRevision,
    CurationQualityReviewOutcome? Outcome,
    CurationQualityReviewEvidenceType? EvidenceType,
    string? Findings,
    string? SuggestedCorrection,
    bool IsValid,
    IReadOnlyList<QualityReviewResultDiagnostic> Diagnostics);

public sealed record QualityReviewExchangePreview(
    bool IsValid,
    IReadOnlyList<QualityReviewResultDiagnostic> Diagnostics,
    IReadOnlyList<QualityReviewResultPreview> Results);

public sealed record AcceptQualityReviewResultsCommand(
    string RawJson,
    IReadOnlyList<int> SelectedResultIndices,
    QualityReviewPromptMode Mode = QualityReviewPromptMode.Standard);

public sealed record QualityReviewExchangeAcceptanceResult(IReadOnlyList<CuratedLearningItemView> AcceptedItems);

public sealed class QualityReviewExchangeValidationException : Exception
{
    public QualityReviewExchangeValidationException(string message, IReadOnlyList<QualityReviewResultDiagnostic> diagnostics)
        : base(message) => Diagnostics = diagnostics;

    public IReadOnlyList<QualityReviewResultDiagnostic> Diagnostics { get; }
}
