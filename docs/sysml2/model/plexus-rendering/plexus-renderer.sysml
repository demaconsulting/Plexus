package PlexusRendering {
    private import SoftwareItemMetadata::*;

    part def PlexusRenderer {
        doc /* Public entry point that renders a page to a PNG image or file according to render options. */

        metadata info : SoftwareInfo {
            description = "Public entry point that renders a page to a PNG image or file according to render options.";
            sourceFiles = ("src/DemaConsulting.Plexus.Rendering/PlexusRenderer.cs", "src/DemaConsulting.Plexus.Rendering/RenderOptions.cs");
            testFiles = ("test/DemaConsulting.Plexus.Rendering.Tests/PlexusRendererTests.cs", "test/DemaConsulting.Plexus.Rendering.Tests/WireStyleRenderingTests.cs", "test/DemaConsulting.Plexus.Rendering.Tests/NodeStyleRenderingTests.cs");
            designFiles = ("docs/design/plexus-rendering/plexus-renderer.md");
            verificationFiles = ("docs/verification/plexus-rendering/plexus-renderer.md");
            requirementFiles = ("docs/reqstream/plexus-rendering/plexus-renderer.yaml");
        }
    }
}
