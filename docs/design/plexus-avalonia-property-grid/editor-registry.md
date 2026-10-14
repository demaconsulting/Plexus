## EditorRegistry Unit Design

![PlexusAvaloniaPropertyGrid Structure](PlexusAvaloniaPropertyGridView.svg)

Scope: Names custom editors by annotation and maps editor keys and property types to the editors of the user interface
layer.

### Purpose

The WinForms property grid lets an author attach a `UITypeEditor` to a property. That mechanism cannot be reused: it is
built on Windows Forms services and GDI drawing. `PropertyEditorAttribute` replaces it with an opaque editor key (or an
editor type), and `PropertyEditorRegistry<TEditor>` lets the UI layer and hosts register the Avalonia editors that
answer to those keys and to property types. The registry stores the editors without knowing what they are, so it stays
UI-free.

### Data Model

- **PropertyEditorAttribute**: an editor key and optionally the editor type (the key is then the type's full name).
  It applies to properties and to types (every property of that type).
- **PropertyEditorRegistry**: a case-insensitive dictionary from key to editor, a dictionary from type to editor and a
  fallback editor. Both dictionaries are concurrent.

### Key Methods

- **`Register(key, editor)` / `Register(type, editor)` / `Unregister`** — add, replace or remove a registration.
- **`Resolve(key)`** — the editor of a key, or null.
- **`Resolve(type)`** — the exact registration; else a base type from the nearest to `object` (so `Enum` serves all
  enumerations); else an implemented interface; else the generic type definition of the type, a base or an interface (so
  `List<>` serves all lists); else, for `Nullable<T>`, the same search for `T`.
- **`Resolve(item)`** — the item's editor key, then its property type, then the fallback.

### Error Handling

Invalid keys and null arguments raise `ArgumentException` or `ArgumentNullException`; an unknown key or type resolves
to null (or the fallback for an item).

### Dependencies

- The PropertyModel unit (`PropertyItem.EditorKey`, `PropertyType`).

### Callers

The PropertyGridControl and PropertyEditors units (the grid keeps a registry of editor factories) and host
applications that add editors.
