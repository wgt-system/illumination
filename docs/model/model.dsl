!element illumination {
    properties {
        "structurizr.inspection.model.softwaresystem.documentation" "info"
        "structurizr.inspection.model.softwaresystem.decisions" "info"
    }

    capabilityRuntime = container "Capability Runtime" "Hostable Illumination runtime owning learning application/domain behavior and Illumination persistence integration. It may run in-process inside an approved host and is not a network service boundary." ".NET 10 / C#" {
        tags "Illumination Runtime"
    }

    desktopHost = container "Standalone Desktop Host" "Optional standalone administration and development host for Illumination capabilities; not the primary end-user presentation." "Avalonia / .NET 10" {
        tags "Illumination Host"
    }

    localStore = container "Local SQLite Store" "Illumination-owned device-local authoritative persistence for learning content, review history, scheduling state, study sessions, and import history." "EF Core / SQLite" {
        tags "Illumination Store"
    }

    wgt -> capabilityRuntime "Hosts and invokes Illumination through explicit Illumination-owned boundaries" ".NET in-process integration"
    desktopHost -> capabilityRuntime "Hosts optional standalone administration and development workflows" ".NET in-process integration"
    capabilityRuntime -> localStore "Reads and writes authoritative Illumination learning state" "EF Core / SQLite"
}
