package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def NodePropertiesDialog {
        doc /* A one-call modal properties dialog for a Plexus node: a staged property grid over the node source whose OK applies everything as one undo entry, with an optional live preview next to the grid (a custom node inspector) that reads the staged values through a read-only preview context, which hosts can also use over their own grid or session. */

        metadata info : SoftwareInfo {
            description = "A one-call modal properties dialog for a Plexus node: a staged property grid over the node source whose OK applies everything as one undo entry, with an optional live preview next to the grid (a custom node inspector) that reads the staged values through a read-only preview context, which hosts can also use over their own grid or session.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/Diagrams/NodePropertiesDialog.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/Diagrams/NodePropertiesDialogOptions.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/Diagrams/NodePreviewPlacement.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/Diagrams/NodePreviewContext.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/NodePropertiesDialogTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/NodePropertiesDialogPreviewTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/node-properties-dialog.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/node-properties-dialog.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/node-properties-dialog.yaml");
        }
    }
}
