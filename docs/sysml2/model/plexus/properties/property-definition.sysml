package Plexus {
    private import SoftwareItemMetadata::*;

    part def PropertyDefinition {
        doc /* Property metadata: name, value kind, default, constraints and editor and display-order hints. */

        metadata info : SoftwareInfo {
            description = "Property metadata: name, value kind, default, constraints and editor and display-order hints.";
            sourceFiles = ("src/DemaConsulting.Plexus/Properties/PropertyDefinition.cs", "src/DemaConsulting.Plexus/Properties/PropertyValueKind.cs", "src/DemaConsulting.Plexus/Properties/PropertyValues.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Properties/PropertyDefinitionTests.cs", "test/DemaConsulting.Plexus.Tests/PropertyBagTests.cs");
            designFiles = ("docs/design/plexus/properties/property-definition.md");
            verificationFiles = ("docs/verification/plexus/properties/property-definition.md");
            requirementFiles = ("docs/reqstream/plexus/properties/property-definition.yaml");
        }
    }
}
