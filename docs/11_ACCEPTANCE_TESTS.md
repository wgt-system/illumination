# Illumination – Acceptance Tests

## Status

Behavioral acceptance baseline.

Scheduling acceptance tests use the deterministic semantics defined in `docs/08A_SCHEDULING_SEMANTICS.md`.

The v0.2 acceptance scope is Study Sessions, five-grade Learning Assessment, Review history, deterministic long-term Learning State/scheduling, and optional opaque submitted-response retention. The v0.3 first slice adds session learning-stack semantics, deterministic assessment previews, Study transparency, and structured Content Acquisition; v0.4 covers the remaining response interaction and evaluation work.

## 1. Learning Item Creation

### Scenario: create a basic item

Given no existing item
When the learner creates one independent question with a Reference Solution
Then Illumination stores one Learning Item
And the item is available for review
And it begins with new Learning State.

### Scenario: reject missing solution

Given a new Learning Item
When no Reference Solution is supplied
Then creation is rejected.

### Scenario: zero hints allowed

Given a valid Learning Item
When no hints are supplied
Then creation succeeds.

### Scenario: multiple hints allowed

Given a valid Learning Item
When several ordered hints are supplied
Then all hints can be retained and revealed progressively.

## 2. Deck Membership

### Scenario: one item in several decks

Given one existing Learning Item
And two existing Decks
When the item is added to both Decks
Then both Decks contain the same Learning Item identity
And no duplicate Learning State is created.

### Scenario: removing membership preserves progress

Given an item with Review history in two Decks
When the item is removed from one Deck
Then its Review history remains unchanged
And its Learning State remains unchanged
And it remains in the other Deck.

## 3. Basic Review

### Scenario: mental/self-assessed review

Given an active due Learning Item
When the learner reviews it without entering a stored answer
And reveals the Reference Solution
And submits one of the five Learning Assessment grades
Then one Review is recorded
And Learning State is updated according to the scheduling model.

Every completed Review changes a new Learning Item to `IsNew = false`.

## 4. Direct Multiple Choice (v0.3)

Given an item authored with direct answer choices
When the learner selects an answer
Then Illumination can determine automatic correctness when the item supports it
And the learner can still complete the configured final assessment flow.

## 5. Answer Choices as Assistance (v0.3)

Given a normal question with optional assistance choices
When the learner first attempts free recall
And later reveals the assistance choices
Then the item remains the same Learning Item
And revealing those choices is recorded as assistance if the configured history requires it
And they are not treated as if the item had originally required direct multiple choice.

## 6. Hint Behavior (v0.3)

### Default hint policy

Given an item with several hints
And default hint policy
When the learner reveals any number of hints
Then no automatic assessment penalty is applied merely because hints were used.

### Configured hint influence

Given hint influence is enabled
When the learner uses hints
Then the configured evaluation/scheduling policy may use that fact.

When enabled, hint use lowers the automatic suggested assessment by at most one grade, without forcing the learner's final grade.

## 7. Automatic Evaluation (v0.3)

### Disabled

Given an item that can be automatically checked
And automatic evaluation is disabled
When the learner supplies a response
Then Illumination does not require automatic correctness to determine the final Learning Assessment
And the learner can assess manually.

### Enabled where supported

Given automatic evaluation is enabled
And the item supports machine checking
When the learner supplies a response
Then Illumination may calculate correctness
And correctness is distinguishable from the final five-grade Learning Assessment.

### Unsupported item

Given automatic evaluation is enabled globally or for the current scope
And an item cannot be reliably checked automatically
When it is reviewed
Then the learner can still complete the review through manual assessment.

## 8. Scheduling Direction

### Ordered assessment behavior

Given two otherwise comparable active items
When one receives a worse normal assessment grade than the other
Then it must be scheduled no later than the better-assessed item.

For the current Study Session, the stronger invariant is:

```text
Nochmal <= Schwer <= Unsicher < Gut < Leicht
```

`Nochmal`, `Schwer`, and `Unsicher` remain in the current session learning stack. `Gut` and `Leicht` graduate into future normal scheduling.

### Lowest grade

Given an active item
When the learner submits the lowest assessment grade
Then the item is treated as requiring very rapid relearning
And it remains in the current session learning stack with a target of 1 intervening card when enough other cards exist.
If too few other cards remain, it goes to the end of the current learning stack; if no other card remains, it remains the next/current item.

### Second-lowest grade

Given an active item
When the learner submits the second-lowest assessment grade
Then the item returns soon
But less aggressively than an otherwise comparable item receiving the lowest grade
And it remains in the current session learning stack with a target of 5 intervening cards when enough other cards exist.

