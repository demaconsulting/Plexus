package Plexus {
    private import SoftwareItemMetadata::*;

    part def Serialization {
        doc /* Versioned JSON clipboard interchange and the snapshot model behind it. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Plexus.Tests/Serialization/SerializationTests.cs");
            designFiles = ("docs/design/plexus/serialization.md");
            verificationFiles = ("docs/verification/plexus/serialization.md");
            requirementFiles = ("docs/reqstream/plexus/serialization.yaml");
        }

        part snapshotUnit : Snapshot;
        part plexusClipboard : PlexusClipboard;
    }
}
