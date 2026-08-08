# Illumination – Subdomains

## 1. Purpose

This document identifies domain responsibilities without turning them into network services or implementation modules prematurely.

A subdomain is a responsibility boundary, not a deployment boundary.

## 2. Core Domain: Review and Retention

### Responsibility

Manage repeated learning interactions and evolve each learning unit's learning state so the learner spends attention primarily where it is needed.

### Includes

- review lifecycle,
- learning assessment,
- review history,
- current learning state,
- repetition scheduling,
- due/eligible review behavior,
- interaction with hints and solution reveal where those actions influence the review.

### Why this is core

The primary product value is not merely storing questions.

The distinctive problem is:

> Given a body of learning material and prior learner performance, what should be reviewed now, how should that review be captured, and when should the material return?

Without this behavior, Illumination becomes a simple flashcard/content store.

## 3. Supporting Subdomain: Learning Content

### Responsibility

Own the reusable learning material that is reviewed.

### Includes conceptually

- prompts/tasks/questions,
- reference solutions,
- hints,
- interaction-form configuration,
- content metadata required for learning,
- activation/retirement state where later required.

### Notes

Learning Content supports the core Review and Retention domain.

The exact learning-item taxonomy is unresolved.

## 4. Supporting Subdomain: Deck Organization

### Responsibility

Allow the learner to organize learning material into freely defined decks.

### Includes

- deck creation,
- naming,
- membership,
- ordering or manual organization if later required,
- selecting a deck as a study scope.

### Important boundary

Decks are not a mandatory ontology or taxonomy.

They must not become the owner of item learning state.

## 5. Supporting Subdomain: Content Acquisition

### Responsibility

Bring new or updated learning material into Illumination efficiently.

### Includes directionally

- prompt generation for external ChatGPT use,
- versioned structured import,
- validation,
- import provenance,
- import reports,
- future update/merge semantics.

### Important boundary

External ChatGPT is not part of the domain model.

After successful import, the resulting learning content is Illumination-owned.

## 6. Supporting Subdomain: Learning Insight

### Responsibility

Provide useful views over learning state and history.

### Includes directionally

- counts of new items,
- due items,
- unstable/difficult items,
- long-term stable items,
- review history summaries,
- deck-scoped progress views,
- later trend analysis.

### Important boundary

Learning Insight derives information from authoritative learning state.

It does not own scheduling or mutate learning state independently.

A future analytics component or service must not become the owner of core review state.

## 7. Supporting Subdomain: External Learning References

### Responsibility

Relate Illumination learning coverage to learning needs owned by other bounded contexts without importing those foreign domains.

### Initial external context

Vocation.

### Possible responsibilities

- store or resolve explicit external learning references,
- associate Illumination content with an external learning need,
- derive bounded coverage summaries,
- publish those summaries through versioned contracts.

### Status

This subdomain is intentionally weakly specified because the actual Vocation-to-Illumination semantics have not yet been decided.

It must not be elaborated further without explicit product decisions.

## 8. Generic / Infrastructure Concerns

The following are not currently treated as product subdomains:

- persistence technology,
- database hosting,
- transport protocol,
- authentication,
- HTTP,
- local process boundaries,
- server deployment,
- synchronization mechanism,
- mobile presentation,
- logging,
- backup,
- serialization library.

These are architecture or infrastructure concerns unless later product semantics make them domain-relevant.

## 9. Not Separate Subdomains At This Stage

The following must not be split out merely because they are conceptually distinguishable:

### Scheduling service

Scheduling is part of the Review and Retention core domain unless later evidence proves otherwise.

### Statistics service

Statistics/analytics are derived capabilities. They do not justify a network microservice by themselves.

### Hint service

Hints are learning content and review interaction, not an independent product area.

### Question service / Deck service / User service

No generic domain services of this kind should be invented.

## 10. Current Context Shape

Illumination is currently one bounded context with several internal domain responsibilities:

```text
Illumination
├── Core: Review and Retention
├── Supporting: Learning Content
├── Supporting: Deck Organization
├── Supporting: Content Acquisition
├── Supporting: Learning Insight
└── Supporting: External Learning References
```

This diagram does not imply separate processes, repositories, databases, or deployables.

## 11. Boundary With Vocation

Vocation owns:

- opportunities,
- companies,
- job-market evidence,
- job assessment,
- Vocation learning clusters / learning needs.

Illumination owns:

- learning content,
- reviews,
- learning state,
- scheduling,
- decks,
- learning progress.

The contexts must communicate only through explicit published contracts when integration is introduced.

## 12. Boundary With Wiiii Got This

Wiiii Got This owns:

- devices,
- platforms,
- service discovery/registration,
- service availability,
- generic capability integration,
- platform/device-dependent presentation.

Illumination may expose capabilities.

Illumination remains the authority for learning semantics and learning state.

## 13. Current Uncertainty

The following may affect subdomain boundaries later:

- whether imported content updates require a substantial content-versioning model,
- whether external learning references become important enough to deserve richer modeling,
- whether learner-response evaluation becomes a substantial automated domain concern,
- whether multi-device operation introduces domain-significant conflict semantics rather than mere synchronization infrastructure.

No new subdomain should be introduced until these concerns become concrete.
