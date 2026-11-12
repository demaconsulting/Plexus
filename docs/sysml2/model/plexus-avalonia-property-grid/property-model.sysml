package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def PropertyModel {
        doc /* Describes one property row: metadata from annotations, value access, type-converter text conversion, standard values, expandable children, collection elements and validation results. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyItem.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyItem.Text.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyItem.Structure.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/ReflectionPropertyItem.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionElementItem.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CollectionTypes.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyContext.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyErrors.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyResult.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/StandardValue.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyAnnotationTests.cs", "test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertyModelTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/property-model.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/property-model.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/property-model.yaml");
        }
    }
}
