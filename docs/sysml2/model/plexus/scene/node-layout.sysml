package Plexus {
    private import SoftwareItemMetadata::*;

    part def NodeLayout {
        doc /* Node geometry, port placement and text layout and wrapping. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/NodeGeometry.cs", "src/DemaConsulting.Plexus/Scene/NodeLayout.cs", "src/DemaConsulting.Plexus/Scene/PortLayout.cs", "src/DemaConsulting.Plexus/Scene/LayoutText.cs", "src/DemaConsulting.Plexus/Scene/TextWrapper.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/NodeLayoutTests.cs");
            designFiles = ("docs/design/plexus/scene/node-layout.md");
            verificationFiles = ("docs/verification/plexus/scene/node-layout.md");
            requirementFiles = ("docs/reqstream/plexus/scene/node-layout.yaml");
        }
    }
}
