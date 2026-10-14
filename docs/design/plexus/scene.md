## Scene Subsystem Design

![Scene Structure](SceneView.svg)

### Overview

The Scene subsystem turns a page into a painter-independent picture and answers pointer questions about it. It is the
single source of geometry shared by every painter and by hit testing, so that what is drawn is what is hit. It contains
five units:

- **DiagramTheme**: the immutable record of metrics, colors and fonts that the model, routers and painters share.
- **NodeLayout**: text-independent node geometry (`NodeGeometry`), per-node layouts (`NodeLayout`, `PortLayout`,
  `LayoutText`) and annotation text wrapping (`TextWrapper`).
- **TextMeasurement**: the `ITextMeasurer` contract and the font-free `ApproximateTextMeasurer`.
- **SceneBuilder**: the composition of primitives and hit targets from a page (`SceneBuilder`, the internal
  `SceneComposer`), the primitive types, wire paths, collapsed group layout and the node visual extension point.
- **DiagramScene**: the built scene with its hit testing, region queries, layout lookups and the uniform-grid spatial
  index.

Boundaries: the subsystem never paints, never reads fonts or UI frameworks, and never modifies the model. Painters live
in the PlexusRendering and PlexusAvalonia systems and consume `DiagramScene.Primitives`. The model uses
`NodeGeometry` (anchors, default sizes, routing obstacles) and `DiagramTheme` without referencing the scene builder.

### Interfaces

Exposed to hosts and painters:

- `SceneBuilder.Build(page, options)` returning an immutable `DiagramScene`; `SceneOptions` supplies an optional theme,
  text measurer, selection, hover element, grid switch and culling bounds.
- `DiagramScene`: `Primitives` (painting order), `Bounds`, `Query(region)`, `HitTest(point, tolerance)` returning a
  `HitResult` with a `HitKind`, and `TryGetNodeLayout`, `TryGetGroupLayout` and `TryGetWirePoints`.
- The closed primitive set `RectPrimitive`, `PathPrimitive`, `TextPrimitive`, `IconPrimitive` and `GridPrimitive` with
  `TextStyle` and `TextAlignment`.
- Extension points: `ITextMeasurer` and `INodeVisual` (with `NodeVisualContext` and the reusable
  `StandardNodeVisual`).
- `NodeGeometry` and `NodeLayout.Compute` for text-independent geometry.
- `NodeStyle` and `NodeStylers`: the per-node appearance (fill, border, title color, icon tint, glow, opacity) that a host's
  `Diagram.NodeStyler` returns, honored by `StandardNodeVisual` and readable by custom visuals from
  `NodeVisualContext.Style`; `IconPrimitive` carries the icon tint and opacity to the painters.

Consumed: the model types (`DiagramPage`, `Node`, `Wire`, `Annotation`, `Group`, `Port`, `NodeType`), the Routing subsystem
(`IWireRouter` through `Diagram.Router`, for transient display routes) and the Geometry types.

### Design

**Single geometry for drawing and hit testing.** The composer produces the primitive list and the list of hit targets
in one pass from the same computed rectangles. A scene is an immutable snapshot; hosts rebuild it when the model
changes. Hit testing therefore cannot disagree with painting, which removes a whole class of "clicked on the wrong
thing" defects and lets hosts build overlays from the published layouts instead of re-deriving geometry from theme
metrics.

**Composition order.** `SceneComposer` first finds the collapsed groups and the wires crossing their boundaries, then
adds, in this order: expanded group frames (outermost first, so nested frames stay visible), wires (so they run under
the nodes), and finally every node, annotation and collapsed group in the page's z-order, each followed by its
selection or hover outline. Finally the content bounds are computed and the background grid, which is decoration, is
inserted at the front. Painters draw in list order, so the order alone expresses layering. Two additions: after the
wires, the junction dots of branching nets (above wires, below nodes); after every element, the handles of selected
wires (above everything).

