# Illumination – Acceptance Tests

## Status

Behavioral acceptance baseline through **v0.9.0**.

The exact deterministic scheduling formulas and queue semantics are defined in `docs/08A_SCHEDULING_SEMANTICS.md`. This document defines product-level acceptance behavior. Automated tests remain the executable evidence; manual real-use checks are required where workflow clarity or external ChatGPT output cannot be established by deterministic tests alone.

The current stable capability line includes:

- Learning Items, Decks and many-to-many membership;
- five-grade Study and deterministic scheduling/relearning;
- Content Bundle 1.0 generation/validation/import;
- Content Quality & Curation;
- response interaction/evaluation modes;
- Learning Insight and history projections;
- local backup/migration safety;
- v0.9 Deck coherence, Practice/Restart semantics, learning-aware generation and usability hardening.

The current standalone Desktop host remains an admin/dev/acceptance-capable surface. Its information density is **not** acceptance of the final production Product Surface UX.

## 1. Learning Item Creation and Editing

### Basic creation

Given no existing item
When the learner creates an independent prompt with a non-empty Reference Solution
Then exactly one Learning Item is stored
And it starts with new Learning State
And zero or more ordered hints may be retained.

### Required solution

Given a new Learning Item
When the Reference Solution is empty
Then creation is rejected.

### Authoring and Deck assignment

Given one or more existing Decks
When a Learning Item is created or edited through the normal authoring workflow
Then explicit Deck assignment can be managed without creating duplicate Learning Item identity or duplicate Learning State.

## 2. Deck Membership and Lifecycle

### One item in several Decks

Given one Learning Item belongs to several Decks
Then every Deck references the same Learning Item identity
And no duplicate Learning State or Review history is created.

### Membership removal preserves learning

Given one item belongs to several Decks
When membership in one Deck is removed
Then Review history and Learning State remain unchanged
And other memberships remain.

### Deck deletion coherence

Given a Deck contains existing Learning Items
When the Deck is deleted
Then its projection disappears consistently from Deck, Library, Study, Generate and Insight reads
And its Learning Items, Review history and Learning State remain unless those Learning Items are explicitly deleted.

### Explicit Learning Item deletion

Given an existing Learning Item
When permanent item deletion is explicitly confirmed
Then the item and its associated Learning State/Review history are removed according to the deletion policy
And no ghost Deck projection remains.

## 3. Study and Review

### Self-assessed review

Given an Active due Learning Item
When the learner reveals the Reference Solution and submits one of the five Learning Assessment grades
Then one immutable Review is recorded
And Learning State is updated according to the scheduling model
And a new item becomes `IsNew = false`.

### Five-grade order

For otherwise comparable state, the accepted direction is:

```text
Nochmal <= Schwer <= Unsicher < Gut < Leicht
```

`Nochmal`, `Schwer`, and `Unsicher` remain in the current learning stack. `Gut` and `Leicht` graduate to future normal scheduling.

### Session reinsertion

Given enough other cards remain:

- `Nochmal` returns after one intervening queue entry;
- `Schwer` returns after five intervening queue entries;
- `Unsicher` returns at the end of the current learning stack.

When too few cards remain, `Nochmal`/`Schwer` append to the current learning stack. In a single-card learning stack, `Nochmal`, `Schwer`, or `Unsicher` keep that item current/next rather than making it disappear.

### Session completion with unfinished relearning

Given reinforcement-required items remain
When the learner explicitly completes the Study Session
Then the session may end
And unfinished items remain immediately due/relearning-required
And no successful future interval is invented.

### Assessment preview

Given unchanged Learning State, Study Session state and controlled time
When grade previews are requested
Then no Review or mutation is created.

When the same grade is subsequently submitted without state/time changes, the real queue/scheduling result matches the preview.

### Immutable Review history

Given the same Learning Item receives several grades in one Study Session
Then every submitted grade remains a separate immutable Review using its real completion time.

## 4. Scheduling and Lifecycle

### Successful repetition

Given repeated successful Reviews
Then future normal intervals generally grow according to the accepted scheduling model.

### Lapse after long stability

Given a long-stable Learning Item:

- `Nochmal` strongly reduces retained stability and caps it at the accepted rapid-relearning bound;
- `Schwer` reduces retained stability and caps it at the accepted harder-relearning bound;
- later `Gut`/`Leicht` growth starts from the reduced state rather than restoring the old interval automatically.

### Suspend / Reactivate

Suspending excludes an item from normal Study while preserving history/state. Reactivating preserves history/state and makes the item immediately due.

### Mastered / Unmark Mastered

Mastered excludes an item from normal Study while preserving history/state. Unmarking Mastered preserves history/state and makes the item immediately due.

## 5. Practice Now versus Restart Learning (v0.9)

### Practice now is non-destructive

Given an Active Learning Item or Deck whose cards are not normally due
When the learner chooses **Practice now**
Then material may be practiced immediately
And merely starting/using Practice now does not rewrite authoritative Review history or current Learning State to make the item due.

