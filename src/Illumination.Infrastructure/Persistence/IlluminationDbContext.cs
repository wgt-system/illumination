using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public class IlluminationDbContext(DbContextOptions<IlluminationDbContext> options) : DbContext(options)
{
    public DbSet<LearningItemRecord> LearningItems => Set<LearningItemRecord>();
    public DbSet<HintRecord> Hints => Set<HintRecord>();
    public DbSet<AnswerChoiceRecord> AnswerChoices => Set<AnswerChoiceRecord>();
    public DbSet<AcceptedShortAnswerRecord> AcceptedShortAnswers => Set<AcceptedShortAnswerRecord>();
    public DbSet<DeckRecord> Decks => Set<DeckRecord>();
    public DbSet<DeckLearningItemRecord> DeckLearningItems => Set<DeckLearningItemRecord>();
    public DbSet<ReviewRecord> Reviews => Set<ReviewRecord>();
    public DbSet<StudySessionRecord> StudySessions => Set<StudySessionRecord>();
    public DbSet<StudySessionDeckRecord> StudySessionDecks => Set<StudySessionDeckRecord>();
    public DbSet<StudySessionQueueRecord> StudySessionQueue => Set<StudySessionQueueRecord>();
    public DbSet<StudySessionReviewRecord> StudySessionReviews => Set<StudySessionReviewRecord>();
    public DbSet<ImportProvenanceRecord> ImportProvenance => Set<ImportProvenanceRecord>();
    public DbSet<QualityReviewRecord> QualityReviews => Set<QualityReviewRecord>();
    public DbSet<UserFlagDefinitionRecord> UserFlagDefinitions => Set<UserFlagDefinitionRecord>();
    public DbSet<LearningItemUserFlagRecord> LearningItemUserFlags => Set<LearningItemUserFlagRecord>();
    public DbSet<StudyPreferenceRecord> StudyPreferences => Set<StudyPreferenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => PersistenceModelConfiguration.Configure(modelBuilder);
}
