# ADR-0004: V1 Desktop Technology Stack

- Status: Partially superseded by ADR-0009
- Date: 2026-08-08

## Context

Illumination V1 is local-first, single-user, and uses an embedded local database. Core operation does not require a remote server. The primary end-user presentation is now owned by Wiiii Got This; the Avalonia host is optional.

The technology decision must follow product architecture rather than the user's prior skillset.

## Decision

Use:

- C# / .NET 10 LTS for the application and domain,
- Avalonia for the optional standalone/admin/dev host,
- SQLite for authoritative local persistence,
- EF Core with the SQLite provider for persistence infrastructure,
- CommunityToolkit.Mvvm for Avalonia presentation state.

## Consequences

- No remote database is required.
- No browser/local-web-server architecture is required for V1.
- A later published local/network adapter can be added for Wiiii Got This without changing domain ownership.
- The domain, application layer, and persistence adapter can remain within one deployable Illumination capability runtime initially.
- Avalonia is not the mandatory primary end-user presentation.
