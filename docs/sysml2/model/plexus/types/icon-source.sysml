package Plexus {
    private import SoftwareItemMetadata::*;

    part def IconSource {
        doc /* SVG icon descriptor supplied by hosts for node types. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Types/IconSource.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Types/IconSourceTests.cs", "test/DemaConsulting.Plexus.Tests/TypesAndGeometryTests.cs");
            designFiles = ("docs/design/plexus/types/icon-source.md");
            verificationFiles = ("docs/verification/plexus/types/icon-source.md");
            requirementFiles = ("docs/reqstream/plexus/types/icon-source.yaml");
        }
    }
}