### Highest grade

Given an active item
When the learner submits the highest assessment grade
Then its normal next-review interval is substantially longer than for lower successful grades
And the item is not automatically marked Mastered.

### Successful repetition

Given an active item repeatedly reviewed successfully
When the scheduling model processes those Reviews
Then normal review intervals generally become longer over successful repetitions.

Exact timings follow the initial constants in `docs/08A_SCHEDULING_SEMANTICS.md`.

## 9. Suspension

Given an active item
When the learner suspends it
Then it remains stored
And Review history remains stored
And it is excluded from normal study selection.

Given a suspended item
When the learner reactivates it
Then it becomes immediately due while retaining its Review history and retained scheduling state.

## 10. Mastered

Given an active item
When the learner marks it Mastered
Then it remains stored
And Review history remains stored
And it is excluded from normal study selection.

Given a Mastered item
When the learner unmarks Mastered
Then it becomes immediately due while retaining its Review history and retained scheduling state.

## 11. Low-Interaction Session (v0.3)

Given a study scope containing suitable and unsuitable items
When the learner starts a low-interaction session
Then only items designated suitable for that mode are selected
And the learner can complete suitable reviews with minimal required input.

Suitability is determined by the item's explicit `lowInteractionEligible` property.

## 12. Import

### Valid bundle

Given a supported versioned JSON bundle
And all items are structurally and semantically valid
When the bundle is imported
Then valid Illumination-owned content is created according to the contract
And an explicit import result is returned.


### Explicit update identity

Given an existing Learning Item with a stable Illumination identifier
And an imported update operation explicitly references that identifier
When the bundle is valid
Then the intended Learning Item may be updated according to the import contract
And its Review history and Learning State are not reset merely because content was edited.

### No fuzzy mutation

Given an imported new item whose text resembles an existing Learning Item
And no explicit update operation references the existing stable identifier
When the bundle is imported
Then Illumination must not silently mutate the existing item based only on semantic or textual similarity.

### Unsupported version

Given a JSON bundle with an unsupported contract version
When import is attempted
Then it is rejected explicitly
And no content is silently interpreted under another version.

### Invalid item

Given an invalid imported item
When validation runs
Then the validation issue is reported
And no invalid authoritative Learning Item is silently created.

### Mixed-validity bundle

Given a bundle containing valid and invalid entries
When validation completes
Then valid and invalid entries are clearly separated
And invalid entries are not committed
And the learner may explicitly accept the valid subset
And rejected/corrected content may be imported later without invalidating the already accepted subset.

### Accepted-subset atomicity

Given a mixed-validity bundle
When the learner selects valid operations for import
Then dependencies are validated again for that selected subset
And all selected operations commit in one transaction.

Given an infrastructure failure while committing the selected subset
Then every operation in that selected subset is rolled back
And no partial accepted-subset mutation remains.

### Bundle-level validation

Given malformed JSON, an invalid root, a missing/invalid contract, an unsupported version, or missing/non-array operations
When the bundle is parsed
Then an explicit bundle-level diagnostic is returned
And no content is mutated.

Malformed JSON may produce a repair prompt containing the parser diagnostic, required Content Bundle 1.0 contract/version, supplied invalid JSON, and an instruction to repair rather than redesign and return corrected JSON only.

### Per-operation validation

Given a bundle containing one malformed operation and other structurally valid operations
When validation runs
Then valid and invalid operations both remain visible in the preview
And the malformed operation is not selectable
And valid operations may be selected independently.

### LocalRef dependencies

Given valid create operations for a Deck `localRef` and Learning Item `localRef` plus an assignment referencing them
When the selected subset is validated
Then the assignment resolves deterministically.

Given an assignment whose referenced create operation is invalid or unselected
Then the assignment is not committable
And the dependency diagnostic is explicit
And an independently valid item or Deck create may still be accepted.

### Create identity and operation safety

Given a valid create operation
When it is committed
Then a new Illumination-owned stable ID is assigned
And the bundle `localRef` does not become permanent identity
And normal new Learning State starts
And no Review history is invented.

Given a valid assignment operation
When it is committed
Then explicit Deck membership is added
And no duplicate Learning State is created
And scheduling/history is not reset.

Given an imported new item whose text resembles an existing Learning Item
And no explicit update operation references the existing stable identifier
When the bundle is imported
Then the existing item is not mutated.

Given a normalized exact duplicate prompt
When a create operation is previewed
Then a duplicate warning may be shown
And the operation remains a create operation unless the learner explicitly selects an update.

### Update significance

