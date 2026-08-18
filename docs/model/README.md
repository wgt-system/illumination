# Illumination Container Model

This model is owned by Illumination. It describes the accepted hostable runtime
and local persistence boundaries without introducing a network or process boundary.

The Capability Runtime may be hosted in-process by Wiiii Got This. The Standalone
Desktop Host is an optional administration and development host, while the Local
SQLite Store remains Illumination-owned authoritative persistence. Logical
Application, Domain, and Infrastructure projects are not containers here.

Wiiii Got This is the primary external host and presentation. No synchronization
topology is represented in this view.
