package Plexus {
    private import SoftwareItemMetadata::*;

    part def DiagramTheme {
        doc /* Visual theme and geometry metrics shared by the model, router and painters. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/DiagramTheme.Appearance.cs", "src/DemaConsulting.Plexus/Scene/DiagramTheme.cs", "src/DemaConsulting.Plexus/Scene/TextStyle.cs", "src/DemaConsulting.Plexus/Scene/TextAlignment.cs", "src/DemaConsulting.Plexus/Scene/PortAlignment.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/DiagramThemeTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/PortAlignmentTests.cs");
            designFiles = ("docs/design/plexus/scene/diagram-theme.md");
            verificationFiles = ("docs/verification/plexus/scene/diagram-theme.md");
            requirementFiles = ("docs/reqstream/plexus/scene/diagram-theme.yaml");
        }
    }
}
