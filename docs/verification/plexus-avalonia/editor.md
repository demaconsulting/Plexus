## Editor Subsystem Verification Design

Scope: Interactive editor control: view, input gestures, clipboard bridge and drag-and-drop of node types.

### Verification Approach

The subsystem is verified by integration tests that exercise the control, the gestures, the clipboard bridge and the
node type drag helper together, in a headless Avalonia window with a real core diagram. The boundary of the subsystem
is the host surface (properties, commands and events of the control) on one side and the core model on the other, so
the tests assert on the model, the history, the selection and the events, never on private state. Nothing is mocked
except where a failure must be provoked: a clipboard double that always throws stands in for a locked desktop session,
and an in-memory clipboard keeps tests independent of the machine's clipboard. The tests are in `EditorTests.cs` and
share the fixtures described in the PlexusAvalonia system verification design.

### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing and the Fluent theme, started once per
test run by the shared fixtures. No other setup is needed.

### Acceptance Criteria

A subsystem test passes when the units behave together as specified: edits reach the model through the gestures as one
undo entry per drag, abandoned interactions leave no history, inline edits commit or cancel through shared commands,
clipboard and drop paths reach the model or raise the right events, and read-only mode blocks editing everywhere.

### Test Scenarios

- **Editor_SceneSync_ModelThemeAndViewChanges_KeepSceneAndHitTestingConsistent**: after a model change, a theme
  override, zoom and scroll, the scene is new and uses the theme, hit testing at the node's on-screen center finds the
  node, and a frame renders.
- **Editor_PointerEditing_SelectMoveConnectResizeBandAndPan_EditModelWithOneUndoStepEach**: in sequence, a click selects
  without history, a drag moves (one entry), a port drag connects (one entry), the annotation handle resizes (one
  entry), a band selects without history and a middle-button drag pans without history.
- **Editor_GestureInFlight_EscapeUndoRedoAndPageChange_CancelWithoutHistory**: four drags are each abandoned by Escape,
  Undo, Redo and a page change; each restores the position with an untouched history, and Undo and Redo report that
  they acted.
- **Editor_InlineEditing_RenameAndAnnotation_CommitOrCancel**: a taken name is rejected with a reason; opening an
  annotation edit replaces the rename; Ctrl+Enter commits one entry; clicking the canvas commits a valid rename; Escape
  cancels.
- **Editor_Clipboard_CopyCutPasteAndFailures_GoThroughBridgeAndRaiseEvents**: copy writes Plexus JSON to the host
  clipboard, paste at a position adds the content, cut removes it, pasting plain words raises `PasteFailed` and a
  failing clipboard on Ctrl+C raises `ClipboardFailed`.
- **Editor_NodeDrop_PayloadDroppedOnEditor_CreatesSelectedNodeAndIgnoresUnknownTypes**: a drop creates a centered,
  selected node as one entry and an unknown type creates nothing; a drag source can be attached to a control.
- **Editor_NodePalette_DragItemOverEditor_ShowsGhostAndDropsNodeWhereGhostWas**: a palette over the registry lists every
  type; dragging an item past the threshold over the editor shows the ghost of that type, and releasing creates one
  selected node at exactly the ghost's position with no ghost left.
- **Editor_HostIntegration_EventsAndReadOnly_AreRaisedAndRespected**: double-click and the two kinds of context request
  raise their events; after switching to read-only a drag and the Delete key change nothing.
- **Editor_Keyboard_Shortcuts_RunCommandsUnlessInlineEditorIsOpen**: Ctrl+A, Ctrl+D, Delete, Ctrl+Z and Ctrl+Y change the
  node count as expected; F2 opens the rename box, which then owns the Delete key.

- **Editor_ToolTips_HoverNodePortAndWire_ShowDefaultOrHostContent**: hovering a node, a port and a wire sets the host-
  changed node text, the port description and the `source → target` text; a drag clears the tooltip.
- **Editor_ElementNavigation_BringIntoViewAndFitElements_ShowElementsAcrossPages**: a node on another page and a node
  hidden in a collapsed group are brought into view (switching pages, showing the group), and fitting two far-apart
  elements zooms out to show both.
- **Editor_DynamicPorts_PropertyChange_SceneHitTestAndConnectFollow**: after a property change the scene lays out the
  new ports, a drag connects to the last new input and hit testing finds it.
