package PlexusAvaloniaPropertyGrid {
    private import SoftwareItemMetadata::*;

    part def PropertySources {
        doc /* Supplies property items: reflection over any object through the component model, host-supplied items, merging of sources, source selection by factory and category and item ordering. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia.PropertyGrid/IPropertySource.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/IPropertySourceProvider.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/IPropertyCommitScope.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertySourceChangedEventArgs.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/ReflectionPropertySource.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CustomPropertySource.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CustomPropertyItem.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CustomPropertyOptions.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/CompositePropertySource.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertySourceFactory.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyOrdering.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertySortMode.cs", "src/DemaConsulting.Plexus.Avalonia.PropertyGrid/PropertyGroup.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests/PropertySourceTests.cs");
            designFiles = ("docs/design/plexus-avalonia-property-grid/property-sources.md");
            verificationFiles = ("docs/verification/plexus-avalonia-property-grid/property-sources.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia-property-grid/property-sources.yaml");
        }
    }
}
