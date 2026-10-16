package Plexus {
    part def ConnectionRules {
        doc /* Connection validation: the default rule (single wire per input unless the port allows fan-in), composable stock rules and the host-extensible validator hook consulted when wires are created and during live drag previews. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/Connections/*.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Connections/ConnectionRulesTests.cs, test/DemaConsulting.Plexus.Tests/ConnectionTests.cs */
        comment designRef /* Design: docs/design/plexus/connection-rules.md */
        comment verificationRef /* Verification: docs/verification/plexus/connection-rules.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/connection-rules.yaml */
    }
}
