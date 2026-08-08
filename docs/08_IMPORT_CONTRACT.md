# Illumination – Structured Learning Content Import

## Status

Product-level import contract specification.

The concrete JSON Schema 1.0 must be created only after import identity/update semantics are decided. This document defines what that schema must represent.

## 1. Purpose

Illumination should allow large amounts of useful learning content to be created quickly through an external ChatGPT interaction and imported as structured data.

The initial workflow is user-mediated:

```text
Illumination
   ↓
generate prompt containing contract requirements
   ↓
ChatGPT
   ↓
versioned JSON
   ↓
Illumination validation
   ↓
import
```

No paid LLM API is required for the initial workflow.

## 2. Contract Principles

The import format must be:

- explicit,
- versioned,
- machine-validated,
- independent from Illumination implementation classes,
- suitable for direct inclusion in generated ChatGPT prompts,
- strict enough to reject ambiguous or malformed content,
- extensible through future contract versions.

## 3. Contract Envelope

Every import bundle must identify at least:

- contract name/type,
- contract version,
- bundle identity if durable import identity is later required,
- generated content payload,
- optional provenance relevant to the import workflow.

Exact field names are deferred until JSON Schema 1.0 is authored.

## 4. Learning Item Payload

Each imported Learning Item must be able to represent:

- item identity or import-local identity as required by the final update strategy,
- question / mini-task prompt,
- Reference Solution,
- `0..*` hints,
- optional direct answer choices,
- optional answer choices that act as assistance,
- interaction/evaluation capabilities,
- low-interaction suitability where explicitly supported,
- optional target Deck placement,
- optional metadata needed for provenance or content maintenance.

## 5. One Independent Item Rule

One imported item must represent one independently reviewable question or mini-task.

Forbidden:

- one item containing a list of unrelated interview questions,
- one reference solution covering several independent prompts,
- a pseudo-item that acts as a hidden lesson/course container.

Decks perform grouping.

## 6. Reference Solution Requirement

Every imported Learning Item must contain a Reference Solution.

For open-ended or coding tasks, the Reference Solution may be an exemplary correct answer rather than an exhaustive definition of every valid answer.

## 7. Hints

Imported items may contain zero or more hints.

Hints must preserve their intended reveal order when order is meaningful.

Default product semantics:

- requesting hints does not automatically penalize the learner,
- scheduling consequences of hint usage are optional/configurable.

The import contract describes content; it must not hard-code a global scheduling policy into each hint unless a later product decision explicitly requires it.

## 8. Answer Choices

The contract must distinguish:

### Direct answer choices

The item is authored to be answered through a choice.

### Assistance choices

The item can first be attempted freely and later reveal answer choices as help.

This distinction must survive import because it changes review interaction.

## 9. Automatic Evaluation Metadata

The contract may describe whether an item supports machine-checkable correctness.

It must not assume that every item can be automatically evaluated.

Automatic correctness is distinct from the final five-grade Learning Assessment.

The user's evaluation policy determines whether automatic checking is used.

## 10. Deck Placement

Imported content may request placement into one or more Decks.

Because Learning Items may belong to multiple Decks, the contract must not assume one-item-to-one-deck ownership.

A bundle may create a new Deck, reference an existing Deck by stable identifier, and place one Learning Item in multiple Decks.

Renaming or deleting an existing Deck requires an explicit update operation. A generated name difference never implies rename or deletion.

Deck placement is organizational and must not create duplicate learning progress.

## 11. New Content vs Update Content

The contract must support both:

- adding new Learning Items,
- explicitly updating/augmenting existing Learning Items.

### Identity strategy

Existing Learning Items are referenced by explicit stable Illumination-owned identifiers.

For an update/extension prompt, Illumination supplies ChatGPT with the relevant existing-item index, including each item's stable identifier and only the content necessary to make the requested content decision.

ChatGPT output must distinguish explicit operations such as conceptually:

```text
create new item
update item <stable-id>
```

The final JSON field names are defined by Schema 1.0.

### Update significance

An explicit update declares one of two significance levels.

#### Minor update

Examples: typo correction, clearer wording without changing tested knowledge, added explanation, or additional hint.

Learning State and Review history remain unchanged.

#### Semantic update

The tested question/solution meaning changes materially.

Review history is retained for traceability, but current scheduling state resets to `new`.

ChatGPT may propose the significance level, but the import preview shows it before the learner accepts the update.

### No fuzzy mutation

Illumination must not automatically update an existing Learning Item merely because newly generated content looks semantically similar.

Fuzzy or semantic similarity may later be used to warn about possible duplicates, but not to authorize mutation.

### Prompt-size strategy

Illumination must not assume that an entire database snapshot belongs in every prompt.

For update/extension prompts it should provide the smallest useful context, for example:

- stable item id,
- question/task text,
- compact Reference Solution when needed,
- relevant Deck or generation scope.

