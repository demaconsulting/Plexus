package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def ScenePainter {
        doc /* Paints scene primitives onto an Avalonia drawing context. */

        metadata info : SoftwareInfo {
            description = "Paints scene primitives onto an Avalonia drawing context.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/ScenePainter.cs", "src/DemaConsulting.Plexus.Avalonia/PlexusAvaloniaExtensions.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/ScenePainterTests.cs", "test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodeStylePixelTests.cs");
            designFiles = ("docs/design/plexus-avalonia/scene-painter.md");
            verificationFiles = ("docs/verification/plexus-avalonia/scene-painter.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/scene-painter.yaml");
        }
    }
}
