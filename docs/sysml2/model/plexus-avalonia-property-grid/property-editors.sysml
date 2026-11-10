package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def PropertyEditors {
        doc /* The editors of the value column of a property grid row (text, password, check box, drop-downs, flags, color, nested, collection), the editor context that connects them to the edit session and the default editor registry. */

        metadata info : SoftwareInfo {
            description = "The editors of the value column of a property grid row (text, password, check box, drop-downs, flags, color, nested, collection), the editor context that connects them to the edit session and the default editor registry.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditorContext.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/BuiltInEditors.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/ColorSupport.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/GlobalUsings.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyGridEditingTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/ColorEditorTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/property-editors.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/property-editors.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/property-editors.yaml");
        }
    }
}
