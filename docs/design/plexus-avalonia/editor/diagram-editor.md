### DiagramEditor Unit Design

![Editor Structure](EditorView.svg)

Scope: The Avalonia editor control: view state, selection, keyboard, rename, annotation editing, history commands,
host events and automation peer.

#### Purpose

`DiagramEditor` is the Avalonia control that shows and edits one page of a Plexus diagram. It is the single place where
Avalonia input becomes model edits and where model changes become a repaint. It is implemented as one `sealed partial`
class spread over files by concern (view and painting, input, keyboard, selection, rename, annotation editing,
commands, drag-and-drop), together with its event argument classes, the XML namespace declaration of the assembly and
its automation peer.

#### Data Model

| Member                          | Type                      | Meaning and invariants                              |
|---------------------------------|---------------------------|-----------------------------------------------------|
| `Page`                          | `DiagramPage?`            | Page shown; null shows an empty canvas              |
| `DiagramTheme`                  | `DiagramTheme?`           | Explicit theme; null uses the diagram's theme       |
| `Zoom`                          | `double`                  | Clamped to the zoom range; non-finite becomes 1     |
| `Offset`                        | `Point`                   | Diagram coordinate at the control's top-left corner |
| `ShowGrid`, `SnapToGrid`        | `bool`                    | Default true                                        |
| `IsReadOnly`                    | `bool`                    | Default false                                       |
| `ShowToolTips`                  | `bool`                    | Default true; false hides and never sets a tooltip  |
| `Clipboard`                     | `IDiagramClipboard?`      | Host clipboard; null uses the window's clipboard    |
| `Selection`                     | Read-only observable list | Distinct live elements of the shown page            |
| Scene, stale flag, culling area | Private                   | Cached scene and the area it was built for          |
| Hover element                   | Private                   | Element drawn highlighted                           |
| Tooltip target, suppressed      | Private                   | Element and port the tooltip describes; pressed one |
| Pending view request            | Private `Func<bool>?`     | Latest fit or bring-into-view waiting for layout    |
| Current gesture                 | Private                   | At most one gesture at a time                       |
| Inline editors                  | Private                   | At most one of rename or annotation edit open       |

View mapping: a diagram point `d` is at screen point `(d - Offset) x Zoom`. The selection never contains an element
that is not on the shown page and is not recorded in history. Zoom is limited to 0.1 to 8. Pointer-over state and the
last pointer position are tracked to place pasted content. An explicit `DiagramTheme` changes the look only: port
positions, triangles, port hit areas and wire ends always follow the diagram's own theme (the core's node layout uses
it), so wires stay attached to their ports.

#### Key Methods

- **View**: `ScreenToDiagram`, `DiagramToScreen`, `ZoomAt(screen, zoom)` (zoom keeping the point under the cursor
  fixed), `ZoomToFit(margin)` (margin finite and not negative, else `ArgumentOutOfRangeException` before the request
  is stored; no effect on an empty page; before the control has a size or a page the request is
  remembered, the latest one wins, and it is applied once when a size and a page exist, from the bounds-changed
  and page-changed reactions), `GetElementScreenBounds`,
  `GetPortScreenPosition`, `HitTest`. These have no preconditions beyond non-null arguments. The pending request is a
  function (`_pendingView`) that returns true once it was applied or became obsolete; `ApplyPendingView` runs it from
  the bounds-changed and page-changed reactions and right after a request, and forgets it only when it is still the
  pending one, so the latest request of any kind wins. Both fits share `FitBox` (zoom never above 1, then `CenterOn`).
