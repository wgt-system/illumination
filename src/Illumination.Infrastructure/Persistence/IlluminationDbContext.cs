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

    protected override void OnModelCreating(ModelBuilder modelBuilder) => PersistenceModelConfiguration.Configure(modelBuilder);
}
