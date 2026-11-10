package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def CollectionEditorUi {
        doc /* The collection editor view and dialog: a list of members with Add, Remove and Move buttons and a nested property grid for the selected member, applying through the owning session on OK. */

        metadata info : SoftwareInfo {
            description = "The collection editor view and dialog: a list of members with Add, Remove and Move buttons and a nested property grid for the selected member, applying through the owning session on OK.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionEditorView.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionEditorDialog.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/CollectionEditorUiTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/collection-editor-ui.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/collection-editor-ui.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/collection-editor-ui.yaml");
        }
    }
}
