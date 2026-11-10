package PlexusAvalonia {
    private import SoftwareItemMetadata::*;

    doc
    /*
     * Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename, host events and an operating-system clipboard bridge.
     */

    part def PlexusAvaloniaSystem {
        doc /* Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename, host events and an operating-system clipboard bridge. */

        metadata info : SoftwareInfo {
            description = "Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename, host events and an operating-system clipboard bridge.";
            testFiles = ("test/DemaConsulting.Plexus.Avalonia.Tests/PlexusAvaloniaTests.cs");
            designFiles = ("docs/design/plexus-avalonia.md");
            verificationFiles = ("docs/verification/plexus-avalonia.md");
            requirementFiles = ("docs/reqstream/plexus-avalonia.yaml");
        }

        ref part core : Plexus::PlexusSystem;
        part editor : Editor;
        part scenePainter : ScenePainter;
        part avaloniaTextMeasurer : AvaloniaTextMeasurer;
        part plexusIcons : PlexusIcons;
        ref part avalonia : OTS::Avalonia;
        ref part svgSkia : OTS::SvgSkia;
    }
}
