package Plexus {
    part def Serialization {
        doc /* Versioned JSON clipboard interchange and the snapshot model behind it. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/Serialization/SerializationTests.cs */
        comment designRef /* Design: docs/design/plexus/serialization.md */
        comment verificationRef /* Verification: docs/verification/plexus/serialization.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/serialization.yaml */

        part snapshotUnit : Snapshot;
        part plexusClipboard : PlexusClipboard;
    }
}
