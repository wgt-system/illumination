# Illumination – Application Design

## 1. Purpose

This document translates the domain scenarios into application capabilities without selecting programming language, UI framework, persistence technology, deployment topology, or transport protocol.

## 2. Application Boundary

The Illumination application layer coordinates:

- content authoring and editing,
- deck organization,
- study-session selection,
- review interaction,
- learning-state evolution,
- prompt generation,
- structured import,
- progress/read-model queries,
- future published integration contracts.

It does not own the scheduling rules themselves; those belong to the domain model.

## 3. Primary Application Capabilities

### Manage Learning Content

The user can:

- create a Learning Item,
- edit prompt/task content,
- edit the Reference Solution,
- add/remove/reorder hints,
- configure direct answer choices where appropriate,
- configure answer choices that may be revealed as assistance,
- configure supported response/evaluation behavior,
- suspend/reactivate an item,
- mark/unmark an item as Mastered.

### Manage Decks

The user can:

- create a Deck,
- rename it,
- add a Learning Item to one or more Decks,
- remove a Learning Item from a Deck without deleting it,
- inspect Deck membership,
- select Decks as study scopes.

### Study

The user can:

- begin a study session for an allowed scope,
- receive one Learning Item at a time,
- answer mentally/verbal-only where supported,
- enter a short response where supported,
- select direct answer choices where supported,
- request zero or more hints,
- reveal optional answer-choice assistance,
- reveal the Reference Solution,
- receive automatic correctness feedback when enabled and available,
- submit one of five final learning assessments,
- immediately continue to the next selected item.

### Progress

The user can inspect derived learning-state summaries without changing authoritative review state.

### Import / Generate

The user can:

- choose a desired content-generation scope,
- generate an external ChatGPT prompt,
- receive structured JSON externally,
- import the JSON,
- receive validation errors without partial silent corruption,
- add new learning content without resetting existing progress.

## 4. Study Session Selection

A session selector must eventually support at least:

- one selected Deck,
- due items within the selected scope,
- low-interaction filtering where requested,
- exclusion of Suspended items,
- exclusion of Mastered items from normal repetition.

Study Session priority is defined as short-term relearning, then already-due items, then new items.

The ordered assessment direction is already fixed:

```text
worse assessment → earlier return
better assessment → later return
```

The lowest assessment may cause very rapid relearning. The highest assessment may create a much longer interval. Neither automatically changes lifecycle state.

## 5. Review Interaction State

For one presented Learning Item, the application may need transient interaction state such as:

- which hints have been revealed,
- whether answer choices were revealed as assistance,
- current learner response,
- automatic correctness result if calculated,
- whether Reference Solution is visible,
- whether final five-grade assessment has been submitted.

This transient UI/application state must not be mistaken for durable Learning State.

## 6. Manual and Automatic Evaluation

Illumination supports two broad evaluation policies:

### Manual final assessment

The learner always chooses the final five-grade assessment.

Automatic checking may be disabled entirely.

### Automatic assistance

Where an item can be checked automatically, the application may present a correctness result and use it as an aid to the review flow.

In Assisted mode, the V1 default suggestion is `Schwer` for an incorrect machine-checkable response and `Gut` for a correct one. The learner may override the suggestion.

The application provides a global default evaluation mode and a per-session override.

Some item forms cannot be reliably checked automatically and therefore always require learner judgment.

## 7. Hint Policy

Default:

- any available hint may be requested,
- multiple hints may be revealed progressively,
- hint use does not automatically penalize the final assessment or scheduling.

Optional behavior:

- the user may enable a policy where hint usage influences evaluation/scheduling.

Default hint influence is off globally.

The learner may override hint influence when starting a Study Session.

When enabled and at least one hint is used, the automatically suggested assessment is lowered by at most one grade. Hint use never forces the learner's final grade.

## 7A. Low-Interaction Eligibility

Each Learning Item has an explicit `lowInteractionEligible` property.

It may be set manually, through structured import, or from an application-proposed default based on the item's interaction form.

The persisted property is explicit; it is not permanently inferred from item type.

Low-interaction / bed mode only filters the Study Session pool. It has no separate scheduling state or learning progress.

## 8. Lifecycle Actions

### Suspend

The user can suspend an item independently from review grading.

Normal study selection excludes suspended items.

### Reactivate

A suspended item can be returned to normal review.

Reactivating the item preserves Review history and makes it immediately due.

### Mastered

The user can explicitly mark an item Mastered.

Normal study selection excludes Mastered items.

### Unmaster

