# Illumination – Implementation Plan

## Status

Implementation baseline through **v0.9.0**.

The accepted V1 technology/domain baseline is stable enough for continued vertical-slice development. Pre-1.0 work is driven by concrete product/reliability/usability evidence rather than an artificial countdown to v1.0.

## 1. Release Model

Use semantic-style versions:

```text
vMAJOR.MINOR.PATCH
```

Pre-1.0 feature releases continue numerically:

```text
v0.8.0
v0.9.0
v0.10.0
v0.11.0
...
```

Minor versions are integers, not decimal fractions. `v0.10.0` follows `v0.9.0`.

`v1.0.0` is reserved for a deliberately accepted coherent first major product baseline. It is not inferred from the previous minor number.

Patch releases may use the third component when needed for compatible fixes/hardening.

## 2. Development and Release Branches

- `dev` is the normal integration branch.
- `main` contains stable reviewed milestone releases.
- release preparation branches use `release/vX.Y.Z` when a candidate has passed product acceptance and needs documentation/final release validation.
- stable releases are tagged `vX.Y.Z` on the exact stable commit.

Do not mix unrelated next-milestone product work into an accepted release candidate.

## 3. Architectural Baseline

Illumination remains one local-first, single-user bounded context using:

- C# / .NET 10 LTS;
- SQLite authoritative local persistence;
- EF Core SQLite infrastructure;
- Avalonia/CommunityToolkit.Mvvm for the current provider-presentation/Desktop direction;
- xUnit v3 for tests.

Wiiii Got This is the containing product/system hub. Illumination retains ownership of learning semantics, workflows, local persistence/scheduling, and substantial provider-specific consumer presentation.

A reusable provider-owned Product Surface is post-v0.9 integration/product work. It must not cause WGT Domain/Application to import Illumination Domain objects or read/write Illumination SQLite, and it must not silently become a generic plugin protocol.

## 4. Implementation Strategy

Prefer **vertical product slices** over isolated layer-by-layer or Issue-by-Issue work.

A useful slice may advance several Issues at once when they share one coherent end-to-end workflow. Issues remain acceptance/ownership boundaries; they do not require artificial serial implementation.

Priority order for validation:

1. domain invariants;
2. Application use cases/projections;
3. persistence/migration behavior;
4. presentation workflow;
5. end-to-end/manual product behavior where needed;
6. cross-repository host integration only after provider behavior is reviewed.

Parallel work is appropriate only where file/contract overlap is low and accepted boundaries are clear.

## 5. Historical Pre-1.0 Milestones

### v0.1.0 – Local Content Foundation (released)

Delivered:

- solution/layer baseline;
- Learning Item and Deck domain;
- SQLite persistence/migrations;
- content-management Application behavior;
- initial Desktop/admin/dev capability surface;
- backup/hardening baseline.

### v0.2.0 – Study and Scheduling (released)

Delivered:

- Study Session;
- five-grade assessment (`Nochmal`, `Schwer`, `Unsicher`, `Gut`, `Leicht`);
- deterministic Learning State/scheduling;
- durable relearning priority with session-local queue behavior;
- due/new/relearning prioritization;
- multiple selected Decks with set-union deduplication;
- default 20-new-item limit plus explicit override/all-new;
- immutable Review history;
- lifecycle reactivation/unmastering semantics.

### v0.3.0 – Study Refinement and Content Acquisition (released)

Delivered:

- exact learning-stack reinsertion semantics;
- deterministic assessment previews;
- Study queue transparency;
- Content Bundle 1.0 generation/parse/validation;
- mixed-validity preview;
- atomic accepted-subset commit;
- `localRef` dependency resolution;
- explicit update significance;
- malformed-JSON repair prompts;
- import results/provenance.

### v0.4.0 – Content Quality and Curation (released)

Delivered:

- User Flags;
- immutable Quality Reviews;
- content revision binding;
- derived current quality state;
- Standard/Strict/SourceGrounded review/generation quality concepts;
- structured quality-review exchange;
- explicit review acceptance/correction application.