Review history, scheduling state, and unrelated content are normally excluded.

For larger scopes, Illumination may:

- generate a prompt plus an attached machine-readable snapshot file,
- divide work into explicit batches,
- restrict the prompt to the selected Deck/scope.

All approaches preserve stable item identifiers so returned update operations remain explicit.

## 12. Import Validation

Validation should conceptually have two layers:

### Structural validation

Examples:

- required fields present,
- correct types,
- supported contract version,
- arrays/objects structurally valid.

### Semantic validation

Examples:

- Reference Solution not empty,
- one independently reviewable prompt,
- direct choice configuration internally consistent,
- hint ordering valid,
- requested Deck references resolvable under the selected import mode.

## 13. Mixed-Validity Import Policy

Illumination supports partial acceptance of a bundle when some entries are valid and others are invalid.

Example:

```text
50 generated items
├── 48 valid
└── 2 invalid
```

The application must:

1. validate the complete bundle,
2. clearly separate valid and invalid entries,
3. show the validation problems for invalid entries,
4. allow the learner to explicitly accept/import the valid subset,
5. leave invalid entries uncommitted,
6. allow missing/corrected content to be supplied later through another import.

Valid content must not be committed merely because parsing succeeded; the user gets an explicit import/review step before authoritative content changes are applied.

Invalid entries must never be silently repaired or imported under guessed semantics.

## 14. Import Result

Every attempted import should produce a clear result describing at least:

- accepted/rejected status,
- contract version,
- number of items considered,
- created items,
- updated items when updates exist,
- rejected items and reasons,
- Deck changes,
- warnings that did not invalidate the import.

## 15. Prompt Generation Requirements

Illumination-generated prompts should include:

- the exact supported import contract,
- instructions to return only/primarily the requested structured output as appropriate,
- requested topic/purpose,
- requested quantity,
- interaction preferences,
- low-interaction constraints when requested,
- requirement for one independent item per entry,
- requirement for a Reference Solution,
- guidance on hints and answer choices.

## 16. Ownership

ChatGPT-generated content is external input until validated.

After successful import:

- Illumination owns the Learning Item identity and learning semantics,
- imported content no longer depends on ChatGPT for existence,
- Review history belongs only to Illumination.

## 17. Versioning

Breaking changes require a new import-contract version.

Illumination may support multiple import versions concurrently during migration.

An implementation must not reinterpret an older bundle under newer semantics without an explicit migration path.

## 18. Import Provenance

Illumination retains import history containing:

- import time,
- contract version,
- generated prompt,
- raw returned JSON,
- validation/import report,
- Learning Items created or updated.

Import provenance is retained as import history rather than duplicated as heavy metadata on every Learning Item.

Potential-duplicate detection may warn about similar content, but never automatically merges or mutates Learning Items.

## 19. Remaining Blockers Before JSON Schema 1.0

Update identity is decided: explicit stable Illumination item identifiers are used for intentional updates.

Remaining contract decisions include:

- exact create/update operation envelope,
- required versus optional provenance,
- Deck create/reference behavior inside an import bundle.

JSON Schema 1.0 should be frozen only after these semantics are resolved.

## V1 Operations

The V1 bundle supports explicit operations for:

- create Learning Item,
- update Learning Item by stable Illumination ID,
- create Deck,
- update Deck by stable Illumination ID,
- assign Learning Items to one or more Decks.

The V1 bundle does **not** support:

- delete Learning Item,
- delete Deck,
- Suspend,
- Mastered,
- destructive lifecycle transitions.

Those remain direct Illumination user actions.

Existing Learning Item updates declare:

- `minor`: wording/explanation/hints changed without changing tested knowledge; Review history and scheduling remain,
- `semantic`: tested meaning changed materially; Review history remains but current scheduling state resets to `new`.

ChatGPT may propose the update significance, but the learner sees it in the import preview before acceptance.

## Malformed JSON

If the returned JSON is syntactically invalid:

- import nothing,
- show the parser error,
- do not silently repair or guess,
- allow Illumination to generate a repair prompt for ChatGPT.

Only after valid JSON parsing does item-level structural/semantic validation run.

For syntactically valid mixed-quality bundles:

- valid and invalid entries are separated,
- the learner explicitly accepts the valid subset,
- invalid entries remain uncommitted and may be corrected/imported later.

## Concrete V1 Schema

The initial machine-readable contract is:

- `schemas/illumination-content-bundle-1.0.schema.json`

An example bundle is:

- `examples/content-bundle-1.0.example.json`

The schema uses explicit discriminated operations:

- `create_learning_item`
- `update_learning_item`
- `create_deck`
- `update_deck`
- `assign_item_to_decks`

New objects can be referenced within the same bundle through import-local references.

Existing objects are updated only through stable Illumination IDs.

The schema intentionally contains no delete, Suspend, or Mastered operation.
