# ADR-0009: Wiiii Got This Primary Presentation and Illumination Capability Runtime

- Status: Accepted
- Date: 2026-08-09

## Context

Illumination owns learning domain, application, persistence, and future domain-specific synchronization/merge semantics. Wiiii Got This owns cross-application platform concerns and is the primary end-user presentation for Illumination on Windows and iPhone.

The prior architecture documents treated a complete standalone Illumination desktop UI as required and treated Avalonia as the mandatory primary presentation technology. That no longer reflects the confirmed product architecture.

## Decision

Illumination is an independent bounded context and executable capability runtime. Wiiii Got This may host Illumination locally in-process and present its capabilities on Windows and iPhone, but only through explicit Illumination-owned application or published-contract boundaries. Wiiii Got This must not use Illumination domain objects directly.

A complete separate Illumination end-user UI is not required. The existing Avalonia project may remain as an optional standalone/admin/dev host and is not the mandatory primary product UI.

Local-first operation remains required. Optional server, Docker, or relay infrastructure may support connectivity, transport, retry, encryption, or synchronization, but must not become mandatory for core local operation. Illumination owns future domain-specific synchronization and merge semantics. No speculative synchronization API is designed by this ADR.

C#/.NET, SQLite, and EF Core remain accepted implementation decisions. This ADR does not select WGT implementation technology or change Illumination's programming language/runtime.

## Consequences

- Illumination's domain/application/persistence semantics remain authoritative and independently executable.
- Wiiii Got This is the primary end-user presentation on Windows and iPhone.
- Integration uses explicit Illumination-owned application or published-contract boundaries.
- Avalonia/MVVM remains available only as an optional standalone/admin/dev host.
- Generic WGT infrastructure may own transport/relay/retry/encryption concerns, while Illumination owns domain-specific synchronization/merge semantics.
- Future capability and synchronization contracts require separate concrete decisions; this ADR does not define them.
