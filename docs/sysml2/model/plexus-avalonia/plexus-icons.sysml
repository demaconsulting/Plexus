package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def PlexusIcons {
        doc /* Converts host SVG icons to cached Avalonia images, tolerating malformed SVG. */

        metadata info : SoftwareInfo {
            description = "Converts host SVG icons to cached Avalonia images, tolerating malformed SVG.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/PlexusIcons.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/PlexusIconsTests.cs");
            designFiles = ("docs/design/plexus-avalonia/plexus-icons.md");
            verificationFiles = ("docs/verification/plexus-avalonia/plexus-icons.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/plexus-icons.yaml");
        }
    }
}
