# Illumination – Review and Scheduling Semantics

## Status

Product semantics and initial deterministic scheduling model are specified for implementation.

The constants below are the initial V1 defaults and may later be tuned through product evidence without changing the underlying five-grade model.

## 1. Assessment Scale

Every normal Review ends with exactly one ordered assessment:

1. `Nochmal`
2. `Schwer`
3. `Unsicher`
4. `Gut`
5. `Leicht`

Invariant:

> Worse assessment means earlier return; better assessment means later return.

`Suspended` and `Mastered` are explicit lifecycle states outside this scale.

## 2. Scheduling State

Each active Learning Item has scheduling state containing conceptually:

- `difficulty` in the range 1.0–10.0,
- `stabilityDays` as a positive real number,
- `dueAt`,
- whether the item is currently in short-term relearning,
- short-term queue placement when applicable.

The implementation may use different internal names, but these semantics must remain visible in tests.

## 3. New Item Defaults

A new item begins with:

- `difficulty = 5.0`,
- `stabilityDays = 0.5`,
- immediately eligible for introduction according to the current Study Session's new-item limit.

The first completed Review establishes its first scheduled interval.

## 4. Grade Semantics

### Nochmal

Meaning: extreme failure.

Effects:

- enter short-term relearning,
- reappear after roughly 3 intervening cards when enough other cards exist,
- reduce `stabilityDays` strongly,
- increase `difficulty`.

After the successful completion of the short-term relearning step, the item returns to normal scheduling with a strongly shortened stability, but its historical Reviews remain intact.

### Schwer

Meaning: clear failure / weak recall.

Effects:

- enter short-term relearning,
- reappear after roughly 10 intervening cards when enough other cards exist,
- reduce `stabilityDays`,
- increase `difficulty`, but less than `Nochmal`.

### Unsicher

Meaning: partial / uncertain success.

Effects:

- no mandatory same-session repeat,
- keep the item in relatively active circulation,
- modestly increase or approximately preserve `stabilityDays`,
- slightly increase `difficulty`.

### Gut

Meaning: solid recall.

Effects:

- meaningfully increase `stabilityDays`,
- slightly reduce `difficulty`.

### Leicht

Meaning: immediate, confident recall.

Effects:

- substantially increase `stabilityDays`,
- reduce `difficulty` more strongly than `Gut`,
- never automatically mark the item `Mastered`.

## 5. Short-Term Relearning Queue

### Nochmal

Target placement: after approximately 3 other cards.

### Schwer

Target placement: after approximately 10 other cards.

If the session contains too few other cards:

- place the relearning item at the end of the current queue,
- do not create a tight immediate loop,
- if no other card remains, keep the item immediately due for continued study or the next session.

Target queue placement is more important than wall-clock minutes for these short-term retries.

## 6. Long-Term Interval Model

The initial V1 scheduler uses a deterministic state-transition model inspired by stability/difficulty approaches but owned by Illumination.

The scheduler does **not** copy a four-grade external algorithm.

### Difficulty update

After every Review:

```text
Nochmal : difficulty += 1.20
Schwer  : difficulty += 0.60
Unsicher: difficulty += 0.15
Gut     : difficulty -= 0.20
Leicht  : difficulty -= 0.45
```

Clamp to:

```text
1.0 <= difficulty <= 10.0
```

### Stability update

For an active item not in the immediate short-term queue:

```text
Nochmal : stability = min(stability * 0.25, 3 days)
Schwer  : stability = min(stability * 0.55, 7 days)
Unsicher: stability *= growth(1.20)
Gut     : stability *= growth(2.20)
Leicht  : stability *= growth(3.60)
```

For positive grades, growth is dampened as difficulty increases:

```text
effectiveGrowth = 1 + (baseGrowth - 1) * difficultyFactor

difficultyFactor = clamp(1.15 - 0.07 * difficulty, 0.45, 1.05)
```

Then:

```text
newStability = max(minimumStabilityForGrade, oldStability * effectiveGrowth)
```

Initial minimums:

```text
Unsicher: 1 day
Gut     : 2 days
Leicht  : 4 days
```

After `Nochmal` or `Schwer`, short-term relearning occurs first. On the first successful post-relearning Review, normal scheduling resumes from the reduced retained stability rather than from a completely new item.

## 7. Due Time

Normal scheduling uses an exact `dueAt` timestamp.

For normal intervals:

```text
dueAt = reviewCompletedAt + stabilityDays
```

The UI may render this as relative human language such as:

- later today,
- tomorrow,
- in 6 days,
- in 3 weeks.

Illumination does not require an artificial fixed day-boundary for its domain model.

## 8. Previously Stable Item Forgotten

A failure after a long stable interval must not erase history.

Example:

```text
old stability = 60 days
Nochmal
→ retained stability is capped at 3 days before relearning handling
```

The item enters short-term relearning immediately.

A subsequent successful Review then grows again from this reduced retained state rather than from zero.

`Schwer` uses a less aggressive retained-stability cap of 7 days.

This prevents a previously long interval from immediately bouncing back to several weeks after a single successful retry.

This prevents one lapse from pretending the learner has never seen the material while still making the item much more frequent again.

## 9. Repeated Success

Repeated `Gut` / `Leicht` Reviews progressively extend the interval.

There is no automatic terminal state.

An item can naturally reach intervals of months or longer.

`Mastered` is only an explicit learner action.

## 10. Lifecycle

### Suspended

- explicit only,
- excluded from normal study,
- bad Reviews never suspend automatically.

### Mastered

- explicit only,
- excluded from normal study,
- `Leicht` never masters automatically.

### Reactivation

Reactivating either state:

- retains all Review history,
- makes the item immediately due,
- lets the next Review determine continued scheduling.

## 11. Automatic Evaluation

Two modes exist:

### Manual

The learner always chooses the assessment grade.

### Assisted

For machine-checkable items:

- incorrect → default suggestion `Schwer`,
- correct → default suggestion `Gut`.

The learner may override the suggestion.

Automatic correctness is distinct from Learning Assessment.

## 12. Hint Influence

Default:

- hint usage has no assessment or scheduling penalty.

Optional per-session override:

- if hint influence is enabled and at least one hint was used, the automatically suggested assessment is lowered by at most one grade.

Hint use never forces the final learner-selected grade.

Multiple hints do not stack repeated penalties.

## 13. Session Ordering

Normal Study Session priority:

1. short-term relearning,
2. already-due items,
3. new items.

Within the same priority class, items are shuffled rather than always presented in creation or Deck order.

Explicit `Nochmal` / `Schwer` queue placements remain respected.

## 14. New Items

Default per-session new-item limit:

```text
20
```

The learner may override the value or explicitly choose all new items.

There is no mandatory hard daily new-item limit.

## 15. Determinism

Given:

- previous scheduling state,
- Review assessment,
- relevant policy configuration,
- Review completion time,

the resulting scheduling state must be deterministic and testable.

Randomness may influence display order, but not the domain state transition itself.

## 16. Tuning Rule

The initial constants are product defaults, not sacred values.

Future tuning may change numeric constants while preserving:

- the five-grade ordering,
- lifecycle separation,
- short-term relearning semantics,
- deterministic transitions,
- retained-history behavior.

A change that alters these semantic invariants requires an explicit product decision.
