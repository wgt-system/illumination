# Illumination – Architecture

## Status

Accepted V1 architecture baseline: local-first single-user executable capability runtime using .NET 10 LTS, SQLite, and EF Core. Wiiii Got This is the primary containing product for the integrated system and may host Illumination through explicit provider-owned presentation/application boundaries; Illumination retains ownership of learning semantics, workflows, local persistence, scheduling state, and provider-specific consumer presentation. Core local operation requires no server; optional server, Docker, or Conveyance-backed delivery infrastructure may support connectivity or future synchronization.

The existing Avalonia Desktop application remains the standalone/admin/dev/acceptance host for v0.9. A reusable Illumination-owned Product Surface for WGT/standalone reuse is tracked by #54 and must not expose Illumination Domain objects or SQLite handles to WGT. The current function-rich Desktop information architecture is not the final production UX baseline.

## 1. Architectural Goal

Preserve Illumination's domain independence while keeping the application simple enough for a personal learning product.

The architecture must support:

- independent Illumination use,
- durable learning state,
- fast study interactions,
- structured JSON import,
- later Vocation integration through published contracts,
- Wiiii Got This hosting/composition without domain leakage,
- possible shared physical infrastructure without shared domain ownership.

## 2. Bounded Context

Illumination is one bounded context.

Nothing in the current domain model justifies splitting it into several network microservices.

Internal subdomains are modular responsibility boundaries, not deployment boundaries.

## 3. Logical Layers / Components

A practical logical decomposition is:

```text
Provider Presentation / Client Adapters
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

Wiiii Got This host
        │
        ▼
Illumination-owned presentation/application boundary
        │
        ▼
Application Layer

Vocation
        │
        ▼
Published Contract Adapter when needed
        │
        ▼
Application Layer
```

This is a responsibility model implemented within the Illumination executable capability runtime.

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
- Deck organization,
- starting Study Sessions,
- revealing assistance,
- recording Reviews,
- non-destructive Practice now and explicit Restart learning,
- deterministic learning-generation profile derivation,
- Application-owned external-generation prompt composition,
- Content Bundle validation/import,
- querying Learning Insight,
- future integration operations.

It may define application ports/interfaces required by adapters.

## 6. Presentation

Illumination owns the presentation semantics of its substantial learning workflows. Wiiii Got This owns containing-product navigation, host chrome, lifecycle/platform integration, transitions, global product composition, and genuine cross-service compositions.

Wiiii Got This must not reimplement Illumination business semantics merely to host the product, use Illumination Domain objects directly, or read/write Illumination SQLite.

The v0.9 Avalonia Desktop application remains a standalone/admin/dev/acceptance host. Its current function-inventory layout is not the target production information architecture.

A reusable provider-owned Product Surface is the post-v0.9 direction tracked by #54. The service-local extraction/packaging details remain subject to the corresponding architecture/acceptance gate; no universal plugin/UI protocol is implied.

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
- entered text/code responses where retained,
- current Learning State,
- lifecycle state,
- Study Session history,
- import history.

Persistence uses SQLite through EF Core's SQLite provider. EF Core remains an infrastructure concern and is not part of the domain model.

Future multi-device access must not silently move authoritative personal learning data to a remote service.

## 8. Optional Infrastructure

A personal server exists but is not required for core Illumination operation and is not the authoritative learning-data store.

It may later provide optional infrastructure such as connectivity, opt-in backup, or deployment of integration components. Generic durable opaque cross-device delivery belongs to Conveyance; any Illumination-specific synchronization payload and semantics remain Illumination-owned.

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

Generated JSON types are boundary types, not domain entities. Content Bundle 1.0 remains the v0.9 validated generation/import contract.

## 10. Published Contract / Host Boundary

Future Vocation integrations use explicit versioned published contracts when concrete semantics require them.

WGT hosting may use a provider-specific presentation/runtime boundary at the outer composition layer. That boundary:

