package Plexus {
    private import SoftwareItemMetadata::*;

    part def Snapshot {
        doc /* Snapshot data model, JSON conversion and codecs for diagram content. */

        metadata info : SoftwareInfo {
            description = "Snapshot data model, JSON conversion and codecs for diagram content.";
            sourceFiles = ("src/DemaConsulting.Plexus/Serialization/Annotation.Snapshot.cs", "src/DemaConsulting.Plexus/Serialization/AnnotationSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/Diagram.Snapshot.cs", "src/DemaConsulting.Plexus/Serialization/DiagramPage.Snapshot.cs", "src/DemaConsulting.Plexus/Serialization/DiagramSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/GroupSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/Node.Snapshot.cs", "src/DemaConsulting.Plexus/Serialization/NodeSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/PageSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/PointSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/PropertyBag.Snapshot.cs", "src/DemaConsulting.Plexus/Serialization/PropertySnapshot.cs", "src/DemaConsulting.Plexus/Serialization/RectSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/SnapshotException.cs", "src/DemaConsulting.Plexus/Serialization/SnapshotFilter.cs", "src/DemaConsulting.Plexus/Serialization/SnapshotWriter.cs", "src/DemaConsulting.Plexus/Serialization/WireSnapshot.cs", "src/DemaConsulting.Plexus/Serialization/PlexusJson.cs", "src/DemaConsulting.Plexus/Serialization/PlexusJsonContext.cs", "src/DemaConsulting.Plexus/Serialization/PropertyCodec.cs", "src/DemaConsulting.Plexus/Serialization/NodeDataCodec.cs", "src/DemaConsulting.Plexus/Serialization/PropertyKindJsonConverter.cs", "src/DemaConsulting.Plexus/Serialization/RawJsonStringConverter.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Serialization/SnapshotTests.cs", "test/DemaConsulting.Plexus.Tests/DiagramJsonTests.cs");
            designFiles = ("docs/design/plexus/serialization/snapshot.md");
            verificationFiles = ("docs/verification/plexus/serialization/snapshot.md");
            requirementFiles = ("docs/reqstream/plexus/serialization/snapshot.yaml");
        }
    }
}
