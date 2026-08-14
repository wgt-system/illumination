# AGENTS.md

## Project name

The project name is **Illumination**.

Do not introduce or use another project name or historical alias.

## Purpose

This repository contains Illumination, an independent bounded context for personal learning.

The accepted V1 specification and implementation gate are coherent; implementation work may proceed only within the documented contracts and architecture.

## Source of truth

Repository documentation is the durable source of truth.

Before proposing or implementing changes:

1. read `README.md`,
2. read the relevant files in `docs/`,
3. identify explicit decisions and explicitly open questions,
4. do not infer product behavior from analogies such as Anki unless the documentation states it.

## WGT System Architecture

The system-level architecture source of truth is `wgt-system/architecture`. Before introducing cross-context integration, synchronization or replication, generic relay/storage infrastructure, shared infrastructure, or another system-wide capability, consult its Capability Catalog and Integration Policy.

Generic durable opaque cross-device delivery is owned by Conveyance. Wiiii Got This owns device/platform integration and presentation. Illumination remains authoritative for Learning-domain semantics and for future Illumination-specific publication, command, authority, merge, conflict, and reconciliation semantics. If an existing generic capability is conceptually correct but insufficient, return the requirement to the System Architecture Control Plane rather than creating a competing subsystem. Runtime code must not depend on the architecture repository.

## Product boundaries

Illumination owns:

- learning content,
- learning interactions,
- reference solutions,
- optional hints,
- user-defined decks,
- review history,
- repetition state,
- learning progress.

Illumination does not own:

- Vocation opportunities,
- job-market research,
- Vocation learning clusters as Vocation domain objects,
- devices,
- platform discovery,
- generic service registration,
- cross-application orchestration.

## Integration rules

Illumination is a separate bounded context from Vocation and Wiiii Got This.

Do not introduce:

- shared domain entities,
- direct imports of foreign domain classes,
- shared business-logic libraries that hide coupling,
- direct persistence access across bounded contexts.

Integration must use explicit published contracts.

Shared physical infrastructure is not automatically forbidden, but domain ownership must remain separate.

## Architecture discipline

Do not invent:

- a technology stack before the architecture documents justify it,
- microservices merely because subdomains exist,
- a server boundary merely because a server is available,
- a local-only architecture merely because the application can run locally,
- mobile-specific architecture merely because Wiiii Got This may later expose the service on mobile.

A bounded context does not need multiple network services internally.

## Working terminology

`Learning Item` is the canonical term for the smallest independently reviewable unit.
`Deck` is the canonical term for a user-defined grouping of Learning Items.

## Implementation gate

The implementation gate is satisfied for the current V1 baseline. Keep the following sources coherent as implementation proceeds:

- Domain Vision
- Scenarios
- Ubiquitous Language
- Subdomains
- Domain Model
- Context Map
- Application Design
- required import/published contracts
- Read Models
- Architecture
- Acceptance Tests
- implementation plan
- ADRs for actual architecture decisions

## Agent behavior

Agents must not spawn subagents, delegates, or explorers unless the control-plane prompt explicitly authorizes it.

When a product decision is genuinely missing:

- do not guess,
- record the open decision,
- continue with independent specification work where possible,
- stop only when further progress would require choosing among materially different product semantics.

Codex/Luna should later receive narrow implementation tasks only after the relevant contracts and acceptance criteria exist.

## Data locality

Authoritative learning data is local-first.

Do not introduce a remote authoritative database or mandatory server dependency without an explicit new architecture decision.

A local browser UI is not equivalent to a remotely hosted application.
