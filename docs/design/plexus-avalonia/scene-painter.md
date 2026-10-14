## ScenePainter Unit Design (PlexusAvalonia)

![PlexusAvalonia Structure](PlexusAvaloniaView.svg)

Scope: Paints scene primitives onto an Avalonia drawing context.

### Purpose

`ScenePainter` paints the painter-agnostic primitives of a core scene onto an Avalonia drawing context. The scene has
already decided what to draw and where, so the painter only maps each primitive kind onto the matching Avalonia
drawing call and caches the Avalonia objects that repeated painting would otherwise allocate. The unit also contains
`PlexusAvaloniaExtensions`, the public extension methods that convert Plexus points, sizes, rectangles and colors to
Avalonia values and back.

### Data Model

The painter is an internal class, used on the UI thread only. One instance is owned by each editor and also lent to the
gestures for their feedback. It holds caches only:

| Cache          | Key                 | Value                                    | Lifetime                      |
|----------------|---------------------|------------------------------------------|-------------------------------|
| Brushes        | color               | immutable solid brush                    | life of the painter           |
| Pens           | color and thickness | immutable pen, flat caps and round joins | life of the painter           |
| Geometries     | path primitive      | stream geometry                          | life of the (immutable) path  |
| Formatted text | text and style      | formatted text                           | emptied at 4096 entries       |

Pens use flat caps to match the headless painter's butt caps: dashed wires arrive as separate figures cut by the
scene, and round caps would lengthen every dash by one wire thickness.

Icons are not cached here but by `PlexusIcons`, shared with the hosts. The context passed to the painter already
carries the view transform, so every coordinate handled here is in diagram units. A grid is thinned so that dots stay
at least seven device-independent pixels apart on screen.

### Key Methods

- **`Paint(context, scene, visible, zoom)`** — iterates the scene's primitives in drawing order and paints each one
  whose bounds intersect the visible area; others cost nothing. Dispatches on the primitive kind: grid, rectangle,
  path, text and icon.
- **`GetBrush(color)`** and **`GetPen(color, thickness)`** — return the shared cached brush or pen.
- **Grid** — draws dots on multiples of the spacing inside the intersection of the grid area and the visible area;
  while the spacing times the zoom is below the minimum on-screen spacing the spacing is doubled; the dot size never
  falls below one device pixel (`1 / zoom`).
- **Rectangle** — fills and strokes with the optional fill, stroke and corner radius.
- **Path** — converts the move, line, cubic curve and close commands to a stream geometry (figures are begun and ended
  explicitly, fills close their figures) and draws it.
- **Text** — one line, formatted through the shared text formatting helper, positioned in its rectangle according to
  the style's alignment and centered vertically; empty text is skipped.
- **Icon** — obtains the image from `PlexusIcons`; when there is none (broken icon) or its size is not positive,
  nothing is drawn; otherwise the image is scaled to fit its rectangle keeping its aspect ratio and centered. A tinted
  icon is drawn as a silhouette: a rectangle of the tint color (alpha scaled by the opacity) is drawn inside an
  opacity mask whose brush is a `DrawingBrush` of the icon image, so the icon's alpha is the coverage, exactly as in
  the PNG painter; the mask brush is cached per icon image. An icon without a tint and with an opacity below 1 is
  drawn inside a uniform opacity mask, because the SVG image is a custom drawing operation that ignores the context's
  `PushOpacity`.
- **Conversions** — `ToAvalonia` for points, rectangles and colors and `ToPlexus` for points and rectangles keep every
  component.

Post-conditions: only the drawing context passed in is affected; no primitive outside the visible area is drawn.

### Error Handling

Failures are not caught here. Unknown primitive kinds are ignored (the set of kinds is owned by the core library). A
malformed icon is already reported as missing by `PlexusIcons`, so the painter just skips it. Argument checks are made
by the editor, which is the only caller that passes arguments from outside.

### Dependencies

- Core scene primitives and the diagram theme types; Plexus geometry types.
- **DiagramClipboard** — the text formatting helper.
- **PlexusIcons** — icon images.
- **Avalonia** (OTS) — drawing context, brushes, pens, geometry and formatted text; see _Avalonia_ in the OTS design
  documents.

### Callers

`DiagramEditor` (paints the scene each frame, and uses the conversions) and the gestures (use the painter's brush and
pen caches and the conversions while drawing their feedback).
