package Plexus {
    private import SoftwareItemMetadata::*;

    part def SceneBuilder {
        doc /* Builds the drawable scene (primitives, wire paths, group layout, node visuals) for one page. */

        metadata info : SoftwareInfo {
            description = "Builds the drawable scene (primitives, wire paths, group layout, node visuals) for one page.";
            sourceFiles = ("src/DemaConsulting.Plexus/Scene/SceneBuilder.cs", "src/DemaConsulting.Plexus/Scene/SceneComposer.Groups.cs", "src/DemaConsulting.Plexus/Scene/SceneComposer.cs", "src/DemaConsulting.Plexus/Scene/SceneOptions.cs", "src/DemaConsulting.Plexus/Scene/GridPrimitive.cs", "src/DemaConsulting.Plexus/Scene/IconPrimitive.cs", "src/DemaConsulting.Plexus/Scene/PathPrimitive.cs", "src/DemaConsulting.Plexus/Scene/RectPrimitive.cs", "src/DemaConsulting.Plexus/Scene/ScenePrimitive.cs", "src/DemaConsulting.Plexus/Scene/TextPrimitive.cs", "src/DemaConsulting.Plexus/Scene/WirePaths.cs", "src/DemaConsulting.Plexus/Scene/WireJunctions.cs", "src/DemaConsulting.Plexus/Scene/WireCrossings.cs", "src/DemaConsulting.Plexus/Scene/CollapsedGroupInfo.cs", "src/DemaConsulting.Plexus/Scene/GroupLayout.cs", "src/DemaConsulting.Plexus/Scene/INodeVisual.cs", "src/DemaConsulting.Plexus/Scene/NodeVisualContext.cs", "src/DemaConsulting.Plexus/Scene/StandardNodeVisual.cs", "src/DemaConsulting.Plexus/Scene/WireStyle.cs", "src/DemaConsulting.Plexus/Scene/WireStylers.cs", "src/DemaConsulting.Plexus/Scene/NodeStyle.cs", "src/DemaConsulting.Plexus/Scene/NodeStylers.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Scene/SceneBuilderTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WireVisualsTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WireJunctionAuditTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WireCrossingTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WiringAudit.cs", "test/DemaConsulting.Plexus.Tests/Scene/SceneGroupTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/WireStyleTests.cs", "test/DemaConsulting.Plexus.Tests/Scene/NodeStyleTests.cs");
            designFiles = ("docs/design/plexus/scene/scene-builder.md");
            verificationFiles = ("docs/verification/plexus/scene/scene-builder.md");
            requirementFiles = ("docs/reqstream/plexus/scene/scene-builder.yaml");
        }
    }
}
