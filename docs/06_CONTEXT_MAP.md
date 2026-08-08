# Illumination – Context Map

## 1. Purpose

This document defines Illumination's relationship to surrounding bounded contexts without prescribing transport protocols, deployment topology, or implementation technology.

## 2. Illumination Bounded Context

Illumination owns the personal learning domain.

Its authoritative concepts include:

- learning content,
- reference solutions,
- hints,
- reviews,
- learning assessment,
- current learning state,
- repetition scheduling,
- user-defined decks,
- learning progress,
- Illumination-side import provenance,
- future Illumination-side external learning references.

Illumination must remain independently usable without any surrounding bounded context.

## 3. Vocation Bounded Context

Vocation owns the personal job-market domain.

Relevant Vocation-owned concepts include:

- opportunities,
- companies,
- postings and sources,
- job-market observations and assessments,
- Vocation-specific learning clusters or learning needs,
- job ranking and comparison logic.

Illumination must not duplicate these as Illumination domain entities.

## 4. Current Relationship: Illumination ↔ Vocation

### Pattern

**Separate Ways**

No integration is required for Illumination's initial independent operation.

### Reason

Both contexts have meaningful standalone value and independent ownership:

- Vocation can identify and assess job-market needs without knowing how learning content is stored.
- Illumination can teach arbitrary subjects without knowing anything about job opportunities.

Prematurely sharing entities or persistence would couple two domains whose evolution is different.

## 5. Possible Future Vocation → Illumination Relationship

A future integration may allow a Vocation-owned learning need to be referenced from Illumination.

The intended direction is conceptually:

```text
Vocation Learning Need
        │
        │ explicit published reference
        ▼
Illumination learning coverage/content
```

This does not mean that the Vocation object becomes an Illumination entity.

The reference should contain only the identity and descriptive information explicitly required by the integration contract.

### Possible future use

A learner may choose to create or associate Illumination content with a Vocation learning need.

One Vocation learning need may correspond to:

- no Illumination content,
- one Illumination deck,
- several decks,
- individual learning items distributed across decks.

No one-to-one mapping is assumed.

### Reference attachment

The future Vocation external learning reference attaches to relevant Illumination Learning Items, independently of Deck membership.

A Deck is never used as the identity of a Vocation learning cluster.

One external reference may cover Learning Items distributed across any number of Decks.

## 6. Possible Future Illumination → Vocation Read Relationship

Illumination may later expose a bounded learning-coverage read contract.

Vocation may consume only an aggregated learning-coverage summary such as:

- number of associated Learning Items,
- new count,
- due count,
- active/stable count according to Illumination semantics,
- last learning activity,
- last content addition/change.

Individual Reviews and Reference Solutions are not part of this Vocation read contract.

The exact aggregation semantics are unresolved.

### Ownership rule

Vocation may use the returned summary inside its own job-assessment logic.

That does not give Vocation ownership of:

- Illumination learning items,
- review history,
- scheduling state,
- decks.

## 7. Anti-Corruption Requirements: Illumination ↔ Vocation

Any future integration must preserve each context's own language.

Illumination must not require internal domain classes such as:

- Opportunity,
- Company,
- VocationLearningCluster.

Vocation must not require internal Illumination domain classes such as:

- LearningItem,
- Review,
- LearningState,
- Deck.

Published DTOs/contracts may exist, but they are boundary types rather than shared domain entities.

## 8. Wiiii Got This Bounded Context

Wiiii Got This owns cross-application platform and integration concerns.

Its responsibility includes conceptually:

- devices,
- platforms,
- service discovery and registration,
- capabilities,
- availability,
- integration,
- device/platform-dependent presentation.

It does not own Illumination learning semantics.

## 9. Illumination → Wiiii Got This Relationship

Illumination may later act as a provider of explicit capabilities and read/command contracts.

Conceptually:

```text
Illumination
    │
    │ Published Capabilities / Contracts
    ▼
Wiiii Got This
    │
    ├── platform-specific presentation
    ├── device-specific presentation
    └── orchestration/integration
```

The internal implementation technology of Illumination must not leak into this contract.

## 10. Independent Usability

Illumination's independent usability means:

- it can provide its own core learning workflows without Wiiii Got This,
- its domain state remains authoritative within Illumination,
- Wiiii Got This is not required for the existence of learning items, reviews, scheduling, or decks.

Independent usability does **not** mean:

- Illumination must implement a native client for every platform,
- Illumination must duplicate Wiiii Got This platform abstractions,
- Illumination must own device discovery or platform presentation.

## 11. Presentation Ownership

Illumination owns the semantics of learning interactions.

Wiiii Got This may own platform-specific presentation of published capabilities.

The exact UI integration model is not decided.

Possible future mechanisms must not be selected in this document.

In particular, the context map does not decide between:

- data-driven presentation by Wiiii Got This,
- declarative service-provided UI descriptions,
- embedded portable UI surfaces,
- other capability presentation mechanisms.

That decision belongs to Wiiii Got This architecture and published-capability design.

## 12. Physical Infrastructure

Bounded-context separation does not require separate physical machines or database servers.

Future deployment may share:

- a physical server,
- a database engine,
- backup infrastructure,
- network infrastructure.

However, shared infrastructure must not imply shared domain ownership.

Forbidden coupling includes:

- one context directly updating another context's tables,
- shared persistence models used as integration contracts,
- foreign domain entities imported directly into another context,
- shared business-logic libraries that bypass published contracts.

## 13. External ChatGPT Interaction

ChatGPT is initially an external content-generation participant, not a bounded context owned by Illumination.

The interaction is conceptually:

```text
Illumination Prompt Generator
        ↓
user-mediated external ChatGPT interaction
        ↓
versioned structured output
        ↓
Illumination Import Boundary
```

Illumination must validate imported content before accepting it.

The external generator does not become the source of truth for imported learning content.

## 14. Context Map Summary

```text
┌────────────────────┐
│      Vocation       │
│                    │
│ job market         │
│ learning needs     │
└─────────┬──────────┘
          │
          │ future explicit Learning Reference /
          │ bounded coverage read contract
          │
          ▼
┌────────────────────┐
│   Illumination      │
│                    │
│ learning content   │
│ reviews            │
│ scheduling         │
│ progress           │
└─────────┬──────────┘
          │
          │ future published capabilities /
          │ read & command contracts
          │
          ▼
┌────────────────────┐
│ Wiiii Got This     │
│                    │
│ devices/platforms  │
│ integration        │
│ presentation       │
└────────────────────┘
```

Current state:

- Vocation ↔ Illumination: Separate Ways.
- Illumination ↔ Wiiii Got This: no concrete contract yet.
- ChatGPT content generation: external, user-mediated import workflow.

## 15. Contract Gate

No public integration schema should be authored until its semantics are required by concrete application scenarios.

In particular, do not create a Vocation learning-reference contract merely to reserve fields.

The first contract should be designed only when identity, cardinality, ownership, and required read semantics are explicitly decided.