### Restart one Learning Item

Given an Active future-scheduled Learning Item with existing Review history
When the learner explicitly confirms **Restart learning**
Then current scheduling state resets to the accepted new-learning baseline
And the item is immediately eligible for a fresh normal Study Session
And immutable Review history remains visible as historical evidence.

### Restart a Deck

Given a Deck with several Active Learning Items
When the learner confirms Deck-level Restart learning
Then eligible current scheduling state is reset deliberately
And historical Reviews and Deck membership remain
And a fresh normal Study Session still obeys the configured new-item limit.

The UI must explain that a new-item limit may prevent every restarted card from appearing in the first session; this is not a failed reset.

## 6. Study Queue and New-Item Limit

Given a normal Study scope containing relearning, due and new material
Then relearning has priority over ordinary due material
And ordinary due material has priority over new material.

Multiple selected Decks use set-union semantics: an item present in several selected Decks appears only once.

The default new-item limit is 20 unless explicitly overridden; the learner may explicitly request all new items.

## 7. Response Modes and Assisted Evaluation

Supported v0.9 interaction modes include `SelfAssessed`, `Selection`, `ShortText`, and `Code`.

### Selection

When Assisted evaluation is enabled, exact correctness is true only when the selected choice-ID set exactly equals the authored correct-choice-ID set. Choice order is irrelevant.

### ShortText

When compared with `acceptedShortAnswers`, comparison:

- trims surrounding whitespace;
- uses Unicode normalization Form C;
- uses ordinal case-insensitive equality;
- does not alter punctuation or internal whitespace;
- does not use fuzzy/semantic matching.

### Code

A Code response may be captured and shown beside the Reference Solution, but v0.9 does not execute or compile arbitrary learner code. Assisted evaluation therefore falls back to learner assessment unless a separately accepted evaluator exists.

### SelfAssessed

No machine-checkable response is required. The learner controls the final five-grade assessment.

### Hints and assistance

Hints may be revealed progressively. By default hint use does not automatically penalize the final assessment. Where `ConsiderAssistance` is explicitly enabled, assistance may conservatively lower the *suggested* grade without taking the final grade away from the learner.

Correctness/suggested assessment remain distinct from the learner-confirmed final grade.

## 8. Low-Interaction Study

Given a Study scope with mixed `lowInteractionEligible` values
When low-interaction filtering is requested
Then only otherwise-eligible items with `lowInteractionEligible = true` participate.

This filter does not create separate Learning State, transform ResponseMode, or invent Review history.

## 9. Content Bundle 1.0 Validation and Import

### Contract boundary

External generated JSON is untrusted until it passes Content Bundle 1.0 parsing, structural validation, semantic/dependency validation and explicit import selection.

Unsupported versions or malformed envelopes are rejected explicitly.

### Mixed-validity preview

Given valid and invalid operations in one bundle
Then preview shows both classes clearly
And invalid operations are not selectable
And valid operations may be selected independently when dependencies remain satisfied.

### Accepted-subset atomicity

When a selected subset is committed
Then dependencies are revalidated
And the selected operations commit atomically in one transaction.

On infrastructure failure, no partial accepted-subset mutation remains.

### Identity and no fuzzy mutation

Create operations receive Illumination-owned stable IDs. `localRef` is bundle-local coordination, not durable identity.

Textual/semantic similarity alone must never silently turn a create into an update. Existing content changes only through explicit supported update identity/semantics.

### Minor versus semantic update

A `minor` update preserves current Learning State, lifecycle, memberships and Review history.

A `semantic` update preserves immutable Review history and lifecycle/membership, while resetting current scheduling to the accepted new-learning baseline and making the item immediately due.

### Import result/provenance

Successful import returns explicit created/updated IDs, memberships, skipped/rejected operation information and retained lightweight provenance where applicable.

After a successful selected-subset import, a second commit requires a new current preview; repeated clicking cannot accidentally duplicate the same accepted batch.

### Malformed JSON repair

Malformed JSON imports nothing. Illumination may create a repair prompt that includes the parser diagnostic and requires repaired Content Bundle 1.0 JSON rather than redesigning the requested content.

## 10. Content Quality and Curation

Quality state is evidence, not factual certainty and not scheduling state.

- User Flags are user-owned and do not alter scheduling or content revision.
- Quality Reviews are immutable and bound to the reviewed content revision.
- Editing quality-relevant content increments `ContentRevision` once per logical update and invalidates older current-revision assurance without deleting history.
- Explicit supersession keeps superseded Quality Reviews as immutable history.
- Current quality precedence is `NeedsReview` > `Warning` > `Pass` > no assurance.
- `Warning` is non-blocking.
- `NeedsReview` is highly visible but remains subject to explicit user acceptance where the workflow permits.
- Suggested corrections never mutate authoritative content without an explicit normal update action.

