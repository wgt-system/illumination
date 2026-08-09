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
- whether short-term reinforcement remains required across sessions.

The current Study Session owns the temporary `session learning stack`: its queue, repeated appearances, and relative reinsertion positions. Queue position is not durable Learning State. Durable Learning State records only whether short-term reinforcement remains required; exact intervening-card position is session-local.

The implementation may use different internal names, but these semantics must remain visible in tests.

## 3. New Item Defaults

A new item begins with:

- `difficulty = 5.0`,
- `stabilityDays = 0.5`,
- immediately eligible for introduction according to the current Study Session's new-item limit.

The first completed Review changes `IsNew` to `false`. `Nochmal`, `Schwer`, and
`Unsicher` may keep the item reinforcement-required without establishing a normal
future interval; `Gut` and `Leicht` establish normal future scheduling.

## 4. Grade Semantics

### Nochmal

Meaning: extreme failure.

Effects:

- remain in the current session learning stack,
- return after 1 intervening card when enough other cards exist,
- reduce `stabilityDays` strongly,
- increase `difficulty`.

The item remains reinforcement-required until `Gut` or `Leicht` graduates it from the current session. No normal future `dueAt` is established while it remains in the stack.

### Schwer

Meaning: clear failure / weak recall.

Effects:

- remain in the current session learning stack,
- return after 5 intervening cards when enough other cards exist,
- reduce `stabilityDays`,
- increase `difficulty`, but less than `Nochmal`.

The item remains reinforcement-required until `Gut` or `Leicht` graduates it from the current session. No normal future `dueAt` is established while it remains in the stack.

### Unsicher

Meaning: partial / uncertain success.

Effects:

- remain in the current session learning stack,
- return at the end of the current learning stack,
- do not perform normal positive long-term stability growth,
- do not establish a future normal `dueAt`,
- slightly increase `difficulty`.

Retained stability is preserved until the item later graduates with `Gut` or `Leicht`.

### Gut

Meaning: solid recall.

Effects:

- leave the current session learning stack,
- meaningfully increase `stabilityDays`,
- slightly reduce `difficulty`.

`Gut` clears short-term reinforcement and establishes the normal future `dueAt`.

### Leicht

Meaning: immediate, confident recall.

Effects:

- leave the current session learning stack,
- substantially increase `stabilityDays`,
- reduce `difficulty` more strongly than `Gut`,
- never automatically mark the item `Mastered`.

`Leicht` clears short-term reinforcement and establishes a later normal future `dueAt` than `Gut` for comparable state.

## 5. Session Learning Stack

### Nochmal

Target placement: after 1 other card.

### Schwer

Target placement: after 5 other cards.

`Unsicher` is appended to the end of the current learning stack.

If the session contains too few other cards:

- place the relearning item at the end of the current queue,
- if no other card remains, make the item the next/current learning item again.

This single-card behavior is intentional. The learner can explicitly complete/end the Study Session instead of being forced to continue indefinitely.

If the learner completes a session while an item remains reinforcement-required, it remains immediately due/high priority for a later session; ending the session does not invent a successful future interval.

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

For every completed Review, the transition order is deterministic:

1. Apply the grade's difficulty delta.
2. Clamp difficulty to `1.0`–`10.0`.
3. Only for `Gut` or `Leicht`, calculate positive stability growth using that resulting clamped difficulty.
4. Apply the grade's retained-stability rule: reduce stability for `Nochmal` or `Schwer`, preserve retained stability for `Unsicher`, and apply positive growth for `Gut` or `Leicht`.
5. Only for `Gut` or `Leicht`, calculate `dueAt` from the Review completion time plus the resulting stability.

Every completed Review also changes `IsNew` to `false`, including a Review that leaves the item reinforcement-required in the session learning stack. `Nochmal`, `Schwer`, and `Unsicher` do not receive a normal future `dueAt` while they remain in that stack.

### Stability update

For every active item, apply the retained-state rule for its assessment. Positive growth applies only when the item graduates from the session learning stack:

```text
Nochmal : stability = min(stability * 0.25, 3 days)
Schwer  : stability = min(stability * 0.55, 7 days)
Unsicher: retained stability is preserved; no normal positive growth
Gut     : stability *= growth(2.20)
Leicht  : stability *= growth(3.60)
```

For graduating grades (`Gut` and `Leicht`), growth is dampened as difficulty increases:

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
Gut     : 2 days
Leicht  : 4 days
```

After `Nochmal` or `Schwer`, the item remains in the session learning stack. `Unsicher` also remains in that stack and preserves retained stability. A subsequent `Gut` or `Leicht` Review clears short-term reinforcement and normal scheduling resumes from the reduced retained stability rather than from a completely new item.

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

The item enters the session learning stack immediately and remains reinforcement-required.

A subsequent `Gut` or `Leicht` Review then grows again from this reduced retained state rather than from zero. `Unsicher` does not establish a future normal dueAt.

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

### Reactivate Suspended

Reactivate applies only to `Suspended`:

- retains all Review history,
- retains the current difficulty and stability,
- makes the item immediately due,
- lets the next Review determine continued scheduling.

### Unmark Mastered

UnmarkMastered applies only to `Mastered`:

- retains all Review history,
- retains the current difficulty and stability,
- makes the item immediately due,
- lets the next Review determine continued scheduling.

## 11. Automatic Evaluation (v0.3)

Two modes exist:

### Manual

The learner always chooses the assessment grade.

### Assisted

For machine-checkable items:

- incorrect → default suggestion `Schwer`,
- correct → default suggestion `Gut`.

The learner may override the suggestion.

Automatic correctness is distinct from Learning Assessment.

## 12. Hint Influence (v0.3)

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

Explicit `Nochmal`, `Schwer`, and `Unsicher` session-stack placements remain respected.

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
