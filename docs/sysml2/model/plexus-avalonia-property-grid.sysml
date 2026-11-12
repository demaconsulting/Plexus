package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Generic, annotation-driven property grid for Avalonia: the UI-free layers and the Avalonia controls (grid, editors, collection editor, dialog) that show and edit any annotated .NET object through System.ComponentModel annotations, type converters, edit sessions and a pluggable editor registry.
     */

    part def PlexusAvaloniaPropertyGridSystem {
        doc /* Generic, annotation-driven property grid for Avalonia: the UI-free layers and the Avalonia controls (grid, editors, collection editor, dialog) that show and edit any annotated .NET object through System.ComponentModel annotations, type converters, edit sessions and a pluggable editor registry. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/NamespaceDoc.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PlexusAvaloniaPropertyGridTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid.yaml");
        }

        part propertyModel : PropertyModel;
        part propertySources : PropertySources;
        part editSession : EditSession;
        part editorRegistry : EditorRegistry;
        part propertyGridControl : PropertyGridControl;
        part propertyEditors : PropertyEditors;
        part collectionEditorUi : CollectionEditorUi;
        part propertyGridDialog : PropertyGridDialog;
        part plexusNodeSource : PlexusNodeSource;
        part nodePropertiesDialog : NodePropertiesDialog;
        ref part avalonia : OTS::Avalonia;
        ref part core : Plexus::PlexusSystem;
    }
}
