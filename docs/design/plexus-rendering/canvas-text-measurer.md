## CanvasTextMeasurer Unit Design

![PlexusRendering Structure](PlexusRenderingView.svg)

Scope: Text measurer backed by CanvasNet fonts, with a bundled fallback font.

### Purpose

`CanvasTextMeasurer` implements the core text-measurement interface with the same CanvasNet fonts that `ScenePainter`
paints with, so the layout the core scene builder computes (node sizes, wrapped lines, label positions) matches the
rendered pixels. The internal `FontProvider` resolves theme font families to loaded fonts for both the measurer and the
painter.

### Data Model

`CanvasTextMeasurer` is stateless. `FontProvider` holds one process-wide thread-safe cache of loaded fonts keyed by
upper-cased family name and weight (regular or bold). Invariants: a family name always resolves to a font (never
null), and the same key always resolves to the same font instance. The line height is the font's ascent plus descent
at the requested size, capped at 1.25 times the size.

### Key Methods

- **`Measure(text, style)`** — returns the width and height of one line of text in diagram units.
  - Preconditions: `text` and `style` are not null; `text` may be an empty string.
  - Algorithm: returns an empty size when the style's font size is zero, negative, NaN or infinite (the font engine
    rejects those); otherwise resolves the font through `FontProvider`, measures the text with CanvasNet at the style's
    size, and returns the measured width with the line height described above.
  - Post-conditions: empty text (a string with no characters, as opposed to null) has zero width and the line height of
    the style; every text of one style has the same line height; the result depends only on the arguments and the
    fonts available on the machine.
- **`FontProvider.Get(family, bold)`** — returns the font for a family and weight, loading it once. It prefers an
  installed system font matching the family and weight; when none matches, or the matching file cannot be parsed
  (invalid data, I/O failure or access denied), it falls back to the bundled Liberation Sans of the requested weight.

### Error Handling

- Null text or style throws `ArgumentNullException`; an empty string is valid and measures zero width.
- Degenerate font sizes (zero, negative, NaN, positive or negative infinity) return an empty size instead of failing.
- Failures to load a system font are handled locally by falling back to the bundled font, because a missing or
  unreadable font must never stop rendering.

### Dependencies

- Core `ITextMeasurer`, `TextStyle` and `Size`.
- CanvasNet (OTS) font catalog, font loading and text measurement; see _CanvasNet_ in the OTS design documents.

### Callers

`PlexusRenderer` passes a measurer to the core scene builder; `ScenePainter` calls `FontProvider` to paint text. Host
applications may also use the public measurer to lay out their own text consistently with rendered images.
