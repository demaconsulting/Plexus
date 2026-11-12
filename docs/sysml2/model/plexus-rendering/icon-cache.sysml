package PlexusRendering {
    private import SoftwareItemMetadata::*;

    part def IconCache {
        doc /* Cache of rasterized SVG icons keyed by icon and size. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Rendering/IconCache.cs");
            testFiles = ("test/DemaConsulting.Plexus.Rendering.Tests/IconCacheTests.cs");
            designFiles = ("docs/design/plexus-rendering/icon-cache.md");
            verificationFiles = ("docs/verification/plexus-rendering/icon-cache.md");
            requirementFiles = ("docs/reqstream/plexus-rendering/icon-cache.yaml");
        }
    }
}
