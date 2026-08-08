# Illumination – Architecture

## Status

Technology-neutral architecture baseline.

Programming language, UI framework, persistence technology, deployment model, and server/local topology remain open.

## 1. Architectural Goal

Preserve Illumination's domain independence while keeping the application simple enough for a personal learning product.

The architecture must support:

- independent Illumination use,
- durable learning state,
- fast study interactions,
- structured JSON import,
- later Vocation integration through published contracts,
- later Wiiii Got This capability integration,
- possible shared physical infrastructure without shared domain ownership.

## 2. Bounded Context

Illumination is one bounded context.

Nothing in the current domain model justifies splitting it into several network microservices.

Internal subdomains are modular responsibility boundaries, not deployment boundaries.

## 3. Logical Layers / Components

A practical logical decomposition is:

```text
Presentation / Client Adapters
            │
            ▼
Application Layer
            │
            ▼
Domain Model
            │
            ▼
Persistence / External Adapters
```

Additional boundaries:

```text
External ChatGPT JSON
        │
        ▼
Import Adapter / Validation
        │
        ▼
Application Layer

Vocation / Wiiii Got This
        │
        ▼
Published Contract Adapters
        │
        ▼
Application Layer
```

This is a responsibility model, not a framework prescription.

## 4. Domain Layer

Owns:

- Learning Item invariants,
- Reference Solution / Hint semantics,
- Review semantics,
- Learning Assessment semantics,
- Learning State,
- scheduling rules,
- Suspend/Mastered lifecycle behavior,
- Deck invariants where domain-relevant.

Must not depend on:

- UI framework,
- database library,
- HTTP framework,
- Vocation domain classes,
- Wiiii Got This domain classes,
- ChatGPT-specific SDKs.

## 5. Application Layer

Coordinates use cases such as:

- content creation/editing,
- deck organization,
- starting study sessions,
- revealing assistance,
- recording Reviews,
- import processing,
- querying progress,
- future integration operations.

It may define application ports/interfaces required by adapters.

## 6. Presentation

Illumination must provide at least one independently usable presentation/client for its own core workflows.

The architecture does not require this presentation to exist on every platform.

Wiiii Got This may later present Illumination capabilities on additional platforms.

The Illumination presentation technology is intentionally open.

## 7. Persistence

Illumination is authoritative for its domain state.

Authoritative learning data is stored locally on the user's device, not on a remote server.

Core Illumination operation must not require remote infrastructure.

The local store persists at least:

- Learning Items,
- Reference Solutions,
- Hints,
- Deck membership,
- Reviews,
- entered text/code responses,
- current Learning State,
- lifecycle state,
- Study Session history,
- import history.

An embedded local database is the natural persistence direction. The exact technology is selected by ADR after the application/runtime choice.

Future multi-device access must not silently move authoritative personal learning data to a remote service.

## 8. Optional Infrastructure

A personal server exists but is not required for core Illumination operation and is not the authoritative learning-data store.

It may later provide optional infrastructure such as connectivity/relay, opt-in encrypted synchronization support, opt-in backup, or deployment of integration components.

Any remote persistence of readable learning data requires a new explicit product/architecture decision.

## 9. Structured Import Boundary

Imported JSON is untrusted external input until validated.

The import boundary must:

- identify contract version,
- structurally validate,
- semantically validate,
- reject unsupported semantics,
- map boundary DTOs into Illumination application/domain operations,
- provide explicit import results.

Generated JSON types are boundary types, not domain entities.

## 10. Published Contract Boundary

Future Vocation and Wiiii Got This integrations must use explicit versioned contracts.

Published contracts:

- expose only required semantics,
- do not expose persistence schema,
- do not serialize internal domain classes by accident,
- evolve independently from internal implementation details.

## 11. Scheduling Component

Scheduling belongs to Illumination's core domain.

It may be implemented as a domain policy/component, but it is not a separate network service by default.

Its inputs conceptually include:

- previous Learning State,
- Review result,
- relevant interaction facts according to configured policy.

Its output conceptually includes:

- updated Learning State,
- next-review schedule.

The exact algorithm remains a pending domain decision.

## 12. Analytics / Learning Insight

Analytics are projections/read models over authoritative state and history.

A future statistics component may exist for performance or organization, but it must not own:

- Review history,
- current due state,
- scheduling transitions.

## 13. Configuration

Some domain/application behavior is configurable, including at least directionally:

- whether automatic evaluation is used when available,
- whether hint use influences assessment/scheduling.

Configuration scope and persistence are not yet finalized.

Do not turn configuration into global mutable flags embedded throughout the domain.

## 13A. User Model

Illumination V1 is explicitly single-user.

There is no domain `User` aggregate or multi-account model.

Any future technical device authentication is an infrastructure concern and does not imply a multi-user learning domain.

## 14. Multi-Device and Wiiii Got This Concerns

Wiiii Got This may later expose Illumination capabilities on other devices, including iPhone.

Because authoritative learning data is local, multi-device access requires an explicit future access/synchronization design.

Possible design directions include direct access to a running Illumination instance, device-to-device replication, or end-to-end encrypted relay/synchronization in which remote infrastructure cannot interpret the learning payload.

No synchronization mechanism is selected yet.

A remote server must not become a hidden requirement for ordinary Illumination use.

## 15. Technology Selection Criteria

When technology is selected, the decision must be based on Illumination requirements rather than the user's existing résumé skillset.

Relevant criteria include:

- fit for the selected client/deployment model,
- reliability of persistence,
- ease of implementing deterministic domain rules,
- JSON/schema tooling,
- testability,
- packaging/deployment,
- integration-contract support,
- maintainability,
- ecosystem maturity,
- operational complexity.

Existing personal familiarity may only be a tie-breaker between otherwise similarly suitable choices.

## 16. Architecture Decisions Still Required

Before implementation:

- five-grade scheduling semantics,
- import update identity,
- independent client/presentation model,
- language/runtime,
- persistence technology,
- automatic-evaluation mechanism and configuration scope,
- low-interaction representation,
- integration transport only when a concrete contract is required.

Each material architecture choice should receive an ADR.

## Accepted V1 Architecture Direction

Illumination V1 is:

- a local-first,
- single-user,
- installed desktop application,
- with an embedded local database,
- requiring no remote server for core operation.

Selected stack:

- C# / .NET,
- Avalonia for the desktop UI,
- SQLite for authoritative local persistence.

This selection is based on the accepted product architecture, not on the user's existing skillset.

A later Wiiii Got This adapter may expose explicit versioned contracts without making the Illumination UI itself web-based.

Docker is not required for the Illumination V1 application.

Optional future remote relay/synchronization infrastructure is a separate concern.

## Local Backup Direction

Illumination must support local backup without requiring cloud storage.

V1 direction:

- automatic rolling local backups,
- mandatory backup before database migrations,
- backup before large/structurally significant imports where practical,
- manual export/backup operation,
- configurable local backup destination,
- no automatic remote/cloud upload.

Backup retention counts and exact file format may be chosen during implementation planning.

## Future Multi-Device Requirement

Wiiii Got This should eventually make Illumination usable on iPhone even when the primary PC is off.

The intended future behavior therefore requires an iPhone-side local copy of the Illumination data needed for study.

The synchronization/replication mechanism is deliberately not specified inside Illumination V1.

Requirements for that later design:

- Illumination remains owner of learning semantics,
- remote readable storage is not assumed,
- device-local operation after synchronization is a goal,
- transport/relay/encryption are Wiiii Got This / integration architecture concerns.
