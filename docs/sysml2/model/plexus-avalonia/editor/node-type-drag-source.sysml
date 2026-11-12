package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def NodeTypeDragSource {
        doc /* Helper that lets hosts start drag-and-drop of node types onto the editor. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/NodeTypeDragSource.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodeTypeDragSourceTests.cs");
            designFiles = ("docs/design/plexus-avalonia/editor/node-type-drag-source.md");
            verificationFiles = ("docs/verification/plexus-avalonia/editor/node-type-drag-source.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/editor/node-type-drag-source.yaml");
        }
    }
}
