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

The application layer is the boundary through which Wiiii Got This may host Illumination locally or consume published capabilities. Wiiii Got This must not use Illumination domain objects directly. The existing Avalonia presentation may remain an optional standalone/admin/dev host; a complete separate end-user UI is not required.

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
- configure the existing response form (`SelfAssessed`, `Selection`, `ShortText`, or `Code`); evaluation behavior is v0.5,
- suspend an item and Reactivate a Suspended item,
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
- answer mentally/verbal-only where supported (v0.5),
- enter a short response where supported (v0.5),
- select direct answer choices where supported (v0.5),
- request zero or more hints (v0.5),
- reveal optional answer-choice assistance (v0.5),
- reveal the Reference Solution (v0.5),
- receive automatic correctness feedback when enabled and available (v0.5),
- submit one of five final learning assessments,
- immediately continue to the next selected item.

### Progress

The user can inspect derived learning-state summaries without changing authoritative review state.

The user can inspect current content-quality state and filter Learning Items by User Flags without changing scheduling state.

### Import / Generate

The user can:

- describe a learning subject/purpose, desired item count, Deck target, and optional generation guidance,
- generate a copyable ChatGPT prompt,
- paste returned JSON or choose a JSON file,
- parse and validate a Content Bundle 1.0,
- preview valid and invalid operations, diagnostics, warnings, and dependencies,
- explicitly select valid operations,
- atomically commit the selected accepted subset,
- receive explicit import results and retained local provenance.

The practical first goal is a low-friction request for substantial content, such as 30–100 `SelfAssessed` Learning Items for one topic. No paid or built-in LLM API is required; the learner may copy/send the generated prompt to ChatGPT and paste or load the returned JSON.

## 4. Study Session Selection

A v0.2 session selector supports:

- one or more selected Decks,
- set-union membership across the selected Decks, without duplicate Learning Items,
- Active items only,
- due and new items within the selected scope,
- the default new-item limit of 20 with explicit override/all-new behavior.

Study Session priority is defined as short-term relearning, then already-due items, then new items.

The Study Session owns a temporary session learning stack rather than a second Learning State. A submitted grade may record a Review while leaving the item in that stack. `Nochmal` returns after one intervening card when possible, `Schwer` after five, and `Unsicher` at the end of the current learning stack. `Gut` and `Leicht` graduate the item from the stack into normal future scheduling. If the stack has no other item, `Nochmal`, `Schwer`, or `Unsicher` leaves the item as the next/current item until the learner completes the session.

The durable Learning State retains difficulty, retained stability, future normal due state, and whether reinforcement remains required across sessions. The exact position in the current stack is Study Session state; the Study Session does not create a second Learning State.

Low-interaction filtering is a v0.5 Study Session option. Broader future Study Session scopes remain possible.

The ordered assessment direction is already fixed:

```text
worse assessment → earlier return
better assessment → later return
```

The lowest assessment may cause very rapid relearning. The highest assessment may create a much longer interval. Neither automatically changes lifecycle state.

The canonical session invariant is `Nochmal <= Schwer <= Unsicher < Gut < Leicht`: the first three remain in the current session learning stack, while `Gut` and `Leicht` graduate into future scheduling. A worse assessment never produces a later return than a better assessment for comparable state.

## 4A. Assessment Preview and Session Transparency (v0.3)

The Application layer must expose a deterministic, side-effect-free assessment preview capability. For each of the five grades, the preview can show whether the item remains in the current session learning stack, its projected reinsertion behavior, and— for `Gut` or `Leicht`—the projected future dueAt or human-readable interval.

The preview uses the real scheduler semantics for controlled time. It does not mutate Learning State, create a Review, generate a Review identity, or duplicate scheduling formulas in the presentation. With unchanged state and time, submitting the previewed grade produces the corresponding actual scheduling and stack result.

The Application layer must also expose enough session transparency for a presentation to show the current item, remaining queue-entry count, a bounded upcoming queue preview where useful, repeated/relearning entries, and projected grade effects. This is an Application-owned read model and exposes neither Domain nor EF types. Debug-oriented queue detail is desirable for the standalone/admin/dev host, not a requirement for the future Wiiii Got This presentation.

## 5. Review Interaction State

For v0.2, Review completion records the final five-grade assessment and may retain an optional submitted response payload as opaque historical content. It does not interpret or automatically evaluate that payload.

Actual response interaction workflows are v0.5. They may need transient state such as:

- which hints have been revealed,
- whether answer choices were revealed as assistance,
- current learner response,
- automatic correctness result if calculated,
- whether Reference Solution is visible,
- whether final five-grade assessment has been submitted.

This transient UI/application state must not be mistaken for durable Learning State.

