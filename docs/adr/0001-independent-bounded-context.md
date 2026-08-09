# ADR-0001: Illumination Is an Independent Bounded Context

- Status: Partially superseded by ADR-0009
- Date: 2026-08-08

Supersession note: ADR-0009 replaces only the assumption that Illumination must provide a complete independently usable end-user application; the independent bounded-context and ownership decision remains valid.

## Context

Illumination, Vocation, and Wiiii Got This have different domain ownership and independent evolution.

## Decision

Illumination is an independent bounded context and independently usable application.

It owns learning content, reviews, repetition state, and learning progress.

Integration with other projects must use explicit published contracts.

## Consequences

Forbidden:

- shared domain entities,
- direct foreign-domain imports,
- cross-context persistence access as integration,
- shared business-logic libraries that erase ownership boundaries.

Shared physical infrastructure remains possible when logical/domain ownership stays separate.

Independent usability does not require Illumination to provide a native client for every platform Wiiii Got This supports.
