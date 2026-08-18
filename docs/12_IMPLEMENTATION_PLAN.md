# Illumination – Implementation Plan

## Status

Implementation baseline. The accepted V1 decisions satisfy the implementation gate.

## 1. Release Model

Use semantic-style versions:

```text
vMAJOR.MINOR.PATCH
```

Planned feature milestones before the first major release can progress as:

```text
v0.1.0
v0.2.0
v0.3.0
...
v1.0.0
```

The exact number of `v0.y.0` milestones is not predetermined.

Patch releases may later use the third component where needed.

`v1.0.0` should represent a coherent first major product baseline, not merely the first runnable build.

## 2. Specification Gate

The implementation gate is satisfied for the accepted V1 baseline. Scheduling semantics, canonical terminology, Content Bundle 1.0, and the technology baseline are decided and documented. Remaining application-detail decisions must not block the listed vertical slices.

## 3. Proposed Milestone Shape

The following milestone sequence is the current implementation baseline.

### Milestone A – Learning Core

Goal:

- create/edit Learning Items,
- Reference Solutions,
- `0..*` hints,
- Decks with many-to-many membership,
- active/suspended/mastered lifecycle,
- deterministic domain tests.

No complex UI requirement should drive this milestone.

### Milestone B – Review and Scheduling

Goal:

- study-session flow,
- five-grade assessments,
- Review history,
- Learning State,
- exact repetition algorithm,
- due selection,
- difficult/relearning behavior,
- Suspend/Mastered scheduling interactions.

This milestone uses the accepted scheduling specification in `docs/08A_SCHEDULING_SEMANTICS.md`.

### Milestone C – Structured Content Acquisition

Goal:

- prompt generation,
- Content Bundle 1.0 validation and import,
- mixed-validity preview and atomic accepted-subset commit,
- new-content import,
- minor/semantic update behavior,
- provenance and import result reporting,
- malformed-JSON repair prompt,
- deterministic duplicate warnings without auto-merge.

Uses the published Content Bundle 1.0 contract and explicit stable Illumination identifiers for intentional updates.

### Milestone D – Content Quality and Curation

Goal:

- User Flags and flag filtering,
- user-defined flag definitions with multiple flags per Learning Item, independent of scheduling, content revision, and quality assurance,
- immutable Quality Reviews with `Pass`, `Warning`, and `NeedsReview`,
- ModelReview, SourceGroundedReview, and UserReview evidence types,
- ContentRevision starting at `1`, exact change-sensitive increments, explicit review supersession, and derived current quality state with deterministic precedence,
- quality-review prompts, findings preview, explicit acceptance, and optional correction application.

### Milestone E – Interaction Variants

Goal:

- SelfAssessed interaction refinement,
- direct multiple choice,
- optional answer-choice assistance,
- short text response,
- small coding response,
- Manual/Assisted evaluation,
- hints and assistance/reveal workflows,
- optional hint influence,
- low-interaction filtering.

### Milestone F – Learning Insight

Goal:

- Deck summaries,
- due/new/relearning views,
- Review history views,
- Suspended/Mastered management,
- useful learning-progress dashboard.

### Milestone G – Integration Surface

Only when required:

- versioned Illumination published capabilities,
- Wiiii Got This integration,
- Vocation Learning Reference / coverage contract.

Do not implement speculative integration endpoints before concrete consumers and semantics exist.

## 4. V1 Direction

The first major release should include at least the product behaviors already declared V1-worthy:

- repeatable Learning Items,
- Decks,
- `0..*` hints,
- five-grade assessment,
- scheduling,
- configurable automatic evaluation support,
- default no-penalty hint behavior with optional configurable influence,
- Suspension,
- Mastered,
- structured ChatGPT JSON content generation/import,
- progress visibility,
- single-user local-first operation.

The exact milestone in which each becomes usable may vary.

## 5. Testing Strategy Direction

Before coding, convert domain invariants and acceptance scenarios into executable test targets.

Priority order:

