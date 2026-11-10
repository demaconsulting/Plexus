package PlexusRendering {
    private import SoftwareItemMetadata::*;

    part def CanvasTextMeasurer {
        doc /* Text measurer backed by CanvasNet fonts, with a bundled fallback font. */

        metadata info : SoftwareInfo {
            description = "Text measurer backed by CanvasNet fonts, with a bundled fallback font.";
            sourceFiles = ("src/DemaConsulting.Plexus.Rendering/CanvasTextMeasurer.cs", "src/DemaConsulting.Plexus.Rendering/FontProvider.cs");
            testFiles = ("test/DemaConsulting.Plexus.Rendering.Tests/CanvasTextMeasurerTests.cs");
            designFiles = ("docs/design/plexus-rendering/canvas-text-measurer.md");
            verificationFiles = ("docs/verification/plexus-rendering/canvas-text-measurer.md");
            requirementFiles = ("docs/reqstream/plexus-rendering/canvas-text-measurer.yaml");
        }
    }
}
