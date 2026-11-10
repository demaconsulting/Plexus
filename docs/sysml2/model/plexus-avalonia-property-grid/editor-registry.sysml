package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def EditorRegistry {
        doc /* Names custom editors by annotation and maps editor keys and property types to the editors of the user interface layer. */

        metadata info : SoftwareInfo {
            description = "Names custom editors by annotation and maps editor keys and property types to the editors of the user interface layer.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditorAttribute.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyEditorRegistry.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/EditorRegistryTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/editor-registry.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/editor-registry.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/editor-registry.yaml");
        }
    }
}