- exposes only required semantics/artifacts,
- does not expose persistence schema,
- does not serialize or import internal Domain classes by accident,
- does not hand WGT a DbContext or SQLite handle,
- remains provider-specific unless a later system Architecture decision establishes a common contract.

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

The deterministic scheduling semantics are defined in `docs/08A_SCHEDULING_SEMANTICS.md`.

`Practice now` does not mutate authoritative Learning State merely to make material immediately available. `Restart learning` is an explicit authoritative reset of current scheduling state while preserving immutable Review history and Deck membership.

## 12. Analytics / Learning Insight

Learning Insight is a projection/read model over authoritative state and history.

It must not own:

- Review history,
- current due state,
- scheduling transitions.

v0.9 uses derived learning evidence to build deterministic generation guidance rather than asking an external LLM to infer learning state from raw scheduler internals. Richer evidence-based Learning Analytics remains post-v0.9 work.

## 13. Configuration

Some domain/application behavior is configurable, including at least directionally:

- whether automatic evaluation is used when available,
- whether hint use influences assessment/scheduling,
- generation language/proficiency/exercise constraints where applicable.

Do not turn configuration into global mutable flags embedded throughout the domain.

## 13A. User Model

Illumination V1 is explicitly single-user.

There is no domain `User` aggregate or multi-account model.

Any future technical device authentication is an infrastructure concern and does not imply a multi-user learning domain.

## 14. Multi-Device and Wiiii Got This Concerns

Wiiii Got This may host Illumination across multiple platform families through provider-owned presentation/runtime integration.

Because authoritative learning data is local, multi-device access still requires an explicit future access/synchronization design.

Possible design directions include device-to-device replication or opaque protected delivery through an accepted Conveyance mode in which remote infrastructure cannot interpret the learning payload. These are possibilities only; no concrete mechanism is selected.

A remote server must not become a hidden requirement for ordinary Illumination use.

## 15. Technology Selection Criteria

The selected V1 technology is based on Illumination requirements rather than the user's existing résumé skillset.

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

## 16. Remaining Architecture Scope

The V1 implementation gate is satisfied. Future decisions include concrete Product Surface packaging/platform evidence, integration transport when a published contract is required, and synchronization semantics when multi-device learning state becomes an implemented requirement.

## Accepted V1 Architecture Direction

Illumination V1 runtime is:

- local-first,
- single-user,
- an executable capability runtime,
- with embedded local authoritative persistence,
- requiring no remote server for core operation.

Selected stack:

- C# / .NET 10 LTS,
- SQLite for authoritative local persistence,
- EF Core with the SQLite provider for persistence infrastructure,
- Avalonia/CommunityToolkit.Mvvm for the current Desktop/provider-presentation direction.

Tests use xUnit.net v3. Time-dependent domain tests use a controllable `TimeProvider`/clock abstraction.

Wiiii Got This may host the provider-owned presentation/runtime boundary without acquiring learning-domain ownership.

Docker, server, or relay infrastructure is optional and must not become mandatory for core local operation.

Optional future delivery/synchronization infrastructure is a separate concern. Conveyance owns generic durable opaque cross-device delivery; Illumination owns the learning-domain meaning of any future synchronized changes.

## Local Backup Direction

Illumination supports local backup without requiring cloud storage.

Current direction:

- automatic rolling local backups,
- mandatory backup before database migrations,
- backup before Content Bundle commit / structurally significant imports,
- manual backup/export operation,
- configurable local rolling-backup destination,
- no automatic remote/cloud upload.

## Future Multi-Device Requirement

The intended future product should allow device-local Illumination use across supported WGT platform families rather than requiring the primary PC to remain online.

The synchronization/replication mechanism is deliberately not specified inside the current release.

Requirements for that later design:

- Illumination remains owner of learning semantics,
- remote readable storage is not assumed,
- device-local operation after synchronization is a goal,
- Illumination must first define future domain-specific publication, change, command, authority, merge, conflict, and reconciliation semantics,
- an accepted Conveyance delivery mode may transport the resulting opaque information only after those semantics are defined.
