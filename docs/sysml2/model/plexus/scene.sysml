package Plexus {
    private import SoftwareItemMetadata::*;

    part def Scene {
        doc /* Painter-independent scene graph: theme, node layout, text measurement, scene building and hit testing. */

        metadata info : SoftwareInfo {
            description = "Painter-independent scene graph: theme, node layout, text measurement, scene building and hit testing.";
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/SceneTests.cs");
            designFiles = ("docs/design/plexus/scene.md");
            verificationFiles = ("docs/verification/plexus/scene.md");
            requirementFiles = ("docs/reqstream/plexus/scene.yaml");
        }

        part diagramTheme : DiagramTheme;
        part nodeLayout : NodeLayout;
        part textMeasurement : TextMeasurement;
        part sceneBuilder : SceneBuilder;
        part diagramScene : DiagramScene;
    }
}
