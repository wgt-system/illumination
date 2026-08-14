# Illumination – Read Models

## 1. Purpose

Read models provide views optimized for user decisions and integration without becoming owners of learning state.

They may be projections, queries, or computed views depending on the eventual architecture.

## 2. Deck List

Purpose:

- navigate user-defined Decks,
- understand immediate learning workload.

Candidate fields:

- Deck identity,
- name,
- Learning Item count,
- new count,
- due count,
- Suspended count if useful,
- Mastered count if useful,
- next due time where meaningful.

Counts are derived from authoritative item state.

## 3. Deck Detail

Purpose:

- inspect one Deck and its content.

Candidate fields:

- Deck identity,
- name,
- member Learning Items,
- each item's current high-level learning status,
- due/next-review information,
- lifecycle state,
- low-interaction suitability,
- optional import/source information.

Because one item may belong to several Decks, this view must not imply deck-local learning state.

## 4. Learning Item Detail

Purpose:

- inspect or maintain one Learning Item.

Candidate fields:

- identity,
- prompt/task,
- Reference Solution,
- hints,
- answer-choice configuration,
- interaction/evaluation capabilities,
- Deck memberships,
- lifecycle state,
- current Learning State,
- next-review information,
- recent Review history,
- import/provenance information where retained.

## 5. Study Queue Item

Purpose:

- present exactly the information needed for one review interaction.

Candidate fields:

- Learning Item identity,
- prompt/task,
- interaction capability description,
- direct answer choices where applicable,
- hidden hint availability/count,
- hidden assistance-choice availability,
- response constraints needed by the client.

Reference Solution and unrevealed assistance may be withheld from the initial projection or returned through explicit reveal operations depending on application architecture.

## 6. Review Result View

Purpose:

- confirm what happened after a review.

Candidate fields:

- Review identity,
- final five-grade assessment,
- automatic correctness if used,
- hints/assistance used where recorded,
- resulting high-level Learning State,
- next-review time,
- lifecycle state.

Exact scheduling internals need not be exposed to every client.

## 7. Learning Dashboard

Purpose:

- show where learning effort is required.

At minimum it should be possible to derive:

- total active Learning Items,
- new,
- due,
- unstable/difficult according to the selected model,
- long-term stable,
- Suspended,
- Mastered.

The definitions of unstable/stable follow the accepted scheduling semantics in `docs/08A_SCHEDULING_SEMANTICS.md`.

## 8. Review History

Purpose:

- inspect learning evolution and diagnose repeated difficulty.

Candidate fields:

- Review time,
- Learning Item identity/prompt summary,
- assessment grade,
- automatic correctness where present,
- assistance usage where retained,
- resulting interval/next-review information where meaningful.

## 9. Low-Interaction Study Availability

Purpose:

- show whether useful low-friction study material is currently available.

Candidate fields:

- number of due low-interaction-suitable items,
- number of new eligible items if new-item introduction is supported,
- Decks contributing eligible items.

Low-interaction suitability is represented by each Learning Item's explicit `lowInteractionEligible` property.

## 10. Suspended Items

Purpose:

- inspect blockers or intentionally paused material.

Candidate fields:

- item identity,
- prompt summary,
- suspension time/reason if reasons are later supported,
- previous scheduling state,
- Deck memberships.

A suspension reason is not currently required by product decisions.

## 11. Mastered Items

Purpose:

- inspect material explicitly removed from normal review as permanently trivial/internalized.

Candidate fields:

- item identity,
- prompt summary,
- mastered time,
- previous learning state,
- Deck memberships.

## 12. Import Report

Purpose:

- make structured content acquisition traceable.

Candidate fields:

- import identity when durable,
- contract version,
- time,
- status,
- created count,
- updated count,
- rejected count,
- validation issues,
- affected Decks.

Exact durability depends on final import design.

## 12A. Study Session History

Purpose:

- inspect lightweight historical learning sessions.

Fields include:

- session start/end,
- selected Decks/filters,
- mode,
- Review identities,
- derived review count/duration.

## 12B. Learning Insight (v0.6 foundation)

Learning Insight is a derived Application read capability over authoritative local
Learning Items, current Deck membership, Reviews, and Study Sessions. It is not a
separate analytics source of truth and performs no state mutation.

The overview exposes total, lifecycle, active new, active due-now, and active
short-term-relearning counts; total Reviews; Review counts in the last 7 and 30 days;
and the most recent Review time. Due/new/relearning predicates are factual and may
overlap, while Suspended and Mastered items remain visible but are excluded from normal
due/new/relearning work counts.

Deck insight aggregates current membership only. It includes lifecycle and workload
counts, Review totals/latest activity, and the five-grade distribution. A Learning Item
in multiple current Decks contributes to each Deck. No historical Deck membership is
reconstructed.

Learning Item insight and the typed DeckLearningContext expose current scheduling,
lifecycle, authored content identity, Review totals/latest activity, last confirmed
learner assessment, and five-grade distributions. Automatic correctness and suggested
assessments remain advisory metadata and never drive outcome statistics.

Review history is newest-first, bounded, and optionally filtered by current Deck
membership or Learning Item. Study Session history exposes only persisted start,
completion, selected Deck, resolved evaluation-mode, session-option, and Review-count
facts. Time-relative calculations use the Application TimeProvider.

## 13. Future External Learning Coverage

Purpose:

- provide a bounded summary to Vocation for an explicitly referenced learning need.

Published future fields are intentionally aggregated:

- external reference identity,
- associated Learning Item count,
- new count,
- due count,
- active/stable count,
- last content change,
- last learning activity.

The Vocation-facing view does not expose individual Reviews or Reference Solutions.

This read model is **not yet a contract**.

Its aggregation rules cannot be finalized until the External Learning Reference semantics are decided.

## 14. Future Wiiii Got This Capability Views

Wiiii Got This should receive purpose-specific published views rather than direct access to Illumination persistence or arbitrary internal models.

Examples may include:

- study-next-item capability,
- reveal-assistance capability,
- record-review capability,
- deck summary capability,
- progress summary capability.

Exact contracts belong to later integration design.

## 15. Read-Model Rule

No read model may become an alternate source of truth for:

- Review history,
- current Learning State,
- lifecycle state,
- Deck membership.

Read models are disposable/rebuildable in principle unless a later architecture explicitly chooses otherwise.
