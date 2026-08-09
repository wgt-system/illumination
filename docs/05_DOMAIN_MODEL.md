# Illumination – Domain Model

## Status

Substantially specified for the first product phase.

The five-grade review semantics and deterministic repetition algorithm are defined in `docs/08A_SCHEDULING_SEMANTICS.md`.

## 1. Modeling Principle

Illumination distinguishes:

- reusable learning content,
- historical review facts,
- current learning/scheduling state,
- user-defined organization,
- explicit lifecycle state,
- external references.

Historical facts are not overwritten merely to represent current state.

Deck membership is organizational and does not own learning progress.

## 2. Aggregate Root: Learning Item

`Learning Item` is the canonical domain term for the smallest independently reviewable unit.

It represents one question or mini-task that can be reviewed independently.

### Known content

A Learning Item owns conceptually:

- stable Illumination identity,
- prompt / task statement,
- exactly one reference solution,
- zero or more hints,
- optional answer choices,
- explicit `lowInteractionEligible` property,
- interaction configuration needed for the item,
- lifecycle state,
- content metadata required by import and review.

### Invariants

- one Learning Item represents one independent review target,
- every Learning Item has a reference solution,
- a Learning Item does not contain several independent questions,
- hints have cardinality `0..*`,
- deck membership does not define Learning Item identity,
- changing deck membership does not reset review history or learning state.

### Interaction model

`Recall` or `self-assessment` is **not** a Learning Item type.

It is a review/evaluation behavior that can be used with many items.

The basic content model is therefore not a rigid hierarchy such as:

```text
RecallItem
MultipleChoiceItem
SelfAssessmentItem
```

Instead, an item is a question or mini-task whose interaction may expose different response mechanisms.

Examples:

- a normal factual question may be answered mentally and self-assessed,
- the same kind of question may expose answer choices as optional help,
- an item may be authored directly as a multiple-choice question,
- a small coding task may request a short code response,
- some items may support automatic checking,
- others inherently require self-assessment.

The exact first implementation representation remains an application-design concern.

### Response forms

A Learning Item may use one of the following existing response forms:

- `SelfAssessed`,
- `Selection`,
- `ShortText`,
- `Code`.

These are interaction forms, not separate aggregate types.

The v0.3 scope covers the end-to-end workflows for these forms, including interaction handling, assistance/reveal behavior, automatic evaluation, normalization/checking, and code-response UX.

## 3. Value / Entity: Reference Solution

Every Learning Item has exactly one conceptual reference solution.

The reference solution:

- provides the canonical answer, explanation, or example,
- is available for rapid comparison,
- does not imply that no alternative answer can be correct,
- may contain text, code, or another representation appropriate to the item.

## 4. Entity / Value: Hint

A Learning Item may have `0..*` hints.

Hints:

- are optional,
- can be requested before revealing the full reference solution,
- may be revealed progressively,
- do not affect the review result by default.

### Configurable hint impact

Illumination must allow hint usage to be considered by evaluation/scheduling when explicitly enabled.

Default behavior:

> The learner may request as many available hints as desired without automatic penalty.

The exact penalty or scheduling consequence, when enabled, is not yet defined.

## 5. Optional Answer Choices

Answer choices may appear in two distinct roles:

### Authored response form

The Learning Item itself is intended to be answered by selecting from supplied choices.

### Assistance

A normal question may reveal answer choices as a form of help after the learner first attempts free recall.

These two cases must not be conflated in import or application design.

## 6. Aggregate / Entity: Review

A Review records one historical learning interaction with exactly one Learning Item.

### Conceptual data

A Review records enough information to preserve:

- review identity,
- Learning Item identity,
- occurrence time,
- final learning assessment,
- relevant interaction facts needed for history or scheduling.

Potentially relevant interaction facts include:

- hints requested,
- whether answer choices were revealed as assistance,
- optional submitted response payload when retained,
- automatically detected correctness when available in v0.3,
- whether the reference solution was revealed.

In v0.2, an optional submitted response payload is opaque historical content; v0.3 defines response interaction workflows and any interpretation or evaluation.

### Invariants

- a Review concerns exactly one Learning Item,
- a recorded Review is historical,
- deck membership does not create a separate Review history,
- the Review outcome must be sufficient for the repetition model to evolve Learning State.

## 7. Value: Learning Assessment

Every normal Review ends with one of five ordered assessment grades:

1. `Nochmal`
2. `Schwer`
3. `Unsicher`
4. `Gut`
5. `Leicht`

The order is monotonic:

> worse assessment → earlier return; better assessment → later return.

### Nochmal

Extreme failure. The item remains in the current session learning stack and should return after one intervening card when possible. It strongly reduces retained stability and keeps reinforcement required.

### Schwer

