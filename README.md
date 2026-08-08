# Illumination

Illumination is an independent personal learning application focused on fast, repeated interaction with small learning units.

Its initial major use case is technical job-interview preparation, but the learning model is intentionally broader and may later cover programming languages, technical concepts, vocabulary, Indonesian, French, and other personal learning domains.

Illumination is a separate bounded context. It owns learning content, learning interaction, review history, repetition state, and learning progress.

It does not own job opportunities, Vocation learning clusters, devices, platform discovery, or generic service orchestration.

## Current project status

The project is in specification phase.

No implementation stack, persistence technology, UI framework, internal service split, or deployment model has been selected yet.

Implementation must not begin before the first coherent domain and application specification exists.

## Product direction

Illumination is inspired by the useful parts of Anki:

- small independently reviewable learning units,
- rapid answer reveal,
- repeated review,
- longer repetition intervals as knowledge stabilizes,
- user-defined decks comparable to decks.

It is not intended to be an Anki clone.

A central design goal is low-friction learning: the learner should often be able to review material with little or no text input, including short sessions in low-attention situations.

Different learning-unit interaction forms are expected, such as factual recall, multiple choice, short text input, language practice, syntax recall, and small coding tasks.

## External content generation

Illumination should be able to generate prompts for external ChatGPT use and import structured, versioned JSON containing new or updated learning content.

The imported content becomes Illumination-owned data after validation and import.

## Relationship to Vocation

Vocation and Illumination initially follow Separate Ways.

Vocation owns the job market and any Vocation-specific learning-cluster or learning-need concepts.

Illumination owns learning content and learning progress.

A later published contract may allow Vocation to reference a learning need and consume limited learning-coverage information from Illumination. The exact semantics are not yet defined.

## Relationship to Wiiii Got This

Illumination must remain independently usable without Wiiii Got This.

Wiiii Got This may later consume explicit, versioned Illumination capabilities and present them on supported devices and platforms.

Illumination does not need to implement its own client for every platform supported by Wiiii Got This.

## Data locality

Illumination is local-first.

Authoritative learning content, Review history, scheduling state, Decks, Study Sessions, and import history remain local to the user's device.

A remote server is not required for core use. Future Wiiii Got This integration may introduce explicit multi-device access or encrypted synchronization, but remote readable persistence is not assumed.

## Specification documents

- `docs/01_DOMAIN_VISION.md`
- `docs/02_SCENARIOS.md`
- `docs/03_UBIQUITOUS_LANGUAGE.md`
- `docs/04_SUBDOMAINS.md`
- `docs/05_DOMAIN_MODEL.md`
- `docs/06_CONTEXT_MAP.md`
- `docs/07_APPLICATION_DESIGN.md`
- `docs/08_IMPORT_CONTRACT.md`
- `docs/09_READ_MODELS.md`
- `docs/10_ARCHITECTURE.md`
- `docs/11_ACCEPTANCE_TESTS.md`
- `docs/12_IMPLEMENTATION_PLAN.md`
- `docs/adr/`

The specification is intentionally technology-neutral until the remaining domain and architecture decisions are resolved.

## Release direction

Feature milestones are expected to use `vMAJOR.MINOR.PATCH`, progressing through `v0.y.0` releases toward a coherent `v1.0.0`. The number of pre-1.0 milestones is not predetermined.


## V1 technology baseline

Illumination V1 is planned as an installed local desktop application using:

- C# / .NET
- Avalonia
- SQLite

Core operation does not require a remote server.

The stack is selected from the current product architecture rather than from prior user skill familiarity.

## Structured content contract

Illumination Content Bundle 1.0 is defined by:

- `schemas/illumination-content-bundle-1.0.schema.json`
- `examples/content-bundle-1.0.example.json`

The contract supports explicit create/update operations and user-reviewed partial imports.
