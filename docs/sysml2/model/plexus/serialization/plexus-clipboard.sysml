package Plexus {
    private import SoftwareItemMetadata::*;

    part def PlexusClipboard {
        doc /* Clipboard envelope, selection, safe import (atomic, size-limited) and element duplication. */

        metadata info : SoftwareInfo {
            description = "Clipboard envelope, selection, safe import (atomic, size-limited) and element duplication.";
            sourceFiles = ("src/DemaConsulting.Plexus/Serialization/PlexusClipboard.cs", "src/DemaConsulting.Plexus/Serialization/ClipboardEnvelope.cs", "src/DemaConsulting.Plexus/Serialization/ClipboardSelection.cs", "src/DemaConsulting.Plexus/Serialization/ElementContent.cs", "src/DemaConsulting.Plexus/Serialization/ElementImporter.cs", "src/DemaConsulting.Plexus/Serialization/ImportOptions.cs", "src/DemaConsulting.Plexus/Serialization/PreparedImport.cs", "src/DemaConsulting.Plexus/Serialization/PasteResult.cs", "src/DemaConsulting.Plexus/Serialization/DiagramPage.Snapshot.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/Serialization/ClipboardTests.cs", "test/DemaConsulting.Plexus.Tests/Serialization/ClipboardSecurityTests.cs", "test/DemaConsulting.Plexus.Tests/Serialization/DuplicateTests.cs");
            designFiles = ("docs/design/plexus/serialization/plexus-clipboard.md");
            verificationFiles = ("docs/verification/plexus/serialization/plexus-clipboard.md");
            requirementFiles = ("docs/reqstream/plexus/serialization/plexus-clipboard.yaml");
        }
    }
}