- **Navigation** (`DiagramEditor.Navigation.cs`): `BringIntoView(element)` accepts an element on an attached page of the
  editor's diagram (of any diagram when no page is shown), else `ArgumentException` (null: `ArgumentNullException`)
  before anything changes, stores the pending request, shows the element's page with
  `SetCurrentValue` (keeping a binding; the page change clears the selection and cancels gestures) and applies the
  request: when the editor has a size it centers the element's target box at the current zoom and zooms out only when
  the box does not fit inside the control minus a 30-unit margin on each side (never in, never below `MinZoom`); a
  request whose element has left the page is dropped. `ZoomToFit(elements, margin)` first validates the sequence
  (`ArgumentNullException`) and the margin (finite and not negative, else `ArgumentOutOfRangeException`), then copies the
  sequence (the live selection is cleared by a page switch), does nothing for an empty sequence (a pending request stays
  pending), validates every element, shows the first element's page and fits the union of the
  target boxes of the elements still on that page (elements of other pages are ignored) with the margin per side, zoom
  at most 1 and then clamped to the zoom limits. The target box of an element is its node layout bounds (name,
  summary and port labels included) or its bounds; an element hidden in collapsed groups is represented by the
  outermost collapsed group, and a wire with a hidden end by the target boxes of both end nodes.
- **Tooltips** (`DiagramEditor.ToolTips.cs`): `GetToolTipContent(element, port)` builds the default text (a port's
  label, the `PortType.DisplayName` of its type key from the diagram's registry (the key when unregistered) and its
  description on a new line; a node's `NodeType.GetToolTip`, where null or empty means no tooltip; a wire's
  `source → target`; null for annotations and groups), raises `ToolTipRequested` with it and returns
  the handlers' content (a handler may supply content for annotations and groups), or null when canceled, null or empty;
  it ignores `ShowToolTips` and lets exceptions of the tooltip function and of handlers propagate.
  `UpdateToolTip(hit)`, called at the end of the hover
  update, computes the target (element and port under the pointer) when tooltips may show (`ShowToolTips`, no gesture,
  no inline edit, no Space pan, no pending context request, target not suppressed); when the target changed it requests
  the content once and sets `ToolTip.Tip` on the editor, which lets Avalonia's tooltip service open the tooltip after its
  show delay at the pointer, or close it when the content is null; when a tooltip was already open it is reopened so it
  follows the pointer. A press records the hit target as suppressed (no tooltip for it until the pointer moves to
  something else) and clears the tooltip; starting a gesture, pointer exit, Space pan, page change, detaching, opening an
  inline editor and turning `ShowToolTips` off clear it too, as does a model change that removed the target element or
  port.
- **Scene**: the scene is built lazily when painting or hit testing needs it and is rebuilt only when stale or when the
  visible area leaves the area it was built for (visible area inflated by half its larger side). Changes of the model,
  selection, hover, theme and grid mark it stale; bursts cost one rebuild.
- **Rendering**: `Render` fills the background, then paints the scene with the view transform applied, then lets the
  running gesture draw its feedback.
- **Input**: pointer press, move, release, capture loss, exit and wheel handlers implement the routing described in
  the Editor subsystem design. The wheel zooms around the cursor with Ctrl (or Cmd) and otherwise scrolls (Shift swaps
  the axis). Double clicks are recognized by the click count of the press: on nodes, on node names and annotations, and
  on a wire segment, where a double-click adds a jog to the wire (see `Wire.AddJog`) and selects the wire. A left press
  on a bend handle starts a `WireBendGesture`, on an end handle a `WireEndGesture`, and on a wire segment (the first
  and last stub included) a `WireSegmentGesture`; the handles exist only on a selected wire, and the scene's hit
  priority makes them win over segments and nodes. While not dragging, the pointer shows a four-way move cursor over a
  bend handle, a cross over an end handle and a north-south or west-east resize cursor over a draggable wire segment
  (none in a read-only editor).
- **Selection**: `Select`, `ClearSelection`, `SelectAll` (visible elements only; wires whose nodes are both hidden are
  skipped). The selection-changed event is raised once per real change. Elements that stop being on the shown page
  (deleted, removed by undo, or moved to another page with `Diagram.MoveToPage`, which removes them from the page)
  are pruned on the model's element-removed notification.
