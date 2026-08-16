workspace extends https://raw.githubusercontent.com/wgt-system/architecture/dev/model/workspace.dsl {
    properties {
        "structurizr.inspection.workspace.scope" "info"
    }

    model {
        !include model.dsl
    }

    views {
        !include views.dsl

        properties {
            "structurizr.sort" "created"
        }
    }

    configuration {
        scope softwaresystem
    }
}