### v0.5.0 – Interaction Modes and Evaluation (released)

Delivered:

- `SelfAssessed`, `Selection`, `ShortText`, `Code` interaction flows;
- Manual/Assisted evaluation;
- exact multiple-choice comparison;
- conservative accepted-short-answer matching;
- progressive hints/assistance;
- optional assistance influence on suggested grade;
- durable interaction facts on Review;
- low-interaction filtering.

Not delivered:

- arbitrary code execution;
- fuzzy/semantic answer grading;
- direct paid LLM API;
- scheduler redesign.

### v0.6.0 – Learning Insight (released)

Delivered:

- Deck summaries;
- due/new/relearning views;
- lifecycle management;
- Study Session/Review history;
- filtering/search;
- `DeckLearningContext` foundation;
- existing-Deck follow-up generation;
- initial `Reinforce`/`Continue`/`Advance` generation intents;
- generation ResponseMode/language controls;
- shell/page structure.

Learning Insight remains derived from authoritative state/history; it is not a second source of truth and does not create a synthetic mastery score.

### v0.7.0 – Product Refinement (released)

Delivered:

- richer Content Bundle generation/import previews;
- language/follow-up generation controls;
- content-improvement prompts;
- Deck export/direct actions;
- bulk curation and Study flags;
- focus-aware Study shortcuts;
- Insight activity/due forecasts;
- local backup/export controls;
- migration hardening;
- repository/C4 maintenance.

Rejected from stable scope:

- invented persisted resume/finish semantics for unfinished Study Sessions;
- staged replacement of authoritative SQLite as a casual UI workflow.

### v0.8.0 – Local Data Reliability & Runtime Coherence (released)

Delivered:

- fresh Insight projection when opened after Study changes;
- change-aware automatic rolling SQLite snapshots;
- five-snapshot retention;
- backup-before-migration;
- backup-before-Content-Bundle commit;
- persistent configurable local backup location;
- explicit local DB/backup-path presentation;
- manual backup and portable export.

Not delivered:

- authoritative database restore/replacement;
- cloud backup;
- remote readable persistence;
- synchronization semantics;
- speculative WGT/Vocation contracts.

### v0.9.0 – Product Usability & Learning-Aware Generation (released)

v0.9 is accepted after real-use testing of the frozen candidate and final Release validation.

Delivered:

- coherent Deck projections and deletion behavior across product reads;
- clearer Learning Item authoring with direct Deck assignment;
- CEFR A1–C2 language-generation controls;
- explicit language exercise profiles;
- controlled progression and existing-content anti-duplication;
- non-destructive **Practice now**;
- explicit authoritative **Restart learning** for item/Deck scheduling reset while preserving immutable history/membership;
- real SQLite/Desktop restart verification and scheduler/new-card-limit explanation;
- deterministic Application-owned Learning Generation Profile/Brief derived from current learning evidence;
- Application-owned semantic prompt composition;
- fresh learning evidence automatically used by Existing Deck generation;
- Content Bundle 1.0 retained as the validated output/import boundary;
- rationalized Study/Library/Generate/Decks/Insights shell hierarchy;
- clearer first-use/filter/empty states;
- Desktop runtime/API warning hardening.

v0.9 explicitly does **not** declare the current standalone/admin/dev Desktop information architecture the final production UX.

## 6. Accepted v0.9 Product Semantics

### Practice now

Practice is immediate access without rewriting authoritative scheduling merely to make an item due.

### Restart learning

Restart is an explicit destructive-to-current-scheduling action, not a history deletion. It resets current scheduling to the accepted new-learning baseline while preserving immutable Review history and membership. Normal Study new-item limits still apply after Deck restart.

### Learning-aware generation

Generation uses a deterministic Application-owned summary of decision-relevant evidence rather than raw unbounded scheduler dumps. The external LLM remains a content generator, not the authority for Learning State or mastery.

