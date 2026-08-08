# Illumination – Scenarios

## 1. Purpose

This document describes concrete product scenarios without committing to implementation technology or unresolved scheduling details.

The scenarios are intended to validate the domain vocabulary and expose missing decisions before implementation.

## 2. Scenario: Review a Simple Recall Item

### Goal

Quickly test whether a fact can be recalled.

### Example

Prompt:

> What does SSH stand for?

### Flow

1. Illumination presents one learning item.
2. The learner attempts to recall the answer mentally or verbally.
3. The learner may request a hint if available.
4. The learner reveals the reference solution.
5. The learner compares the attempted answer with the reference solution.
6. The learner records a graded assessment of how well the item was recalled.
7. Illumination records the review and updates the item's learning state.
8. Illumination determines when the item should next be reviewed.

### Important properties

- No text input is required.
- Revealing a hint and revealing the reference solution are distinct actions.
- The exact rating labels and scheduling calculation are intentionally unspecified.

## 3. Scenario: Multiple-Choice Review

### Goal

Review an item through a low-friction explicit response.

### Flow

1. Illumination presents a prompt and answer options.
2. The learner selects one option.
3. Illumination can determine whether the selected option matches the expected answer.
4. The reference solution remains available for explanation or confirmation.
5. A review result is recorded.
6. The learning state and future repetition are updated.

### Open semantic detail

It is not yet decided whether an automatically correct answer directly determines the final review grade or merely informs the learner's manual assessment.

## 4. Scenario: Very Short Text Answer

### Goal

Require limited active production without turning the learning session into extended typing.

### Example

Prompt:

> Which keyword declares inheritance from a class in Java?

### Flow

1. Illumination presents the prompt.
2. The learner enters a short answer.
3. The learner reveals or receives the reference solution.
4. The answer may be automatically compared where appropriate.
5. The learner's review result is recorded.
6. The next repetition is scheduled.

### Constraint

Short text input must remain appropriate to the low-friction learning model. Long-form written answers are not assumed.

## 5. Scenario: Small Coding Task

### Goal

Practice syntactic or implementation recall with a small concrete task.

### Example

Prompt:

> Write a function that returns whether an integer is even.

### Flow

1. Illumination presents the coding task.
2. The learner writes a small code fragment.
3. The learner may request a hint.
4. The learner reveals a reference implementation.
5. The learner compares their implementation with the reference.
6. A graded review result is recorded.
7. The item returns according to its updated learning state.

### Important property

The reference solution is exemplary rather than necessarily the only valid implementation.

## 6. Scenario: Low-Interaction Session

### Goal

Review suitable material with minimal physical interaction.

### Flow

1. The learner starts a session restricted to items suitable for low-interaction study.
2. Illumination chooses a due or otherwise eligible item.
3. The learner answers mentally or verbally, or selects a simple option.
4. The learner reveals assistance or the reference solution as needed.
5. The learner records a quick review assessment.
6. Illumination immediately advances to the next item.
7. The session continues until the learner stops or no eligible items remain.

### Product implication

Illumination needs some way to determine whether a learning item is suitable for this mode.

The exact representation is not yet decided.

## 7. Scenario: Difficult Item Reappears Quickly

### Goal

Reinforce an item that was not recalled successfully.

### Flow

1. The learner reviews an item.
2. The learner assesses the result as poor or difficult.
3. Illumination records the review.
4. The item is scheduled substantially sooner than an item reviewed successfully.
5. The item may reappear again within the same broader learning period if the chosen scheduling model supports that behavior.
6. After subsequent successful reviews, the interval begins to increase.

### Open semantic detail

The exact short-term relearning behavior is not yet specified.

## 8. Scenario: Stable Item Moves Further Into the Future

### Goal

Avoid wasting attention on material that is already well retained.

### Flow

1. An item has already been reviewed successfully several times.
2. The learner reviews it successfully again.
3. Illumination records the review.
4. The next repetition is scheduled further into the future.
5. Continued successful reviews increase the interval further.

### Product implication

Long-term learning is represented by increasing review intervals, not by requiring a permanent terminal mastered state.

## 9. Scenario: Explicitly Remove a Trivial Item From Normal Review

### Goal

Allow the learner to stop normal repetition for material that has become permanently trivial or irrelevant.

### Flow

1. The learner decides that an item no longer needs normal review.
2. The learner invokes an explicit action.
3. Illumination excludes the item from normal repetition while retaining the content and relevant history.

### Status

This capability is plausible but not yet required for the first version.

Its final semantics and terminology are open.

## 10. Scenario: Create a Deck Manually

### Goal

Group learning items according to the learner's current purpose.

### Flow

1. The learner creates a new deck.
2. The learner gives it a name.
3. The learner adds selected learning items.
4. The deck becomes available for targeted study.

