### Gestures Unit Design

![Editor Structure](EditorView.svg)

Scope: Pointer gesture state machines: connect, move, resize, pan, rubber-band and wire segment, bend and end drag.

#### Purpose

The Gestures unit holds one small state machine for each pointer interaction of the editor. A gesture starts with the
press that the editor routed to it, receives the pointer movement, and ends with a release (`Complete`) or an
abandonment (`Cancel`). All gestures are internal classes of the `Interaction` folder deriving from `EditorGesture`;
the editor runs at most one at a time.

#### Data Model

`EditorGesture` (abstract base) holds the editor and the control-relative position of the press, and defines:

| Member                   | Meaning                                                               |
|--------------------------|-----------------------------------------------------------------------|
| `Cursor`                 | The cursor to show while the gesture runs; null for the default arrow |
| `Move(screen, mods)`     | Handle pointer movement                                               |
| `Complete(screen, mods)` | Handle the release that finishes the gesture, committing its edits    |
| `Cancel()`               | Abandon the gesture, undoing what it did                              |
| `Draw(...)`              | Draw transient feedback in diagram coordinates                        |
| drag threshold           | 3 device-independent pixels before a press becomes a drag             |

Concrete gestures and their state:

- `MoveGesture`: the pressed element, whether it was selected, the press position in diagram coordinates, the
  element's corner at the start, the selection before the press, the open transaction (null until the drag starts),
  the moving elements and the offset applied so far.
- `ConnectGesture`: the start port, the port under the pointer, its connection verdict (also exposed internally as
  `Verdict`, denied when no port is under the pointer) and the pointer position.
- `ResizeGesture`: the annotation, its bounds at the start and the open transaction.
- `WireReshapeGesture` (abstract, derives from `EditorGesture`): the wire, the route and both anchors at the start, the
  press position in diagram coordinates, the stub length from the theme and the open transaction (null until the drag
  starts). Derived classes only turn the pointer into a new route.
- `WireSegmentGesture` (a `WireReshapeGesture`): the index of the segment within the full polyline, the orientation and
  the start coordinate.
- `WireBendGesture` (a `WireReshapeGesture`): the bend index within the route and the bend position at the start.
- `WireEndGesture`: the wire, whether the source or the target end is dragged, the port under the pointer (other than
  the one the end is on), its reconnection verdict, the pointer position and whether the drag threshold was passed.
- `PanGesture`: the view offset at the start.
- `RubberBandGesture`: the selection to keep (additive mode), the press position and the current band.

Invariants: a transaction is open only between the first move past the threshold and the release or cancellation, and a
gesture never leaves one open after `Complete` or `Cancel`.

#### Key Methods

- **`MoveGesture.Move`** — nothing happens until the pointer has traveled past the threshold. Then it opens the
  "Move" transaction (or "Duplicate" when Ctrl or Cmd is held: the movable selection is duplicated in place and the
  copies become the selection). On every move it computes where the pressed element's own top-left corner wants to be,
  snaps that corner to the grid, and applies only the change since the previous move through the core move operation
  (which re-routes wires and carries group members). `Complete` commits the transaction; for a press that never became
  a drag it finishes the deferred selection change (narrow to the pressed element, or remove it when Shift or Ctrl was
  held). `Cancel` rolls the transaction back, which also removes duplicates, and restores the previous selection. For an
  expanded group the snapped corner is that of its first node or annotation rather than the frame.
- **`ConnectGesture.Move`** — hit tests the pointer, picks a port other than the start port, and asks the core whether
  the two ports may be connected; the preview turns green or red and shows the reason beside the pointer. `Complete`
  re-evaluates at the release position and, if allowed, connects (the core orders the ports output to input) and
  selects the new wire. A race with a rule change makes the core throw `InvalidOperationException`, which is swallowed
  because the drop simply does nothing. `Cancel` forgets the candidate.
- **`ResizeGesture.Move`** — past the threshold opens the "Resize annotation" transaction; the bottom-right corner
  follows the pointer snapped to the grid with a minimum size of 30 x 20 units; the top-left corner is fixed.
  `Complete` commits and `Cancel` rolls back.
- **`WireSegmentGesture.TryCreate`** — returns null unless the wire is shown with its stored route and the segment
  exists and is exactly horizontal or exactly vertical; the port stubs are included. `Reshape` snaps the segment's
  coordinate to the grid and asks `RouteEditor.MoveSegment` for the route (a stub gets a jog beside its port).
- **`WireReshapeGesture.Move`** — nothing happens until the pointer has traveled past the threshold. Then it opens a
  transaction ("Move wire segment" or "Move wire bend") and on every move recomputes the route from the route at the
  start of the drag and stores it with `Wire.SetRoute` (which also merges bends that became collinear). A pointer
  position without a valid route (the editor returns null) leaves the last good shape. `Complete` commits and `Cancel`
  rolls back.
- **`WireBendGesture.TryCreate`** — returns null unless the wire is shown with its stored route and the bend exists.
  `Reshape` snaps the dragged bend position (start position plus pointer travel) to the grid on both axes and asks
  `RouteEditor.MoveBend`; the cursor is a four-way move cursor.
- **`WireEndGesture.Move`** — past the threshold hit tests the pointer, picks a port other than the one the end is on
  and asks `DiagramPage.CanReconnect` for the verdict; nothing in the model changes meanwhile. `Draw` paints a dashed
  elbow from the end that stays put to the pointer or the candidate's anchor, green when allowed and red when rejected
  with the reason beside the pointer, and rings the candidate. `Complete` re-evaluates at the release position and, if
  a drag happened and the move is allowed, calls `Wire.Reconnect` (one undo entry); a rejected port, a port of the wrong
  direction or empty canvas does nothing, and an `InvalidOperationException` from a race with a rule change is
  swallowed. `Cancel` forgets the candidate. The cursor is a cross.
- **`PanGesture.Move`** — sets the editor's offset to the start offset minus the pointer travel divided by the zoom.
- **`RubberBandGesture.Move`** — past the threshold computes the band from the press to the pointer and rebuilds the
  selection from scratch: the kept selection plus visible nodes and annotations the band touches, collapsed groups the
  band touches and expanded groups the band fully contains. Wires are never banded. `Draw` paints a translucent
  rectangle with a hairline outline.

Preconditions: the editor shows an attached page (gestures return silently when it does not). Post-conditions: after
`Complete` or `Cancel` no transaction of the gesture remains open.

#### Error Handling

Gestures detect nothing exceptional themselves. A missing live page ends a move silently; the only exception handled is
`InvalidOperationException` from the core when a connection is rejected at connect time. Anything else thrown by the
core propagates to the editor's input handler.

#### Dependencies

- **DiagramEditor** — hit testing, coordinate conversion, snapping, selection changes, the live page and repainting.
- **Core model** — move, duplicate, connect and connection check, route changes, and history transactions.
- **ScenePainter** and **TextFormats** — for drawing the connection preview and the rubber band.
- **Avalonia** (OTS) — cursors, key modifiers and drawing primitives.

#### Callers

`DiagramEditor` creates a gesture on a press, forwards moves, and completes or cancels it.
