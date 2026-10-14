package PlexusAvalonia {
    doc
    /*
     * Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename, host events and an operating-system clipboard bridge.
     */

    part def PlexusAvaloniaSystem {
        doc /* Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename, host events and an operating-system clipboard bridge. */

        comment sourceRef /* Source: src/DemaConsulting.Plexus.Avalonia/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.Plexus.Avalonia.Tests/PlexusAvaloniaTests.cs */
        comment designRef /* Design: docs/design/plexus-avalonia.md */
        comment verificationRef /* Verification: docs/verification/plexus-avalonia.md */
        comment reqRef /* Requirements: docs/reqstream/plexus-avalonia.yaml */

        ref part core : Plexus::PlexusSystem;
        part editor : Editor;
        part scenePainter : ScenePainter;
        part avaloniaTextMeasurer : AvaloniaTextMeasurer;
        part plexusIcons : PlexusIcons;
        ref part avalonia : OTS::Avalonia;
        ref part svgSkia : OTS::SvgSkia;
    }
}
