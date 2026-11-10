package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    part def NodePalette {
        doc /* Palette control that lists node types, each drawn as it looks on the canvas, and lets the user drag or activate them. */

        metadata info : SoftwareInfo {
            description = "Palette control that lists node types, each drawn as it looks on the canvas, and lets the user drag or activate them.";
            sourceFiles = ("src/DemaConsulting.Plexus.Avalonia/NodePalette.Items.cs", "src/DemaConsulting.Plexus.Avalonia/NodePalette.cs", "src/DemaConsulting.Plexus.Avalonia/NodePaletteAutomationPeer.cs", "src/DemaConsulting.Plexus.Avalonia/NodePaletteItem.cs", "src/DemaConsulting.Plexus.Avalonia/NodePaletteItemAutomationPeer.cs", "src/DemaConsulting.Plexus.Avalonia/NodePalettePanel.cs", "src/DemaConsulting.Plexus.Avalonia/NodeTypeActivatedEventArgs.cs");
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodePaletteColumnTests.cs", "test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodePaletteEditorTests.cs", "test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodePalettePixelTests.cs", "test/DemaConsulting.Plexus.Avalonia.Tests/Editor/NodePaletteTests.cs");
            designFiles = ("docs/design/plexus-avalonia/editor/node-palette.md");
            verificationFiles = ("docs/verification/plexus-avalonia/editor/node-palette.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia/editor/node-palette.yaml");
        }
    }
}