### Important property

Illumination does not impose a required topic taxonomy.

A deck may be narrow, broad, mixed, temporary, or long-lived.

## 11. Scenario: Generate New Learning Content Through ChatGPT

### Goal

Create useful learning material quickly without manually authoring every item.

### Flow

1. The learner chooses to generate content for a desired subject or purpose.
2. Illumination creates a prompt that describes the required structured output.
3. The learner sends the prompt to ChatGPT externally.
4. ChatGPT returns structured JSON.
5. The learner imports the JSON into Illumination.
6. Illumination validates the contract and semantic constraints.
7. Valid content is imported atomically or through another explicitly defined import policy.
8. The new content becomes available for organization and review.

### Important properties

- ChatGPT is external to the Illumination domain.
- Illumination owns the imported content after successful import.
- The JSON contract must later be versioned.
- The exact update/merge semantics are not yet defined.

## 12. Scenario: Extend Existing Learning Content

### Goal

Add new items to an existing body of learning material.

### Flow

1. The learner selects an existing deck, subject, or other content scope.
2. Illumination generates a prompt describing the desired additions and output contract.
3. ChatGPT produces structured JSON.
4. Illumination validates and imports the new content.
5. Existing learning state must not be accidentally reset merely because additional content was imported.

### Open semantic detail

How imported items are matched against existing items is not yet defined.

## 13. Scenario: Learn for a Vocation-Identified Need

### Goal

Use Illumination to address a learning need that was identified in Vocation.

### Flow

1. Vocation identifies a learning need or learning cluster.
2. The learner decides to create or associate learning content in Illumination.
3. Illumination receives only the explicit reference information required by the future integration contract.
4. One or more Illumination decks or learning items may be created or associated with that external need.
5. Learning proceeds entirely within Illumination.
6. Illumination may later expose a limited coverage/progress view for that external reference.

### Boundary

Illumination does not import or own the Vocation opportunity itself.

Vocation does not own or manipulate Illumination learning items.

## 14. Scenario: Vocation Consumes Learning Coverage

### Goal

Allow Vocation to consider whether an identified learning need is already well covered.

### Possible future flow

1. Vocation holds an external learning-need identity.
2. Vocation queries a published Illumination read contract.
3. Illumination returns a bounded summary such as whether relevant learning content exists and how much of it is new, due, or stable.
4. Vocation uses that summary within its own job-market assessment logic.

### Status

This scenario is directional only.

The precise meaning of coverage, stability, identity, and aggregation must be designed before a contract is created.

## 15. Scenario: Wiiii Got This Presents Illumination Capability

### Goal

Use Illumination through Wiiii Got This on a platform for which Illumination may not have its own native client.

### Flow

1. Wiiii Got This discovers or knows an available Illumination capability.
2. Wiiii Got This invokes an explicit published Illumination contract.
3. Illumination returns or accepts only the data required by that capability.
4. Wiiii Got This presents the capability appropriately for the current device/platform.
5. Illumination remains the owner of learning state and review semantics.

### Boundary

Wiiii Got This does not obtain ownership of Illumination domain entities merely because it presents their capabilities.

## 16. Scenario: Review History Survives Content Organization Changes

### Goal

Keep learning history attached to the learning item rather than to an incidental deck placement.

### Flow

1. A learner has reviewed an item several times.
2. The item is moved between decks or a deck is renamed.
3. The item's review history and repetition state remain intact.

### Product implication

Deck membership is organizational and must not itself define learning identity.

## 17. Scenario: Study One Deck

### Goal

Focus a learning session on material chosen by the learner.

### Flow

1. The learner selects one deck.
2. Illumination determines which items in that deck are currently eligible for review.
3. The learner reviews them using their appropriate interaction modes.
4. Review history and scheduling are updated globally for the items.

### Product implication

A review changes the learning state of the item, not a deck-local copy of that learning state.

## 18. Scenario: Inspect Learning Progress

### Goal

Understand where learning effort is currently required.

### Flow

1. The learner opens a progress view.
2. Illumination derives summary information from authoritative item learning state and review history.
3. The learner can distinguish at least new, due, difficult/unstable, and long-term stable material.
4. The view may be scoped to a deck or broader content set.

### Boundary

The analytics view is derived from learning state; it does not own or independently mutate that state.

## 19. Scenario Coverage Summary

These scenarios establish the following domain pressures:

- individual reviewable learning units,
- reference solutions,
- optional assistance,
- multiple interaction modes,
- graded review outcomes,
- durable review history,
- scheduling state,
- user-defined decks,
- structured import,
- external references without shared ownership,
- derived progress views.

The scenarios intentionally do not decide the scheduling algorithm, final rating scale, canonical learning-unit taxonomy, or cross-context contract schema.
