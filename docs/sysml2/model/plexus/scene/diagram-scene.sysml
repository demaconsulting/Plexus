package Plexus {
    private import SoftwareItemMetadata::*;

    part def DiagramScene {
        doc /* Built scene with hit testing and a spatial index for visible-region culling. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/DiagramScene.cs", "src/DemaConsulting.Plexus/Scene/HitKind.cs", "src/DemaConsulting.Plexus/Scene/HitResult.cs", "src/DemaConsulting.Plexus/Scene/HitTarget.cs", "src/DemaConsulting.Plexus/Scene/SpatialIndex.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/SceneHitTestTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WireVisualsTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/SpatialIndexTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/SceneGroupLayoutTests.cs");
            designFiles = ("docs/design/plexus/scene/diagram-scene.md");
            verificationFiles = ("docs/verification/plexus/scene/diagram-scene.md");
            requirementFiles = ("docs/reqstream/plexus/scene/diagram-scene.yaml");
        }
    }
}
