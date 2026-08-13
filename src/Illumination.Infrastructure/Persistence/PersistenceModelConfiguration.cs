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
        learningItems.Property(x => x.Difficulty).HasColumnName("Difficulty").IsRequired();
        learningItems.Property(x => x.StabilityDays).HasColumnName("StabilityDays").IsRequired();
        learningItems.Property(x => x.IsInShortTermRelearning).HasColumnName("IsInShortTermRelearning").IsRequired();
        learningItems.Property(x => x.ContentRevision).HasColumnName("ContentRevision").HasDefaultValue(1).IsRequired();
        learningItems.HasIndex(x => x.DueAt).HasDatabaseName("IX_LearningItems_DueAt");
        learningItems.HasMany(x => x.Hints).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.AnswerChoices).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.AcceptedShortAnswers).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.DeckMemberships).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.QualityReviews).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        learningItems.HasMany(x => x.UserFlagAssignments).WithOne(x => x.LearningItem).HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);

        ConfigureHint(modelBuilder.Entity<HintRecord>());
        ConfigureChoice(modelBuilder.Entity<AnswerChoiceRecord>());
        ConfigureShortAnswer(modelBuilder.Entity<AcceptedShortAnswerRecord>());
        ConfigureDeck(modelBuilder.Entity<DeckRecord>());
        ConfigureMembership(modelBuilder.Entity<DeckLearningItemRecord>());
        ConfigureReview(modelBuilder.Entity<ReviewRecord>());
        ConfigureStudySession(modelBuilder.Entity<StudySessionRecord>());
        ConfigureStudySessionDeck(modelBuilder.Entity<StudySessionDeckRecord>());
        ConfigureStudySessionQueue(modelBuilder.Entity<StudySessionQueueRecord>());
        ConfigureStudySessionReview(modelBuilder.Entity<StudySessionReviewRecord>());
        ConfigureImportProvenance(modelBuilder.Entity<ImportProvenanceRecord>());
        ConfigureQualityReview(modelBuilder.Entity<QualityReviewRecord>());
        ConfigureUserFlagDefinition(modelBuilder.Entity<UserFlagDefinitionRecord>());
        ConfigureLearningItemUserFlag(modelBuilder.Entity<LearningItemUserFlagRecord>());
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

    private static void ConfigureReview(EntityTypeBuilder<ReviewRecord> entity)
    {
        entity.ToTable("Reviews");
        entity.HasKey(x => x.ReviewId);
        entity.Property(x => x.ReviewId).HasColumnName("ReviewId").ValueGeneratedNever();
        entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId").IsRequired();
        entity.Property(x => x.CompletedAt).HasColumnName("CompletedAt").HasConversion(
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)).IsRequired();
        entity.Property(x => x.Assessment).HasColumnName("Assessment").HasConversion<string>().IsRequired();
        entity.Property(x => x.SubmittedResponse).HasColumnName("SubmittedResponse");
        entity.HasOne(x => x.LearningItem).WithMany().HasForeignKey(x => x.LearningItemId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => x.LearningItemId).HasDatabaseName("IX_Reviews_LearningItemId");
    }

    private static void ConfigureStudySession(EntityTypeBuilder<StudySessionRecord> entity)
    {
        entity.ToTable("StudySessions");
        entity.HasKey(x => x.StudySessionId);
        entity.Property(x => x.StudySessionId).HasColumnName("StudySessionId").ValueGeneratedNever();
        entity.Property(x => x.StartedAt).HasColumnName("StartedAt").HasConversion(
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)).IsRequired();
        entity.Property(x => x.CompletedAt).HasColumnName("CompletedAt").HasConversion(
            value => value.HasValue ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null,
            value => value == null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        entity.HasMany(x => x.SelectedDecks).WithOne(x => x.StudySession).HasForeignKey(x => x.StudySessionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(x => x.Queue).WithOne(x => x.StudySession).HasForeignKey(x => x.StudySessionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(x => x.Reviews).WithOne(x => x.StudySession).HasForeignKey(x => x.StudySessionId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureStudySessionDeck(EntityTypeBuilder<StudySessionDeckRecord> entity)
    {
        entity.ToTable("StudySessionDecks");
        entity.HasKey(x => new { x.StudySessionId, x.DeckId });
    }

    private static void ConfigureStudySessionQueue(EntityTypeBuilder<StudySessionQueueRecord> entity)
    {
        entity.ToTable("StudySessionQueue");
        entity.HasKey(x => new { x.StudySessionId, x.Position });
        entity.Property(x => x.LearningItemId).HasColumnName("LearningItemId").IsRequired();
    }

    private static void ConfigureStudySessionReview(EntityTypeBuilder<StudySessionReviewRecord> entity)
    {
        entity.ToTable("StudySessionReviews");
        entity.HasKey(x => new { x.StudySessionId, x.Position });
        entity.HasOne(x => x.Review).WithMany(x => x.StudySessionAssociations).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureImportProvenance(EntityTypeBuilder<ImportProvenanceRecord> entity)
    {
        entity.ToTable("ImportProvenance");
        entity.HasKey(x => x.ImportBatchId);
        entity.Property(x => x.ImportBatchId).HasColumnName("ImportBatchId").ValueGeneratedNever();
        entity.Property(x => x.ImportedAt).HasColumnName("ImportedAt").HasConversion(
            value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)).IsRequired();
        entity.Property(x => x.Contract).HasColumnName("Contract").IsRequired();
        entity.Property(x => x.Version).HasColumnName("Version").IsRequired();
        entity.Property(x => x.ExternalBundleId).HasColumnName("ExternalBundleId");
        entity.Property(x => x.GeneratedFor).HasColumnName("GeneratedFor");
    }

    private static void ConfigureQualityReview(EntityTypeBuilder<QualityReviewRecord> entity)
    {
        entity.ToTable("QualityReviews");
        entity.HasKey(x => x.QualityReviewId);
        entity.Property(x => x.QualityReviewId).ValueGeneratedNever();
        entity.Property(x => x.Outcome).HasConversion<string>().IsRequired();
        entity.Property(x => x.EvidenceType).HasConversion<string>().IsRequired();
        entity.Property(x => x.Findings).IsRequired();
        entity.HasIndex(x => new { x.LearningItemId, x.ContentRevision });
        entity.HasOne<QualityReviewRecord>().WithMany().HasForeignKey(x => x.SupersededBy).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureUserFlagDefinition(EntityTypeBuilder<UserFlagDefinitionRecord> entity)
    {
        entity.ToTable("UserFlagDefinitions");
        entity.HasKey(x => x.UserFlagDefinitionId);
        entity.Property(x => x.UserFlagDefinitionId).ValueGeneratedNever();
        entity.Property(x => x.Name).IsRequired();
        entity.Property(x => x.Meaning).IsRequired();
    }

    private static void ConfigureLearningItemUserFlag(EntityTypeBuilder<LearningItemUserFlagRecord> entity)
    {
        entity.ToTable("LearningItemUserFlags");
        entity.HasKey(x => new { x.LearningItemId, x.UserFlagDefinitionId });
        entity.HasOne(x => x.UserFlagDefinition).WithMany().HasForeignKey(x => x.UserFlagDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
