# Illumination – Domain Model

## Status

Substantially specified for the first product phase.

The exact five-grade review semantics and repetition algorithm remain deliberately open and are isolated as later domain-design decisions.

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

### V1 response forms

A Learning Item may support one of the following V1 interaction forms:

- no recorded input with mental/verbal recall and self-assessment,
- selection response,
- very short text response,
- small code response.

These are interaction forms, not separate aggregate types.

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
- learner response when stored,
- automatically detected correctness when available,
- whether the reference solution was revealed.

Not all raw learner responses are necessarily required to be retained permanently.

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

Extreme failure. The item should return extremely quickly and, where possible, after roughly three intervening cards in the same Study Session.

### Schwer

Clear failure or very weak recall. The item should return soon and, where possible, after roughly ten intervening cards in the same Study Session.

### Unsicher

Partial or uncertain success. No mandatory same-session repeat is required; the next interval remains comparatively short.

### Gut

Solid recall. The interval grows meaningfully.

### Leicht

Immediate, confident recall. The interval grows substantially further than for `Gut`.

### Separation from lifecycle

`Suspended` and `Mastered` are explicit lifecycle states outside this five-grade scale.

Neither is reached automatically through assessment grades.

## 8. Automatic Evaluation Policy

Automatic answer evaluation is optional.

Illumination supports two evaluation modes:

### Manual

No automatic correctness judgment is required. The learner always chooses the final five-grade Learning Assessment.

### Assisted

When the response is machine-checkable, Illumination may determine correctness and suggest a grade.

Default V1 suggestion:

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
- moving an item between decks does not reset Learning State,
- analytics do not mutate Learning State independently.

The exact scheduling fields remain open until the repetition model is designed.

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

### Reactivation

Reactivating a Suspended or Mastered item preserves its Review history and makes it immediately due. The following Review then determines its continued scheduling state.

### Distinction

Suspended and Mastered are explicit lifecycle decisions, not ordinary review grades.

- `Suspended` means normal repetition is intentionally paused, for example because the item is currently a blocker or unsuitable.
- `Mastered` means the learner explicitly considers normal repetition unnecessary.
- a very poor review remains a review assessment and should normally cause earlier repetition rather than suspension.
- a very strong review remains a review assessment and should normally cause a much longer interval rather than mastering the item.

## 11. Aggregate Root: Deck

`Deck` is the canonical domain term for an Anki-like user-defined grouping.

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

When the learner enters a text or code response, that response is stored as part of the Review. Mental/verbal-only responses naturally have no stored response content.

## 12. Study Session

A Study Session selects reviewable Learning Items within a chosen scope and records Reviews.

### Scope

A session may select:

- one Deck,
- several Decks,
- all due material,
- low-interaction-suitable material,
- another explicitly supported filter.

When several Decks are selected, their Learning Items form a set union: an item belonging to several selected Decks appears only once in the session queue.

### Queue priority

Normal priority is:

1. short-term relearning items,
2. already-due items,
3. new items.

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

The Study Session does not own a separate copy of Learning State.

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

Intentional updates reference explicit stable Illumination Learning Item identifiers. Semantic/fuzzy similarity must not silently authorize mutation. The exact import operation envelope remains a contract-design concern.

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
- ReactivateLearningItem
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

The remaining scheduling design work is now narrower:

- final names for the five ordered assessment grades,
- exact interval progression,
- exact same-session/short-term relearning behavior for the lowest grades,
- interaction with automatic correctness,
- optional configured hint influence,
- transition behavior after previously difficult items become successful,
- reactivation behavior after `Suspended` or `Mastered`.

The ordering itself is decided: worse normal assessments return earlier, better normal assessments return later. `Suspended` and `Mastered` remain explicit lifecycle states outside the assessment scale.
