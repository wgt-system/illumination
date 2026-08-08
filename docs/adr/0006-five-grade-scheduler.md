# ADR-0006: Five-Grade Deterministic Scheduler

- Status: Accepted
- Date: 2026-08-08

## Context

Illumination needs a five-grade assessment model with two distinct low grades and explicit `Suspended` / `Mastered` lifecycle states outside the scale.

## Decision

Use an Illumination-owned deterministic stability/difficulty scheduler with:

- `Nochmal`
- `Schwer`
- `Unsicher`
- `Gut`
- `Leicht`

`Nochmal` and `Schwer` additionally enter short-term relearning.

The initial constants are documented in `docs/08A_SCHEDULING_SEMANTICS.md` and validated by `docs/08B_SCHEDULING_SIMULATION.md`.

## Consequences

- Scheduling is not delegated to an external four-grade algorithm.
- Numeric constants may be tuned without changing the domain semantics.
- Domain transitions are deterministic and acceptance-testable.