Clear failure or very weak recall. The item remains in the current session learning stack and should return after five intervening cards when possible. It reduces retained stability and keeps reinforcement required.

### Unsicher

Partial or uncertain success. The item remains in the current session learning stack and returns at the end of the current learning stack. It may increase difficulty modestly, but does not perform normal positive stability growth or establish a future normal dueAt.

### Gut

Solid recall. The item graduates from the current session learning stack, clears short-term reinforcement, and receives a normal future dueAt from the resulting stability.

### Leicht

Immediate, confident recall. The item graduates from the current session learning stack and receives a longer normal future dueAt than `Gut` for comparable state.

### Separation from lifecycle

`Suspended` and `Mastered` are explicit lifecycle states outside this five-grade scale.

Neither is reached automatically through assessment grades.

## 8. Automatic Evaluation Policy (v0.3)

Automatic answer evaluation is optional.

Illumination supports two evaluation modes:

### Manual

No automatic correctness judgment is required. The learner always chooses the final five-grade Learning Assessment.

### Assisted

When the response is machine-checkable, Illumination may determine correctness and suggest a grade.

Default v0.3 suggestion:

- incorrect → `Schwer`,
- correct → `Gut`.

The learner can change the suggested grade before completing the Review.

Automatic correctness and final Learning Assessment remain distinct.

The user has:

- a global default evaluation mode,
- a per-Study-Session override.

Items that cannot be checked automatically continue through manual assessment.

## 9. Aggregate / State: Learning State

Learning State represents the current review-relevant state of one Learning Item.

It contains enough information to answer at least:

- is the item new,
- is the item currently due,
- when should it next be reviewed,
- what retention/scheduling state does the chosen repetition model require,
- is normal repetition currently enabled.

### Invariants

- one authoritative current Learning State exists per Learning Item for the learner,
- Learning State evolves from Reviews and explicit lifecycle actions,
- every completed Review changes `IsNew` to `false`,
- moving an item between decks does not reset Learning State,
- analytics do not mutate Learning State independently.

The scheduling fields and transitions follow `docs/08A_SCHEDULING_SEMANTICS.md`; internal field names may vary while preserving those tested semantics.

### Durable/session distinction

Learning State contains durable difficulty, retained stability, future normal due state, and whether short-term reinforcement remains required across sessions. It does not contain the position of an item in a particular Study Session.

The Study Session owns the temporary session learning stack, including the current queue, repeated appearances, and assessment-driven reinsertion position. The Study Session does not own a second copy of Learning State. The existing persisted `InterveningCardTarget` concept is superseded conceptually by this distinction; a later implementation slice may retire or deprecate that field without a migration decision in this specification.

### Failure after long-term stability

When a previously long-stable Learning Item is forgotten, its prior history is retained.

A severe failure substantially reduces its current stability and enters short-term relearning, but does not erase the item's history or blindly recreate it as a never-seen item.

## 10. Lifecycle: Active, Suspended, Mastered

Two explicit actions are required for the first major product line.

### Suspended

A Learning Item can be suspended.

Intended purpose:

- real blockers,
- temporarily unsuitable content,
- material that should not currently appear in normal repetition.

A suspended item remains stored with its history but is excluded from normal scheduling until reactivated.

### Mastered

A Learning Item can be explicitly marked mastered.

Intended purpose:

- content the learner considers permanently trivial or sufficiently internalized that normal repetition is no longer useful.

A mastered item remains stored with its history but is excluded from normal repetition unless the learner later reverses the state.

Normal scheduling never automatically produces `Mastered`.

Instead, repeated strong reviews can result in increasingly long intervals until an item is practically encountered only very rarely.

### Reactivate Suspended

Reactivate applies only to a `Suspended` item. It preserves Review history and retained scheduling state, and makes the item immediately due. The following Review then determines its continued scheduling state.

### Unmark Mastered

UnmarkMastered applies only to a `Mastered` item. It preserves Review history and retained scheduling state, and makes the item immediately due. The following Review then determines its continued scheduling state.

### Distinction

Suspended and Mastered are explicit lifecycle decisions, not ordinary review grades.

- `Suspended` means normal repetition is intentionally paused, for example because the item is currently a blocker or unsuitable.
- `Mastered` means the learner explicitly considers normal repetition unnecessary.
- a very poor review remains a review assessment and should normally cause earlier repetition rather than suspension.
- a very strong review remains a review assessment and should normally cause a much longer interval rather than mastering the item.

## 11. Aggregate Root: Deck

`Deck` is the canonical domain term for a user-defined grouping of Learning Items.

### Known data

- stable deck identity,
- name,
- membership,
- optional organizational metadata introduced later.

### Invariants

- a deck may contain arbitrary Learning Items,
- a deck does not need to correspond to one topic,
- one Learning Item **may belong to multiple Decks simultaneously**,
- deck membership does not own or duplicate Learning State,
- deck membership does not own or duplicate Review history,
- changing membership does not reset progress.