The user can return a Mastered item to active learning.

Unmarking Mastered preserves Review history and makes the item immediately due.

## 9. Deck Membership

Because one Learning Item may belong to multiple Decks:

- deleting/removing one membership must not delete the Learning Item if it remains otherwise valid,
- a Review updates the single authoritative Learning State of the item,
- the same item must not accumulate separate progress merely because it appears in several Decks.

### Delete Deck

Deleting a Deck removes only the Deck and memberships. Learning Items are preserved.

### Delete Learning Item

Explicit Learning Item deletion requires confirmation and permanently removes that item's associated learning state/history.

## 10. Content Authoring Model

The application should not require the user to think in a rigid technical item hierarchy.

A practical authoring flow may expose:

- question/task,
- reference solution,
- optional hints,
- optional answer choices,
- response behavior,
- low-interaction suitability,
- deck placement.

The exact UI and stored schema remain open.

## 11. Prompt Generation

Prompt generation should construct an instruction that tells ChatGPT:

- the requested learning subject/purpose,
- desired amount of content,
- desired interaction characteristics,
- low-interaction suitability requirements where relevant,
- the exact supported versioned JSON contract,
- explicit stable identifiers for existing Learning Items when an update/extension operation is requested,
- constraints such as one independent question/mini-task per Learning Item,
- requirement for a Reference Solution,
- optional hints and answer choices.

The prompt generator does not call a paid LLM API as a required initial workflow.

For existing-content updates, the prompt generator should emit only the smallest useful existing-item snapshot. It may generate an auxiliary JSON/text file or split work into batches when embedding the selected scope directly would be unnecessarily large.

## 12. Import Workflow

Conceptually:

```text
Receive JSON
   ↓
Parse contract version
   ↓
Structural validation
   ↓
Semantic validation
   ↓
Resolve import identity/update policy
   ↓
Preview valid and invalid entries
   ↓
User explicitly accepts the valid subset
   ↓
Commit accepted changes
   ↓
Return import result
```

No invalid item should silently enter authoritative learning content.

Atomicity policy for mixed-validity imports remains to be finalized with the import contract.

## 13. Queries / Read Use Cases

Application queries should support at least:

- list Decks,
- show Deck detail,
- show Learning Item detail,
- show due counts,
- show new/unstable/stable counts,
- show next review information,
- show Suspended items,
- show Mastered items,
- show review history,
- show content/import provenance where retained.

## 14. Future Vocation Integration

The application layer may later expose a bounded operation such as:

- associate Illumination content with an external learning reference,
- obtain a coverage summary for an external learning reference.

No such command/query should be implemented until the reference identity and coverage semantics are specified.

## 15. Future Wiiii Got This Integration

Illumination may later expose application capabilities through versioned contracts suitable for Wiiii Got This.

The application contract must expose learning semantics without leaking:

- persistence schema,
- internal classes,
- UI framework types,
- implementation language types.

Wiiii Got This may present these capabilities on platforms not directly targeted by Illumination itself.

## 16. Error Handling Principles

Application operations must distinguish at least conceptually between:

- invalid user input,
- invalid imported content,
- unavailable/deactivated content,
- stale update identity,
- unsupported contract version,
- unavailable optional automation/evaluation,
- infrastructure failure.

Domain-invalid operations must not be converted into partially valid state.

## 17. Still Open

Application design cannot yet finalize:

- exact import create/update envelope (stable update identity itself is decided),
- contract schema version 1.0 details,
- persistence/synchronization behavior,
- authentication/user model if server-backed operation is chosen.

## Automatic Evaluation

Illumination supports:

- `Manual`: no automatic correctness is required; final assessment is fully manual.
- `Assisted`: machine-checkable answers may be evaluated and a grade suggested.

V1 default suggestions:

- incorrect → `Schwer`,
- correct → `Gut`.

The learner may override the suggestion.

Configuration:

- one global default,
- one per-Study-Session override.

V1 machine-checkable behavior:

- direct selection answers: exact option correctness,
- short text answers: normalized text comparison against explicitly stored accepted answers,
- code answers: no execution/compilation in V1; compare against Reference Solution and self-assess,
- no semantic LLM-based correctness guessing in V1.

## Low-Interaction Mode

Every Learning Item has explicit `lowInteractionEligible`.

It may be:

- set manually,
- supplied by structured import,
- initially suggested by the application from interaction form.

The persisted value is explicit.

Low-interaction / bed mode only filters the Study Session pool. It has no separate Learning State, scheduler, or Review history.
