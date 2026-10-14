## AvaloniaTextMeasurer Unit Design

![PlexusAvalonia Structure](PlexusAvaloniaView.svg)

Scope: Text measurer backed by Avalonia text APIs.

### Purpose

`AvaloniaTextMeasurer` implements the core text-measurement interface with Avalonia text formatting. The core scene
builder asks it for the size of every label while it lays out nodes, so measuring with the same formatting that the
painter uses guarantees that boxes fit the glyphs that are drawn.

### Data Model

The measurer holds a cache from text and style (with the color normalized away, because color does not change size) to
the measured size, bounded to 4096 entries; when the bound is reached the whole cache is emptied rather than evicting
individual entries, because the working set normally refills almost immediately. It is a public sealed class and, like
the control that owns it, used on the UI thread only.

### Key Methods

- **`Measure(text, style)`** — returns the size of the text drawn in the style. Preconditions: neither argument is null.
  The text is formatted through the shared text formatting helper; the width is the width including trailing
  whitespace (so that text ending in a space is measured honestly), the height is the line height. Empty text has zero
  width but a line height (Avalonia formats the empty string as a single space, whose width is not wanted).
  Postcondition: the result is cached by text and style without the color.

### Error Handling

A null text or a null style throws `ArgumentNullException`. Formatting failures are not expected and are not handled
locally.

### Dependencies

- **DiagramClipboard** — the shared text formatting helper.
- Core text style, size and measurement interface types.
- **Avalonia** (OTS) — formatted text; see _Avalonia_ in the OTS design documents.

### Callers

`DiagramEditor` passes it to the core scene builder and uses it to compute the inline editor placement and the content
bounds for zoom to fit; hosts may use it for their own layout.