This implies a many-to-many organizational relationship:

```text
Deck * <──> * Learning Item
```

### Deletion semantics

Deleting a Deck removes the Deck and its memberships only. It never automatically deletes Learning Items.

A Learning Item may be explicitly deleted with confirmation. Explicit Learning Item deletion removes the item and its associated Learning State and Review history permanently.

Review history is otherwise retained completely and without a product-level retention limit.

In v0.2, a Review may retain an optional submitted response payload without interpreting it. Actual text/code interaction workflows, normalized matching, and code execution are v0.3 concerns.

## 12. Study Session

A Study Session selects reviewable Learning Items within a chosen scope and records Reviews. The initial v0.2 scope supports one or more selected Decks.

### Scope

A session may select:

- one Deck,
- several Decks,
- active items from the selected Decks.

When several Decks are selected, their Learning Items form a set union: an item belonging to several selected Decks appears only once in the session queue.

### Session learning stack and queue priority

The session learning stack is temporary Study Session state. It may contain repeated appearances of an item before that item graduates. Normal initial priority is:

1. short-term relearning items,
2. already-due items,
3. new items.

During the session, the assessment determines the stack behavior:

- `Nochmal` returns after one intervening card when possible.
- `Schwer` returns after five intervening cards when possible.
- `Unsicher` is appended to the end of the current learning stack.
- `Gut` and `Leicht` leave the current learning stack and enter future normal scheduling.

If fewer other entries remain than requested, `Nochmal` and `Schwer` are appended to the end. If no other entry remains, the item is the next/current learning item again. This intentional single-card behavior may be ended explicitly by completing the Study Session.

If the session ends while an item still requires reinforcement, durable Learning State keeps that requirement and the item remains immediately due/high priority in a later session. No successful future interval is invented merely by ending the session.

### New-item limit

New items have a configurable per-session limit.

Default: `20` new items.

The learner may explicitly choose to include all new items.

There is no mandatory hard daily new-item limit.

### Durable session record

A Study Session is retained as lightweight history including:

- start,
- end,
- selected Decks/filters,
- mode,
- associated Review identities.

The Study Session does not own a separate copy of Learning State. Low-interaction filtering is deferred to v0.3.

## 13. Content Import

Structured imports create or update Illumination-owned Learning Items and organizational data.

The import model must support:

- questions / mini-tasks,
- reference solutions,
- `0..*` hints,
- direct multiple-choice items where requested,
- optional answer choices as assistance,
- short-response/coding-oriented interaction configuration,
- explicit deck placement where supplied,
- versioned contract metadata.

Import must not reset existing review history merely because new content is added.

Intentional updates reference explicit stable Illumination Learning Item identifiers. Semantic/fuzzy similarity must not silently authorize mutation. The Content Bundle 1.0 operation envelope is defined by the published schema.

## 14. External Learning Reference

A future External Learning Reference connects Illumination coverage to a learning need owned elsewhere, initially Vocation.

It is not a shared domain entity.

Open questions remain around:

- identity,
- attachment level,
- cardinality,
- coverage aggregation,
- synchronization direction.

No final aggregate should be introduced until the Vocation integration semantics require it.

## 15. Commands

Conceptual commands now include:

- CreateLearningItem
- EditLearningItem
- SuspendLearningItem
- ReactivateSuspendedLearningItem
- MarkLearningItemMastered
- UnmarkLearningItemMastered
- RevealHint
- RevealAnswerChoices
- RevealReferenceSolution
- RecordReview
- CreateDeck
- RenameDeck
- AddLearningItemToDeck
- RemoveLearningItemFromDeck
- ImportLearningContent
- StartStudySession
- CompleteStudySession

Exact API shapes are intentionally unspecified.

## 16. Queries

Conceptual queries include:

- GetLearningItem
- GetDeck
- ListDecks
- GetDueLearningItems
- GetLearningProgress
- GetReviewHistory
- GetEligibleLowInteractionItems
- GetSuspendedLearningItems
- GetMasteredLearningItems

Exact read models are specified separately.

## 17. Historical vs Current State

### Historical

- Reviews,
- recorded interaction facts required by the chosen model,
- import history where retained.

### Current

- Learning Item content,
- Deck membership,
- lifecycle state,
- Learning State,
- next-review scheduling state.

Historical Review records must not be replaced by mutable counters alone if later progress analysis depends on them.

## 18. Remaining Domain Decisions

Scheduling names, interval progression, short-term relearning, hint influence, and lifecycle transitions are defined in `docs/08A_SCHEDULING_SEMANTICS.md`. Remaining domain work concerns future integration references. `Suspended` and `Mastered` remain explicit lifecycle states outside the assessment scale.
