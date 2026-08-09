using System.Collections.Concurrent;
using System.Reflection;
using Illumination.Application.ContentManagement;
using Illumination.Application.Study;
using Xunit;

#pragma warning disable xUnit1051

namespace Illumination.Application.Tests;

public class StudySessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task Starting_requires_a_deck_and_unknown_decks_do_not_create_a_session()
    {
        var store = new FakeStudyPersistence();
        var service = CreateService(store);

        await Assert.ThrowsAsync<StudyValidationException>(() => service.StartStudySessionAsync(new StartStudySessionCommand([])));
        await Assert.ThrowsAsync<StudyNotFoundException>(() => service.StartStudySessionAsync(new StartStudySessionCommand([Guid.NewGuid()])));

        Assert.Equal(0, store.StartedSessionCount);
    }

    [Fact]
    public async Task Multiple_decks_form_a_union_and_exclude_inactive_or_future_items()
    {
        var store = new FakeStudyPersistence();
        var firstDeck = Guid.NewGuid();
        var secondDeck = Guid.NewGuid();
        var duplicate = Guid.NewGuid();
        var active = Guid.NewGuid();
        var suspended = Guid.NewGuid();
        var future = Guid.NewGuid();
        store.Decks[firstDeck] = new StudyDeckSnapshot(firstDeck, [duplicate, active, suspended]);
        store.Decks[secondDeck] = new StudyDeckSnapshot(secondDeck, [duplicate, future]);
        store.Items[duplicate] = Item(duplicate, dueAt: Now.AddDays(-1));
        store.Items[active] = Item(active, dueAt: Now.AddDays(-1));
        store.Items[suspended] = Item(suspended, lifecycle: LearningItemLifecycle.Suspended, dueAt: Now.AddDays(-1));
        store.Items[future] = Item(future, dueAt: Now.AddDays(1));

        var session = await CreateService(store).StartStudySessionAsync(new StartStudySessionCommand([firstDeck, secondDeck]));

        Assert.Equal(2, session.Queue.Count);
        Assert.Equal(2, session.Queue.Distinct().Count());
        Assert.DoesNotContain(suspended, session.Queue);
        Assert.DoesNotContain(future, session.Queue);
    }

    [Fact]
    public async Task Queue_priority_is_relearning_then_due_then_new()
    {
        var store = new FakeStudyPersistence();
        var deckId = Guid.NewGuid();
        var relearning = Guid.NewGuid();
        var due = Guid.NewGuid();
        var newItem = Guid.NewGuid();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, [newItem, due, relearning]);
        store.Items[relearning] = Item(relearning, isNew: false, dueAt: Now.AddDays(10), relearning: true, target: 3);
        store.Items[due] = Item(due, isNew: false, dueAt: Now.AddDays(-1));
        store.Items[newItem] = Item(newItem, isNew: true, dueAt: Now);

        var session = await CreateService(store).StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        Assert.Equal([relearning, due, newItem], session.Queue);
    }

    [Fact]
    public async Task New_item_default_override_and_all_new_options_are_supported()
    {
        var store = new FakeStudyPersistence();
        var deckId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, ids);
        foreach (var id in ids)
        {
            store.Items[id] = Item(id, isNew: true, dueAt: Now);
        }

        var service = CreateService(store);
        var defaultSession = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));
        var overrideSession = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId], NewItemLimit: 3));
        var allNewSession = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId], AllNew: true));

        Assert.Equal(20, defaultSession.Queue.Count);
        Assert.Equal(3, overrideSession.Queue.Count);
        Assert.Equal(25, allNewSession.Queue.Count);
    }

    [Fact]
    public async Task Ordering_is_injected_per_priority_class()
    {
        var store = new FakeStudyPersistence();
        var deckId = Guid.NewGuid();
        var dueA = Guid.NewGuid();
        var dueB = Guid.NewGuid();
        var newA = Guid.NewGuid();
        var newB = Guid.NewGuid();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, [dueA, newA, dueB, newB]);
        store.Items[dueA] = Item(dueA, isNew: false, dueAt: Now.AddDays(-1));
        store.Items[dueB] = Item(dueB, isNew: false, dueAt: Now.AddDays(-1));
        store.Items[newA] = Item(newA, isNew: true, dueAt: Now);
        store.Items[newB] = Item(newB, isNew: true, dueAt: Now);

        var session = await new StudySessionService(store, new FixedTimeProvider(Now), new ReverseOrdering()).StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        Assert.Equal([dueB, dueA, newB, newA], session.Queue);
    }

    [Fact]
    public async Task Session_identity_is_stable_and_get_next_repeats_until_review()
    {
        var store = CreateStoreWithSingleItem(out var deckId, out var itemId);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        var first = await service.GetNextStudySessionItemAsync(session.Id);
        var second = await service.GetNextStudySessionItemAsync(session.Id);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(itemId, first!.Id);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Review_uses_injected_time_and_preserves_opaque_response_through_atomic_commit()
    {
        var store = CreateStoreWithSingleItem(out var deckId, out var itemId);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        var result = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, itemId, StudyLearningAssessment.Gut, "  raw response  "));

        Assert.Equal(Now, result.CompletedAt);
        Assert.Equal(Now, store.LastReview!.CompletedAt);
        Assert.Equal("  raw response  ", store.LastReview.SubmittedResponse);
        Assert.Equal(1, store.AtomicCommitCount);
        Assert.NotNull(store.LastCommittedItem);
        Assert.Equal(itemId, store.LastCommittedItem!.Id);
        Assert.False(store.LastCommittedItem.IsNew);
        Assert.Equal(store.LastReview.Id, store.LastCommittedSession!.ReviewIds.Single());
    }

    [Fact]
    public async Task Review_must_match_the_queue_head()
    {
        var store = CreateStoreWithTwoDueItems(out var deckId, out var first, out var second);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        await Assert.ThrowsAsync<StudyValidationException>(() => service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, second, StudyLearningAssessment.Gut)));
        Assert.Equal(0, store.AtomicCommitCount);
        Assert.Equal(first, (await service.GetNextStudySessionItemAsync(session.Id))!.Id);
    }

    [Fact]
    public async Task Nochmal_is_reinserted_after_three_intervening_cards()
    {
        var store = CreateStoreWithRelearningAndOthers(4, out var deckId, out var relearningId, out var others);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        var result = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, relearningId, StudyLearningAssessment.Nochmal));

        Assert.Equal([others[0], others[1], others[2], relearningId, others[3]], result.Session.Queue);
        Assert.Equal(3, store.LastCommittedItem!.InterveningCardTarget);
    }

    [Fact]
    public async Task Schwer_is_reinserted_after_ten_intervening_cards()
    {
        var store = CreateStoreWithRelearningAndOthers(10, out var deckId, out var relearningId, out var others);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));

        var result = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, relearningId, StudyLearningAssessment.Schwer));

        Assert.Equal(others.Append(relearningId), result.Session.Queue);
        Assert.Equal(10, store.LastCommittedItem!.InterveningCardTarget);
    }

    [Fact]
    public async Task Relearning_is_appended_when_fewer_cards_remain_and_omitted_when_none_remain()
    {
        var withOneOther = CreateStoreWithRelearningAndOthers(1, out var firstDeck, out var firstItem, out var firstOthers);
        var firstService = CreateService(withOneOther);
        var firstSession = await firstService.StartStudySessionAsync(new StartStudySessionCommand([firstDeck]));
        var appended = await firstService.SubmitReviewAsync(new SubmitStudyReviewCommand(firstSession.Id, firstItem, StudyLearningAssessment.Nochmal));
        Assert.Equal([firstOthers[0], firstItem], appended.Session.Queue);

        var alone = CreateStoreWithRelearningAndOthers(0, out var secondDeck, out var secondItem, out _);
        var secondService = CreateService(alone);
        var secondSession = await secondService.StartStudySessionAsync(new StartStudySessionCommand([secondDeck]));
        var noSelfLoop = await secondService.SubmitReviewAsync(new SubmitStudyReviewCommand(secondSession.Id, secondItem, StudyLearningAssessment.Nochmal));
        Assert.Empty(noSelfLoop.Session.Queue);
    }

    [Fact]
    public async Task Successful_post_relearning_review_is_not_reinserted()
    {
        var store = CreateStoreWithRelearningAndOthers(1, out var deckId, out var relearningId, out var others);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));
        var afterFailure = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, relearningId, StudyLearningAssessment.Nochmal));
        var afterOther = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, others[0], StudyLearningAssessment.Gut));
        var afterSuccess = await service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, relearningId, StudyLearningAssessment.Gut));

        Assert.Equal([relearningId], afterOther.Session.Queue);
        Assert.Empty(afterSuccess.Session.Queue);
        Assert.False(store.LastCommittedItem!.IsInShortTermRelearning);
        Assert.NotEmpty(afterFailure.Session.Queue);
    }

    [Fact]
    public async Task Early_completion_retains_history_and_rejects_later_reviews()
    {
        var store = CreateStoreWithSingleItem(out var deckId, out var itemId);
        var service = CreateService(store);
        var session = await service.StartStudySessionAsync(new StartStudySessionCommand([deckId]));
        var completed = await service.CompleteStudySessionAsync(session.Id);

        Assert.Equal(Now, completed.CompletedAt);
        Assert.Equal(session.SelectedDeckIds, completed.SelectedDeckIds);
        await Assert.ThrowsAsync<StudyValidationException>(() => service.SubmitReviewAsync(new SubmitStudyReviewCommand(session.Id, itemId, StudyLearningAssessment.Gut)));
    }

    [Fact]
    public void Public_study_contracts_do_not_expose_domain_types()
    {
        var studyTypes = typeof(StudySessionService).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace?.StartsWith("Illumination.Application.Study", StringComparison.Ordinal) == true);

        foreach (var type in studyTypes)
        {
            foreach (var memberType in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .SelectMany(GetMemberTypes))
            {
                Assert.DoesNotContain("Illumination.Domain", memberType.FullName ?? memberType.Name);
            }
        }
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) => member switch
    {
        PropertyInfo property => [property.PropertyType],
        MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(parameter => parameter.ParameterType)],
        ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
        _ => [],
    };

    private static StudySessionService CreateService(FakeStudyPersistence store) => new(store, new FixedTimeProvider(Now), new IdentityOrdering());

    private static FakeStudyPersistence CreateStoreWithSingleItem(out Guid deckId, out Guid itemId)
    {
        var store = new FakeStudyPersistence();
        deckId = Guid.NewGuid();
        itemId = Guid.NewGuid();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, [itemId]);
        store.Items[itemId] = Item(itemId, isNew: true, dueAt: Now);
        return store;
    }

    private static FakeStudyPersistence CreateStoreWithTwoDueItems(out Guid deckId, out Guid first, out Guid second)
    {
        var store = new FakeStudyPersistence();
        deckId = Guid.NewGuid();
        first = Guid.NewGuid();
        second = Guid.NewGuid();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, [first, second]);
        store.Items[first] = Item(first, isNew: false, dueAt: Now.AddDays(-1));
        store.Items[second] = Item(second, isNew: false, dueAt: Now.AddDays(-1));
        return store;
    }

    private static FakeStudyPersistence CreateStoreWithRelearningAndOthers(int otherCount, out Guid deckId, out Guid relearningId, out Guid[] others)
    {
        var store = new FakeStudyPersistence();
        deckId = Guid.NewGuid();
        relearningId = Guid.NewGuid();
        others = Enumerable.Range(0, otherCount).Select(_ => Guid.NewGuid()).ToArray();
        store.Decks[deckId] = new StudyDeckSnapshot(deckId, [relearningId, .. others]);
        store.Items[relearningId] = Item(relearningId, isNew: false, dueAt: Now, relearning: true, target: 3);
        foreach (var id in others)
        {
            store.Items[id] = Item(id, isNew: false, dueAt: Now.AddDays(-1));
        }

        return store;
    }

    private static StudyLearningItemSnapshot Item(
        Guid id,
        bool isNew = false,
        DateTimeOffset? dueAt = null,
        LearningItemLifecycle lifecycle = LearningItemLifecycle.Active,
        double difficulty = 5.0,
        double stabilityDays = 2.0,
        bool relearning = false,
        int? target = null) => new(
        id, "Prompt " + id, "Solution " + id, LearningItemResponseMode.SelfAssessed,
        [], [], [], [], false, lifecycle, isNew, dueAt ?? Now, difficulty, stabilityDays, relearning, target, []);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class IdentityOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.ToArray();
    }

    private sealed class ReverseOrdering : IStudySessionOrdering
    {
        public IReadOnlyList<Guid> Order(IReadOnlyList<Guid> learningItemIds) => learningItemIds.Reverse().ToArray();
    }

    private sealed class FakeStudyPersistence : IStudySessionPersistence
    {
        public Dictionary<Guid, StudyDeckSnapshot> Decks { get; } = [];
        public Dictionary<Guid, StudyLearningItemSnapshot> Items { get; } = [];
        public Dictionary<Guid, StudySessionSnapshot> Sessions { get; } = [];
        public int StartedSessionCount { get; private set; }
        public int AtomicCommitCount { get; private set; }
        public StudyLearningItemSnapshot? LastCommittedItem { get; private set; }
        public StudyReviewSnapshot? LastReview { get; private set; }
        public StudySessionSnapshot? LastCommittedSession { get; private set; }

        public Task<IReadOnlyList<StudyDeckSnapshot>> LoadDecksAsync(IReadOnlyList<Guid> deckIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudyDeckSnapshot>>(deckIds.Where(Decks.ContainsKey).Select(id => Decks[id]).ToArray());

        public Task<IReadOnlyList<StudyLearningItemSnapshot>> LoadLearningItemsAsync(IReadOnlyList<Guid> learningItemIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StudyLearningItemSnapshot>>(learningItemIds.Where(Items.ContainsKey).Select(id => Items[id]).ToArray());

        public Task<StudyLearningItemSnapshot?> FindLearningItemAsync(Guid learningItemId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(learningItemId));

        public Task<StudySessionSnapshot?> FindStudySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.GetValueOrDefault(sessionId));

        public Task SaveStartedStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default)
        {
            StartedSessionCount++;
            Sessions[session.Id] = session;
            return Task.CompletedTask;
        }

        public Task CommitReviewAsync(StudyLearningItemSnapshot learningItem, StudyReviewSnapshot review, StudySessionSnapshot session, CancellationToken cancellationToken = default)
        {
            AtomicCommitCount++;
            Items[learningItem.Id] = learningItem;
            Sessions[session.Id] = session;
            LastCommittedItem = learningItem;
            LastReview = review;
            LastCommittedSession = session;
            return Task.CompletedTask;
        }

        public Task CompleteStudySessionAsync(StudySessionSnapshot session, CancellationToken cancellationToken = default)
        {
            Sessions[session.Id] = session;
            return Task.CompletedTask;
        }
    }
}
