# PlexusAvalonia System Verification Design

Scope: Avalonia editor control that shows one diagram page at a time with pointer and keyboard editing, inline rename,
host events and an operating-system clipboard bridge.

## Verification Approach

System verification is end to end: a real editor control is hosted in a headless Avalonia window together with a real
core diagram, and the tests drive it the way a user would, with simulated pointer, wheel, keyboard and drag-and-drop
input, then check the diagram, the undo history, the selection, the scene and the rendered frame. There are no mocks
except where a failure has to be provoked (a clipboard that always fails). The tests are xUnit tests in
`PlexusAvaloniaTests.cs` in the PlexusAvalonia test project and share the fixtures `EditorHost.cs` (a small diagram
model with three node types, a window holding the editor, and helpers that move, click, drag and type like a user),
`TestApp.cs` (the minimal Avalonia application) and `UiTest.cs` (runs each test body on the UI thread of one headless
session for the whole run). Subsystem and unit behavior is verified separately (see the subsystem and unit verification
designs), and Avalonia and Svg.Controls.Skia.Avalonia are verified as OTS items through these tests (see _Avalonia
Verification_ and _SvgSkia Verification_).

## Test Environment

- .NET 10 test runner, run on Windows, Linux and macOS in the build pipeline; the platform requirements use source
  filters so each platform's own results are the evidence.
- The Avalonia headless platform with Skia drawing (real text measurement and real rendered frames) and the Fluent
  theme, so that text boxes behave like those of an application. No display, network or other service is needed.
- Tests that use the system clipboard rely on the headless platform's clipboard and do not depend on the machine's
  clipboard. Tests that save pictures for inspection write them to the temporary folder of the operating system.
- The window is 800 by 600 units and the editor fills it, so window coordinates equal editor coordinates, and the view
  starts at zoom 1 without offset.

## Acceptance Criteria

A system test passes when the observable result of the user scenario is as described: the model, history, selection,
view, events and rendered frame match, nothing outside the scenario changed, and no exception escaped. All system tests
must pass on every supported platform for the platform requirements to be satisfied.

## Test Scenarios

- **PlexusAvalonia_Display_ConnectedPage_ShowsSceneAndFollowsModelChanges**: a connected pair is shown; moving a node
  and adding another through the model changes the scene (rebuilt, wire route follows, new node known).
- **PlexusAvalonia_Selection_ClickBandAndSelectAll_SelectElements**: click, Shift-click, click on canvas, rubber band and
  Ctrl+A select the expected elements, and no history is recorded.
- **PlexusAvalonia_DragMove_SnapsAndRecordsOneUndoEntry**: a drag of an off-grid distance lands on the grid, one undo
  entry is recorded, and one Ctrl+Z restores the position.
- **PlexusAvalonia_CtrlDrag_DuplicatesSelection**: a Ctrl-drag leaves the original and moves a uniquely named, selected
  copy.
- **PlexusAvalonia_PortDrag_CompatibleAndIncompatiblePorts_ConnectsOnlyWhenAllowed**: with the same-type rule, a drag to
  a mismatching port connects nothing and a drag from the input of a matching node back to the output connects in
  output-to-input order.
- **PlexusAvalonia_WireSegmentDrag_MovesInteriorSegmentWithOneUndoEntry**: an interior segment moves sideways, the port
  ends stay, one history entry is recorded, and undo restores the route.
- **PlexusAvalonia_WireEditing_BendStubJogAndEndpoint_AreSingleUndoEntries**: on a selected wire, dragging a bend handle,
  pulling the port stub, double-clicking a segment (jog) and dragging the target end handle onto another input each
  record exactly one history entry; the wire keeps its identity and four Ctrl+Z presses restore the original route
  and target.
- **PlexusAvalonia_Annotation_ResizeAndInlineEdit_AreUndoable**: the corner handle resizes (one entry), a double-click
  opens the text editor, Ctrl+Enter commits (one entry) and undo restores the text.
- **PlexusAvalonia_GroupToggle_CollapsesAndExpandsUndoably**: clicking the toggle collapses (one entry), undo expands,
  and two more clicks collapse and expand again.
