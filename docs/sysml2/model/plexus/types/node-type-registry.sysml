package Plexus {
    private import SoftwareItemMetadata::*;

    part def NodeTypeRegistry {
        doc /* Registry of node types, port definitions and port types, including host payload serializers, custom node visuals, per-node port providers (port sets) and the descriptions editors show as tooltips. */

        metadata info : SoftwareInfo {
            description = "Registry of node types, port definitions and port types, including host payload serializers, custom node visuals, per-node port providers (port sets) and the descriptions editors show as tooltips.";
            sourceFiles = ("src/DemaConsulting.Plexus/Types/NodeType.Ports.cs", "src/DemaConsulting.Plexus/Types/NodeType.Visual.cs", "src/DemaConsulting.Plexus/Types/NodeType.cs", "src/DemaConsulting.Plexus/Types/NodeTypeRegistry.cs", "src/DemaConsulting.Plexus/Types/PortDefinition.cs", "src/DemaConsulting.Plexus/Types/PortDirection.cs", "src/DemaConsulting.Plexus/Types/PortSet.cs", "src/DemaConsulting.Plexus/Types/PortType.cs", "src/DemaConsulting.Plexus/Types/PortTypeRegistry.cs", "src/DemaConsulting.Plexus/Types/FreezableList.cs", "src/DemaConsulting.Plexus/Types/INodeDataSerializer.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Types/NodeTypeRegistryTests.cs", "test/DemaConsulting.Plexus.Tests/TypesAndGeometryTests.cs", "test/DemaConsulting.Plexus.Tests/PortPathTests.cs", "test/DemaConsulting.Plexus.Tests/SharedPortNameTests.cs", "test/DemaConsulting.Plexus.Tests/PropertyBagTests.cs", "test/DemaConsulting.Plexus.Tests/Types/NodeTypeCategoryTests.cs");
            designFiles = ("docs/design/plexus/types/node-type-registry.md");
            verificationFiles = ("docs/verification/plexus/types/node-type-registry.md");
            requirementFiles = ("docs/reqstream/plexus/types/node-type-registry.yaml");
        }
    }
}
