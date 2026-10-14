package Plexus {
    part def Scene {
        doc /* Painter-independent scene graph: theme, node layout, text measurement, scene building and hit testing. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus/Scene/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Scene/SceneTests.cs */
        comment designRef /* Design: docs/design/plexus/scene.md */
        comment verificationRef /* Verification: docs/verification/plexus/scene.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/scene.yaml */

        part diagramTheme : DiagramTheme;
        part nodeLayout : NodeLayout;
        part textMeasurement : TextMeasurement;
        part sceneBuilder : SceneBuilder;
        part diagramScene : DiagramScene;
    }
}
