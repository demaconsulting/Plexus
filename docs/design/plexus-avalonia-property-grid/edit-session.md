## EditSession Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: Applies edits to the edited object immediately or staged until commit, with per-property errors, a commit scope
hook and collection editing through a working copy.

### Purpose

`PropertyEditSession` is what a grid calls when the user changes a value. In live mode each edit is written at once; in
staged mode edits are buffered by property path and written together on `Commit` (or dropped by `Cancel`), which is what
an OK / Cancel dialog needs. A host-supplied commit scope wraps the writes, typically in an undo transaction.
`CollectionEditor` edits a collection property through a working copy.

### Data Model

- **Pending edits**: an ordered list of `PendingEdit` (item, kind `Value` / `Reset` / `Collection`, value or collection
  editor, last error), at most one per item path. The order is the order of first edit.
- **Errors**: a dictionary from item path to the latest error message.
- **Mode, source and commit scope**: fixed at construction. `CollectionItemFactory` is settable.
- **CollectionEditor**: the collection item, the element type, a working copy (a shallow copy: element instances are
  shared with the target), the capabilities `CanResize` / `CanReorder` computed from the target and the property, and an
  active flag that ends when the session commits or cancels.
- **CommitResult**: applied count, failed edits and a scope error.

### Key Methods

- **`SetValue` / `SetText`** — check read-only state and validity (`Validate`; text is parsed with the given culture).
  Live: write through the commit scope. Staged: record, replace or remove the pending edit (an edit back to the current
  value removes it; re-editing keeps the position). Failures are recorded as the item's error and returned.
- **`Reset`** — requires `CanReset`; live resets at once, staged records a `Reset` edit.
- **`GetValue` / `GetText` / `IsPending` / `GetError` / `GetErrors` / `GetPendingEdits`** — read the session state; the
  pending value is shown instead of the target's value. A pending collection edit shows the working copy.
- **`FindItem(path)`** — resolves a path through the source's items, children and collection elements: first the
  top-level item whose `Path` is the longest prefix of the path that ends at a segment boundary (a root path may contain
  dots, as `Property.Gain` of the node source does), then `.Child` and `[index]` segments.
- **Commit scope** — the writes of a commit (staged) or of one edit (live) run inside the commit scope of the source when
  the source implements `IPropertyCommitScope` (outermost), and then inside the host's commit scope. A
  `PropertyCommitRefusedException` thrown by a scope names the refused property: the session records the message as that
  property's error, commits nothing (the commit is atomic: no edit is applied and, for the Plexus sources, no undo
  entry is
  recorded), lists the refused property in `CommitResult.Failed` and reports the reason in `CommitResult.Error`. The
  session does not rely on the scope for that: before each write it snapshots what the item holds (a copy of the
  elements for a collection edit) and, when the scope refuses or fails after the writes ran, writes the snapshots back,
  newest first, skipping every item that already holds its snapshot (a scope that rolled back by itself needs no
  restore, and no spurious write or undo entry results). Every attempted write is remembered, also one whose setter or
  applier changed the target and then threw, so a refusal puts that partial write back too. A value that cannot be put
  back is named in `CommitResult.Error`. When the scope does not refuse, a failed edit is not restored: it stays pending
  with its error and the other edits are still applied.
- **`Commit`** — snapshots the pending edits and applies them in order inside the commit scope. Failed edits stay pending
  with their error; the others are removed and their collection editors end. If the scope throws or does not run the
  action, nothing counts as committed and the scope error is reported; `Failed` then lists the edits that failed by
  themselves and the edit the refusal named (a transient edit of its item when that row was not edited). Commit with
  nothing pending does not call the scope.
- **Error messages** — `PropertyErrors.Message` unwraps reflection exceptions and removes the framework's
  " (Parameter 'name')" suffix of an `ArgumentException`, so reasons are clean text.
- **`Cancel` / `Discard`** — drop all, or one item's, pending edits and errors; collection editors end.
- **`BeginCollectionEdit`** — creates a `CollectionEditor` (or returns the active one of a pending collection edit).
  Editor mutations validate the element type and the index, then update the working copy and call back into the session:
  staged mode records or removes one `Collection` edit; live mode applies it through the commit scope and re-reads the
  working copy from the target.
- **`CollectionEditor.SetItems`** — replaces the whole working copy in one mutation (one live write, one staged pending
  edit); a different element count needs a resizable collection, and every element must be an instance of the element type.
  A dialog arranges a list of its own and applies it with this call.
- **`ReportError`** — records an error for an item without changing its value, for an editor that rejects its own input;
  it lasts until the item is edited successfully, discarded or cancelled.
- **Collection application** — applies a working copy to the target: an array of the same length (and a writable list of
  the same length when it cannot be resized) is updated in place; a resizable `IList` or `ICollection<T>` is cleared and
  refilled in place, so a get-only property keeps its instance; otherwise a new array or list is built and assigned
  through the setter; read-only collections and fixed-size arrays behind read-only properties are rejected.
- **Element creation** — the session's factory, else the default constructor (empty string, default value
  types, null for nullable types).

### Error Handling

User-code failures (setters, converters, factory, commit scope, collection operations) become failed results,
`PendingEdit.Error`, `CommitResult` and per-path errors; nothing is thrown at the caller for them. Null arguments raise
`ArgumentNullException`, and `BeginCollectionEdit` raises `ArgumentException` for an item that is not a collection.

### Dependencies

- The PropertyModel unit (items, validation, text conversion) and the PropertySources unit (`IPropertySource`).

### Callers

The PropertyGridControl, PropertyEditors and CollectionEditorUi units and host applications.
