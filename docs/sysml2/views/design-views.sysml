// Views rendered into docs/design/generated/ for the Design document.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage
// (not a 'view def' definition), and it - not 'render' - is what scopes the
// rendered diagram content.
package PlexusDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        expose Plexus::PlexusSystem;
        expose PlexusRendering::PlexusRenderingSystem;
        expose PlexusAvalonia::PlexusAvaloniaSystem;
        expose PlexusAvaloniaPropertyGrid::PlexusAvaloniaPropertyGridSystem;
    }

    view PlexusView {
        expose Plexus::PlexusSystem;
    }

    view ModelView {
        expose Plexus::Model;
    }

    view HistoryView {
        expose Plexus::History;
    }

    view TypesView {
        expose Plexus::Types;
    }

    view PropertiesView {
        expose Plexus::Properties;
    }

    view RoutingView {
        expose Plexus::Routing;
    }

    view SceneView {
        expose Plexus::Scene;
    }

    view SerializationView {
        expose Plexus::Serialization;
    }

    view PlexusRenderingView {
        expose PlexusRendering::PlexusRenderingSystem;
    }

    view PlexusAvaloniaView {
        expose PlexusAvalonia::PlexusAvaloniaSystem;
    }

    view EditorView {
        expose PlexusAvalonia::Editor;
    }

    view PlexusAvaloniaPropertyGridView {
        expose PlexusAvaloniaPropertyGrid::PlexusAvaloniaPropertyGridSystem;
    }
}
