using Illumination.Domain.Decks;
using Illumination.Domain.Identity;
using Illumination.Domain.Learning;
using Illumination.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public class PersistenceTests
{
    [Fact]
    public void Migrations_create_the_schema_and_can_be_applied_again()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        context.Database.Migrate();
        context.Database.Migrate();

        var tables = context.Database.GetDbConnection().CreateCommand();
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '__EF%' ORDER BY name";
        using var reader = tables.ExecuteReader();
        var tableNames = new List<string>();
        while (reader.Read()) tableNames.Add(reader.GetString(0));

        Assert.Equal(["AcceptedShortAnswers", "AnswerChoices", "DeckLearningItems", "Decks", "Hints", "ImportProvenance", "LearningItemUserFlags", "LearningItems", "QualityReviews", "Reviews", "StudyPreferences", "StudySessionDecks", "StudySessionQueue", "StudySessionReviews", "StudySessions", "UserFlagDefinitions"], tableNames);
    }

    [Fact]
    public void Domain_content_and_membership_round_trip_without_duplicate_learning_state()
    {
        using var connection = OpenConnection();
        using (var setup = CreateContext(connection))
        {
            setup.Database.Migrate();

            var dueAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var active = LearningItem.Create(
                LearningItemId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                "Prompt", "Reference solution", dueAt, ResponseMode.Selection,
                hints: [new Hint("First"), new Hint("Second")],
                directAnswerChoices: [new AnswerChoice("Wrong"), new AnswerChoice("Correct", true)],
                assistanceAnswerChoices: [new AnswerChoice("Help one"), new AnswerChoice("Help two")],
                lowInteractionEligible: true);
            var suspended = LearningItem.Create(
                LearningItemId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                "Suspended prompt", "Suspended solution", dueAt.AddDays(1), ResponseMode.ShortText,
                acceptedShortAnswers: ["first", "second"]);
            suspended.Suspend();
            var mastered = LearningItem.Create(
                LearningItemId.From(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
                "Mastered prompt", "Mastered solution", dueAt.AddDays(2));
            mastered.MarkMastered();

            var firstDeck = Deck.Create(DeckId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")), "First deck");
            var secondDeck = Deck.Create(DeckId.From(Guid.Parse("22222222-2222-2222-2222-222222222222")), "Second deck");
            firstDeck.AddLearningItem(active.Id); secondDeck.AddLearningItem(active.Id);

            setup.LearningItems.AddRange(
                DomainPersistenceMapper.ToRecord(active),
                DomainPersistenceMapper.ToRecord(suspended),
                DomainPersistenceMapper.ToRecord(mastered));
            setup.Decks.AddRange(DomainPersistenceMapper.ToRecord(firstDeck), DomainPersistenceMapper.ToRecord(secondDeck));
            setup.SaveChanges();
        }

        using var reload = CreateContext(connection);
        var records = reload.LearningItems
            .Include(x => x.Hints).Include(x => x.AnswerChoices).Include(x => x.AcceptedShortAnswers)
            .OrderBy(x => x.LearningItemId).ToArray();

        var reloadedActive = DomainPersistenceMapper.ToDomain(records[0]);
        var reloadedSuspended = DomainPersistenceMapper.ToDomain(records[1]);
        var reloadedMastered = DomainPersistenceMapper.ToDomain(records[2]);
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", reloadedActive.Id.ToString());
        Assert.Equal(["First", "Second"], reloadedActive.Hints.Select(x => x.Text));
        Assert.Equal(["Wrong", "Correct"], reloadedActive.DirectAnswerChoices.Select(x => x.Text));
        Assert.Equal(["Help one", "Help two"], reloadedActive.AssistanceAnswerChoices.Select(x => x.Text));
        Assert.True(reloadedActive.DirectAnswerChoices[1].IsCorrect);
        Assert.True(reloadedActive.LowInteractionEligible);
        Assert.Equal(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), reloadedActive.LearningState.DueAt);
        Assert.True(reloadedActive.LearningState.IsNew);
        Assert.Equal(LearningItemLifecycleState.Suspended, reloadedSuspended.LifecycleState);
        Assert.Equal(["first", "second"], reloadedSuspended.AcceptedShortAnswers);
        Assert.Equal(LearningItemLifecycleState.Mastered, reloadedMastered.LifecycleState);

        var memberships = reload.DeckLearningItems.OrderBy(x => x.DeckId).ToArray();
        Assert.Equal(2, memberships.Length);
        Assert.All(memberships, membership => Assert.Equal(reloadedActive.Id.Value, membership.LearningItemId));
        Assert.DoesNotContain(reload.LearningItems, item => item.LearningItemId == Guid.Empty);
    }

    [Fact]
    public void Deleting_a_deck_keeps_the_item_and_deleting_the_item_cascades_content_and_membership()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);
        context.Database.Migrate();
        var item = LearningItem.Create("Prompt", "Solution", DateTimeOffset.UtcNow, hints: [new Hint("Hint")]);
        var deck = Deck.Create("Deck"); deck.AddLearningItem(item.Id);
        context.LearningItems.Add(DomainPersistenceMapper.ToRecord(item));
        context.Decks.Add(DomainPersistenceMapper.ToRecord(deck));
        context.SaveChanges();

        context.Decks.Remove(context.Decks.Single());
        context.SaveChanges();
        Assert.Single(context.LearningItems);
        Assert.Empty(context.DeckLearningItems);

        context.LearningItems.Remove(context.LearningItems.Single());
        context.SaveChanges();
        Assert.Empty(context.Hints);
        Assert.Empty(context.LearningItems);
    }

    [Fact]
    public void Content_quality_history_and_flags_round_trip_across_contexts()
    {
        using var connection = OpenConnection();
        using (var setup = CreateContext(connection))
        {
            setup.Database.Migrate();
            var item = LearningItem.Create(
                LearningItemId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                "Prompt", "Solution", new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
            var first = QualityReview.Create(item.Id, item.ContentRevision, Illumination.Domain.Learning.QualityReviewOutcome.Warning, Illumination.Domain.Learning.QualityReviewEvidenceType.ModelReview, "Ambiguous wording.");
            var replacement = QualityReview.Create(item.Id, item.ContentRevision, Illumination.Domain.Learning.QualityReviewOutcome.Pass, Illumination.Domain.Learning.QualityReviewEvidenceType.UserReview, "Reviewed wording.", "Use the precise term.");
            item.AcceptQualityReview(first);
            item.AcceptQualityReview(replacement, [first.Id]);
            var flag = UserFlagDefinition.Create(UserFlagDefinitionId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), "Needs follow-up", "Review source later.");
            item.AddUserFlag(flag);

            setup.UserFlagDefinitions.Add(new UserFlagDefinitionRecord { UserFlagDefinitionId = flag.Id.Value, Name = flag.Name, Meaning = flag.Meaning });
            setup.LearningItems.Add(DomainPersistenceMapper.ToRecord(item));
            setup.SaveChanges();
        }

        using var reload = CreateContext(connection);
        var record = reload.LearningItems.Include(x => x.QualityReviews).Include(x => x.UserFlagAssignments).Single();
        var itemAfterRestart = DomainPersistenceMapper.ToDomain(record);
        Assert.Equal(1, itemAfterRestart.ContentRevision);
        Assert.Equal(2, itemAfterRestart.QualityReviews.Count);
        var activeReview = itemAfterRestart.QualityReviews.Single(x => !x.IsSuperseded);
        Assert.Equal(activeReview.Id, itemAfterRestart.QualityReviews.Single(x => x.IsSuperseded).SupersededBy);
        Assert.Equal(Illumination.Domain.Learning.QualityReviewEvidenceType.UserReview, itemAfterRestart.QualityReviews.Single(x => !x.IsSuperseded).EvidenceType);
        Assert.Equal(Illumination.Domain.Learning.QualityReviewOutcome.Pass, itemAfterRestart.CurrentQualityState!.Outcome);
        Assert.Single(itemAfterRestart.UserFlagDefinitionIds);
        Assert.Equal("Needs follow-up", reload.UserFlagDefinitions.Single().Name);
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static IlluminationDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<IlluminationDbContext>().UseSqlite(connection).Options);
}
