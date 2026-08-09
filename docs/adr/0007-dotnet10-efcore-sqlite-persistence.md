# ADR-0007: .NET 10, EF Core, and SQLite Persistence Baseline

- Status: Partially superseded by ADR-0009
- Date: 2026-08-09

## Context

Illumination V1 is a local-first executable capability runtime. Authoritative learning data must remain local and core operation must not require a remote server.

## Decision

Use C# with .NET 10 LTS for the application, SQLite for authoritative local persistence, and EF Core with the SQLite provider as the persistence infrastructure. EF Core types and configuration remain outside the domain layer.

## Consequences

- The V1 deployable is a local capability runtime with no mandatory server; Wiiii Got This may host it as the primary presentation.
- SQLite migrations and backup safeguards are part of the persistence implementation.
- Docker is not part of the core V1 deployment.
- Database concerns do not become domain concepts.