## 6. Manual and Assisted Evaluation (v0.5)

Illumination supports two broad evaluation policies:

### Manual final assessment

The learner always chooses the final five-grade assessment.

Automatic checking may be disabled entirely.

### Assisted evaluation

Where an item can be checked automatically, the application may present a correctness result and use it as an aid to the review flow.

In Assisted mode, Selection and ShortText produce `Schwer` for an incorrect response and `Gut` for a correct response. With the optional `ConsiderAssistance` session setting, a correct response after hint or assistance-choice reveal produces `Unsicher` instead. The learner may override any suggestion; the scheduler consumes only the confirmed final grade. SelfAssessed and Code fall back to manual assessment.

The application provides a global default evaluation mode and a per-session override.

Some item forms cannot be reliably checked automatically and therefore always require learner judgment.

## 7. Hint and Assistance Policy (v0.5)

Default:

- any available hint may be requested,
- hints are revealed at most once per current Learning Item appearance and in authored order,
- hint use does not automatically penalize the final assessment or scheduling.

Optional behavior:

- the learner may enable the per-session `ConsiderAssistance` setting.

Default hint influence is off globally.

`assistanceAnswerChoices` are optional help and remain distinct from Selection `directAnswerChoices`. They are revealed only explicitly. Revealing assistance does not change ResponseMode. Reference Solution reveal is explicit and is not itself treated as hint usage.

When `ConsiderAssistance` is enabled and a correct Assisted result used at least one hint or assistance answer choice, the suggested assessment is `Unsicher` instead of `Gut`. Incorrect still suggests `Schwer`. This changes only the suggestion and never constrains or rewrites the learner's final grade.

## 7A. Low-Interaction Eligibility

Each Learning Item has an explicit `lowInteractionEligible` property.

It may be set manually, through structured import, or from an application-proposed default based on the item's interaction form.

The persisted property is explicit; it is not permanently inferred from item type.

Low-interaction / bed mode only filters the Study Session pool. It has no separate scheduling state or learning progress.
The explicit persisted property already exists; v0.5 adds optional Study Session filtering by that property.

## 8. Lifecycle Actions

### Suspend

The user can suspend an item independently from review grading.

Normal study selection excludes suspended items.

### Reactivate

A suspended item can be returned to normal review.

Reactivating the Suspended item preserves Review history and retained scheduling state, and makes it immediately due.

### Mastered

The user can explicitly mark an item Mastered.

Normal study selection excludes Mastered items.

### Unmaster

The user can return a Mastered item to active learning.

Unmarking Mastered preserves Review history and retained scheduling state, and makes the item immediately due.

## 8A. Standalone Host Direction

The broad standalone structure remains Decks, Learning Items, and Study. Future refinement should remove implementation/developer wording such as “Standalone admin/dev host for v0.2 workflows”, use denser desktop-application-like navigation with suitable icons, reduce dashboard-like whitespace, make the Study card the visual focus, show grade outcome previews near the five grade controls, and show useful remaining-session information with an optional compact queue preview.

This is product/presentation direction only, not a polished independent end-user UI requirement. Wiiii Got This remains the intended primary future end-user presentation.

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

The exact UI presentation remains open; the Content Bundle 1.0 import contract and stored operation schema are defined by `schemas/illumination-content-bundle-1.0.schema.json`.

## 11. Prompt Generation

Prompt generation is an Application-owned capability. It constructs an instruction that tells ChatGPT:

- the requested learning subject/purpose,
- the requested item count,
- the requested new Deck name or selected existing Deck,
- optional free-form generation guidance,
- low-interaction suitability requirements where relevant,
- return JSON only using the exact `illumination.content-bundle` contract and version `1.0`,
- create one independent question/mini-task per Learning Item,
- provide a Reference Solution for every item,
- use `localRef` consistently for bundle-created objects,
- create and assign the requested Deck when applicable, or use the existing Deck's stable ID,
- use only the supported operations and response-mode representation,
- respect the requested count and prefer concise, repeatable items over essay questions.

The prompt generator does not call a paid LLM API as a required initial workflow.

The prompt generator derives its contract guidance from `schemas/illumination-content-bundle-1.0.schema.json`; it must not maintain a contradictory second schema.

For existing-content updates, the prompt generator should emit only the smallest useful existing-item snapshot. It may generate an auxiliary JSON/text file or split work into batches when embedding the selected scope directly would be unnecessarily large.

When generating for an existing Deck, the prompt uses that Deck's stable Illumination ID and does not ask ChatGPT to recreate the Deck. When generating a new Deck, the prompt uses a Deck `localRef` and assignments may target that local reference.

