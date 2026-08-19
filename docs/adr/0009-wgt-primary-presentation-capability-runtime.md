# ADR-0009: Wiiii Got This Primary Presentation and Illumination Capability Runtime

- Status: Accepted
- Date: 2026-08-09

## Current Architecture Context

This ADR remains historically valid for using Wiiii Got This as the primary presentation
and capability host for Illumination. Its generic WGT transport/relay/retry/encryption
wording reflects the architecture state at the time of the decision. Current system-wide
capability ownership is governed by `wgt-system/architecture`: WGT owns device/platform
integration and presentation, while Conveyance owns generic durable opaque cross-device
delivery. `Current Object` is the only currently accepted Conveyance delivery mode and is
not a bidirectional Learning synchronization solution by itself. Illumination retains its
domain-specific publication, command, authority, merge, conflict, and reconciliation
semantics; concrete Illumination synchronization semantics remain open. This note does not
rewrite the historical Decision below.

## Context

Illumination owns learning domain, application, persistence, and future domain-specific synchronization/merge semantics. Wiiii Got This owns cross-application platform concerns and is the primary end-user presentation for Illumination on Windows and iPhone.

The prior architecture documents treated a complete standalone Illumination desktop UI as required and treated Avalonia as the mandatory primary presentation technology. That no longer reflects the confirmed product architecture.

## Decision

Illumination is an independent bounded context and executable capability runtime. Wiiii Got This may host Illumination locally in-process and present its capabilities on Windows and iPhone, but only through explicit Illumination-owned application or published-contract boundaries. Wiiii Got This must not use Illumination domain objects directly.

A complete separate Illumination end-user UI is not required. The existing Avalonia project may remain as an optional standalone/admin/dev host and is not the mandatory primary product UI.

The current Avalonia host is therefore **not a production UX blueprint**. It may deliberately expose many capabilities, technical states, diagnostics, and specialist controls because its job includes development, acceptance testing, administration, and independent runtime validation. Future production presentation must be designed around user workflows and accepted WGT/service-hosting boundaries rather than mechanically polishing or copying the current screens. Reusing individual presentation components is allowed where they fit that deliberately designed product experience; the host's present information architecture is not itself a product contract.

Local-first operation remains required. Optional server, Docker, or relay infrastructure may support connectivity, transport, retry, encryption, or synchronization, but must not become mandatory for core local operation. Illumination owns future domain-specific synchronization and merge semantics. No speculative synchronization API is designed by this ADR.

C#/.NET, SQLite, and EF Core remain accepted implementation decisions. This ADR does not select WGT implementation technology or change Illumination's programming language/runtime.

## Consequences

- Illumination's domain/application/persistence semantics remain authoritative and independently executable.
- Wiiii Got This is the primary end-user presentation on Windows and iPhone.
- Integration uses explicit Illumination-owned application or published-contract boundaries.
- Avalonia/MVVM remains available only as an optional standalone/admin/dev host.
- The current standalone host may optimize for capability coverage and testability; its screen structure and control density are not requirements for production UX.
- Production presentation may reuse suitable components or interaction pieces, but must be deliberately designed instead of treating the standalone host as a screen-for-screen prototype.
- Generic WGT infrastructure may own transport/relay/retry/encryption concerns, while Illumination owns domain-specific synchronization/merge semantics.
- Future capability and synchronization contracts require separate concrete decisions; this ADR does not define them.
