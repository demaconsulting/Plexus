package Plexus {
    private import SoftwareItemMetadata::*;

    part def Wire {
        doc /* Directed connection from an output port to an input port with a persisted route. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Model/Wire.cs", "src/DemaConsulting.Plexus/Model/ReconnectCommand.cs", "src/DemaConsulting.Plexus/Model/WireTraffic.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Model/WireTests.cs", "test/DemaConsulting.Plexus.Tests/Model/WireEditingTests.cs", "test/DemaConsulting.Plexus.Tests/ConnectionTests.cs", "test/DemaConsulting.Plexus.Tests/ElementConvenienceTests.cs");
            designFiles = ("docs/design/plexus/model/wire.md");
            verificationFiles = ("docs/verification/plexus/model/wire.md");
            requirementFiles = ("docs/reqstream/plexus/model/wire.yaml");
        }
    }
}
