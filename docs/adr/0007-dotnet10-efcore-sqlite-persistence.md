# ADR-0007: .NET 10, EF Core, and SQLite Persistence Baseline

- Status: Partially superseded by ADR-0009
- Date: 2026-08-09

Supersession note: ADR-0009 replaces the assumption that the V1 deployable's primary presentation is a standalone local desktop application; the .NET 10, SQLite, EF Core, migration, backup, and local-first decisions remain valid.

## Context

Illumination V1 is an installed, local-first desktop application. Authoritative learning data must remain local and core operation must not require a remote server.

## Decision

Use C# with .NET 10 LTS for the application, SQLite for authoritative local persistence, and EF Core with the SQLite provider as the persistence infrastructure. EF Core types and configuration remain outside the domain layer.

## Consequences

- The V1 deployable is a local desktop application with no mandatory server.
- SQLite migrations and backup safeguards are part of the persistence implementation.
- Docker is not part of the core V1 deployment.
- Database concerns do not become domain concepts.
