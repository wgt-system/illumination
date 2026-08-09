# ADR-0003: Learning Data Is Local-First

- Status: Accepted
- Date: 2026-08-08

## Context

Illumination stores personal learning content, entered responses, Review history, scheduling state, progress, and import history.

A personal server exists, but authoritative learning data should not be stored online merely for architectural convenience.

## Decision

Authoritative Illumination learning data is stored locally on the user's device.

Core Illumination operation does not require a remote server.

Illumination V1 is single-user.

Remote infrastructure may later support explicit connectivity, end-to-end encrypted synchronization/relay, or opt-in backup, but it does not become the readable authoritative learning-data store without a new explicit decision.

## Consequences

- Remote PostgreSQL is not the default persistence model.
- SQLite is the selected local database; EF Core's SQLite provider is the infrastructure adapter (ADR-0007).
- A browser-based UI remains possible if served locally; a web UI does not imply online storage.
- Docker is not required for the core local application merely because a server exists.
- Wiiii Got This multi-device use needs a separate access/synchronization design.
