package Plexus_Scene {
    public import Plexus_DiagramScene::*;
    public import Plexus_DiagramTheme::*;
    public import Plexus_NodeLayout::*;
    public import Plexus_SceneBuilder::*;
    public import Plexus_TextMeasurement::*;
    private import SoftwareItemMetadata::*;

    part def Scene {
        doc /* Painter-independent scene graph: theme, node layout, text measurement, scene building and hit testing. */

        metadata info : SoftwareInfo {
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
