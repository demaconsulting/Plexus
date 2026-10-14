## NodePropertiesDialog Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The `NodePropertiesDialog` helper: the ready-made modal properties dialog of a Plexus node.

### Purpose

A host that shows a `DiagramEditor` wants one line to edit a node on double-click:
`editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);`. The dialog is a
`PropertyGridDialog` (a staged grid with OK and Cancel) over the node's `NodePropertySource`, so OK applies the rename,
the changed properties and the edited host data as one undo entry and Cancel changes nothing.

### Data Model

- No state of its own: a static class that creates a `PropertyGridDialog` titled "<name> - Properties" (480 by 640
  device-independent pixels) for the node.

### Key Methods

- **`ShowAsync(owner, node)`** — shows the dialog modally; the task completes with true after a successful OK and false
  after Cancel.
- **`Create(node)`** — the window without showing it. **`GetTitle(node)`** — the title.

### Error Handling

A null owner or node raises `ArgumentNullException`; a node that is not attached to a diagram raises `ArgumentException`.
An invalid or vetoed name keeps the dialog open with the reason (see the PropertyGridDialog unit).

### Dependencies

The PropertyGridDialog unit and the PlexusNodeSource unit; Avalonia (OTS) windows.

### Callers

Host applications that edit nodes.