Prompt generation may later accept `Standard`, `Strict`, or `SourceGrounded` quality mode. These modes affect prompting only; they do not certify content or create a Quality Review.

## 11A. Malformed-JSON Repair Prompt

When supplied JSON is malformed, the Application retains the parser diagnostic for display and can generate a repair prompt. The repair prompt includes:

- an instruction to repair rather than redesign the content,
- the required Content Bundle 1.0 contract and version,
- parser and validation diagnostics,
- the supplied invalid JSON,
- an instruction to return corrected JSON only.

Generating a repair prompt never mutates authoritative content and does not require an LLM API call.

## 12. Import Workflow

Conceptually:

```text
Describe learning scope
   ↓
Generate ChatGPT prompt
   ↓
Copy/send prompt to ChatGPT
   ↓
Receive Content Bundle 1.0 JSON
   ↓
Paste JSON or choose JSON file
   ↓
Parse and validate envelope
   ↓
Validate each operation structurally
   ↓
Validate semantics and dependencies
   ↓
Preview valid/invalid operations and warnings
   ↓
User explicitly accepts valid operations
   ↓
Atomic commit of accepted subset
   ↓
Imported Decks/Learning Items become available
```

The canonical supported operations are `create_learning_item`, `update_learning_item`, `create_deck`, `update_deck`, and `assign_item_to_decks`. The Content Bundle 1.0 schema remains authoritative for their envelope and payloads. Delete, Suspend, Mastered, fuzzy auto-merge, and arbitrary executable operations are not supported.

### Validation layers

Validation has three distinct stages:

1. **Parse/envelope validation.** Malformed JSON, a root that is not an object, missing or invalid `contract`, unsupported `version`, or missing/non-array `operations` are bundle-level failures. Nothing can be committed; the Application returns an explicit bundle-level error. Malformed JSON may produce a repair prompt.
2. **Operation structural validation.** Each operation is validated independently against its Content Bundle 1.0 operation shape. A malformed operation must not make structurally valid sibling operations disappear from the preview.
3. **Semantic/dependency validation.** The Application validates duplicate `localRef` values, stable-ID syntax, existing targets, localRef references, content/domain invariants, response-mode requirements, and target identity/type. Diagnostics identify the bundle, operation, and field/reference where useful. EF exceptions are not user-facing validation.

No invalid operation silently enters authoritative learning content.

### Mixed validity and atomicity

Mixed validity is partial at preview level and atomic at accepted-subset level:

```text
mixed bundle
   ↓
preview
   ↓
invalid operations excluded
   ↓
user selects valid operations
   ↓
selected subset is one atomic commit
```

Invalid operations are never committed. Valid operations may be accepted independently. Before commit, dependencies are validated again for the selected subset. If any accepted operation fails during commit, the entire selected subset rolls back.

### LocalRef dependencies

An assignment is valid only when each referenced item or Deck either already exists through its stable Illumination ID or is a valid create operation in the accepted subset. If a referenced create operation is invalid or not selected, the dependent assignment is not committable and reports the dependency explicitly. A valid item create may still be accepted without that assignment; the accepted subset is not silently broadened.

### Create and update semantics

`create_learning_item` creates one authoritative Learning Item with a new Illumination-owned stable ID, normal new Learning State, and no invented Review history. `create_deck` creates one Deck with a new Illumination-owned stable ID. A bundle `localRef` is import-local and never becomes the permanent identity.

`assign_item_to_decks` adds explicit membership without duplicating Learning State or resetting scheduling/history.

For `update_learning_item`, only an explicit stable `itemId` authorizes mutation. New items start at ContentRevision `1`. A successful logical update increments ContentRevision exactly once if Prompt, Reference Solution, Hints, response mode, answer choices, or accepted short answers actually change; a no-op update does not. Scheduling state, lifecycle, memberships, User Flags, and `lowInteractionEligible` do not themselves increment it. A `minor` update changes content while preserving Review history, current Learning State, lifecycle, and memberships. A `semantic` update changes content while preserving immutable Review history, lifecycle, and memberships, then resets current scheduling to new defaults (`IsNew = true`, difficulty `5.0`, stability `0.5`, reinforcement not required) and makes the item immediately due at update time. Either significance may increment ContentRevision when quality-relevant content changes. Historical Quality Reviews describe the previous revision and are not deleted.

`update_deck` may rename or update supported Deck metadata; it does not change membership unless explicit assignment operations do so.

### Duplicate warnings

Duplicate detection is advisory and deterministic. A normalized exact prompt match against an existing Learning Item, or equivalent duplicate create operations detectable within the bundle, may produce a warning. Warnings do not invalidate an operation, auto-merge, or silently convert create into update. Semantic or embedding/LLM similarity never authorizes mutation.