Given an explicit `minor` update
When it is accepted
Then content changes
And current Learning State, lifecycle, memberships, and Review history remain unchanged.

Given an explicit `semantic` update
When it is accepted
Then content changes
And immutable Review history and lifecycle remain
And memberships remain unless separately changed by assignment
And current scheduling resets to `IsNew = true`, difficulty `5.0`, stability `0.5`, reinforcement not required
And the item is immediately due at the update time.

Given an explicit Deck update
When its supported metadata is changed
Then the Deck changes accordingly
And membership is not silently altered.

### Import preview, result, and provenance

Given a parsed bundle
When preview is requested
Then each operation exposes its index/type, localRef or target stable ID, summary, validity, diagnostics, warnings, dependencies, and selectable state
And the preview creates no content, membership, Review, scheduling, or successful import-history mutation.

Given an accepted subset commits successfully
Then the result identifies created/updated item and Deck IDs, applied memberships, skipped/rejected operation indices, diagnostics where applicable, and a batch identity where retained.

Given an accepted subset commits successfully
Then lightweight local provenance retains import/batch ID, timestamp, contract/version, optional bundle metadata, and operation counts/results.

## 13. Progress Views

Given Reviews and Learning State exist
When progress is requested
Then the view is derived from authoritative state/history
And can distinguish at least new, due, Suspended, and Mastered material
And later can distinguish unstable/stable material according to the selected scheduling model.

## 14. Vocation Boundary

Given no Vocation integration is configured
When Illumination is used
Then all core learning workflows remain available independently.

Given a future Vocation reference
When coverage is queried
Then Illumination returns only the published summary
And does not expose internal persistence or mutable domain objects.

## 15. Wiiii Got This Boundary

Given Wiiii Got This presents Illumination on Windows or iPhone
When the learner invokes an Illumination capability
Then the capability executes through an Illumination-owned application boundary
And Illumination remains authoritative for the resulting learning-state change.

Given Wiiii Got This is unavailable
When Illumination runtime/application capabilities are invoked locally
Then core Illumination workflows remain available without requiring Wiiii Got This.

## 16. Acceptance Scope Notes

The five-grade names, scheduling transitions, lifecycle behavior, and Content Bundle 1.0 envelope are accepted and testable. Content Acquisition validation, accepted-subset atomicity, update significance, preview, result, and provenance semantics are also accepted and testable. Later response interaction/evaluation behavior remains outside this baseline.

## 17. Canonical Deck and Deletion Behavior

### Same item in several Decks

Given one Learning Item belongs to several Decks
When one Deck is deleted
Then the Learning Item remains
And its Review history and Learning State remain unchanged.

### Explicit item deletion

Given an existing Learning Item
When the learner explicitly confirms permanent deletion
Then the Learning Item and its associated Learning State and Review history are removed.

## 18. Review Response Persistence (v0.2 opaque payload)

Given a v0.2 Review has an optional submitted response payload
When the Review is completed
Then the opaque payload may be retained with the Review
And it is not interpreted or automatically evaluated.

Given a v0.2 Review has no submitted response payload
When the Review is completed
Then no artificial response text is required.

Actual text/code interaction workflows are deferred to v0.3, including normalized short-text matching and code execution.

## 19. Five-Grade Behavior

Given an active item is rated `Nochmal`
Then, where the session has enough intervening material, it should return after one other card.

Given an active item is rated `Schwer`
Then, where the session has enough intervening material, it should return after five other cards.

Given an active item is rated `Unsicher`
Then it remains in the current session learning stack
And it is appended to the end of that stack
And it does not receive normal positive stability growth or a future normal dueAt
And reinforcement remains required.

Given an item is rated `Leicht`
Then its next normal interval is longer than for `Gut`
And it is not automatically marked Mastered.

Given an item is in the session learning stack
When it receives `Gut`
Then it leaves the current session learning stack
And reinforcement is cleared
And a future normal dueAt is calculated from the retained stability.

Given an item is in the session learning stack
When it receives `Leicht`
Then it leaves the current session learning stack
And reinforcement is cleared
And its future normal dueAt is later than `Gut` for comparable state.

### Session reinsertion and completion

Given enough other cards remain
When the current item receives `Nochmal`
Then it returns after exactly one intervening queue entry.

Given enough other cards remain
When the current item receives `Schwer`
Then it returns after exactly five intervening queue entries.

Given fewer other queue entries remain than the requested separation
When the current item receives `Nochmal` or `Schwer`
Then it is appended to the end of the current learning stack.

Given a single-card session
When the only remaining item receives `Nochmal`, `Schwer`, or `Unsicher`
Then it remains the next/current learning item rather than disappearing.

