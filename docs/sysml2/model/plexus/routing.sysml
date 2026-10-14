package Plexus {
    part def Routing {
        doc /* Wire routing strategies producing orthogonal waypoints between ports. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/Routing/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Routing/RoutingTests.cs */
        comment designRef /* Design: docs/design/plexus/routing.md */
        comment verificationRef /* Verification: docs/verification/plexus/routing.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/routing.yaml */

        part orthogonalRouter : OrthogonalRouter;
        part simpleRouter : SimpleRouter;
    }
}