- **PlexusAvalonia_PanAndZoom_ChangeViewWithoutEditingModel**: Ctrl+wheel zooms around the cursor, the wheel scrolls,
  the middle button pans, zoom-to-fit fits two far apart nodes, and the history is untouched.
- **PlexusAvalonia_KeyboardShortcuts_DeleteUndoRedoSelectAllAndDuplicate**: Delete removes a node with its wire,
  Ctrl+Z restores both, Ctrl+Y and Ctrl+Shift+Z redo, Ctrl+A and Ctrl+D duplicate everything, F2 opens the rename box.
- **PlexusAvalonia_Undo_DuringDrag_CancelsOnlyTheGesture**: Ctrl+Z mid-drag restores the position without touching the
  history; a second Ctrl+Z removes the node added before.
- **PlexusAvalonia_Rename_DuplicateThenValidName_RejectsThenCommits**: double-clicking the name opens the editor; a taken
  name and a name vetoed by a host handler are rejected with reasons and the editor stays open; a valid name commits and
  undo restores the old name.
- **PlexusAvalonia_Clipboard_SystemClipboard_CarriesPlexusFormatAndTextFallback**: copy writes the Plexus format and an
  equal plain-text copy to the window's clipboard and paste adds the node again.
- **PlexusAvalonia_Clipboard_Failures_RaiseHostEvents**: pasting plain words raises `PasteFailed` and a failing
  clipboard on Ctrl+C raises `ClipboardFailed`, with the diagram unchanged.
- **PlexusAvalonia_DropNodeType_OntoEditor_CreatesNode**: dropping a node type payload creates a selected node centered
  at the drop point as one undo entry.
- **PlexusAvalonia_NodePalette_ItemsLookLikeCanvasNodes_AndActivate**: a palette item and the same type dropped on the
  editor are pixel-identical (a node with icon, ports and summary at zoom 1, and a custom-visual node on a light theme
  at zoom 1.5), the palette lists every registry type and a double-click activates a type.
- **PlexusAvalonia_HostEvents_DoubleClickAndContextRequest_AreRaised**: double-click on a node body raises
  `NodeDoubleClicked`; right-click on a node and on the canvas raise `DiagramContextRequested` with the element and the
  diagram position.
- **PlexusAvalonia_ReadOnly_DragAndDelete_LeaveDiagramUnchanged**: a read-only editor selects but does not move or
  delete.
- **PlexusAvalonia_Icons_ValidAndMalformedSvg_DisplayWithoutFailure**: a good icon converts, a malformed icon is
  missing without error, both nodes appear in the scene and a frame is rendered.
- **StyledNode_PaintsTheSameInTheEditorAndThePng**: a node styled with a fill, border, halo and tinted icon, and a
  half-opaque node, are rendered in the headless editor and by the PNG renderer at zoom 1 and 2; the fill and halo
  pixels agree within a tolerance of 12 per channel and at most 5% of the icon pixels (2% of the halo) differ.
- **Editor_NodeStyler_AssignAndRefresh_RepaintsAndPaletteStaysDefault**: assigning the styler and refreshing the
  appearance change the node on the canvas, while the palette prototype of the same type keeps the default look.
- **PlexusAvalonia_Layout_LongerNodeName_WidensNameLabel**: a longer node name gets a wider laid-out label.
- **PlexusAvalonia_Headless_HostedEditor_LaysOutAndRendersFrame**: the editor fills the 800 by 600 window and a frame of
  that size is rendered. This also proves that the headless platform works at all.
- **PlexusAvalonia_EditingSession_ConnectRenderAndUndo_WorksOnThisPlatform**: connect by dragging, render a frame and
  undo. This is the platform-evidence scenario for every platform requirement.

- **PlexusAvalonia_ToolTips_HoverElements_ShowDescriptionsAndHostOverrides**: hovering a node shows its type
  description, hovering a port shows the host-extended port text, and turning tooltips off clears the tooltip.
- **PlexusAvalonia_ElementNavigation_FindAndFit_ShowElements**: a node found by name on another page is brought into view
  (the page switches and the node is visible), and fitting two far-apart nodes of the first page shows both.
- **PlexusAvalonia_DynamicPorts_PropertyChange_EditorConnectsAndUndoes**: a port added by a property change is connected
  by dragging; lowering the count removes the wire; undo restores the wire on the same port and the scene shows four
  ports again.