### Progression intent

`Reinforce`, `Continue`, and `Advance` are v0.9 generation intents, not scheduler states. Their production UI wording may be replaced post-v0.9 with clearer goal language without changing the underlying accepted distinction.

## 7. Post-v0.9 Product Line

The following items are active **post-v0.9 planning/implementation work**. They are not retroactive v0.9 release scope and should be implemented as coherent vertical slices rather than one isolated Issue at a time.

### Product Surface extraction / WGT integration — #54

Goal:

- replay the reusable Illumination-owned Product Surface from the accepted v0.9 baseline;
- preserve current Study/Decks/Library/Generate/Insights semantics;
- keep the standalone Desktop host as optional admin/dev/acceptance host;
- host the same provider-owned consumer surface from WGT;
- keep Illumination Domain/persistence private;
- repin WGT only to a reviewed provider revision;
- physically validate WGT-hosted Windows entry, substantive workflows and return to Atlas.

Do not merge the old stale Product Surface branch wholesale over v0.9.

### Flexible Deck facets / learning profiles — #69

Goal:

Separate:

1. user-owned topic/subject labels (zero or more per Deck);
2. learning/exercise profiles such as language, general recall, coding/problem-solving, geospatial;
3. observed learning evidence derived from Reviews/Learning State/sessions.

Avoid a mandatory single Deck `Category` enum unless a real invariant later requires it.

### Evidence-based Learning Analytics — #70

Goal:

Build inspectable metrics over authoritative history/state, including candidates such as:

- acquisition effort/time;
- retention/forgetting behavior;
- stability growth;
- lapse/relearning rates;
- normalized response-latency trends;
- assistance/hint dependence;
- calibration between deterministic correctness evidence and learner-confirmed assessment;
- weak/relearning versus established/new material;
- scope comparisons by Deck/topic/profile/time.

Before locking formulas, review current spaced-retrieval, forgetting-model, response-time/knowledge-tracing and metacognitive-calibration literature. Distinguish evidence-backed measures from exploratory product metrics. Do not fabricate population-level psychometric validity from one user's sparse local data.

The resulting deterministic summaries should feed future content generation without making ChatGPT authoritative for learning state.

### Goal-first Create / Extend workflow — #71

Goal:

Separate the normal user intent into:

- create new learning material;
- extend an existing Deck using current learning evidence and duplicate-avoidance context.

Replace ambiguous normal-UI progression vocabulary with clear goals such as:

- practice weak areas;
- add new material at this level;
- progress to harder material.

Profile-specific configuration must reveal only relevant controls.

Generated batch review must be bounded:

- review cards one-by-one when desired;
- trust/accept a validated batch directly;
- optionally review only deterministic problem cases;
- never require scrolling through an arbitrarily long generated-card list to reach the finish action.

### Focused production Product Surface — #72

Goal:

Design the real consumer UI around progressive disclosure rather than the current function inventory.

Candidate top-level product destinations:

- Study;
- Library;
- Create/Extend;
- Insights.

Deck management may become contextual inside Library if workflow testing supports it.

Explore compact circular **icon-only** top-right navigation/actions with:

- no permanent text inside normal circles;
- hover/focus labels;
- accessible names;
- touch-safe discovery and hit targets;
- clear active state;
- no requirement to force every action into a circle.

Study becomes a focus mode with minimal chrome and content centered.

Assessment policy should evolve toward:

1. Manual;
2. Assisted;
3. Automatic where deterministic evaluation is genuinely supported, with immediate undo/override.

Unsupported auto-grading must fall back to learner control rather than inventing correctness.

### Learning-generation refinement — #56 follow-on relationship

The v0.9 deterministic Learning Generation Profile is shipped. Post-v0.9 Analytics/facets/Create-Extend work may enrich the bounded generation brief with better evidence and workflow semantics without introducing an opaque mastery score or moving prompt semantics back into Desktop presentation code.

