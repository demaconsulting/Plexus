## PropertyEditors Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: The editors of the value column of a property grid row, the editor context that connects them to the edit
session, and the editor registry of the grid.

### Purpose

The value column of a row is its editor. An editor is a small Avalonia control created by an editor factory
(`Func<PropertyEditorContext, Control>`); the editors that ship with the grid cover the common property types, and a
host registers its own for annotated properties or for types. Editors are always visible and styled flat, so the grid
reads like a table, and every write goes through the session so that staged mode, validation, error display and the
host's undo scope apply uniformly.

### Data Model

- **PropertyEditorContext**: what an editor sees: the grid, the item, the session, `IsReadOnly`, the current `Value`
  and `Text` (pending values included), the rejected `PendingText`, and the `Error`. It exposes `SetValue`, `SetText`,
  `RejectText` and `Cancel`, and the events `Refreshed`, `CommitRequested` and `CancelRequested` that the grid raises.
- **Editor registry**: a `PropertyEditorRegistry<Func<PropertyEditorContext, Control>>`. The shared default registry
  holds the built-in editors; `CreateEditorRegistry` creates a fresh copy to customize one grid.
- **Built-in editors** (`BuiltInEditors`): text box (masked for password text), read-only text, check box (three-state for
  nullable booleans), drop-down over the standard values, editable drop-down (an auto-complete box), multi-select
  drop-down for `[Flags]` enumerations, color (swatch and hex text for Avalonia and System.Drawing colors, `ColorSupport`)
  and the collection summary with its "…" button. Registered by type: `bool`, `Enum`, both color types; the fallback
  chooses between drop-down, text box and read-only text from the item's converter.

### Key Methods

- **Text editors** — commit when Enter is pressed (through `CommitRequested`) or the focus leaves; Escape (through
  `CancelRequested`) discards the pending edit and error and shows the value again. A rejected text is remembered in
  the context and shown again, with the error border, until it is fixed or cancelled. While the editor has the focus a
  refresh does not overwrite what the user is typing.
- **`SetText`** — clears the pending text, writes through the session, remembers the text when the write fails and
  raises `Refreshed` so that the editor shows the final state.
- **Choice editors** — build their list from `PropertyItem.GetStandardValues` (recomputed on refresh because converters
  may depend on the instance) and write the chosen value; flags editors combine the bits of the checked members.
- **Color editor** — parses `#RGB`, `#RRGGBB`, `#AARRGGBB` and color names, writes a value of the property's color type
  (empty text clears an optional color) and reports anything else with `RejectText`.
- **Custom editors** — a host registers a factory under a key (matched against `PropertyEditorAttribute`) or a type;
  the factory reads `Value`, writes through the context and re-reads on `Refreshed`.

### Error Handling

Rejected input is never thrown: the session records the error and the grid shows it inline. Editors tolerate values
they cannot interpret (a non-color in a color editor shows empty text and no swatch).

### Dependencies

- The EditSession unit (`PropertyEditSession`), the PropertyModel unit (`PropertyItem`, standard values, text
  conversion), the EditorRegistry unit (the registry and the editor annotation) and the PropertyGridControl unit (which
  creates the context and the editor of each row).
- Avalonia (OTS): the controls the editors use.

### Callers

The PropertyGridControl unit and host applications that register custom editors.
