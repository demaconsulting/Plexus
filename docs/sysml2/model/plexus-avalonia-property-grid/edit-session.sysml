package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def EditSession {
        doc /* Applies edits to the edited object immediately or staged until commit, with per-property errors, a commit scope hook and collection editing through a working copy. */

        metadata info : SoftwareInfo {
            description = "Applies edits to the edited object immediately or staged until commit, with per-property errors, a commit scope hook and collection editing through a working copy.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditSession.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditMode.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertySessionChangedEventArgs.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PendingEdit.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CommitResult.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionEditor.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionApplier.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyCommitRefusedException.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionCollectionTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionUiSupportTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/CommitScopeTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/edit-session.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/edit-session.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/edit-session.yaml");
        }
    }
}
