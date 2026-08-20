# ADR-0010: Provider-Owned Reusable Illumination Product Surface

- Status: Proposed
- Date: 2026-08-20
- Tracks: #54
- System authority: accepted `wgt-system/architecture` ADR-0005; Architecture #11 / Draft PR #12 remain the control plane for unresolved runtime/packaging details.

## Context

Wiiii Got This is the primary containing product for the integrated personal system. That does not transfer ownership of Illumination's substantial learning workflows, learning state or local persistence to WGT.

Illumination v0.9.0 has a mature function-rich Avalonia consumer/acceptance UI around Study, Decks, Insights, Library, Generate/Import and Local Data. Rebuilding those workflows independently in WGT would create duplicate presentation logic and parity drift.

At the same time, the v0.9 standalone `Window` is host chrome and its current information architecture is not the final production UX. The reusable boundary therefore needs to preserve provider-owned workflow code without treating today's shell layout as permanent.

## Proposed service-local decision

Illumination publishes a reusable provider-owned Avalonia `Control` named `IlluminationProductSurface`.

The initial host boundary is deliberately narrow and provider-specific:

```text
IlluminationProductSurfaceFactory.CreateAsync()
    -> Task<Avalonia.Controls.Control>
```

The factory:

- composes Illumination Application/Infrastructure internally;
- creates provider-owned ViewModels and presentation behavior;
- returns only a presentation artifact to the containing host;
- does not expose Illumination Domain objects, EF types, DbContext instances or SQLite handles.

`MainWindow` becomes an optional standalone/admin/dev host around the same Product Surface.

## Ownership

Illumination owns:

- learning semantics and application workflows;
- authoritative local persistence and scheduling state;
- Study/Deck/Library/Generate/Insights consumer interaction semantics;
- the reusable Product Surface and future production UX inside it.

WGT owns:

- Atlas/system navigation and entry/return transitions;
- containing-product host chrome;
- device/platform integration and host lifecycle;
- WGT-global appearance/accessibility policy where it can be applied without taking over provider semantics;
- genuine cross-service compositions.

WGT must not:

- reimplement the complete Illumination product merely to make it WGT-native;
- import Illumination Domain objects into WGT Domain/Application;
- read/write Illumination SQLite directly;
- treat this provider-specific factory as a universal plugin/UI protocol.

## Host interaction

Provider presentation may resolve clipboard, file-picker and related UI services against the actual containing Avalonia `TopLevel`/`Window`. This allows the same Product Surface to operate inside the standalone host or a WGT host without transferring learning semantics to the host.

## Nested build isolation

Because WGT may check out Illumination below its own repository and compile it through a real `ProjectReference`, Illumination roots its own `Directory.Build.props`, package-management policy and NuGet source configuration. A provider nested inside WGT must not accidentally inherit unrelated parent repository build/package policy.

## Platform/runtime status

This ADR proposes the **service-local ownership and presentation boundary only**.

It does not by itself accept or prove:

- Windows/macOS/Linux/iOS/iPadOS/Android runtime/package support;
- a common provider lifecycle/compatibility manifest;
- dynamic download/hot-swap semantics;
- a universal Product Surface protocol.

Those details remain governed by Architecture #11 and Draft PR #12 until explicitly accepted.

Avalonia compatibility with the concrete WGT host is a technical integration requirement and must be validated explicitly. Framework compilation alone is not physical platform evidence.

## Production UX consequence

The current v0.9 consumer shell is replayed first so no accepted workflow is lost during extraction. Post-v0.9 UX work may then redesign the Product Surface around focused Study, progressive disclosure, goal-first Create/Extend and richer Insights without creating a second independently owned WGT UI.

## Acceptance evidence for #54

Before #54 closes:

- the current accepted consumer workflows run through `IlluminationProductSurface`;
- standalone `MainWindow` hosts that same surface;
- WGT consumes a reviewed provider revision through the narrow provider-specific boundary;
- build/tests/vulnerability audit are green;
- WGT host integration does not expose Illumination Domain/persistence;
- physical Windows smoke validates entry, Study, another substantive workflow and return to Atlas;
- no unsupported platform claim is made;
- unresolved system runtime/packaging policy is not silently marked Accepted here.

## Rejected alternatives

### Rebuild Illumination workflows inside WGT

Rejected because it duplicates provider behavior and weakens bounded-context ownership.

### Expose the standalone `Window` as the integration contract

Rejected because top-level window chrome is host-specific rather than a reusable provider presentation boundary.

### Expose Illumination Application/Domain objects directly to WGT presentation

Rejected because it creates foreign-domain coupling and invites persistence leakage.

### Define a universal WGT plugin/Product Surface protocol now

Rejected. One concrete provider integration is insufficient evidence for system-wide generalization.
