# ADR-0001: Illumination Is an Independent Bounded Context

- Status: Accepted
- Date: 2026-08-08

## Context

Illumination, Vocation, and Wiiii Got This have different domain ownership and independent evolution.

## Decision

Illumination is an independent bounded context and independently executable capability runtime.

It owns learning content, reviews, repetition state, and learning progress.

Integration with other projects must use explicit published contracts.

## Consequences

Forbidden:

- shared domain entities,
- direct foreign-domain imports,
- cross-context persistence access as integration,
- shared business-logic libraries that erase ownership boundaries.

Shared physical infrastructure remains possible when logical/domain ownership stays separate.

Wiiii Got This may provide the primary end-user presentation without changing Illumination's bounded-context ownership. A complete separate Illumination end-user UI is not required.
