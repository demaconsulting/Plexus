package PlexusAvalonia {
    part def Editor {
        doc /* Interactive editor control: view, input gestures, clipboard bridge and drag-and-drop of node types. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Avalonia.Tests/Editor/EditorTests.cs */
        comment designRef /* Design: docs/design/plexus-avalonia/editor.md */
        comment verificationRef /* Verification: docs/verification/plexus-avalonia/editor.md */
        comment reqRef /* Requirements: docs/reqstream/plexus-avalonia/editor.yaml */

        part diagramEditor : DiagramEditor;
        part gestures : Gestures;
        part diagramClipboard : DiagramClipboard;
        part nodeTypeDragSource : NodeTypeDragSource;
        part nodePalette : NodePalette;
    }
}
