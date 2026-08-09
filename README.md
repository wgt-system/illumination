# Illumination

Illumination is an independent personal learning application focused on fast, repeated interaction with small learning units.

Its initial major use case is technical job-interview preparation, but the learning model is intentionally broader and may later cover programming languages, technical concepts, vocabulary, Indonesian, French, and other personal learning domains.

Illumination is a separate bounded context. It owns learning content, learning interaction, review history, repetition state, and learning progress.

It does not own job opportunities, Vocation learning clusters, devices, platform discovery, or generic service orchestration.

## Current project status

The implementation gate for the V1 baseline is satisfied. Illumination is being built as a local-first, single-user executable capability runtime. Wiiii Got This is the primary end-user presentation on Windows and iPhone; an Avalonia host may remain for standalone administration and development.

## Product direction

Illumination is inspired by useful spaced-review workflows, but its canonical terms are `Learning Item` and `Deck`:

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

Wiiii Got This is the primary end-user presentation for Illumination on Windows and iPhone.

Illumination remains an independent bounded context and executable capability runtime. Wiiii Got This may host it locally in-process, but only through explicit Illumination-owned application or published-contract boundaries; it must not use Illumination domain objects directly.

The existing Avalonia project may remain as an optional standalone/admin/dev host. A complete separate Illumination end-user UI is not required.

## Data locality

Illumination is local-first.

Authoritative learning content, Review history, scheduling state, Decks, Study Sessions, and import history remain local to the user's device.

A remote server is not required for core local operation. Optional server, Docker, or relay infrastructure may support connectivity and synchronization, but remote readable persistence is not assumed. Illumination owns future domain-specific synchronization and merge semantics; generic WGT infrastructure may own transport, relay, retry, and encryption concerns.

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

The accepted V1 runtime baseline is C# / .NET 10 LTS, SQLite, and EF Core's SQLite provider. Avalonia and CommunityToolkit.Mvvm remain accepted for the optional standalone/admin/dev host. Core local operation requires no remote server; Docker is optional infrastructure and not mandatory.

## Release direction

Feature milestones are expected to use `vMAJOR.MINOR.PATCH`, progressing through `v0.y.0` releases toward a coherent `v1.0.0`. The number of pre-1.0 milestones is not predetermined.


## V1 technology baseline

Illumination V1 is planned as a local-first executable capability runtime using:

- C# / .NET
- SQLite

Core operation does not require a remote server.

The stack is selected from the current product architecture rather than from prior user skill familiarity. Wiiii Got This provides the primary end-user presentation.

## Structured content contract

Illumination Content Bundle 1.0 is defined by:

- `schemas/illumination-content-bundle-1.0.schema.json`
- `examples/content-bundle-1.0.example.json`

The contract supports explicit create/update operations and user-reviewed partial imports.
