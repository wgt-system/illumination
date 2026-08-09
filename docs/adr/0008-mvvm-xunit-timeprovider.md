# ADR-0008: Avalonia MVVM and Controllable Time Testing Baseline

- Status: Partially superseded by ADR-0009
- Date: 2026-08-09

Supersession note: ADR-0009 replaces the assumption that Illumination requires a standalone primary desktop presentation; the CommunityToolkit.Mvvm, xUnit.net v3, and controllable TimeProvider decisions remain valid for the optional host and test infrastructure.

## Context

Illumination needs a maintainable desktop presentation and deterministic tests for time-dependent learning behavior.

## Decision

Use CommunityToolkit.Mvvm for Avalonia presentation state and xUnit.net v3 for automated tests. Domain and application code that depends on time must receive a controllable `TimeProvider` or equivalent clock abstraction so tests never depend on wall-clock time.

## Consequences

- View models remain presentation concerns and do not own domain rules.
- Scheduler and persistence tests can assert exact timestamps deterministically.
- Test infrastructure follows the same local-first, no-server V1 boundary.
