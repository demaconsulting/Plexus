package PlexusAvaloniaPropertyGrid {
    part def EditSession {
        doc /* Applies edits to the edited object immediately or staged until commit, with per-property errors, a commit scope hook and collection editing through a working copy. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditSession.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditMode.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertySessionChangedEventArgs.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PendingEdit.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CommitResult.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionEditor.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionApplier.cs, src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyCommitRefusedException.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionTests.cs, test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionCollectionTests.cs, test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditSessionUiSupportTests.cs, test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/CommitScopeTests.cs */
        comment designRef /* Design: docs/design/plexus-avalonia-property-grid/edit-session.md */
        comment verificationRef /* Verification: docs/verification/plexus-avalonia-property-grid/edit-session.md */
        comment reqRef /* Requirements: docs/reqstream/plexus-avalonia-property-grid/edit-session.yaml */
    }
}