1. pure domain-rule tests,
2. application-use-case tests,
3. import-contract tests,
4. persistence integration tests,
5. application-capability interaction tests,
6. published-contract tests when integrations exist.

Scheduling must be deterministic under controlled time.

## 6. Codex/Luna Use

Codex/Luna should only receive implementation work after:

- relevant specification is stable,
- file/module scope is known,
- expected behavior is written as acceptance criteria,
- contracts are versioned where applicable.

Parallel work is appropriate only for genuinely independent tasks with little file/contract overlap.

## 7. No Premature Microservices

Milestones describe capabilities, not deployables.

Do not map:

- Scheduling,
- Statistics,
- Decks,
- Import

to separate network services without an actual architectural reason.

## 8. Next Planning Step

Proceed with the v0.1.0 vertical slices in order, keeping the accepted contracts and ADRs under test. Create narrower implementation tasks only where the listed milestone behavior requires them.


## Accepted V1 Technology Baseline

- C# / .NET 10 LTS
- SQLite
- executable capability runtime with Wiiii Got This as the primary end-user presentation
- no mandatory remote server
- optional server/Docker/Conveyance-backed delivery infrastructure for connectivity or future synchronization only

Implementation planning should use this baseline unless a later ADR explicitly supersedes it.

## Concrete Pre-1.0 Milestones

### v0.1.0

Scope: Local Content Foundation.

Deliver in order:

