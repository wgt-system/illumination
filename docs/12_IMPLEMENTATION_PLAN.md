# Illumination – Implementation Plan

## Status

Planning baseline only. No implementation should start until the remaining domain and architecture blockers are resolved.

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

Before implementation starts, finalize:

- final five-grade terminology,
- exact repetition/scheduling behavior,
- exact create/update contract envelope,
- low-interaction suitability representation,
- automatic-evaluation configuration scope,
- hint-policy configuration scope,
- language/runtime and persistence technology,
- architecture ADRs.

## 3. Proposed Milestone Shape

The exact version numbers may change after architecture decisions.

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

This milestone depends on the unresolved scheduling specification.

### Milestone C – Interaction Variants

Goal:

- mental/self-assessed flow,
- direct multiple choice,
- optional answer-choice assistance,
- short text response,
- small coding response,
- optional automatic checking,
- low-interaction filtering.

### Milestone D – Structured Content Acquisition

Goal:

- prompt generation,
- versioned JSON Schema,
- validation,
- import reporting,
- new-content import,
- update/augmentation behavior,
- provenance required by the chosen workflow.

Uses explicit stable Illumination identifiers for intentional updates and a user-confirmed partial-import workflow; the exact bundle operation structure remains to be finalized.

### Milestone E – Learning Insight

Goal:

- Deck summaries,
- due/new/stable views,
- Review history views,
- Suspended/Mastered management,
- useful learning-progress dashboard.

### Milestone F – Integration Surface

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
5. presentation interaction tests,
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

After the remaining product decisions are completed:

1. finalize `05_DOMAIN_MODEL.md`,
2. freeze the initial scheduling specification,
3. freeze import contract 1.0,
4. select architecture/technology through explicit ADRs,
5. assign concrete release milestone numbers,
6. create implementation issues,
7. only then begin Codex implementation.


## Accepted V1 Technology Baseline

- C# / .NET
- Avalonia
- SQLite
- installed local desktop application
- no mandatory remote server
- no Docker requirement for the core V1 application

Implementation planning should use this baseline unless a later ADR explicitly supersedes it.

## Concrete Pre-1.0 Milestones

### v0.1.0 – Local Content Foundation

Deliver:

- C#/.NET + Avalonia application shell,
- SQLite schema/migrations,
- Learning Item create/edit/delete,
- Reference Solution,
- `0..*` hints,
- Deck create/edit/delete,
- many-to-many item/Deck membership,
- Active/Suspended/Mastered lifecycle,
- local rolling backup baseline,
- core domain tests.

### v0.2.0 – Study and Scheduling

Deliver:

- Study Session,
- five-grade assessment (`Nochmal`, `Schwer`, `Unsicher`, `Gut`, `Leicht`),
- deterministic scheduling state,
- short-term relearning queue,
- due/new/relearning prioritization,
- Review history,
- stored short-text/code responses,
- reactivation behavior,
- scheduling simulations/acceptance tests.

### v0.3.0 – Interaction Modes

Deliver:

- self-assessed mental/verbal flow,
- direct selection answers,
- assistance answer choices,
- short-text answers,
- code-response editor without execution,
- Assisted/Manual evaluation modes,
- accepted short-answer comparison,
- optional hint-influence policy,
- low-interaction eligibility/filtering.

### v0.4.0 – Structured Content Acquisition

Deliver:

- prompt generator,
- Content Bundle 1.0 JSON Schema,
- create/update operations,
- stable ID updates,
- minor/semantic update semantics,
- mixed-validity preview and partial commit,
- malformed-JSON repair prompt,
- import provenance,
- local import history,
- duplicate warnings without auto-merge.

### v0.5.0 – Learning Insight

Deliver:

- Deck summaries,
- due/new/stable views,
- Suspended/Mastered management,
- Study Session history,
- Review history,
- progress dashboard,
- useful filtering/search.

### v0.6.0+ – Product Refinement

Use further `v0.y.0` releases as needed for:

- usability,
- scheduler tuning,
- backup/export improvements,
- content-management refinements,
- performance,
- migration hardening.

Do not force `v1.0.0` after a fixed number of pre-1.0 milestones.

### v1.0.0 – Coherent Independent Illumination

`v1.0.0` requires a stable independent Illumination workflow covering:

- content acquisition,
- Deck organization,
- repeated study,
- deterministic scheduling,
- progress/history,
- local authoritative persistence,
- backup/migration safety,
- acceptance-tested core invariants.

Vocation and Wiiii Got This integrations are not prerequisites for `v1.0.0`; they evolve through separate published-contract milestones when their semantics are needed.
