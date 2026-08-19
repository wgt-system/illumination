# ADR-0010: Provider-Owned Reusable Illumination Product Surface

- Status: Proposed
- Date: 2026-08-19
- Tracks: #54

## Context

Wiiii Got This is the primary **containing product** for the integrated personal system. That does not transfer presentation ownership of every rich bounded-context workflow to WGT.

System Architecture ADR-0005 accepts provider-owned Product Surfaces when rebuilding a substantial provider workflow in WGT would duplicate behavior or blur bounded-context ownership. Illumination already has mature consumer presentation for Study, Insights, Decks, Library, Generate / Import and local-data workflows alongside provider-owned Application, Domain and Infrastructure layers.

The historical ADR-0009 statement that WGT is the primary end-user presentation was too broad if interpreted as requiring those Illumination workflows to be independently rebuilt in WGT. The current architecture interpretation is narrower:

- WGT owns Atlas, WGT-level navigation/composition, device/platform integration, transitions, global appearance/accessibility policy and cross-service compositions;
- Illumination owns learning semantics, persistence, Application behavior and the normal Illumination consumer workflow presentation;
- the standalone Illumination desktop executable may remain a Standalone/Admin/Dev Host;
- useful consumer UI is not intentionally disposable merely because WGT is the primary containing product.

The same provider-owned consumer presentation must therefore be reusable by WGT and by an optional standalone host without exposing Illumination Domain types or its SQLite database to WGT.

The required WGT platform families are Windows, macOS, Linux, iOS/iPadOS and Android. Reusing an Avalonia control establishes a portable presentation direction, but it does not by itself prove packaging/runtime support on all five platforms. System Architecture #11 / proposed ADR-0006 separately defines that acceptance gate.

## Decision

Illumination publishes a reusable Avalonia **Product Surface** from the provider repository.

The provider-owned Product Surface contains the normal consumer workflows:

- Study;
- Insights;
- Decks;
- Library;
- Generate / Import;
- user-facing local-data/backup controls that are part of normal product operation.

These workflows remain provider-owned even when the surface is hosted inside WGT.

The current `Illumination.Desktop` executable becomes a thin Standalone/Admin/Dev Host around the same Product Surface. Top-level window lifecycle and any future host-only diagnostics/admin chrome may remain outside the reusable Product Surface. If a future feature is genuinely diagnostic, development-only or administrator-only, it must not be moved into the consumer Product Surface merely to make reuse convenient.

### Public presentation boundary

The initial host boundary remains intentionally narrow and provider-specific:

```text
IlluminationProductSurfaceFactory.CreateAsync()
    -> Avalonia Control
```

The factory:

- composes Illumination Application/Infrastructure internally through the provider-owned composition root;
- creates provider-owned ViewModels and presentation behavior;
- returns only an Avalonia presentation artifact to the host;
- does not expose Illumination Domain objects or persistence handles to WGT.

The Product Surface may resolve platform interaction from its actual containing Avalonia `TopLevel`/`Window` for capabilities such as clipboard and file picking. Those interaction adapters remain provider presentation concerns; the host does not acquire learning semantics by supplying a platform window.

### Host responsibilities

WGT may place the returned control inside WGT-owned host chrome. WGT owns:

- entry and return transitions;
- containing-product sizing/layout and lifecycle;
- WGT-global appearance/accessibility policy where it can be applied without taking over provider UI semantics;
- device/platform integration;
- failure isolation at the provider boundary;
- Atlas integration and cross-service composition.

WGT must not:

- reimplement the full Study/Insights/Decks/Library/Generate-Import workflow merely to make it look WGT-native;
- import Illumination Domain types into WGT Domain/Application;
- read or write Illumination SQLite directly;
- treat the Product Surface factory as a universal plugin protocol.

### Standalone/Admin/Dev Host

`MainWindow` is host chrome, not the canonical consumer workflow definition. It hosts `IlluminationProductSurface` and may add host-specific behavior later.

The standalone host and WGT therefore render the same provider-owned consumer surface rather than maintaining parallel copies.

### Platform and packaging disposition

This ADR accepts ownership and reuse. It does **not** claim physical five-platform support merely from Avalonia source compatibility.

