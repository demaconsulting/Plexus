package PlexusRendering {
    doc
    /*
     * Headless renderer that paints a Plexus scene for one page to a PNG image using CanvasNet.
     */

    part def PlexusRenderingSystem {
        doc /* Headless renderer that paints a Plexus scene for one page to a PNG image using CanvasNet. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus.Rendering/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Rendering.Tests/PlexusRenderingTests.cs */
        comment designRef /* Design: docs/design/plexus-rendering.md */
        comment verificationRef /* Verification: docs/verification/plexus-rendering.md */
        comment reqRef /* Requirements: docs/reqstream/plexus-rendering.yaml */

        ref part core : Plexus::PlexusSystem;
        part plexusRenderer : PlexusRenderer;
        part scenePainter : ScenePainter;
        part canvasTextMeasurer : CanvasTextMeasurer;
        part iconCache : IconCache;
        ref part canvasNet : OTS::CanvasNet;
    }
}
