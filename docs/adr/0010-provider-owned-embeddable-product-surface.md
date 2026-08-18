# ADR-0010: Provider-Owned Embeddable Illumination Product Surface

- Status: Accepted
- Date: 2026-08-18

## Context

Wiiii Got This is the primary end-user presentation for Illumination, while Illumination remains authoritative for learning semantics, application behavior and local persistence. System Architecture ADR-0005 accepts provider-owned Product Surfaces when rebuilding a rich provider workflow in WGT would duplicate behavior or blur bounded-context ownership.

Illumination already has a substantial Avalonia presentation covering Study, Insights, Decks, Library, Import and local-data workflows. Reimplementing these screens in WGT would create a second presentation implementation over the same Illumination behavior and would make capability parity harder to maintain.

The existing `Illumination.Desktop` project is also the optional standalone/admin/dev host. Its useful product presentation must therefore be reusable independently from the top-level standalone `Window`.

## Decision

Illumination publishes an embeddable Avalonia **Product Surface** from the provider repository.

The provider-owned surface:

- is a reusable Avalonia `Control` containing the same Study, Insights, Decks, Library, Import and local-data presentation used by the standalone host;
- composes Illumination Application/Infrastructure internally through the existing provider-owned composition root;
- owns its Illumination-specific ViewModels and presentation behavior;
- may use the containing Avalonia `Window` only for desktop interaction services such as clipboard and file pickers;
- is created through a narrow public provider-owned factory rather than by exposing Illumination Domain objects to the host.

The standalone Illumination `Window` hosts that same Product Surface instead of maintaining a second copy of the product layout.

WGT may reference the provider-owned presentation artifact and place the returned `Control` inside WGT-owned host chrome. WGT owns entry/exit, sizing, platform host behavior, Atlas integration, failure isolation and WGT-global presentation policy. WGT must not reach through the Product Surface into Illumination Domain or persistence types.

## Boundary

The initial public presentation boundary is intentionally small:

```text
IlluminationProductSurfaceFactory.CreateAsync()
    -> Avalonia Control
```

This is a concrete provider-specific integration boundary. It is **not** a universal WGT plugin protocol, downloaded extension system, shared business-logic library, or cross-context domain API.

The implementation may later be moved from the executable-named `Illumination.Desktop` assembly into a dedicated provider presentation assembly if packaging/reuse pressure justifies that split. That refactoring does not change the Product Surface ownership decision.

## Consequences

- Standalone Illumination and WGT can render one provider-owned product presentation rather than two divergent UI implementations.
- Illumination retains complete learning workflow and UI-semantic ownership.
- WGT can provide a first-class full-service entry without importing Illumination Domain objects.
- Local-first storage remains Illumination-owned and continues to use the existing local composition/runtime.
- Desktop clipboard/file-picker interactions continue to resolve through the actual containing Avalonia `Window`.
- A future iPhone implementation still requires a real provider/runtime/platform decision; this Desktop Product Surface does not claim iPhone compatibility.
- Repeated concrete Product Surface integrations are still required before introducing any generic Service Host abstraction.
