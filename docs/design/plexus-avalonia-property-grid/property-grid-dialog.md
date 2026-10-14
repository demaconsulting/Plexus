## PropertyGridDialog Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The `PropertyGridDialog` helper: a modal dialog that edits an object in a staged grid with OK and Cancel.

### Purpose

Most hosts want one call that shows the properties of an object and returns whether the user accepted them.
`PropertyGridDialog.ShowAsync(owner, target, title, commitScope)` does that: the object is untouched until OK, OK
commits the edits (inside the host's commit scope, so the dialog is one undo step) and Cancel discards them.

### Data Model

- A window holding a `PropertyGrid` (`EditMode` staged, `CommitScope` from the call, `SelectedObject` the target), a
  status line and the OK and Cancel buttons (OK is the default button and Cancel the cancel button).
- The title: the given title, else the `DisplayName` annotation of the object's type, else its type name, followed by
  " - Properties".

### Key Methods

- **`ShowAsync`** — creates the dialog and shows it modally; the task completes with true after a successful OK and false
  after Cancel.
- **`Accept` (OK)** — refuses while the session has errors: it selects the first row with an error and shows the
  reason in the status line. Otherwise it commits; when the commit reports a failure the dialog stays open with the
  reason and the failed edits stay pending with their errors. On success it closes with true.
- **`GetDefaultTitle`** — the default title.

### Error Handling

Invalid input and failed commits are shown, never thrown; a null owner or target raises `ArgumentNullException`.

### Dependencies

The PropertyGridControl unit (the grid) and the EditSession unit (commit and errors); Avalonia (OTS) windows and buttons.

### Callers

Host applications.
