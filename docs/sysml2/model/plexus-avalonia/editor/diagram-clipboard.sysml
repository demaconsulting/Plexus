package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def DiagramClipboard {
        doc /* Operating-system clipboard bridge and clipboard text formats. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/IDiagramClipboard.cs", "src/DemaConsulting.Plexus.Avalonia/SystemDiagramClipboard.cs", "src/DemaConsulting.Plexus.Avalonia/TextFormats.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/Editor/DiagramClipboardTests.cs");
            designFiles = ("docs/design/plexus-avalonia/editor/diagram-clipboard.md");
            verificationFiles = ("docs/verification/plexus-avalonia/editor/diagram-clipboard.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/editor/diagram-clipboard.yaml");
        }
    }
}
