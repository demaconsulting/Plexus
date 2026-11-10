package Plexus {
    private import SoftwareItemMetadata::*;

    part def Types {
        doc /* Host-declared node types, port definitions, port types and icon sources. */

        metadata info : SoftwareInfo {
            description = "Host-declared node types, port definitions, port types and icon sources.";
            testFiles = ("test/DemaConsulting.Plexus.Tests/Types/TypesTests.cs");
            designFiles = ("docs/design/plexus/types.md");
            verificationFiles = ("docs/verification/plexus/types.md");
            requirementFiles = ("docs/reqstream/plexus/types.yaml");
        }

        part nodeTypeRegistry : NodeTypeRegistry;
        part iconSource : IconSource;
    }
}
