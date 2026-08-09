# ADR-0008: Avalonia MVVM and Controllable Time Testing Baseline

- Status: Partially superseded by ADR-0009
- Date: 2026-08-09

## Context

Illumination needs deterministic tests for time-dependent learning behavior. An Avalonia/MVVM presentation remains optional for standalone administration and development because Wiiii Got This is the primary end-user presentation.

## Decision

Retain CommunityToolkit.Mvvm for the optional Avalonia host and xUnit.net v3 for automated tests. Domain and application code that depends on time must receive a controllable `TimeProvider` or equivalent clock abstraction so tests never depend on wall-clock time.

## Consequences

- Optional Avalonia view models remain presentation concerns and do not own domain rules.
- Scheduler and persistence tests can assert exact timestamps deterministically.
- Test infrastructure follows the same local-first, no-server V1 boundary.
