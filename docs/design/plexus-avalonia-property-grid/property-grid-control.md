## PropertyGridControl Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The `PropertyGrid` Avalonia control: the two-column layout, categories, sorting, search, expansion, selection,
description pane and keyboard handling over a property source and an edit session.

### Purpose

`PropertyGrid` is the window a user looks at: it turns any `IPropertySource` (an object through
`PropertySourceFactory`, or a custom source) into rows and lets the user edit them through a `PropertyEditSession`. It
is generic; it knows no Plexus types. The look and the behavior follow the WinForms property grid so that users
migrating from it feel at home.

### Data Model

- **Inputs** (styled properties): `SelectedObject`, `Source` (setting one clears the other), `EditMode` (live by
  default), `SortMode`, `ShowToolbar`, `ShowSearch`, `ShowDescription`, `Filter`, `NameColumnWidth`,
  `DescriptionHeight`; plain properties `CommitScope`, `CollectionItemFactory`, `CollectionItemFormatter` and
  `EditorRegistry`.
- **Outputs**: `Session` (the current `PropertyEditSession`, replaced when the content or the edit mode changes),
  `SelectedItem`, and the `CollectionEditorRequested` event.
- **State kept by the grid**: the collapsed categories (case-insensitive set, kept across content changes), the expanded
  property paths (cleared when the content changes), the selected row key (`c:Category` or `p:Path`) and the row views
  by key.
- **Row model**: `RowSpec` records (key, category or item, depth, expandable, expanded) computed from the source on every
  refresh, and the internal `CategoryRowView` and `PropertyRowView` controls that render them. A property row is a name
  cell (indent, expander, label), a one pixel column line and a value cell that hosts the editor and carries the error
  border and tooltip.
- **Chrome**: a toolbar (two toggle buttons with vector icons and a search box), the scrolled rows with a column splitter
  thumb over them, and a description pane under a row splitter. Brushes come from the Fluent theme resources through
  `GridTheme` (bound, so they follow theme variant changes) with neutral fallbacks.

### Key Methods

- **Content change** — `OnContentChanged` keeps `SelectedObject` and `Source` exclusive, then `AttachSource` creates the
  source (disposing sources it created), starts a new session whose commit scope and element factory call the grid's
  current properties, and rebuilds the rows. A source passed through `Source` is never disposed by the grid. While the
  grid is out of the visual tree it disposes a source it created (so the source stops listening to the object) and creates
  it again, keeping the expanded rows and the selection, when the grid is attached again.
- **`RefreshRows`** — builds the row specs (`PropertyOrdering.Group`, the filter, the category and nested expansion
  state), reuses the row view of every row whose item is unchanged (so editors keep their text and focus), replaces the
  panel children only when the sequence changed, then updates selection, the description pane and the empty message. It
  runs on every session or source change; no change needs a full rebuild.
- **Filtering** — a row matches when its display name, name or category contains the filter; a nested property matches
  when it or a nested property below it does (to a fixed depth, so self-referencing graphs terminate), and matching
  parents are shown expanded.
- **Selection** — `SelectKey` highlights the name cell, updates `SelectedItem` and the description pane and optionally
  focuses the grid. Editors that get the focus select their row.
- **Keyboard** — a tunnel handler distinguishes the grid itself having the focus (the name side: arrows, Home, End,
  page keys, Left and Right, Enter, F2, Escape, Tab) from an editor having it (Enter commits, Escape cancels, Tab moves
  to the next row's name, Shift+Tab to the name of the same row) and ignores keys from the toolbar and the search box.
- **`CreateEditor`** — chooses the editor of a row: the registry entry of its editor key, else the collection editor
  for collections, else read-only text for read-only rows and for expandable values without a text form, else the
  registry entry for the property type, else the registry fallback. The editors are described in the PropertyEditors unit.
- **`EditCollectionAsync`** — raises `CollectionEditorRequested`, then opens a `CollectionEditorDialog` over the grid's
  session, modal to the window that hosts the grid.

### Error Handling

Writes go through the session, so user-code failures become errors that the row shows (red border and tooltip). The grid
itself raises `ArgumentNullException` for null arguments and `InvalidOperationException` when a collection editor is
requested without a source. Rows that cannot read their value show empty text.

### Dependencies

- The PropertySources unit (`IPropertySource`, `PropertySourceFactory`, `PropertyOrdering`), the PropertyModel unit
  (`PropertyItem`), the EditSession unit (`PropertyEditSession`), the EditorRegistry unit (the registry), the
  PropertyEditors unit (editors and the editor context) and the CollectionEditorUi unit (the dialog).
- Avalonia (OTS): controls, input, headless-testable rendering.

### Callers

Host applications and the PropertyGridDialog and CollectionEditorUi units, which embed a grid.