Current disposition:

| Platform | Product Surface direction | Runtime/package evidence |
| --- | --- | --- |
| Windows | reusable Avalonia Product Surface in standalone or WGT host | current implementation/build integration exists; physical WGT-hosted smoke required by #54 |
| macOS | same provider-owned Avalonia Product Surface direction | packaging/runtime/device evidence still required |
| Linux | same provider-owned Avalonia Product Surface direction | packaging/runtime/device evidence still required |
| iOS/iPadOS | same consumer workflow ownership; no mobile-lite fork | runtime/persistence/package/device evidence still required |
| Android | same consumer workflow ownership; no mobile-lite fork | runtime/persistence/package/device evidence still required |

A platform-specific host adapter may differ. A platform-specific implementation detail must not silently create a second independently owned Illumination consumer UI.

If the executable-named `Illumination.Desktop` assembly becomes an awkward packaging boundary, the reusable surface may later move into a dedicated provider presentation assembly. That refactoring does not alter provider ownership.

### Versioning and WGT rebuild consequences

The Product Surface is currently referenced by WGT as a provider build artifact rather than discovered dynamically.

Therefore, while it is bundled/referenced with WGT:

- changing the Product Surface binary or its Avalonia/runtime compatibility requires WGT build validation and normally a WGT rebuild to ship the new provider artifact;
- changing only standalone host chrome does not require a WGT rebuild if the Product Surface artifact and WGT-facing provider boundary are unchanged;
- changing provider Domain/Application internals requires a WGT rebuild only when the bundled provider artifact or WGT-facing compatibility boundary changes;
- this does not establish downloaded plugins or an independently hot-swappable provider protocol.

A common provider compatibility manifest may be introduced only by a later system Architecture decision after repeated provider integration evidence.

## Consequences

- Illumination has one canonical consumer presentation implementation for standalone and WGT hosting.
- Current consumer UI work remains durable product work instead of becoming demo/disposable UI.
- Study plus every other current consumer workflow can evolve in the provider repository without a parallel WGT screen implementation.
- WGT remains the containing product without taking learning-domain or learning-UI ownership.
- Local persistence remains Illumination-owned.
- Host-only admin/debug concerns have an explicit place outside the Product Surface.
- Avalonia/.NET remains the current Product Surface technology direction; this ADR does not force Vocation, Orientation or future providers to use Avalonia/.NET.
- Five-platform support remains an evidence obligation rather than an inference from framework capability.

## Rejected alternatives

### Rebuild the normal Illumination consumer UI in WGT

Rejected. It duplicates provider workflows, creates parity drift and weakens bounded-context ownership.

### Treat the current Illumination desktop UI as disposable dev-only presentation

Rejected. Consumer workflow code should migrate/refactor into the reusable Product Surface and remain production-quality provider code.

### Make the standalone `Window` itself the integration boundary

Rejected. A top-level desktop window is host chrome and is not a portable embeddable presentation contract.

### Expose Illumination Domain/Application objects directly to WGT presentation

Rejected. WGT may host the provider presentation artifact but does not take ownership of Illumination semantics.

### Define a universal WGT Product Surface/plugin protocol now

Rejected. This is a concrete provider-specific integration. System-wide generalization still requires repeated evidence and an Architecture Control Plane decision.

### Build a reduced mobile-only Illumination product

Rejected as a default architecture. Platform constraints may change host/runtime implementation details, but the normal consumer capability set is not intentionally reduced merely because a target is mobile.

## Acceptance evidence for #54

Before #54 closes:

- `IlluminationProductSurface` is the reusable consumer presentation boundary;
- the standalone host uses that same surface;
- at minimum Study and another substantive workflow execute through the reusable surface;
- current `dev` consumer UI changes are preserved rather than replaced by the older extraction snapshot;
- provider Domain/Application ownership and local persistence remain unchanged;
- build/tests/vulnerability audit are green;
- WGT consumes the updated provider Product Surface artifact without duplicating Illumination UI;
- a physical Windows WGT-hosted smoke validates entry, Study plus another workflow, and return to Atlas;
- no unsupported macOS/Linux/iOS/iPadOS/Android runtime claim is made without physical/platform evidence.