1. Bootstrap solution architecture (C#/.NET 10 LTS capability runtime, EF Core infrastructure, and test baseline; optional Avalonia host may remain available for administration/development).
2. Learning Item and Deck domain.
3. SQLite persistence and migrations.
4. Content-management application capabilities and their optional/admin/dev host integration.
5. Backup, hardening, and v0.1.0 acceptance.

### v0.2.0 – Study and Scheduling

Deliver:

- Study Session,
- five-grade assessment (`Nochmal`, `Schwer`, `Unsicher`, `Gut`, `Leicht`),
- deterministic scheduling state,
- durable short-term reinforcement prioritization with session-local stack ordering,
- due/new/relearning prioritization,
- one or more selected Decks with set-union queue deduplication and Active items only,
- default 20-new-item limit with explicit override/all-new behavior,
- Review history,
- optional opaque submitted response payload retention,
- Reactivate for Suspended and UnmarkMastered for Mastered,
- scheduling simulations/acceptance tests.

### v0.3.0 – Study Refinement and Content Acquisition

Deliver:

- Session learning-stack semantics and deterministic assessment previews:
  - `Nochmal` returns after 1 intervening card when possible,
  - `Schwer` returns after 5 intervening cards when possible,
  - `Unsicher` returns at the end of the current learning stack,
  - `Gut` and `Leicht` graduate into future normal scheduling,
  - small-queue and single-card fallback remains explicit,
  - unfinished reinforcement remains durable across sessions while stack position remains session-local.
- Standalone Study transparency and compact UI integration, including remaining-session information, bounded queue preview, and grade-outcome previews near the five grade controls.
- structured Content Acquisition using the canonical Content Bundle 1.0 schema:
  - prompt generation for substantial content requests,
  - parse/envelope, per-operation structural, and semantic/dependency validation,
  - mixed-validity preview with explicit accepted-subset selection,
  - atomic accepted-subset commit,
  - localRef dependency resolution,
  - minor/semantic update semantics,
  - deterministic duplicate warnings,
  - malformed-JSON repair prompt,
  - import results and lightweight provenance.

v0.2 does not interpret submitted response payloads. v0.3 adds session refinement, Study transparency, and Content Acquisition. v0.4 is Content Quality & Curation. Response interaction workflows, automatic correctness/grade suggestions, hint influence, and low-interaction filtering belong to v0.5.

The v0.3 first slice is session learning-stack semantics, grade previews, standalone Study transparency, and Content Acquisition. Content Acquisition is pulled forward so realistic Study/scheduler validation is practical without manually authoring large item sets.

### v0.4.0 – Content Quality and Curation

Deliver:

- User Flags and flag filtering,
- immutable Quality Reviews with current-revision binding,
- derived current quality state,
- Standard/Strict/SourceGrounded generation quality modes,
- quality-review prompt and structured-result workflow,
- explicit review acceptance and optional normal content correction.

### v0.5.0 – Interaction Modes and Evaluation (released)

The v0.5 implementation is released and covered by the integrated acceptance and hardening baseline.

Deliver:

- `SelfAssessed`, `Selection`, `ShortText`, and `Code` end-to-end interaction workflows over the existing authored response fields,
- Manual and Assisted evaluation with advisory correctness and grade suggestions,
- exact multi-choice set comparison and conservative accepted-short-answer comparison,
- progressive hint and explicit assistance-answer-choice reveal workflows,
- optional `ConsiderAssistance` suggestion influence without changing the final learner-selected grade,
- durable v0.5 interaction facts on Review,
- optional low-interaction filtering by the existing persisted `lowInteractionEligible` property.

v0.5 does not introduce arbitrary code execution, fuzzy or semantic answer checking, direct LLM/API integration, Content Bundle 1.0 changes, or scheduler redesign.

### v0.6.0 – Learning Insight (released)

The v0.6 implementation is released and covered by the accepted integrated test, build, and Desktop startup baseline. Shipped scope includes the Desktop shell/page structure, Learning Insight, Review and Study Session history, filtering/search, DeckLearningContext, learning-aware follow-up Deck generation, Reinforce/Continue/Advance progression modes, generation ResponseMode controls, language-learning prompt hardening, improved JSON drag/drop, and explicit Suspended/Mastered lifecycle management.

Deliver:

- Deck summaries,
- due/new/relearning views,
- Suspended/Mastered management,
- Study Session history,
- Review history,
- useful filtering/search,
- a typed DeckLearningContext for later follow-Deck generation.

The v0.6 foundation is a derived read capability over authoritative local Learning
Items, current Deck membership, Reviews, and Study Sessions. It does not introduce a
separate analytics source of truth, arbitrary mastery scoring, denormalized statistics,
or a generated prompt/LLM integration. The five-grade distribution is based only on
learner-confirmed final assessments.

### v0.7.0 – Product Refinement (released)

The v0.7 implementation is released after the explicit Product Refinement hardening gate in Issue #19 and the accepted Release build, full test suite, and NuGet vulnerability audit. Shipped scope includes richer Content Bundle generation/import previews, language and follow-up generation controls, existing-content improvement prompts, Deck export and direct Deck actions, bulk content curation and Study flags, focus-aware Study shortcuts, Learning Insights activity history and due forecasts, local backup/export controls, migration hardening, and repository/C4 maintenance improvements.

The v0.7 release gate deliberately removed two unaccepted product semantics that appeared during refinement: product-level resume/finish controls for persisted unfinished Study Sessions and staged replacement of the authoritative local SQLite database. Durable Study state, normal explicit session completion, rolling/manual backups, portable backup export, and backup-before-migration safety remain within the accepted baseline.

Use further `v0.y.0` releases as needed for:

- usability,
- scheduler tuning,
- backup/export improvements,
- content-management refinements,
- performance,
- migration hardening.

Do not force `v1.0.0` after a fixed number of pre-1.0 milestones.

### v1.0.0 – Coherent Independent Illumination

`v1.0.0` requires a stable independent Illumination capability runtime covering:

- content acquisition,
- Deck organization,
- repeated study,
- deterministic scheduling,
- progress/history,
- local authoritative persistence,
- backup/migration safety,
- acceptance-tested core invariants.

Wiiii Got This may provide the primary end-user presentation; a complete separate Illumination end-user UI is not required.

Vocation and Wiiii Got This integrations are not prerequisites for `v1.0.0`; they evolve through separate published-contract milestones when their semantics are needed.

The v0.6 Desktop slice exposes the existing Learning Insight read models and hands a
selected source Deck's derived `DeckLearningContext` to the existing Content
Acquisition prompt workflow. It does not introduce a mastery score, mutate the source
Deck, or change Content Bundle 1.0.
