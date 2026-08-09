# ADR-0002: Wiiii Got This Does Not Own Illumination Learning Semantics

- Status: Partially superseded by ADR-0009
- Date: 2026-08-08

Supersession note: ADR-0009 replaces the assumption that Wiiii Got This is only a possible later presentation consumer; the decision that WGT does not own Illumination learning semantics remains valid.

## Context

Wiiii Got This is intended to integrate services across devices and platforms.

Illumination must remain independently usable while still being exposable through Wiiii Got This on platforms Illumination may not directly target.

## Decision

Illumination remains authoritative for learning semantics and state.

Wiiii Got This may later consume versioned Illumination capabilities/read/command contracts and present them appropriately per device/platform.

Illumination's internal implementation language and UI technology are not part of those contracts.

## Consequences

Wiiii Got This may:

- discover Illumination,
- understand advertised capabilities,
- present supported Illumination workflows,
- invoke explicit application contracts.

Wiiii Got This may not:

- own Learning Items,
- own Reviews,
- recalculate Illumination scheduling independently,
- access Illumination persistence as an integration mechanism.
