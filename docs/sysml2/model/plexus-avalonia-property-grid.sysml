package PlexusAvaloniaPropertyGrid {
    doc
    /*
     * Generic, annotation-driven property grid for Avalonia: the UI-free layers and the Avalonia controls (grid, editors, collection editor, dialog) that show and edit any annotated .NET object through System.ComponentModel annotations, type converters, edit sessions and a pluggable editor registry.
     */

    part def PlexusAvaloniaPropertyGridSystem {
        doc /* Generic, annotation-driven property grid for Avalonia: the UI-free layers and the Avalonia controls (grid, editors, collection editor, dialog) that show and edit any annotated .NET object through System.ComponentModel annotations, type converters, edit sessions and a pluggable editor registry. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus.Avalonia.PropertyGrid/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PlexusAvaloniaPropertyGridTests.cs */
        comment designRef /* Design: docs/design/plexus-avalonia-property-grid.md */
        comment verificationRef /* Verification: docs/verification/plexus-avalonia-property-grid.md */
        comment reqRef /* Requirements: docs/reqstream/plexus-avalonia-property-grid.yaml */

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