## 11. Learning Insight

Given authoritative Learning Items, memberships, Reviews, Study Sessions and Learning State
When Insight views are opened/refreshed
Then projections are derived from current authoritative data
And distinguish at least new, due/relearning, Suspended and Mastered material where applicable.

Insight projections must not become a second source of truth and must not invent opaque mastery values.

## 12. Learning-Aware Existing-Deck Generation (v0.9)

### Fresh evidence

Given an existing Deck with Review/Learning State history
When the learner opens the normal Existing Deck generation path
Then generation context is derived from current evidence rather than a stale cached snapshot.

### Deterministic generation profile

The Application layer derives a compact deterministic Learning Generation Profile/Brief from Illumination-owned evidence. It distinguishes:

- reviewed versus unreviewed/new material;
- due/relearning/weak evidence;
- comparatively stable/established evidence;
- aggregate confirmed assessment evidence;
- existing-content inventory needed for duplicate avoidance.

It does not invent an opaque universal mastery score and does not delegate authoritative learning-state interpretation to ChatGPT.

### Progression intent

Existing-Deck generation differentiates the accepted intents:

- reinforce/practice weak areas at approximately the current requested level;
- continue/add new material at the current level;
- advance/progress to harder material without silently jumping several levels.

Normal production wording may later replace the internal `Reinforce`/`Continue`/`Advance` labels; their v0.9 semantics remain generation intent, not scheduler state.

### Language controls

Where language-learning generation is selected, the prompt can carry:

- instruction/source/target language;
- CEFR A1–C2 target where applicable;
- requested exercise profile such as vocabulary, phrases, translation, grammar, comprehension or mixed practice.

Vocabulary/card-style requests must instruct the external model to produce concise independently reviewable material rather than unrelated essay tasks.

### Anti-duplication and bounded context

Existing content remains available for duplicate avoidance, but prompt context is structured/bounded rather than an unbounded dump of scheduler internals.

### Application-owned prompt composition

Semantic generation instructions are composed in Application code from typed options/context. Desktop presentation must not independently append competing semantic rules.

### External real-use acceptance

For v0.9 release acceptance, real ChatGPT output through the normal Existing Deck flow was manually exercised and accepted as release-worthy. Future UX/analytics improvements identified during that testing are follow-up product work, not retroactive v0.9 blockers.

Content Bundle 1.0 remains the validated output/import contract; no direct paid LLM API is required.

## 13. Local Data Reliability

### Backup before migration

Before a database migration mutates the local database, a readable local backup is created according to the migration coordinator policy.

### Rolling backups

Normal Desktop operation can create change-aware rolling local snapshots with the accepted retention policy and configurable persistent backup location.

### Backup before import

Before a selected Content Bundle commit changes authoritative content, backup protection runs without weakening the atomic import transaction.

### Manual backup/export

Manual local backup and portable export remain available without remote infrastructure.

v0.9 does not introduce cloud backup or authoritative staged database replacement/restore.

## 14. Local-First Operation

Given no remote server is available
Then local authoritative content, Deck, Study, Review, scheduling, Insight and Content Bundle workflows remain usable.

Remote infrastructure is not a hidden requirement for core operation.

## 15. Vocation Boundary

Illumination remains usable with no Vocation integration configured.

Any future Vocation integration exposes only explicit published semantics and must not expose Illumination persistence or mutable Domain objects.

## 16. Wiiii Got This / Product Surface Boundary

Illumination remains authoritative for learning semantics and state changes when hosted by Wiiii Got This.

WGT must not read/write Illumination SQLite or import Illumination Domain objects into WGT Domain/Application.

The current v0.9 Desktop UI is an accepted standalone/admin/dev/acceptance host, not the final production UX. Post-v0.9 Product Surface extraction/redesign may reuse the same tested application behavior without declaring the current shell layout permanent.

## 17. v0.9 Whole-Product Release Smoke

The accepted v0.9 RC must support, on the same persisted candidate:

1. create/edit a Learning Item and assign Deck membership;
2. create/rename/delete a Deck without ghost projections;
3. start Study and submit grades;
4. use Practice now without destructive scheduling mutation;
5. explicitly Restart learning and observe new-learning selection semantics/new-card-limit explanation;
6. Generate → validate → import an Existing Deck extension using current learning evidence;
7. open Insights and observe current derived state;
8. close/reopen and retain authoritative state;
9. use the previously accepted Local Data/backup controls.

The user completed this real-use pass on the frozen v0.9 candidate and reported follow-up product/UX ideas rather than release-blocking defects. That feedback is tracked separately for post-v0.9 work.

## 18. Release Evidence

A stable milestone release requires:

- accepted manual product gate where specified;
- Release configuration build;
- full automated test suite;
- NuGet vulnerability audit;
- reviewed release PR to `main`;
- stable branch/tag verification.

Post-v0.9 ideas do not enter v0.9 release preparation unless they fix a demonstrated release blocker.
