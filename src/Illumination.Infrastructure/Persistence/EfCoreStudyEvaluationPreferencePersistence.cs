using Illumination.Application.Study;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public sealed class EfCoreStudyEvaluationPreferencePersistence : IStudyEvaluationPreferencePersistence
{
    private const int PreferenceId = 1;
    private readonly IDbContextFactory<IlluminationDbContext> _contextFactory;

    public EfCoreStudyEvaluationPreferencePersistence(IDbContextFactory<IlluminationDbContext> contextFactory) =>
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<StudyEvaluationMode> LoadDefaultEvaluationModeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var preference = await context.StudyPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.Id == PreferenceId, cancellationToken);
        return preference?.DefaultEvaluationMode ?? StudyEvaluationMode.Manual;
    }

    public async Task SaveDefaultEvaluationModeAsync(StudyEvaluationMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported evaluation mode.");
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var preference = await context.StudyPreferences.SingleOrDefaultAsync(x => x.Id == PreferenceId, cancellationToken);
        if (preference is null)
        {
            context.StudyPreferences.Add(new StudyPreferenceRecord { Id = PreferenceId, DefaultEvaluationMode = mode });
        }
        else
        {
            preference.DefaultEvaluationMode = mode;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