- **Page following**: while the control is in the visual tree it listens to the page collection of the shown page's
  diagram (subscribed together with the diagram's change event, so the listener follows `Page` and attachment). When a
  removal of the shown page is reported, the control switches to the page now at the removed page's index, clamped to
  the last page, or to no page when none is left. It sets `Page` as a current value, which keeps a binding in place
  and writes the new page back through a two-way binding; the usual page-change reaction then clears the selection and
  cancels gestures. Only removals of the shown page act: additions, reordering, the removal of other pages and the
  return of a page through undo or redo leave `Page` alone, so the control never jumps back unexpectedly.
- **Keyboard**: Space (pan mode), Escape (cancel gesture), Delete, Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z, Ctrl+A, Ctrl+D,
  Ctrl+C, Ctrl+X, Ctrl+V and F2. Ctrl means Ctrl or Cmd. Edit shortcuts do nothing while read-only or while a gesture
  runs; all shortcuts are ignored while an inline editor is open.
- **History**: `Undo()` and `Redo()` return whether they acted. They do nothing (return false) when the editor is
  read-only or shows no page; when a gesture or inline edit is in flight they cancel it only and return true;
  otherwise they delegate to the core history. They throw `InvalidOperationException` when the host has a history
  transaction open (propagated from the core).
- **Delete**: `Delete(elements)` ignores elements not on the shown page, removes collapsed groups with their members
  and everything else individually, in one transaction; `DeleteSelection()` deletes the selection and clears it.
- **Duplicate**: `DuplicateSelection()` copies the selection offset by one grid cell and selects the copies.
- **Clipboard**: `CopyAsync`, `CutAsync`, `PasteAsync()` and `PasteAsync(at)`. Paste reads the clipboard, checks that the
  shown page did not change meanwhile, lets the core import the text (one undo step), selects the result or raises
  `PasteFailed`; without an explicit position the content goes to the snapped pointer position when the pointer is
  over the control.
- **Rename**: `BeginRename(node)`, `CommitRename()`, `CancelRename()`; the box holds the name selected, Enter commits
  through the core rename pipeline, a rejected name keeps the box open, shows the reason in a tooltip and in
  `RenameError`, and typing clears the rejection. Losing focus commits an acceptable name and abandons an unacceptable
  one.
- **Annotation edit**: `BeginEditAnnotation(annotation)`; a multi-line box; Ctrl+Enter or losing focus commits, Escape
  cancels; line breaks are normalized so an unedited text records nothing. `CommitEdit()` and `CancelEdit()` serve both
  kinds of inline editor.
- **Drag-and-drop**: `CreateNodeTypeDataTransfer`, `StartNodeTypeDragAsync` and `EnableNodeTypeDrag` (static) and the
  drop target handlers: a drop with a known node type key adds a node centered at the drop point and snapped, selects
  it and focuses the control; drags over the control report copy only when a drop would create a node. While such a
  drag is over the control a translucent ghost of the node (a `NodePrototype`, the node as the palette draws it, painted
  at 55 percent opacity by the shared painter) is shown at the position `DropPosition` computes; the drop uses the same
  computation, so the node lands exactly where the ghost was. The ghost is removed when the drag leaves, is dropped,
  stops being acceptable or the control leaves the visual tree.
- **Add at center**: `AddNodeAtCenter(type)` (in `DiagramEditor.AddNode.cs`) places the diagram's own type of that key
  with `DropPosition` at the center of the control (so the node is centered there and snapped), moves it diagonally by
  one grid cell while another node has exactly that position, adds it as one history entry, selects it and returns it;
  it returns null for a read-only editor, no page or an unknown key. `NodePalette` activation and a host's "insert
  node" menu entries use it.
- **Events**: `NodeDoubleClicked`, `DiagramContextRequested` (raised on release of the right button), `PasteFailed`,
  `ClipboardFailed`, `SelectionChanged`, `ToolTipRequested` (with `DiagramToolTipRequestedEventArgs`: `Element`,
  `Port`, settable `Content` pre-filled with the default, `Cancel`).

Post-conditions common to all editing methods: the change is one history entry, selection and view state are not
recorded, and a read-only editor is never changed.

Selection and view commands (`DiagramEditor.SelectionCommands.cs`): `GroupSelection(title)` creates a group from the
selected elements other than wires, selects it and returns it, or returns null; `UngroupSelection()` and
`ToggleCollapseSelection()` run inside `History.Transact`, the latter treating a selected member as its parent group,
and return how many groups changed; `MoveSelectionToPage(page)` copies the selection, calls `Diagram.MoveToPage` and
clears the selection, and refuses (false) the shown page, a removed page and a page of another diagram. `ViewCenter`
converts the middle of the control to diagram coordinates; `ZoomIn` and `ZoomOut` call `ZoomAt` at that middle with the
zoom multiplied or divided by `ZoomStep` (clamped by `Zoom`); `SnapPoint` applies the existing `Snap` rule (so it
honors `SnapToGrid`); `AddAnnotationAtCenter(text)` centers a 150 by 60 annotation on the view center, snapped,
selects it and returns it. All of them do nothing when the editor is read-only or shows no page.

#### Error Handling

- Null arguments throw `ArgumentNullException`. `BringIntoView` and `ZoomToFit(elements)` throw `ArgumentException`
  for an element that is not on an attached page, an element of another diagram than the shown page (or the first
  element), and a null entry; `ZoomToFit(margin)` and `ZoomToFit(elements, margin)` throw `ArgumentOutOfRangeException`
  for a negative, NaN or
  infinite margin. All of them check before anything changes, so page, zoom, offset and selection stay as they were.
- An exception thrown by a node type's tooltip function or by a `ToolTipRequested` handler propagates unchanged to the
  caller of `GetToolTipContent`, or out of the hover handling; the editor never swallows it.
- Ports that a port provider adds or removes need no handling: the editor rebuilds its scene on the model's `Ports`
  notification, so drawing, hit testing and the connection preview always use the node's current ports, and a tooltip
  for a removed port is cleared.
- Invalid user actions are not errors: a rejected rename stays open with the reason; a rejected connection simply does
  not happen; a drop of an unknown key, onto a read-only editor or with no page, is ignored.
- Clipboard content is untrusted. A failed paste returns the core's failure result, raises `PasteFailed` and changes
  nothing. Platform clipboard exceptions (invalid operation, external, input/output, not supported) of a shortcut-started
  command are caught and raised through `ClipboardFailed`; the public asynchronous methods let them reach the caller.
- Losing pointer capture, a page change, leaving the visual tree, becoming read-only and removal of an element under
  edit all cancel the affected gesture or inline editor instead of failing.
- Everything else, including a history transaction opened by the host, is propagated from the core.

#### Dependencies

- **Core model** — page and diagram API, history and transactions, scene builder and hit testing, clipboard
  serialization, rename pipeline, theme.
- **Gestures** — one gesture instance at a time, created by the control.
- **DiagramClipboard** — `IDiagramClipboard`, the system bridge and the text formatting helper.
- **NodeTypeDragSource** — created by `EnableNodeTypeDrag`.
- **NodePalette** (the `NodePrototype` and `NodePrototypeCache` helpers) — the drop ghost draws the same prototype the
  palette shows.
- **ScenePainter**, **AvaloniaTextMeasurer** — painting and layout; **PlexusIcons** indirectly through the painter.
- **Avalonia** (OTS) — control, input, drawing, text, drag-and-drop, clipboard, automation; see _Avalonia_ in the OTS
  design documents.

#### Callers

Host applications (through XAML or code), the Gestures (which call the control's internal selection, hit-testing,
coordinate and snapping helpers), and the NodeTypeDragSource (which calls the static drag start).
