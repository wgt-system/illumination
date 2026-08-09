# ADR-0002: Wiiii Got This Does Not Own Illumination Learning Semantics

- Status: Accepted
- Date: 2026-08-08

## Context

Wiiii Got This is intended to integrate services across devices and platforms.

Illumination must remain an independent executable capability runtime while Wiiii Got This provides the primary end-user presentation on Windows and iPhone.

## Decision

Illumination remains authoritative for learning semantics and state.

Wiiii Got This consumes explicit versioned Illumination capabilities/read/command contracts and presents them appropriately per device/platform.

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
