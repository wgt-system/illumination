using Illumination.Application.ContentManagement;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreLearningStateBatchPersistence : ILearningStateBatchPersistence
{
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreLearningStateBatchPersistence(IDbContextFactory<IlluminationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task SaveLearningStatesAtomicallyAsync(
        IReadOnlyList<LearningStateMaintenanceSnapshot> states,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0) return;

        var ids = states.Select(x => x.LearningItemId).ToArray();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Learning State batch must contain unique non-empty Learning Item IDs.", nameof(states));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var records = await context.LearningItems
            .Where(item => ids.Contains(item.LearningItemId))
            .ToDictionaryAsync(item => item.LearningItemId, cancellationToken);

        if (records.Count != ids.Length)
            throw new InvalidOperationException("One or more Learning Items disappeared before the Learning State batch could be committed.");

        foreach (var state in states)
        {
            var record = records[state.LearningItemId];
            record.IsNew = state.IsNew;
            record.DueAt = state.DueAt;
            record.Difficulty = state.Difficulty;
            record.StabilityDays = state.StabilityDays;
            record.IsInShortTermRelearning = state.IsInShortTermRelearning;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
