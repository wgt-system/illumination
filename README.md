# Illumination

Illumination is an independent personal learning application focused on fast, repeated interaction with small learning units.

Its initial major use case is technical job-interview preparation, but the learning model is intentionally broader and may later cover programming languages, technical concepts, vocabulary, Indonesian, French, and other personal learning domains.

Illumination is a separate bounded context. It owns learning content, learning interaction, review history, repetition state, and learning progress.

It does not own job opportunities, Vocation learning clusters, devices, platform discovery, or generic service orchestration.

## Current release baseline

Illumination v0.8.0 is the current stable release. It builds on the v0.7 Product Refinement baseline with focused local-data reliability and runtime-coherence hardening: change-aware automatic rolling SQLite backups, a persistent configurable local backup directory, backup-before-import protection, preserved backup-before-migration safety, and fresh Learning Insights projections when the Insights surface is opened. Manual backup and portable export remain available, while authoritative database restore/replacement, cloud backup, synchronization, and speculative WGT/Vocation contracts remain outside this release.

Further development remains explicitly pre-1.0 until the product is deliberately judged ready as a complete first major version. Pre-1.0 minor versions continue numerically (`v0.9.0`, `v0.10.0`, `v0.11.0`, and so on); `v1.0.0` is not an automatic successor to `v0.9.0`.

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

The current Avalonia host is intentionally optimized for capability coverage, administration, development and acceptance testing. It is **not** the production UX baseline and its current screen density or information architecture should not be copied mechanically into the future end-user product. A later production presentation should be deliberately designed around low-friction learning workflows and the accepted WGT/service-hosting architecture. Individual components or interactions may be reused where they fit that product design.

## Data locality

Illumination is local-first.

Authoritative learning content, Review history, scheduling state, Decks, Study Sessions, and import history remain local to the user's device.

A remote server is not required for core local operation. Optional server, Docker, or Conveyance-backed delivery infrastructure may support connectivity or future synchronization, but remote readable persistence is not assumed. Illumination must first define future domain-specific publication, change, command, authority, merge, conflict, and reconciliation semantics; generic delivery does not transfer that ownership. No concrete bidirectional synchronization mechanism is selected.

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

Feature milestones use `vMAJOR.MINOR.PATCH`. Until the first major product baseline is deliberately declared complete, development may continue through as many `v0.y.0` releases as needed, including minor numbers above 9 such as `v0.10.0` and `v0.11.0`. `v1.0.0` is reserved for the intentionally completed first major product version rather than being inferred from the previous minor number.

## Technology stack

Illumination is a local-first executable capability runtime using:

- C# / .NET
- SQLite
- EF Core SQLite provider
- Avalonia and CommunityToolkit.Mvvm for the optional standalone Desktop host
- xUnit v3 for tests

Core operation does not require a remote server.

Wiiii Got This provides the primary end-user presentation. Core operation does not require a remote server.

## Repository layout

- `src/` — Domain, Application, Infrastructure, and optional Desktop host
- `tests/` — automated test projects
- `docs/` — product, architecture, acceptance, and roadmap sources of truth
- `schemas/` — versioned machine-readable contracts
- `examples/` — contract examples
- `.github/` — CI and contribution templates

## Prerequisites and commands

Install the .NET 10 SDK. From the repository root:

```powershell
dotnet restore Illumination.slnx
dotnet build Illumination.slnx --no-restore
dotnet test Illumination.slnx --no-build
```

Run the optional standalone Desktop host with:

```powershell
dotnet run --project src/Illumination.Desktop/Illumination.Desktop.csproj
```

## Structured content contract

Illumination Content Bundle 1.0 is defined by:

- `schemas/illumination-content-bundle-1.0.schema.json`
- `examples/content-bundle-1.0.example.json`

The contract supports explicit create/update operations and user-reviewed partial imports.

Quality-review exchange schemas are also versioned under `schemas/`. The durable product and architecture decisions are documented in `docs/`, especially the Domain Model, Application Design, Architecture, Acceptance Tests, Implementation Plan, and ADRs.

## Branch and release model

Normal development happens on `dev`. `main` contains stable milestone releases. Releases are tagged with semantic-style version names such as `v0.4.0`. Issues describe planned work and decisions; they do not imply a mandatory branch-per-Issue workflow.
