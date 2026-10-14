## ScenePainter Unit Design (PlexusRendering)

![PlexusRendering Structure](PlexusRenderingView.svg)

Scope: Paints scene primitives (rectangles, paths, text, icons, grid) onto a CanvasNet surface.

### Purpose

`ScenePainter` paints painter-agnostic scene primitives onto a CanvasNet pixel surface. It is the only place where
scene geometry becomes pixels in this system: it maps diagram coordinates to device coordinates and draws every
primitive kind the core library defines.

### Data Model

An instance is bound to one surface and holds immutable state set at construction:

| Field   | Type      | Meaning                                                     |
|---------|-----------|-------------------------------------------------------------|
| surface | `Surface` | The pixel surface being painted; must not be null           |
| canvas  | `Canvas`  | The CanvasNet drawing facade over the surface               |
| origin  | `Point`   | The diagram point that maps to the surface's top-left pixel |
| scale   | `double`  | Pixels per diagram unit; finite and greater than zero       |

The caller guarantees the scale. Device position = (diagram position - origin) x scale. The class is internal and an
instance is not thread-safe.

### Key Methods

- **`Paint(IEnumerable<ScenePrimitive>)`** — paints primitives in the given order (back to front).
- **`Paint(ScenePrimitive)`** — dispatches on the primitive kind:
  - Rectangle: converts to device space; when the outline has a whole-pixel stroke width of at least one pixel, the
    edges are snapped (an odd width centers on a pixel middle, an even width on a pixel boundary) so one-pixel
    borders stay crisp. Zero or negative sizes are skipped. Fills and strokes a rectangle or, for a corner radius,
    a rounded rectangle path; strokes use mitered joins.
  - Path: skips empty command lists; fills treat every figure as closed; strokes use round joins and butt caps, are
    never thinner than a quarter pixel, and leave figures open. A drawing command without a preceding move starts a
    figure implicitly.
  - Text: skips empty text and non-positive or non-finite sizes; anchors the line at the left edge, center or right
    edge of its rectangle according to the style; centers the line box (ascent plus descent) vertically in the
    rectangle and rounds the baseline to a whole pixel; uses the same `FontProvider` lookup as `CanvasTextMeasurer`.
  - Icon: converts the rectangle to a whole-pixel position and size, skips boxes smaller than one pixel or larger
    than the maximum surface dimension, obtains the rasterized icon from `IconCache`, skips it when the cache reports
    it as missing, and composites it row by row with straight-alpha "over" blending, clipped to the surface. With a
    tint, each pixel the icon paints is replaced by the tint's color with coverage `icon alpha * tint alpha` (a
    silhouette, so multi-colored artwork becomes one flat color); the primitive's opacity is the extra coverage factor
    of the blend. The cached rasterization is shared and never modified, so tinting costs one pass over the pixels.
  - Grid: draws dots directly into the pixels on multiples of the spacing from the diagram origin, restricted to the
    grid area and the surface, each dot at least one pixel and anti-aliased by area coverage.
- **`ToPixel(Color)`** — converts a core color to a CanvasNet pixel.

Post-conditions: only pixels of the surface are modified; primitives extending beyond the surface are clipped.

### Error Handling

- A null surface, primitive or primitive sequence throws `ArgumentNullException`.
- A primitive of a kind the painter does not know throws `NotSupportedException`. The primitive kinds are a closed set
  owned by the core library (its base class cannot be derived outside it), so this branch guards against the core
  gaining a kind without this painter learning it; it cannot be reached from a test.
- An icon that cannot be rasterized is skipped (the cache reports it as null); no other failure is handled locally.

### Dependencies

- Core scene primitives, `Point`, `Rect`, `Color` and text style types.
- `IconCache` and `FontProvider` (part of the CanvasTextMeasurer unit) of this system.
- CanvasNet (OTS) canvas, path, text-rendering and surface types; see _CanvasNet_ in the OTS design documents.

### Callers

`PlexusRenderer` creates one painter per render. No other unit uses it.
