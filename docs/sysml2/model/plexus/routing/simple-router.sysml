package Plexus {
    private import SoftwareItemMetadata::*;

    part def SimpleRouter {
        doc /* Lightweight router that produces simple orthogonal routes without obstacle avoidance. */

        metadata info : SoftwareInfo {
            description = "Lightweight router that produces simple orthogonal routes without obstacle avoidance.";
            sourceFiles = ("src/DemaConsulting.Plexus/Routing/SimpleRouter.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Routing/SimpleRouterTests.cs");
            designFiles = ("docs/design/plexus/routing/simple-router.md");
            verificationFiles = ("docs/verification/plexus/routing/simple-router.md");
            requirementFiles = ("docs/reqstream/plexus/routing/simple-router.yaml");
        }
    }
}