### Orientation geospatial capability

Future geospatial learning exercises should consume an accepted Orientation-owned generic geospatial/map capability through explicit architecture boundaries.

Illumination should not clone Orientation's map stack or treat map capability as Deck identity. Language + geography and similar composed exercise profiles should remain possible.

### Coding/problem-solving profile

Coding must be modeled as its own learning/evaluation profile rather than a stray ResponseMode mixed into language-learning configuration.

Do not imply arbitrary code execution/automatic grading before a specific safe deterministic evaluator/runtime policy is accepted.

## 8. Product UX Working Principles

These are post-v0.9 design principles to validate in implementation, not excuses to remove functionality.

- one dominant task/state per view;
- progressive disclosure of contextual/advanced actions;
- bounded dynamic-result regions;
- primary actions remain visible/reachable independent of result count;
- explicit workflow transitions such as configure → act → review → finish;
- minimal persistent chrome during focused Study;
- mouse, keyboard, touch and accessibility remain first-class;
- admin/dev/diagnostic controls stay separable from the normal consumer path.

## 9. Learning Analytics / Generation Data Rule

Authoritative facts remain:

- Learning Items/content revision;
- Deck membership;
- Reviews and confirmed final assessments;
- Study Sessions;
- current Learning State/lifecycle;
- explicit interaction facts such as correctness/hint use/response payload/timing when validly captured.

Analytics and generation profiles are deterministic projections over those facts. They must expose definitions/units, handle sparse data honestly, and never become a hidden competing source of truth.

## 10. Testing Strategy

Every new slice should cover, as applicable:

- deterministic Domain tests;
- Application projection/use-case tests;
- SQLite migration/integration tests;
- prompt/contract tests;
- presentation-state tests;
- physical Desktop/WGT smoke where framework/host behavior cannot be proved in unit tests;
- vulnerability audit before stable releases.

Time-dependent tests use controlled time.

## 11. Codex/Luna Use

Codex/Luna implementation work should receive:

- stable acceptance direction;
- clear architectural ownership;
- known module/file scope where possible;
- explicit non-goals;
- tests/validation expectations.

Do not create branch/Issue churn merely to simulate progress. Parallelize coherent independent slices, and coordinate shared Application/presentation contracts explicitly.

## 12. No Premature Microservices or Plugin Protocol

Milestones describe capabilities, not deployables.

Do not map Scheduling, Analytics, Decks, Import, generation profiles, or Product Surface subareas to separate network services without an actual architectural reason.

Do not generalize one provider-specific Product Surface integration into a universal downloadable plugin/UI protocol without a separate accepted system Architecture decision.

## 13. v1.0 Direction

`v1.0.0` requires an intentionally accepted complete independent Illumination product baseline, including at least:

- content acquisition;
- Deck/content organization;
- repeated Study;
- deterministic scheduling;
- progress/history and useful learning analysis;
- local authoritative persistence;
- backup/migration safety;
- coherent production UX;
- acceptance-tested invariants.

These are necessary but not automatically sufficient. WGT/Vocation integration is not a prerequisite for v1.0 unless concrete product readiness later makes it one.

## 14. Immediate Next Step after v0.9

Proceed from the released v0.9 baseline with the Product Surface/production-UX line and related Learning Intelligence work as vertical slices. The current highest-leverage sequence is:

1. replay #54 from v0.9 and establish the clean reusable provider surface;
2. implement an initial production shell/navigation + Create/Extend slice spanning #71/#72;
3. introduce #69 facets/profile foundations where required by Create/Analytics;
4. build #70 Analytics projections and connect them to generation briefs;
5. add provider/capability-specific exercise profiles such as Coding and Orientation-backed geospatial work only through accepted boundaries;
6. repin/rebuild WGT against reviewed provider revisions as substantial Illumination Product Surface slices land.

This sequence is directional, not a requirement to finish each Issue completely before work on the next one begins.
