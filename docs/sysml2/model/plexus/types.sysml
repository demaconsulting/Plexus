package Plexus {
    part def Types {
        doc /* Host-declared node types, port definitions, port types and icon sources. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Types/TypesTests.cs */
        comment designRef /* Design: docs/design/plexus/types.md */
        comment verificationRef /* Verification: docs/verification/plexus/types.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/types.yaml */

        part nodeTypeRegistry : NodeTypeRegistry;
        part iconSource : IconSource;
    }
}