## 12A. Content Acquisition Presentation Direction

The standalone host should add a compact Content Acquisition surface without becoming a generic JSON IDE or embedded ChatGPT client.

The Generate area conceptually supports:

- topic or learning purpose,
- requested item count,
- new Deck name or existing Deck selection,
- optional instructions,
- Generate Prompt,
- copyable prompt text.

The Import area conceptually supports:

- pasted JSON or JSON file selection,
- Validate,
- operation preview with errors, warnings, dependencies, and selectable valid operations,
- Import Selected,
- explicit result summary.

For malformed JSON, the surface shows the parser error and offers Generate Repair Prompt. It does not add an embedded ChatGPT browser/API integration.

Presentation does not parse business semantics, validate Domain rules, open DbContexts, resolve localRefs, perform import transactions, or calculate semantic-update resets. Those responsibilities remain in the Application and Infrastructure boundaries.

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

Import preview/read models also expose operation index/type, localRef or target stable ID, summary, validity, diagnostics, warnings, dependencies, and whether an operation is selectable. Preview is side-effect free: it creates no Learning Items, Decks, memberships, Reviews, scheduling changes, or successful import-history entry.

After an accepted-subset commit, the result identifies created/updated item and Deck IDs, applied memberships, skipped/rejected operation indices, diagnostics where applicable, and an import/batch identity when import history is retained. There is no silent partial commit.

Each successful accepted-subset commit retains lightweight local provenance: an Illumination import/batch ID, import timestamp, contract/version, optional external `bundleId` and `generatedFor`, and accepted-operation counts/results. Imported content does not depend on that record for normal use, and the history does not duplicate all Learning Items.

## 13A. Content Quality and Curation Capabilities

Application capabilities should support:

- quickly adding/removing user-owned flags during Study,
- filtering Learning Items by flags,
- generating a quality-review prompt for an item or generated bundle,
- receiving structured Quality Review results,
- previewing findings, reasons, outcome, evidence type, and suggested corrections,
- explicitly accepting a Quality Review result,
- optionally applying a suggested correction through normal content-update semantics.

Quality Review results are bound to the Learning Item content revision they reviewed. An accepted result may explicitly supersede older reviews for the same item and revision; supersession is never inferred from Review Type, and superseded reviews remain historical. A content edit that changes quality-relevant content creates exactly one new revision; a no-op or non-content update does not. Current quality state is derived only from non-superseded reviews for the current revision, with precedence `NeedsReview` → `Warning` → `Pass` → no assurance. `NeedsReview` is visible and actionable but is not an automatic import block. Application contracts expose no Domain or EF types.

The two supported external review exchanges use different identity anchors.  For already
persisted Learning Items, the generated prompt carries the stable Learning Item ID and
current `ContentRevision`; returned results are accepted only when both still match.  For
pre-import creates, the prompt carries the operation `localRef` and a content fingerprint;
the result must preserve both so that a changed bundle cannot receive an old review.  Both
flows preview results before persistence, keep invalid/stale/provenance-mismatched results
visible but non-accepting, and require explicit selection.  Warning and NeedsReview are
importable evidence states, not automatic blocks.  A suggested correction remains advisory
until separately applied through normal content-update semantics.

Quality Review is evidence, not factual verification: Illumination does not expose a generic
`Verified` state or infer trust from evidence type.  User Flags are user-defined annotations;
creating, assigning, removing, or filtering flags is independent of ContentRevision,
scheduling/Learning State, and CurrentQualityState.  Multiple flags may coexist, and review
history remains immutable when flags change.

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

- persistence/synchronization behavior,
- authentication/user model if server-backed operation is chosen.

## Automatic Evaluation (v0.5)

Illumination supports:

- `Manual`: no automatic correctness is required; final assessment is fully manual.
- `Assisted`: machine-checkable answers may be evaluated and a grade suggested.

v0.5 default suggestions:

- incorrect → `Schwer`,
- correct → `Gut`.

The learner may override the suggestion.

Configuration:

- one global default,
- one per-Study-Session override.

v0.5 machine-checkable behavior:

- direct selection answers: exact option correctness,
- short text answers: normalized text comparison against explicitly stored accepted answers,
- code answers: capture and show alongside the Reference Solution; no execution/compilation or automatic checking without a future explicit checker,
- no fuzzy, semantic, or LLM-based correctness guessing.

## Low-Interaction Mode

Every Learning Item has explicit `lowInteractionEligible`.

It may be:

- set manually,
- supplied by structured import,
- initially suggested by the application from interaction form.

The persisted value is explicit.

Low-interaction / bed mode only filters the Study Session pool. It has no separate Learning State, scheduler, or Review history.
