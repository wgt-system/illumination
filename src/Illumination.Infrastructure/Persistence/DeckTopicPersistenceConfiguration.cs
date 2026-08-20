using Illumination.Domain.Decks;
using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

internal static class DeckTopicPersistenceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var labels = modelBuilder.Entity<DeckTopicLabelRecord>();
        labels.ToTable("DeckTopicLabels");
        labels.HasKey(x => new { x.DeckId, x.Label });
        labels.Property(x => x.DeckId).HasColumnName("DeckId");
        labels.Property(x => x.Label)
            .HasColumnName("Label")
            .HasMaxLength(Deck.MaximumTopicLabelLength)
            .UseCollation("NOCASE")
            .IsRequired();
        labels.HasIndex(x => x.Label).HasDatabaseName("IX_DeckTopicLabels_Label");

        modelBuilder.Entity<DeckRecord>()
            .HasMany(x => x.TopicLabels)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
