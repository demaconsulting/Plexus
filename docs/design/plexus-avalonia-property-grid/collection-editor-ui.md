## CollectionEditorUi Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The collection editor view and dialog: a list of members with Add, Remove and Move buttons and a nested property
grid for the selected member.

### Purpose

Collection properties are edited as a whole, as in the WinForms collection editor. `CollectionEditorView` is the
embeddable editor; `CollectionEditorDialog` hosts it in a window with OK and Cancel. The editor arranges a working list
of its own and hands the result to the owning session in one step, so Cancel leaves the collection untouched and OK is
one write (live) or one pending edit (staged).

### Data Model

- **Working list**: a copy of the collection's elements (the pending working copy of the session when one exists) and a
  snapshot of the starting list to tell whether anything changed. A flag records edits made through a `Value` row,
  because a boxed structure is edited in place.
- **Member list**: a list box of the members shown as index and text. The text is the formatter's result, else
  `ToString()`, else the default property value, the `DisplayName` annotation of the type or the type name; `null` is
  `(null)` and an empty string `(empty)`.
- **Member grid**: a nested `PropertyGrid` (live, alphabetical, no search). A member that is an object with properties
  (a reference type that is not a string, not a collection and has no text converter) is shown through the grid's
  object path; any other member is shown as a single `Value` row over a custom item that reads and writes the working list,
  which lets structures expand and nested collections open their own editor.
- **Capabilities**: `CanResize` and `CanReorder` come from the same rules that apply the collection, so the buttons enable
  accordingly.

### Key Methods

- **`Add`** — creates an element through the session's element factory or the default constructor and inserts it after
  the selection; failure (no default constructor, read-only collection) is returned and shown below the editor.
- **`Remove`, `Move`** — change the working list and keep the selection on the moved or neighboring member.
- **`Apply`** — does nothing when nothing changed; otherwise begins a collection edit on the session and replaces its
  working copy with the list (`CollectionEditor.SetItems`).
- **Dialog `Accept`** — applies and closes with true; when the session rejects the list the dialog stays open and shows
  the reason. Cancel closes with false without applying.
- **Recursion** — the member grid is an ordinary grid, so a collection property of a member opens another dialog over
  the member grid's session.

### Error Handling

Failures are returned as `PropertyResult` and shown in the view; an item that is not a collection raises
`ArgumentException` and null arguments `ArgumentNullException`. Changes to the properties of member objects are made on
the objects themselves (as in the WinForms editor) and are not undone by Cancel; only the list is.

### Dependencies

- The EditSession unit (`PropertyEditSession`, `CollectionEditor.SetItems`, element creation), the PropertyModel unit,
  the PropertySources unit (`CustomPropertySource` for value members) and the PropertyGridControl unit (the nested grid).
- Avalonia (OTS): list box, buttons, window.

### Callers

The PropertyGridControl unit (the collection button) and host applications that embed the view or open the dialog.
