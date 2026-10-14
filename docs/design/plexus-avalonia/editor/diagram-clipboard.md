### DiagramClipboard Unit Design

![Editor Structure](EditorView.svg)

Scope: Operating-system clipboard bridge and clipboard text formats.

#### Purpose

The DiagramClipboard unit is the editor's access to clipboard services and to text formatting. It provides the
clipboard abstraction `IDiagramClipboard` (so hosts and tests can substitute their own clipboard), its
operating-system implementation `SystemDiagramClipboard`, and the internal `TextFormats` helper that creates the
formatted text used both for painting and for measuring.

#### Data Model

| Type                     | Members and invariants                                               |
|--------------------------|----------------------------------------------------------------------|
| `IDiagramClipboard`      | `SetAsync(json)` and `GetAsync()`; carries text only (Plexus JSON)   |
| `SystemDiagramClipboard` | The Avalonia clipboard it wraps and the Plexus application format    |
| `TextFormats`            | A cache of typefaces by font family and weight; UI thread only       |

The Plexus application format is named by the format identifier of the core clipboard serialization.

#### Key Methods

- **`SystemDiagramClipboard.SetAsync(json)`** — creates one clipboard item holding the JSON both in the Plexus
  application format and as plain text, and sets it as the clipboard content. One item keeps the two representations
  together as one entry.
- **`SystemDiagramClipboard.GetAsync()`** — returns the Plexus-format value when present, otherwise the plain text, and
  null when the clipboard holds neither.
- **`TextFormats.Create(text, style)`** — returns formatted text in the invariant culture, left to right, with the
  style's family, weight, size and color; empty text is formatted as a single space so that a line height can still be
  measured. Typefaces are cached per family and weight.

#### Error Handling

The unit does not catch anything: exceptions of the platform clipboard propagate to the caller. The editor decides
what to do: it lets them reach the caller of the public asynchronous commands and reports them through its
`ClipboardFailed` event for shortcut-started commands. Content validation is the job of the core clipboard
serialization, not of this unit.

#### Dependencies

- **Core model** — the clipboard format identifier, and the text style and color types.
- **Avalonia** (OTS) — the clipboard, data transfer, data format, and text formatting and typeface types; see _Avalonia_
  in the OTS design documents.

#### Callers

`DiagramEditor` (clipboard commands), `ScenePainter` and `AvaloniaTextMeasurer` (text formatting) and the gestures
(text for connection preview reasons).