**Hit target order and priority.** Hit targets are recorded in the same order. Within a node the order is body, name,
ports, so ports win; nodes and annotations come after wires, so they win over wires, which win over group frames
because frames are added first. The handles and segments of a selected wire are the exception: they are appended
last, so a handle (bend, source end, target end) beats the wire's segments and the segments beat nodes. The query walks
the candidate targets backwards, so the last (topmost) matching target
wins. Targets are rectangles, polylines (wires) or border bands (expanded group frames, whose interior must stay
transparent to clicks so that members inside can be selected).

**Hit-test tolerance.** `HitTest(point, tolerance)` queries a square of side twice the tolerance around the point
against the spatial index. Rectangle targets must contain the point itself. A wire target is a hit when the distance from
the point to the nearest segment is at most the tolerance plus half the wire thickness; the nearest segment's index
is returned so editors know which segment was grabbed. A group border band is at least two units wide or the tolerance.
The default tolerance of three units makes one- and two-unit wires reachable without making them grab nearby nodes,
because nodes win by order.

**Spatial index.** Primitives and hit targets are indexed in a uniform-grid `SpatialIndex` keyed by cell
(128-unit cells). Items keep insertion order; a query visits the cells covering the region, de-duplicates through a set
and returns indexes in ascending order, which for a scene is draw order. Items that would span more than 64 cells (the
background grid, large frames) are kept in a short list that every query checks instead of being copied into hundreds
of buckets, and a region so large that it covers more cells than there are buckets is answered by scanning the buckets.
Cell coordinates are clamped so extreme coordinates cannot overflow. The index is chosen over a tree because a scene
is built once and queried many times, items are of similar size, and the uniform grid is simple, allocation-light and
preserves order for free.

**Text independence.** Port anchors depend only on theme metrics, the port's row and a deterministic label width
estimate (the approximate measurer widened by a 5% safety factor and rounded up), never on a painter's measurer. The
model can therefore place and route wires without a painter, and painters with exact measurers merely shift label
rectangles by a pixel or two. Only label, name and summary rectangles use the measurer supplied in the options.

**Groups.** Expanded groups draw a translucent frame, a title bar, a minus toggle and the title; their interior is
transparent to hits. Only the outermost collapsed group of a nest is drawn, as a summary box with a plus toggle; inner
groups and all members are hidden with it. A wire with exactly one end inside a collapsed group (or the ends in two
different collapsed groups) contributes one proxy port per distinct inner port to that group; the box grows taller when
there are more proxies than fit. Such wires are displayed with a transient route computed by the diagram's router
from the proxy anchors, and the stored route of the wire is untouched. Wires with both ends in the same collapsed group
are hidden. Each drawn group publishes a `GroupLayout` with the very rectangles used for drawing and hit testing.

**Wire drawing.** Corners of the polyline are replaced by cubic curves approximating circular arcs (the radius shrinks
at short segments), keeping the result a plain list of path commands that every painter understands. The last corner
leaves a straight run of the arrowhead length and the arrowhead shrinks on a very short last segment, so it always sits
on a straight piece of wire. Where wires of one net (same source port) branch apart a filled junction dot is drawn in
the wire color and the branching corner is drawn square (a crisp "T"), while ordinary corners stay rounded; where a
horizontal wire crosses a wire of a different net it draws a small semicircular hop (`WireCrossingJumps`). The stroked
line is
cut back inside the arrowhead so its flat end does not poke past the tip.

**Culling.** With visible bounds, elements whose drawn extent does not intersect them are not built; the grid then covers
exactly the visible area. Without bounds the grid covers the content plus a margin.

**Extension.** `ITextMeasurer` lets painters supply exact metrics. `INodeVisual` lets a node type replace or decorate
the standard look; visuals only add primitives (attributed to the node), so a custom visual works in every painter.
Ports, anchors and hit areas always come from the node layout, so a custom visual cannot detach wires.

**Theme.** `DiagramTheme` is a record with init-only properties split over two partial files (geometry metrics and
appearance). Customizing is a `with` expression; the shared classic instance is never modified. The model reads the
metrics, so a custom theme changes layout, routing obstacles and painting consistently.

**Performance.** Building touches each element once, wire display routes are computed only for collapsed-group wires,
and hit tests and queries visit only the cells near the point. Speed is assessed by profiling, not by timed tests; the
thousand-node tests check only that building succeeds and every hit is correct.
