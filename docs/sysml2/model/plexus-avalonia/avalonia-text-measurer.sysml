package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def AvaloniaTextMeasurer {
        doc /* Text measurer backed by Avalonia text APIs. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/AvaloniaTextMeasurer.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/AvaloniaTextMeasurerTests.cs");
            designFiles = ("docs/design/plexus-avalonia/avalonia-text-measurer.md");
            verificationFiles = ("docs/verification/plexus-avalonia/avalonia-text-measurer.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/avalonia-text-measurer.yaml");
        }
    }
}
