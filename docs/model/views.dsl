container illumination IlluminationContainers {
    include capabilityRuntime
    include desktopHost
    include localStore
    include wgt
    autolayout tb 180 120
    title "Illumination — Containers"
    description "Accepted Illumination host, capability-runtime, and local-persistence boundaries."
}

styles {
    element "Illumination Runtime" {
        shape Box
        background #F7F7F5
        color #1F2933
        stroke #4B5563
        strokeWidth 1
        description false
    }
    element "Illumination Host" {
        shape Box
        background #F7F7F5
        color #1F2933
        stroke #4B5563
        strokeWidth 1
        description false
    }
    element "Illumination Store" {
        shape Cylinder
        background #F7F7F5
        color #1F2933
        stroke #4B5563
        strokeWidth 1
        description false
    }
    relationship "Relationship" {
        color #6B7280
        thickness 1
        fontSize 15
    }
}
