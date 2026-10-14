### NodeTypeDragSource Unit Design

![Editor Structure](EditorView.svg)

Scope: Helper that lets hosts start drag-and-drop of node types onto the editor.

#### Purpose

`NodeTypeDragSource` turns any Avalonia control (typically an entry of a host's node palette) into a drag source for a
node type: pressing the left button on the control and dragging a few pixels starts a drag whose payload is the node
type key, which the editor accepts as a drop. It is created through `DiagramEditor.EnableNodeTypeDrag`.

#### Data Model

| Field            | Meaning                                                                          |
|------------------|----------------------------------------------------------------------------------|
| source           | The control being observed                                                       |
| type key         | The node type key carried by the drag                                            |
| pending press    | The left-button press waiting to become a drag, or null                          |
| press position   | Where the pending press happened, relative to the control                        |
| drag threshold   | 4 device-independent pixels in either direction                                  |

The helper listens to press, move and release on the control with a handler that also receives events other handlers
already marked as handled, because list items and buttons mark their presses as handled.

#### Key Methods

- **Constructor** — subscribes to the three pointer events of the control.
- **Press handler** — remembers the press when the left button is down; other buttons are ignored.
- **Move handler** — does nothing without a pending press; clears it when the left button is no longer down; when the
  pointer has moved at least the threshold in either direction it clears the pending press and starts the drag by
  calling the editor's static drag start with the pending press and the type key. A press therefore starts at most one
  drag.
- **Release handler** — clears the pending press.
- **`Dispose`** — unsubscribes from the control and clears the pending press.

#### Error Handling

Starting a drag can fail on platforms without drag support (invalid operation, not supported, or an external
exception). Those failures are deliberately swallowed: the drag simply does not happen, and the host's alternatives
(for example double-clicking a palette entry) remain. Other exceptions propagate.

#### Dependencies

- **DiagramEditor** — the static drag start and the drag payload format.
- **Avalonia** (OTS) — pointer events, event routing and drag-and-drop.

#### Callers

`DiagramEditor.EnableNodeTypeDrag`, which returns the helper to the host as a disposable handle.
