using Illumination.Application.ContentManagement;
using Illumination.Application.Insights;
using Illumination.Application.Study;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public sealed class LearningInsightPersistenceTests
{
    [Fact]
    public async Task Insight_read_persistence_returns_current_membership_content_reviews_and_sessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options;
        await using (var setup = new IlluminationDbContext(options))
        {
            await setup.Database.MigrateAsync();
            var itemId = Guid.NewGuid();
            var deckId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            setup.LearningItems.Add(DomainPersistenceMapper.ToRecord(LearningItem.Create(LearningItemId.From(itemId), "Insight prompt", "Reference", DateTimeOffset.UtcNow)));
            setup.Decks.Add(new DeckRecord { DeckId = deckId, Name = "Insight deck" });
            setup.DeckLearningItems.Add(new DeckLearningItemRecord { DeckId = deckId, LearningItemId = itemId });
            setup.Reviews.Add(new ReviewRecord { ReviewId = reviewId, LearningItemId = itemId, CompletedAt = DateTimeOffset.UtcNow, Assessment = LearningAssessment.Gut, AutomaticCorrectness = false, SuggestedAssessment = LearningAssessment.Schwer });
            setup.StudySessions.Add(new StudySessionRecord { StudySessionId = sessionId, StartedAt = DateTimeOffset.UtcNow, EvaluationMode = StudyEvaluationMode.Assisted });
            setup.StudySessionDecks.Add(new StudySessionDeckRecord { StudySessionId = sessionId, DeckId = deckId });
            setup.StudySessionReviews.Add(new StudySessionReviewRecord { StudySessionId = sessionId, Position = 0, ReviewId = reviewId });
            await setup.SaveChangesAsync();

            var persistence = new EfCoreLearningInsightPersistence(new FixedFactory(options));
            var items = await persistence.LoadLearningItemsAsync();
            var decks = await persistence.LoadDecksAsync();
            var reviews = await persistence.LoadReviewsAsync();
            var sessions = await persistence.LoadStudySessionsAsync();

            var item = Assert.Single(items);
            Assert.Equal("Reference", item.ReferenceSolution);
            Assert.Equal([deckId], item.CurrentDeckIds);
            Assert.Equal(StudyLearningAssessment.Gut, Assert.Single(item.Reviews).Assessment);
            Assert.Equal(StudyLearningAssessment.Gut, Assert.Single(reviews).Assessment);
            Assert.Equal([itemId], Assert.Single(decks).CurrentLearningItemIds);
            Assert.Equal(StudyEvaluationMode.Assisted, Assert.Single(sessions).EvaluationMode);
            Assert.Equal([sessionId], Assert.Single(reviews).StudySessionIds);
        }
    }

    private sealed class FixedFactory(DbContextOptions<IlluminationDbContext> options) : IDbContextFactory<IlluminationDbContext>
    {
        public IlluminationDbContext CreateDbContext() => new(options);
        public Task<IlluminationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new IlluminationDbContext(options));
    }
}
