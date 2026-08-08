# Illumination – Acceptance Tests

## Status

Behavioral acceptance baseline.

Tests that depend on the exact five-grade scheduling algorithm are expressed abstractly until that algorithm is specified.

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

## 4. Direct Multiple Choice

Given an item authored with direct answer choices
When the learner selects an answer
Then Illumination can determine automatic correctness when the item supports it
And the learner can still complete the configured final assessment flow.

## 5. Answer Choices as Assistance

Given a normal question with optional assistance choices
When the learner first attempts free recall
And later reveals the assistance choices
Then the item remains the same Learning Item
And revealing those choices is recorded as assistance if the configured history requires it
And they are not treated as if the item had originally required direct multiple choice.

## 6. Hint Behavior

### Default hint policy

Given an item with several hints
And default hint policy
When the learner reveals any number of hints
Then no automatic assessment penalty is applied merely because hints were used.

### Configured hint influence

Given hint influence is enabled
When the learner uses hints
Then the configured evaluation/scheduling policy may use that fact.

The exact interval effect is deferred until scheduling semantics are specified.

## 7. Automatic Evaluation

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

### Lowest grade

Given an active item
When the learner submits the lowest assessment grade
Then the item is treated as requiring very rapid relearning
And it may be eligible to reappear again very soon, including within the same broader study period under the final scheduling algorithm.

### Second-lowest grade

Given an active item
When the learner submits the second-lowest assessment grade
Then the item returns soon
But less aggressively than an otherwise comparable item receiving the lowest grade.

### Highest grade

Given an active item
When the learner submits the highest assessment grade
Then its normal next-review interval is substantially longer than for lower successful grades
And the item is not automatically marked Mastered.

### Successful repetition

Given an active item repeatedly reviewed successfully
When the scheduling model processes those Reviews
Then normal review intervals generally become longer over successful repetitions.

Exact timings remain pending.

## 9. Suspension

Given an active item
When the learner suspends it
Then it remains stored
And Review history remains stored
And it is excluded from normal study selection.

Given a suspended item
When the learner reactivates it
Then it may return to normal study according to the later-defined scheduling rule.

## 10. Mastered

Given an active item
When the learner marks it Mastered
Then it remains stored
And Review history remains stored
And it is excluded from normal study selection.

Given a Mastered item
When the learner unmarks Mastered
Then it may return to normal study according to the later-defined scheduling rule.

## 11. Low-Interaction Session

Given a study scope containing suitable and unsuitable items
When the learner starts a low-interaction session
Then only items designated suitable for that mode are selected
And the learner can complete suitable reviews with minimal required input.

The exact suitability representation remains open.

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

Given Wiiii Got This is unavailable
When the learner launches Illumination through its independent presentation
Then core Illumination workflows remain usable.

Given a future Wiiii Got This capability integration
When a capability is invoked
Then Illumination remains authoritative for the resulting learning-state change.

## 16. Acceptance Blockers Before Implementation Freeze

The following tests cannot be made exact until product decisions are completed:

- final five grade names,
- exact interval transitions,
- exact short-term relearning timing for the lowest grades,
- post-reactivation scheduling,
- post-unmaster scheduling,
- exact create/update JSON operation envelope,

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

## 18. Review Response Persistence

Given a Review uses text or code input
When the Review is completed
Then the entered response is retained with the Review.

Given a Review uses mental/verbal-only recall
When the Review is completed
Then no artificial response text is required.

## 19. Five-Grade Behavior

Given an active item is rated `Nochmal`
Then, where the session has enough intervening material, it should return after roughly three other cards.

Given an active item is rated `Schwer`
Then, where the session has enough intervening material, it should return after roughly ten other cards.

Given an item is rated `Leicht`
Then its next normal interval is longer than for `Gut`
And it is not automatically marked Mastered.

A normal Review never automatically Suspends or Masters an item.

## 20. Reactivation

Given a Suspended or Mastered item
When the learner reactivates it
Then its prior Review history remains
And it becomes immediately due.

## 21. Assisted Evaluation

Given automatic evaluation is enabled
And a machine-checkable response is incorrect
Then Illumination suggests `Schwer`
And the learner may override the suggestion.

Given the response is correct
Then Illumination suggests `Gut`
And the learner may override the suggestion.

## 22. Low-Interaction Eligibility

Given an item is explicitly marked `lowInteractionEligible`
When a low-interaction session is started
Then the item may participate if otherwise eligible.

Low-interaction mode does not create separate Learning State or Review history.

## 23. Study Queue

Given a normal Study Session contains relearning, due, and new items
Then relearning items have priority over ordinary due items
And ordinary due items have priority over new items.

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

Given remote infrastructure is unavailable
When Illumination is used through its independent local presentation
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

## 31. Installed Local Operation

Given the remote network is unavailable
When the learner starts the installed Illumination desktop application
Then the local SQLite-backed core workflows remain usable.

## 32. Lapse After Long Stability

Given a Learning Item with a long existing stability
When it receives `Nochmal`
Then retained stability is strongly reduced and capped at 3 days before recovery scheduling.

Given a Learning Item with a long existing stability
When it receives `Schwer`
Then retained stability is reduced and capped at 7 days before recovery scheduling.

A subsequent successful Review grows from the reduced retained state rather than immediately restoring the previous long interval.
