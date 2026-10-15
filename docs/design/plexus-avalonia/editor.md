## Editor Subsystem Design

![Editor Structure](EditorView.svg)

The Editor subsystem is the interactive part of PlexusAvalonia: the `DiagramEditor` control and the helpers that
carry its pointer gestures, its clipboard access and drag-and-drop of node types.

Scope: Interactive editor control: view, input gestures, clipboard bridge and drag-and-drop of node types.

### Overview

**Responsibility**: turn user input on a shown page into undoable edits of the core model, keep what is drawn and what
is hit tested consistent with the model, and tell the host what happened.

**Contained units**:

- **DiagramEditor** — the control: view state, selection, scene cache, input routing, inline editors, commands and
  events.
- **Gestures** — the state machines of the pointer interactions.
- **DiagramClipboard** — the clipboard abstraction, the operating-system bridge and the text formatting helper.
- **NodeTypeDragSource** — a helper that turns a palette control into a drag source for node types.
- **NodePalette** — the palette control that lists node types drawn as they look on the canvas, and the prototype
  helpers it shares with the editor's drop ghost.

**Boundaries**: the subsystem edits only through the core model API and owns no model state. Painting is delegated to
`ScenePainter`, text measurement to `AvaloniaTextMeasurer` and icon conversion to `PlexusIcons`, which are units of
the PlexusAvalonia system outside this subsystem.

### Interfaces

**Provided** (to the host application):

- `DiagramEditor`: properties for the page, theme, zoom, offset, grid, snapping, read-only mode, clipboard and
  `ShowToolTips`; commands for selection, delete, duplicate, undo, redo, copy, cut, paste, rename, annotation edit, zoom
  and hit testing; the events `SelectionChanged`, `NodeDoubleClicked`, `DiagramContextRequested`, `ToolTipRequested`
  (with `DiagramToolTipRequestedEventArgs`), `PasteFailed` and `ClipboardFailed`; the drag helpers
  `CreateNodeTypeDataTransfer`, `StartNodeTypeDragAsync` and `EnableNodeTypeDrag`.
- Tooltips and navigation of `DiagramEditor`, with their constraints (all on the UI thread):
  - `ShowToolTips` (default true): when false, hovering sets no tooltip and raises no `ToolTipRequested`, and a showing
    tooltip is hidden.
  - `ToolTipRequested`: raised once per element or port the pointer moves onto, with the default `Content`; a handler
    replaces it or sets `Cancel`; canceled, null and empty content show nothing.
  - `GetToolTipContent(element, port)`: the content the editor would show, usable for any element (also on another
    page); it raises `ToolTipRequested` and ignores `ShowToolTips`; `ArgumentNullException` for a null element.
  - `BringIntoView(element)`: any element on an attached page of the editor's diagram (of any diagram when no page is
    shown), otherwise `ArgumentException` (null: `ArgumentNullException`) with the view unchanged; switches the page
    (which clears the selection and cancels a gesture or inline edit), centers the element, zooms out only when it does
    not fit with 30 device-independent pixels on each side, never zooms in; waits for the first layout.
  - `ZoomToFit(elements, margin)`: margin in device-independent pixels per side (default 30), finite and not negative
    (`ArgumentOutOfRangeException`, checked first); `ArgumentNullException` for a null sequence, `ArgumentException`
    for a null entry, a detached element or an element of another diagram, in all cases with the view unchanged; zoom
    is the fitting one but at most 1, within the zoom limits; an empty set changes nothing; elements on other pages
    than the first element's are ignored; waits for the first layout, and the latest request wins.
- `NodePalette`: properties `Types`, `Editor`, `DiagramTheme`, `Zoom`, `Filter`, `ShowFilter`, `GroupByCategory`,
  `UncategorizedHeader` and `SelectedNodeType`, the methods `Refresh`, `Activate`, `ScrollIntoView`, `Select` and the
  static `GetAutomationId`, and the event `NodeTypeActivated`. `DiagramEditor` also provides `AddNodeAtCenter`.
- `IDiagramClipboard`: the clipboard abstraction a host may implement.

**Consumed**:

- The core model (`DiagramPage`, `Diagram`, the history and its transactions), scene builder and hit testing, connection
  rules, rename pipeline and clipboard serialization.
- Avalonia input, drawing, text, drag-and-drop and clipboard services.

### Design

The control receives every input event. It first lets an open inline editor keep events that originate inside it, then
asks the cached scene what is under the pointer and creates the matching gesture:

