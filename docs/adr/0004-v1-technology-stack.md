# ADR-0004: V1 Desktop Technology Stack

- Status: Accepted
- Date: 2026-08-08

## Context

Illumination V1 is local-first, single-user, desktop-oriented, and uses an embedded local database. Core operation does not require a remote server.

The technology decision must follow product architecture rather than the user's prior skillset.

## Decision

Use:

- C# / .NET for the application and domain,
- Avalonia for the installed desktop UI,
- SQLite for authoritative local persistence.

## Consequences

- No remote database is required.
- No browser/local-web-server architecture is required for V1.
- A later published local/network adapter can be added for Wiiii Got This without changing domain ownership.
- The UI, domain, application layer, and persistence adapter can remain within one deployable bounded context initially.
