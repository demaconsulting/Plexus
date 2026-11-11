package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def Editor {
        doc /* Interactive editor control: view, input gestures, clipboard bridge and drag-and-drop of node types. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/Editor/EditorTests.cs");
            designFiles = ("docs/design/plexus-avalonia/editor.md");
            verificationFiles = ("docs/verification/plexus-avalonia/editor.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/editor.yaml");
        }

        part diagramEditor : DiagramEditor;
        part gestures : Gestures;
        part diagramClipboard : DiagramClipboard;
        part nodeTypeDragSource : NodeTypeDragSource;
        part nodePalette : NodePalette;
    }
}
