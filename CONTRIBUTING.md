# Contributing to Illumination

Normal development happens on `dev`; `main` contains stable versioned milestone releases. Releases are tagged. Issues describe planned work, but there is no mandatory branch-per-Issue workflow.

Before changing behavior, identify the authoritative specification and acceptance scenarios in `docs/`. Issues do not authorize inventing product behavior, roadmap scope, or architecture decisions.

Run focused tests during development. Before a release, run the complete restore, build, and test suite plus the NuGet vulnerability audit.

Do not commit local databases, generated personal learning data, credentials, logs, IDE metadata, or machine-specific files. Preserve the local-first bounded-context architecture and explicit Application/published-contract boundaries.
