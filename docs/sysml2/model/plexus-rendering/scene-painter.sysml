package PlexusRendering {
    private import SoftwareItemMetadata::*;

    part def ScenePainter {
        doc /* Paints scene primitives (rectangles, paths, text, icons, grid) onto a CanvasNet surface. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Rendering/ScenePainter.cs");
            testFiles = ("test/DemaConsulting.Plexus.Rendering.Tests/ScenePainterTests.cs");
            designFiles = ("docs/design/plexus-rendering/scene-painter.md");
            verificationFiles = ("docs/verification/plexus-rendering/scene-painter.md");
            requirementFiles = ("docs/reqstream/plexus-rendering/scene-painter.yaml");
        }
    }
}
