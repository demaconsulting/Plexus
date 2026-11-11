package PlexusRendering {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Headless renderer that paints a Plexus scene for one page to a PNG image using CanvasNet.
     */

    part def PlexusRenderingSystem {
        doc /* Headless renderer that paints a Plexus scene for one page to a PNG image using CanvasNet. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Rendering/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.Plexus.Rendering.Tests/PlexusRenderingTests.cs");
            designFiles = ("docs/design/plexus-rendering.md");
            verificationFiles = ("docs/verification/plexus-rendering.md");
            requirementFiles = ("docs/reqstream/plexus-rendering.yaml");
        }

        ref part core : Plexus::PlexusSystem;
        part plexusRenderer : PlexusRenderer;
        part scenePainter : ScenePainter;
        part canvasTextMeasurer : CanvasTextMeasurer;
        part iconCache : IconCache;
        ref part canvasNet : OTS::CanvasNet;
    }
}
