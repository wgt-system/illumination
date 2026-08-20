using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

internal static class DeckLearningActivityProfilePersistenceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var profiles = modelBuilder.Entity<DeckLearningActivityProfileRecord>();
        profiles.ToTable("DeckLearningActivityProfiles");
        profiles.HasKey(x => new { x.DeckId, x.Profile });
        profiles.Property(x => x.DeckId).HasColumnName("DeckId");
        profiles.Property(x => x.Profile)
            .HasColumnName("Profile")
            .HasConversion<string>()
            .IsRequired();
        profiles.HasIndex(x => x.Profile).HasDatabaseName("IX_DeckLearningActivityProfiles_Profile");

        modelBuilder.Entity<DeckRecord>()
            .HasMany(x => x.LearningActivityProfiles)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
