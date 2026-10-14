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

- `DiagramEditor`: properties for the page, theme, zoom, offset, grid, snapping, read-only mode and clipboard;
  commands for selection, delete, duplicate, undo, redo, copy, cut, paste, rename, annotation edit, zoom and hit
  testing; the events `SelectionChanged`, `NodeDoubleClicked`, `DiagramContextRequested`, `PasteFailed` and
  `ClipboardFailed`; the drag helpers `CreateNodeTypeDataTransfer`, `StartNodeTypeDragAsync` and `EnableNodeTypeDrag`.
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
