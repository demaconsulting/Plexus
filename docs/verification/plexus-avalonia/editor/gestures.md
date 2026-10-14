### Gestures Unit Verification Design

Scope: Pointer gesture state machines: connect, move, resize, pan, rubber-band and wire segment, bend and end drag.

#### Verification Approach

Each gesture is created directly (the classes are internal and visible to the test project) and driven the way the
editor drives it: `Move` with a sequence of positions, then `Complete` or `Cancel`. The editor, the core model and
Avalonia are real: a gesture's whole purpose is to edit the model through the editor's helpers, and there is no useful
way to stub that. A test host (a headless window with an editor and a small diagram) supplies the editor and the model,
but no pointer input is simulated, so a failure points at the gesture rather than at input routing, which is verified by
the DiagramEditor and Editor tests. The tests are in `GesturesTests.cs`.

#### Test Environment

The standard test runner plus the Avalonia headless platform started by the shared fixtures. The view is at zoom 1 and
no offset, so screen and diagram coordinates coincide, and the default theme's 15 unit grid is used for snapping.

#### Acceptance Criteria

A gesture test passes when the model, selection and history after `Move`, `Complete` and `Cancel` are exactly as
specified: positions and bounds snapped or restored, one undo entry per committed drag, none per cancelled one, and
no transaction left open.

#### Test Scenarios

- **MoveGesture_Move_BelowThreshold_EditsNothing**: a one-pixel wobble changes nothing and requests no cursor
  (boundary below the threshold).
- **MoveGesture_Complete_AfterDrag_SnapsAndRecordsOneUndoEntry**: a two-step drag lands the corner on the grid, the
  cursor is the move cursor, and one history entry is recorded.
- **MoveGesture_Move_ControlHeld_DuplicatesAndMovesCopy**: with Ctrl the original stays, the selected copy moves.
- **MoveGesture_Cancel_AfterDrag_RestoresPositionSelectionAndHistory**: cancelling a Ctrl-drag removes the copy,
  restores the selection and leaves no entry.
- **MoveGesture_Complete_ClickOnSelectedNode_FinishesDeferredSelectionChange**: a click on one of two selected nodes
  narrows the selection; with Shift it removes the node instead.
- **ConnectGesture_Complete_OverCompatiblePort_ConnectsAndSelectsWire**: moving and releasing over a compatible port
  connects output to input and selects the wire; the cursor is a cross.
- **ConnectGesture_Complete_OverRejectedPort_ConnectsNothing**: with the same-type rule a mismatching port is rejected
  and no entry is recorded.
- **ConnectGesture_SecondWireIntoInput_RejectedUnlessInputAllowsFanIn**: hovering an occupied plain input yields a denied
  verdict with the "does not accept more than one" reason and a release connects nothing (no history entry), while an
  occupied input that allows fan-in yields an allowed verdict and takes the wire. The pointer-level drag is covered by
  `DiagramEditor_PortDrag_SecondWireIntoOccupiedInput_IsRejected`.
- **ConnectGesture_Complete_OverOwnPortOrCanvas_ConnectsNothing**: releasing on the start port or on the canvas
  connects nothing.
- **ConnectGesture_Cancel_AfterHoveringTarget_ConnectsNothing**: a cancelled gesture forgets its candidate.
- **ResizeGesture_Complete_AfterDrag_ResizesWithOneUndoEntry**: the corner snaps to the grid, the top-left corner stays,
  one entry, resize cursor.
- **ResizeGesture_Move_PointerPastTopLeft_ClampsToMinimumSize**: the box never becomes smaller than the minimum (boundary
  value).
- **ResizeGesture_Cancel_AfterDrag_RestoresBoundsAndHistory**: cancel restores the bounds.
- **WireSegmentGesture_TryCreate_PortStubOrOutOfRangeSegment_StubsDraggableOutOfRangeNull**: the first and last stubs
  are draggable (they get a jog), a missing segment is not.
- **WireSegmentGesture_Complete_AfterDrag_MovesSegmentWithOneUndoEntry**: an interior segment moves with both ends, the
  ports stay, one entry, and the cursor matches the orientation.
- **WireSegmentGesture_Cancel_AfterDrag_RestoresRoute**: cancel restores the route.
- **WireGestures_Cursor_BendMoveAndEndCross** (`DiagramEditorWireEditingTests.cs`): a bend gesture exists for a real
  bend only and shows the move cursor; the end gesture shows a cross. The bend, end and jog behavior is verified through
  real pointer input in the same file (see the DiagramEditor verification design).
- **PanGesture_Move_ZoomedView_KeepsPressedDiagramPointUnderPointer**: at zoom 2 with an offset, the pressed diagram
  point stays under the pointer and no history is recorded.
- **RubberBandGesture_Move_Band_SelectsTouchedNodesLive**: the wobble selects nothing, growing selects touched nodes,
  shrinking deselects, and completing keeps the selection.
- **RubberBandGesture_Move_Additive_KeepsPreviousSelection**: the earlier selection stays.
- **RubberBandGesture_Move_ExpandedGroup_SelectedOnlyWhenEnclosed**: a node in a group is selected when touched, the
  group only when fully enclosed.
- **RubberBandGesture_Cancel_AfterMove_RecordsNoHistory**: no history, even when cancelled.
