package Plexus {
    private import SoftwareItemMetadata::*;

    part def OrthogonalRouter {
        doc /* Default obstacle-avoiding orthogonal wire router and its route request contract. */

        metadata info : SoftwareInfo {
            description = "Default obstacle-avoiding orthogonal wire router and its route request contract.";
            sourceFiles = ("src/DemaConsulting.Plexus/Routing/OrthogonalRouter.cs", "src/DemaConsulting.Plexus/Routing/OrthogonalSearch.cs", "src/DemaConsulting.Plexus/Routing/IWireRouter.cs", "src/DemaConsulting.Plexus/Routing/RouteRequest.cs", "src/DemaConsulting.Plexus/Routing/PortSide.cs", "src/DemaConsulting.Plexus/Routing/RouteSegment.cs", "src/DemaConsulting.Plexus/Routing/ForeignSegments.cs", "src/DemaConsulting.Plexus/Routing/RouteCleaner.cs", "src/DemaConsulting.Plexus/Routing/RouteEditor.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Routing/OrthogonalRouterTests.cs", "test/DemaConsulting.Plexus.Tests/Routing/NetSeparationTests.cs", "test/DemaConsulting.Plexus.Tests/Routing/RouteCleanerTests.cs", "test/DemaConsulting.Plexus.Tests/Routing/RouteEditorTests.cs");
            designFiles = ("docs/design/plexus/routing/orthogonal-router.md");
            verificationFiles = ("docs/verification/plexus/routing/orthogonal-router.md");
            requirementFiles = ("docs/reqstream/plexus/routing/orthogonal-router.yaml");
        }
    }
}
