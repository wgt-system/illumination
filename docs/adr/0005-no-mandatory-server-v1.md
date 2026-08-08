# ADR-0005: No Mandatory Server for Illumination V1

- Status: Accepted
- Date: 2026-08-08

## Context

The learner does not want authoritative personal learning data stored online.

## Decision

Illumination V1 requires no remote server.

Authoritative data remains in the local SQLite store.

Docker is not part of the core Illumination V1 deployment.

## Consequences

- Core study works offline.
- Server infrastructure may later be introduced only for explicit integration, encrypted relay/synchronization, or opt-in backup.
- Such infrastructure does not automatically become the source of truth.
