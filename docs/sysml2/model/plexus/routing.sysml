package Plexus {
    private import SoftwareItemMetadata::*;

    part def Routing {
        doc /* Wire routing strategies producing orthogonal waypoints between ports. */

        metadata info : SoftwareInfo {
            description = "Wire routing strategies producing orthogonal waypoints between ports.";
            testFiles = ("test/DemaConsulting.Plexus.Tests/Routing/RoutingTests.cs");
            designFiles = ("docs/design/plexus/routing.md");
            verificationFiles = ("docs/verification/plexus/routing.md");
            requirementFiles = ("docs/reqstream/plexus/routing.yaml");
        }

        part orthogonalRouter : OrthogonalRouter;
        part simpleRouter : SimpleRouter;
    }
}
