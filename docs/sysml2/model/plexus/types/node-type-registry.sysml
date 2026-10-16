package Plexus {
    part def NodeTypeRegistry {
        doc /* Registry of node types, port definitions and port types, including host payload serializers, custom node visuals, per-node port providers (port sets) and the descriptions editors show as tooltips. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/Types/NodeType*.cs, src/DemaConsulting.Plexus/Types/Port*.cs, src/DemaConsulting.Plexus/Types/FreezableList.cs, src/DemaConsulting.Plexus/Types/INodeDataSerializer.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Types/NodeTypeRegistryTests.cs, test/DemaConsulting.Plexus.Tests/TypesAndGeometryTests.cs, test/DemaConsulting.Plexus.Tests/PortPathTests.cs, test/DemaConsulting.Plexus.Tests/SharedPortNameTests.cs, test/DemaConsulting.Plexus.Tests/PropertyBagTests.cs, test/DemaConsulting.Plexus.Tests/Types/NodeTypeCategoryTests.cs */
        comment designRef /* Design: docs/design/plexus/types/node-type-registry.md */
        comment verificationRef /* Verification: docs/verification/plexus/types/node-type-registry.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/types/node-type-registry.yaml */
    }
}