| Press on                       | Result                                                            |
|--------------------------------|-------------------------------------------------------------------|
| Port                           | connect gesture                                                   |
| Annotation resize handle       | resize gesture                                                    |
| Group collapse toggle          | immediate collapse or expand (one undo entry)                     |
| Wire bend handle (selected)    | wire bend gesture (corner drag)                                   |
| Wire end handle (selected)     | wire end gesture (reconnect to another port)                      |
| Wire segment                   | wire segment gesture (stubs included), otherwise only a selection |
| Any other element              | selection rules, then move gesture (duplicates with Ctrl held)    |
| Empty canvas                   | clear selection (unless additive), then rubber band gesture       |
| Middle button, or Space + left | pan gesture                                                       |
| Right button                   | select the element, then raise the context event on release       |
| Double-click on a node name    | inline rename                                                     |
| Double-click on a node body    | `NodeDoubleClicked` event                                         |
| Double-click on an annotation  | inline annotation edit (editable editors only)                    |
| Double-click on a wire segment | add a jog to the wire and select it (editable editors only)       |

While a gesture runs it owns the pointer: movement and release are forwarded to it, and it draws its own feedback
(rubber band, connection preview) after the scene. A gesture that edits the model does so inside one history
transaction, so release commits one undo entry and cancellation rolls the model back completely.

Cancelling is centralized in the control. Escape, Undo, Redo, a page change, losing pointer capture, leaving the visual
tree and switching to read-only mode all abandon the running gesture; Undo and Redo also abandon an open inline editor.
In those cases the request is consumed and the history is untouched, so a second request is needed to revert the
previous completed entry.

The inline editors (node name and annotation text) are real text boxes added as children of the control and arranged
over their element; they follow the element as the view changes and share the commit and cancel commands. Keyboard
shortcuts are ignored while an inline editor is open, because the text box owns the keyboard.

Clipboard commands go through an `IDiagramClipboard`, which is the host's if assigned and otherwise the
operating-system bridge of the control's window. Keyboard-started clipboard commands have no caller, so their failures
are reported through `ClipboardFailed`; failures of the public asynchronous methods reach the caller. Paste failures
that the core classifies (not Plexus data, unknown types, too large) are reported through `PasteFailed`.

Dropping the node type payload creates a node at the drop point, centered and snapped; `NodeTypeDragSource` is the
matching helper for the drag side. While a node type drag is over the editor it draws a translucent ghost of the node
(built by the same `NodePrototype` the palette uses) at the position the drop would use, so the palette item, the
ghost and the created node all come from one drawing pipeline and one position computation. `NodePalette` is the ready-made
drag side: it draws every node type with the editor's scene builder and painter and starts the node type drag from its
items.

Tooltips are part of hover tracking: while no gesture, inline edit or context request is in progress and `ShowToolTips`
is true, the element (and port) under the pointer gets a tooltip whose content the control builds and then offers to the
host through `ToolTipRequested`; the content is set on the control's own `ToolTip.Tip`, so Avalonia's tooltip service
shows it with the standard delay at the pointer. The default content is: for a port `"{Label} ({PortType.DisplayName,
else the type key})"` followed by the port description on a new line; for a node `NodeType.GetToolTip(node)`; for a
wire `"A.out → B.in"`; for an annotation or a group nothing, unless a handler supplies content. Canceled, null and empty
content show nothing. An exception of a node type's tooltip function propagates to the caller of `GetToolTipContent` or
the hover handling and is never swallowed. A press, a gesture, leaving the control and opening an inline editor clear
the tooltip; after a press it stays away for the pressed element or port until the pointer moves to something else.

Navigation requests (`BringIntoView`, `ZoomToFit` of a set of elements) are validated first (an element must be on an
attached page of the editor's diagram, any diagram when no page is shown; the margin must be finite and not negative),
so a refused request changes nothing; they then switch the shown page when needed and center or fit the elements; like
the page-wide `ZoomToFit` they are remembered until the control has a size, and the latest request of any kind wins
(an empty set is not a request). Elements hidden in a collapsed group are shown through the outermost collapsed group.

Ports computed per node by a port provider need nothing special: a recomputation is a model change (wire removals and
the node's `Ports` notification, all inside the undo entry of the change) that marks the scene stale and rebuilds it, so
the connection preview, hit testing and drawing always use the node's current ports. A port that was removed is never
hit again (until an undo restores it), a port that was added can be connected, and a tooltip shown for a removed port is
reset.