Given reinforcement-required items remain in an active Study Session
When the learner explicitly completes the Study Session
Then the session can end
And those items remain immediately due/reinforcement-required for a future session
And no successful future interval is invented.

### Assessment preview and transparency

Given unchanged Learning State, Study Session state, and controlled time
When assessment previews are requested
Then no Review is created
And Learning State and the session learning stack remain unchanged.

Given an assessment preview is obtained
When the learner submits the same grade without state/time changes
Then the actual scheduling and session-stack result matches the preview.

The Application read model exposes enough information for presentation to show the current item, remaining queue-entry count, a bounded upcoming queue preview where useful, repeated/relearning entries, and the projected effect of each grade.

### Review history within one session

Given the same card receives several grades in one Study Session
When each grade is submitted
Then every submitted grade is retained as its own immutable Review
And each Review uses its actual completion timestamp.

A normal Review never automatically Suspends or Masters an item.

## 20. Lifecycle Actions

Given a Suspended item
When the learner Reactivates it
Then its prior Review history and retained scheduling state remain
And it becomes immediately due.

Given a Mastered item
When the learner Unmarks Mastered
Then its prior Review history and retained scheduling state remain
And it becomes immediately due.

## 21. Assisted Evaluation (v0.3)

Given automatic evaluation is enabled
And a machine-checkable response is incorrect
Then Illumination suggests `Schwer`
And the learner may override the suggestion.

Given the response is correct
Then Illumination suggests `Gut`
And the learner may override the suggestion.

## 22. Low-Interaction Study Filtering (v0.3)

Given a Study Session scope contains items with different explicit `lowInteractionEligible` values
When v0.3 low-interaction filtering is requested
Then only eligible items may participate if otherwise eligible
And items that are not eligible are excluded.

The existing persisted `lowInteractionEligible` property is the filtering input. Low-interaction filtering does not create separate Learning State or Review history.

## 23. Study Queue

Given a normal Study Session contains relearning, due, and new items
Then relearning items have priority over ordinary due items
And ordinary due items have priority over new items.

The v0.2 session scope supports one or more selected Decks, uses set-union semantics, includes Active items only, and does not duplicate an item present in several selected Decks.

Given several selected Decks contain the same Learning Item
Then that item appears only once in the session queue.

## 24. New-Item Limit

Given the default Study Session settings
Then no more than 20 new items are introduced unless the learner overrides the limit.

The learner may explicitly choose all new items.

## 25. Import Update Significance

Given an explicit `minor` update
When it is accepted
Then existing Learning State and Review history remain unchanged.

Given an explicit `semantic` update
When it is accepted
Then Review history remains
And current scheduling state resets to new.

## 26. Local-First Operation

Given no remote server is available
When Illumination runtime/application capabilities are invoked locally
Then core content, study, Review, scheduling, Deck, progress, and import workflows remain usable from local authoritative data.


## 27. Malformed JSON

Given ChatGPT returns syntactically invalid JSON
When import is attempted
Then no content is imported
And the parser error is shown
And Illumination can generate a repair prompt.

## 28. Automatic Checking Scope

Given a direct selection item
When Assisted evaluation is enabled
Then Illumination can determine exact correctness.

Given a short-text item with explicit accepted answers
When Assisted evaluation is enabled
Then Illumination compares normalized input against those accepted answers.

Given a code-response item
When V1 is used
Then Illumination does not execute or compile the submitted code
And the learner may compare against the Reference Solution and self-assess.

## 29. V1 Import Operation Safety

Given a valid V1 import bundle
Then it may create/update Learning Items and Decks and assign memberships.

Given a V1 bundle requests deletion, Suspend, or Mastered
Then the operation is rejected as unsupported.

## 30. Local Backup

Given a database migration is about to run
Then Illumination creates a local backup before mutating the database.

Given normal operation
Then rolling local backups can be produced without remote infrastructure.

## 31. Local Capability-Runtime Operation

Given the remote network is unavailable
When Illumination runtime/application capabilities are invoked locally
Then the local SQLite-backed core workflows remain usable without requiring a standalone Illumination UI.

## 32. Lapse After Long Stability

Given a Learning Item with a long existing stability
When it receives `Nochmal`
Then retained stability is strongly reduced and capped at 3 days while reinforcement remains required.

Given a Learning Item with a long existing stability
When it receives `Schwer`
Then retained stability is reduced and capped at 7 days while reinforcement remains required.

A subsequent `Gut` or `Leicht` Review grows from the reduced retained state rather than immediately restoring the previous long interval.
