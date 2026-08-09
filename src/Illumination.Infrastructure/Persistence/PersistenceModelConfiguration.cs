using System.Globalization;
using Illumination.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Illumination.Infrastructure.Persistence;

internal static class PersistenceModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var learningItems = modelBuilder.Entity<LearningItemRecord>();
        learningItems.ToTable("LearningItems");
        learningItems.HasKey(x => x.LearningItemId);
        learningItems.Property(x => x.LearningItemId).HasColumnName("LearningItemId").ValueGeneratedNever();
        learningItems.Property(x => x.Prompt).HasColumnName("Prompt").IsRequired();
        learningItems.Property(x => x.ReferenceSolutionContent).HasColumnName("ReferenceSolutionContent").IsRequired();
        learningItems.Property(x => x.ResponseMode).HasColumnName("ResponseMode").HasConversion<string>().IsRequired();
        learningItems.Property(x => x.LowInteractionEligible).HasColumnName("LowInteractionEligible").IsRequired();
        learningItems.Property(x => x.LifecycleState).HasColumnName("LifecycleState").HasConversion<string>().IsRequired();
        learningItems.Property(x => x.IsNew).HasColumnName("IsNew").IsRequired();
        learningItems.Property(x => x.DueAt).HasColumnName("DueAt").HasConversion(
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)).IsRequired();
        learningItems.HasIndex(x => x.DueAt).HasDatabaseName("IX_LearningItems_DueAt");
        learningItems.HasMany(x => x.Hints).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.AnswerChoices).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.AcceptedShortAnswers).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.DeckMemberships).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);

        ConfigureHint(modelBuilder.Entity<HintRecord>());
        ConfigureChoice(modelBuilder.Entity<AnswerChoiceRecord>());
        ConfigureShortAnswer(modelBuilder.Entity<AcceptedShortAnswerRecord>());
        ConfigureDeck(modelBuilder.Entity<DeckRecord>());
        ConfigureMembership(modelBuilder.Entity<DeckLearningItemRecord>());
    }

    private static void ConfigureHint(EntityTypeBuilder<HintRecord> entity)
    {
        entity.ToTable("Hints"); entity.HasKey(x => new { x.LearningItemId, x.Position });
        entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId"); entity.Property(x => x.Position).HasColumnName("Position"); entity.Property(x => x.Text).HasColumnName("Text").IsRequired();
    }

    private static void ConfigureChoice(EntityTypeBuilder<AnswerChoiceRecord> entity)
    {
        entity.ToTable("AnswerChoices"); entity.HasKey(x => new { x.LearningItemId, x.Role, x.Position });
        entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId"); entity.Property(x => x.Role).HasColumnName("Role").HasConversion<string>(); entity.Property(x => x.Position).HasColumnName("Position"); entity.Property(x => x.Text).HasColumnName("Text").IsRequired(); entity.Property(x => x.IsCorrect).HasColumnName("IsCorrect").IsRequired();
    }

    private static void ConfigureShortAnswer(EntityTypeBuilder<AcceptedShortAnswerRecord> entity)
    {
        entity.ToTable("AcceptedShortAnswers"); entity.HasKey(x => new { x.LearningItemId, x.Position });
        entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId"); entity.Property(x => x.Position).HasColumnName("Position"); entity.Property(x => x.Value).HasColumnName("Value").IsRequired();
    }

    private static void ConfigureDeck(EntityTypeBuilder<DeckRecord> entity)
    {
        entity.ToTable("Decks"); entity.HasKey(x => x.DeckId); entity.Property(x => x.DeckId).HasColumnName("DeckId").ValueGeneratedNever(); entity.Property(x => x.Name).HasColumnName("Name").IsRequired();
        entity.HasMany(x => x.Memberships).WithOne(x => x.Deck).HasForeignKey(x => x.DeckId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureMembership(EntityTypeBuilder<DeckLearningItemRecord> entity)
    {
        entity.ToTable("DeckLearningItems"); entity.HasKey(x => new { x.DeckId, x.LearningItemId });
        entity.Property(x => x.DeckId).HasColumnName("DeckId"); entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId");
    }
}
