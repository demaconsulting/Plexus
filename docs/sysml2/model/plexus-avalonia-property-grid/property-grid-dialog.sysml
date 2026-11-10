package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def PropertyGridDialog {
        doc /* A one-call modal dialog that edits an object in a staged property grid: OK commits (optionally inside a host commit scope) and Cancel discards. */

        metadata info : SoftwareInfo {
            description = "A one-call modal dialog that edits an object in a staged property grid: OK commits (optionally inside a host commit scope) and Cancel discards.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGridDialog.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyGridDialogTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/property-grid-dialog.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/property-grid-dialog.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/property-grid-dialog.yaml");
        }
    }
}
