# Illumination – Ubiquitous Language

## 1. Purpose

This document defines the accepted domain language.

No implementation name should silently become domain language merely because it appears in code.

## 2. Accepted Concepts

### Review

A single learning interaction in which the learner engages with one learning unit and a resulting learning assessment is recorded.

A review is historical. Once recorded, it describes what happened at that point in time.

### Reference Solution

The stored canonical answer, explanation, or example solution shown to the learner for comparison.

A reference solution does not imply that no alternative answer can be correct.

Every learning unit has a reference solution.

### Hint

Optional assistance that can be revealed before the full reference solution.

A learning unit may have no hints.

Hints are ordered `0..*` and may be revealed progressively.

### Learning State

The current domain state describing how the learning system should treat a learning unit based on prior reviews.

This includes the conceptually defined scheduling state needed to determine future review behavior, including difficulty, stabilityDays, dueAt, and short-term relearning state.

### Repetition / Review Scheduling

The domain process by which Illumination determines when a learning unit should next be eligible or due for review.

The initial deterministic scheduling semantics are defined in `docs/08A_SCHEDULING_SEMANTICS.md`.

### Due

A learning unit is due when its current scheduling state says it should now be reviewed.

The exact boundary between due, overdue, eligible, and manually selectable is not yet defined.

### Learning Progress

The evolution of learning state over time.

Progress is grounded in review history and current scheduling/learning state rather than being merely an arbitrary score.

### Learning Assessment

The evaluation of how well a learner handled a review.

Several degrees are desired so difficult material can return sooner than successfully recalled material.

The V1 rating scale is `Nochmal`, `Schwer`, `Unsicher`, `Gut`, `Leicht`.

### Low-Interaction Learning

A learning mode in which suitable items can be reviewed with minimal typing and very few actions.

This includes mental or verbal recall, simple selection, rapid hint reveal, solution reveal, and quick assessment.

Suitability is represented by the explicit `lowInteractionEligible` Learning Item property. Low-interaction filtering is deferred to v0.3.

### Structured Import

Import of versioned machine-readable learning content, initially expected to use JSON generated externally through ChatGPT.

Imported content becomes Illumination-owned after successful validation and import.

### External Learning Reference

A future explicit reference connecting Illumination content or coverage to a learning need owned by another bounded context such as Vocation.

The exact identity and semantics are not yet defined.

## 3. Canonical Content and Organization Terms

### Learning Item / Card

`Learning Item` is the canonical domain term for the smallest independently reviewable unit.

`Card` / `Karte` is the natural user-facing term.

A Learning Item:

- represents one question or mini-task,
- has exactly one Reference Solution,
- may have `0..*` hints,
- may offer direct answer choices or optional answer-choice assistance,
- has one authoritative Learning State,
- has one Review history,
- may belong to multiple Decks.

There is no separate universal `Exercise` entity. A small coding task is still a Learning Item.

### Deck

`Deck` is the canonical term for a user-defined grouping of Learning Items.

A Deck:

- may be thematic or mixed,
- is controlled by the learner,
- may contain arbitrary Learning Items,
- does not define Learning Item identity or Learning State,
- is not equivalent to a Vocation learning cluster.

## 4. Interaction Forms

Interaction forms describe how a Learning Item is answered during a Review. They are not separate top-level item entities.

Existing Learning Item response forms include:

- `SelfAssessed`,
- `Selection`,
- `ShortText`,
- `Code`.

The v0.3 scope is the end-to-end workflow for these forms, including interaction handling, assistance/reveal behavior, automatic evaluation, normalization/checking, and code-response UX.

Answer choices may be either:

- the direct required response form,
- optional assistance revealed after free recall.

## 5. Terms Explicitly Not Equivalent

### Learning Item != Deck

A learning item is individually reviewed.

A deck groups learning items.

### Deck != Vocation Learning Cluster

A deck is Illumination-owned user organization.

A Vocation learning cluster is Vocation-owned job-market/domain information.

There may later be references between them, but they are not the same object.

### Learning Assessment != Automatic Correctness

An automatically detectable correct answer may inform a learning assessment.

The final assessment may still represent difficulty, recall quality, or required assistance.

Automatic correctness may inform a suggested grade in v0.3, while the learner chooses the final assessment.

### Learning Progress != Analytics Dashboard

Learning progress is domain state and history.

Analytics are derived views over that information.

### Reference Solution != Learner Response

The reference solution is stored learning content.

A learner response is what happened during a review.

In v0.2, a Review may retain an optional submitted response payload as opaque historical content. Interpretation, automatic evaluation, normalization, and execution belong to v0.3.

### Independent Application != Every-Platform Native Client

Illumination must be usable independently of Wiiii Got This.

That does not require Illumination to implement a native client for every platform Wiiii Got This supports.

## 6. External Terms

### Vocation Learning Cluster / Learning Need

Owned by Vocation.

Illumination must not redefine or duplicate the Vocation concept as its own aggregate.

### Wiiii Got This Capability

Owned by Wiiii Got This integration semantics.

Illumination may publish capabilities, but Wiiii Got This owns device/platform discovery and presentation concerns.

## 7. Terms To Avoid Until Defined

Do not use the following as if they were settled domain concepts:

- level,
- XP,
- score,
- course,
- lesson,
- curriculum,
- module,
- chapter,
- exam,
- AI tutor,
- spaced-repetition algorithm name,
- difficulty number,
- proficiency percentage.

They may later become valid concepts, but none are currently defined.

## 8. Language Rule

When a concept is unresolved, specifications should say so explicitly rather than selecting a familiar term from Anki, LMS products, language-learning apps, or implementation frameworks.


## 9. Newly Decided Terms

### Suspended

Explicit lifecycle state in which a Learning Item remains stored but is excluded from normal repetition until reactivated.

Used for real blockers or material that should temporarily not participate in study.

### Mastered

Explicit lifecycle state in which the learner intentionally removes a Learning Item from normal repetition because it is considered permanently trivial or sufficiently internalized.

Mastered is reversible and is not merely the highest normal review grade.

### Automatic Correctness

A machine-determined judgment that a supplied response matches the expected answer where such checking is possible.

Automatic Correctness is not synonymous with Learning Assessment.

The learner may choose a workflow in which final assessment remains manual even when automatic checking is available.
