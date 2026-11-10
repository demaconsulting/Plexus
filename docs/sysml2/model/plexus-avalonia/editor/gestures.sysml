package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def Gestures {
        doc /* Pointer gesture state machines: connect, move, resize, pan, rubber-band and the wire gestures (segment, bend and end drag). */

        metadata info : SoftwareInfo {
            description = "Pointer gesture state machines: connect, move, resize, pan, rubber-band and the wire gestures (segment, bend and end drag).";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/Interaction/ConnectGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/EditorGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/MoveGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/PanGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/ResizeGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/RubberBandGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/WireBendGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/WireEndGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/WireReshapeGesture.cs", "src/DemaConsulting.Plexus.Avalonia/Interaction/WireSegmentGesture.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/Editor/GesturesTests.cs", "test/DemaConsulting.Plexus.Avalonia.Tests/Editor/DiagramEditorWireEditingTests.cs");
            designFiles = ("docs/design/plexus-avalonia/editor/gestures.md");
            verificationFiles = ("docs/verification/plexus-avalonia/editor/gestures.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/editor/gestures.yaml");
        }
    }
}
