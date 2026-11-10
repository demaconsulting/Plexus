package Plexus {
    private import SoftwareItemMetadata::*;

    part def ConnectionRules {
        doc /* Connection validation: the default rule (single wire per input unless the port allows fan-in), composable stock rules and the host-extensible validator hook consulted when wires are created and during live drag previews. */

        metadata info : SoftwareInfo {
            description = "Connection validation: the default rule (single wire per input unless the port allows fan-in), composable stock rules and the host-extensible validator hook consulted when wires are created and during live drag previews.";
            sourceFiles = ("src/DemaConsulting.Plexus/Connections/ConnectionCheck.cs", "src/DemaConsulting.Plexus/Connections/ConnectionRules.cs", "src/DemaConsulting.Plexus/Connections/ConnectionValidator.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Connections/ConnectionRulesTests.cs", "test/DemaConsulting.Plexus.Tests/ConnectionTests.cs");
            designFiles = ("docs/design/plexus/connection-rules.md");
            verificationFiles = ("docs/verification/plexus/connection-rules.md");
            requirementFiles = ("docs/reqstream/plexus/connection-rules.yaml");
        }
    }
}
