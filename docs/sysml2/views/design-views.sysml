// Views rendered into docs/design/generated/ for the Design document.
// Each package is reopened so that every 'expose' statement can reference its target part def
// directly. Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a
// 'view def' definition), and it - not 'render' - is what scopes the rendered diagram content.
package Plexus {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose Plexus::**;
        expose PlexusRendering::**;
        expose PlexusAvalonia::**;
        expose PlexusAvaloniaPropertyGrid::**;
        filter not @SysML::Documentation;
    }

    view PlexusView {
        expose PlexusSystem;
    }

    view ModelView {
        expose Model;
    }

    view HistoryView {
        expose History;
    }

    view TypesView {
        expose Types;
    }

    view PropertiesView {
        expose Properties;
    }

    view RoutingView {
        expose Routing;
    }

    view SceneView {
        expose Scene;
    }

    view SerializationView {
        expose Serialization;
    }
}

package PlexusRendering {
    view PlexusRenderingView {
        expose PlexusRenderingSystem;
    }
}

package PlexusAvalonia {
    view PlexusAvaloniaView {
        expose PlexusAvaloniaSystem;
    }

    view EditorView {
        expose Editor;
    }
}

package PlexusAvaloniaPropertyGrid {
    view PlexusAvaloniaPropertyGridView {
        expose PlexusAvaloniaPropertyGridSystem;
    }
}
