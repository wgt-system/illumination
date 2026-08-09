using Illumination.Application.ContentManagement;

namespace Illumination.Application.ContentAcquisition;

public interface IContentAcquisitionPersistence
{
    Task<IReadOnlyList<LearningItemSnapshot>> LoadLearningItemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeckSnapshot>> LoadDecksAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(ContentAcquisitionCommitSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed record ContentAcquisitionProvenanceSnapshot(Guid ImportBatchId, DateTimeOffset ImportedAt, string Contract, string Version, string? ExternalBundleId, string? GeneratedFor, int AcceptedOperationCount, int CreatedLearningItemCount, int UpdatedLearningItemCount, int CreatedDeckCount, int UpdatedDeckCount, int AssignmentCount);
public sealed record ContentAcquisitionCommitSnapshot(IReadOnlyList<LearningItemSnapshot> LearningItems, IReadOnlyList<DeckSnapshot> Decks, ContentAcquisitionProvenanceSnapshot Provenance);
