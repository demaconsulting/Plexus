package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def PropertyGridControl {
        doc /* The PropertyGrid Avalonia control: two-column layout, categories, sorting, search, expansion, selection, description pane and keyboard handling over a property source and an edit session. */

        metadata info : SoftwareInfo {
            description = "The PropertyGrid Avalonia control: two-column layout, categories, sorting, search, expansion, selection, description pane and keyboard handling over a property source and an edit session.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGrid.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGrid.Layout.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGrid.Rows.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGrid.Input.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/GridRowViews.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/GridTheme.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionEditorRequestedEventArgs.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyGridDisplayTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyGridKeyboardTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyGridScreenshotTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/GridHarness.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/property-grid-control.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/property-grid-control.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/property-grid-control.yaml");
        }
    }
}
