### SceneBuilder Unit Design

![Scene Structure](SceneView.svg)

#### Purpose

Builds a `DiagramScene` from a page: the ordered, painter-independent primitives and the matching hit targets,
including highlights, culling, group boxes with proxy ports and custom node visuals.

#### Data Model

- `SceneBuilder` (static): the entry point, `Build(page, options)`.
- `SceneOptions`: init-only `Theme`, `TextMeasurer`, `Selection`, `Hover`, `ShowGrid` (default true) and `Bounds`
  (culling and grid area). Options are read once.
- `SceneComposer` (internal, partial over two files, single use): the page, theme, measurer, a set copy of the
  selection, the primitive list, the hit target list, and dictionaries of node layouts, wire polylines, collapsed group
  information and group layouts, plus lazily built routing obstacles.
- Primitives: `ScenePrimitive` base (bounds and owning element), `RectPrimitive` (fill, stroke, thickness, corner
  radius), `PathPrimitive` (`PathCommand` list with move, line, cubic and close commands), `TextPrimitive`,
  `IconPrimitive` and `GridPrimitive` (dots at multiples of the spacing in diagram coordinates).
- `WirePaths` (static): rounded polyline paths (with arrowhead clearance, end trim, S-curves for small jogs, square
  corners at junction points and crossing hops), arrowhead, last-segment length, end trimming and dashing.
  `WireJunctions` (internal static): junction detection for one net. `WireCrossings` (internal static) with `WireRun` and
  `WireHop`: crossings between wires of different nets. `CollapsedGroupInfo`: box, proxy ports and their edge and anchor
  points. `GroupLayout` and `GroupProxyPortLayout`: the published group geometry.
- `WireStyle` (public, immutable record) and `WireStylers` (public static helpers): the per-wire appearance a host's
  `Diagram.WireStyler` returns (color, thickness, dash pattern and arrowhead, each optional) and ready-made stylers (by
  source port type, by a source or target node property, by condition, `PerPage` and `Combine`).
- `NodeStyle` (public, immutable record) and `NodeStylers` (public static helpers): the per-node appearance a host's
  `Diagram.NodeStyler` returns (fill, border, border thickness, title color, icon tint, glow and opacity, each optional)
  and ready-made stylers (by a node property, by condition, `PerPage` and `Combine`); `NodeStyle.None` is the empty style.
- `INodeVisual`, `NodeVisualContext` and `StandardNodeVisual`: the node drawing extension point and the standard look.
  The context carries the resolved `Style` and the theme-fallback helpers `Fill`, `Border`, `BorderThickness`,
  `HeaderTextStyle` and `Faded(color)` that custom visuals use to honor it.

#### Key Methods

`Build(page, options)`: throws for a null page, then runs a new composer, whose `Compose` does the following in order:
find collapsed groups and their crossing wires and size their boxes once all proxies are known; add expanded group
frames outermost first; work out the displayed polyline and style of every visible wire (`PrepareWires`, page order, which
also computes the transient display routes) and, from all of them together, the junctions of every net and the crossings
between nets (`AnalyzeWires`); add all wires; add the junction dots; walk the page's elements in z-order adding nodes,
annotations and collapsed groups; add the handles and top-priority hit targets of the selected wires; compute the
content bounds as the union of the primitive bounds; insert the grid at the front when enabled and the grid size is
positive.

`AddNode`: computes the layout, skips the node when its bounds are outside the culling bounds, runs the node type's visual
(or the standard visual) with a context that collects primitives, attributes primitives without an owner to the node,
adds the selection or hover outline, and records hit targets for the body, then the name, then each port.

`AddWire`: takes the displayed polyline prepared by `PrepareWires` (the stored route for ordinary wires; a transient
route from the diagram's
router and the proxy anchors for wires crossing a collapsed group boundary, which also avoids the frame of every expanded
visible group that contains neither end node when `DiagramTheme.GroupsAreObstacles` is on, via the shared internal
`GroupObstacles`, and keeps clear of the other displayed wires: the request carries the net of the wire's source port,
merged with the nets of other wires that end at the same fan-in input (`WireTraffic.NetOf`, the inner port for a wire
leaving a collapsed group) and the segments of every wire drawn with its stored route plus the display routes computed
before it, built once per scene in page order so the result is deterministic; none for wires inside a collapsed group),
culls by the polyline's inflated box, resolves the wire's style (below), adds a wider underlay when selected or hovered,
the stroked rounded path and the arrowhead, and records a polyline hit target with the styled wire thickness. The
arrowhead must sit on a straight final segment: when the last segment is shorter than the arrowhead length the head
(length and width) shrinks to the segment; `WirePaths.Rounded` is called on the full polyline with the arrowhead
length as end clearance, which limits the radius of the last corner to the last segment length minus that clearance
(a sharp corner when nothing is left, the full radius for the usual stub), and with three quarters of the arrowhead
length as end trim, which stops the stroke inside the arrowhead by shortening the final line of the finished path
instead of the polyline before rounding, so corners keep the same radius everywhere. When
`DiagramTheme.WireSmoothSmallJogs` is on, `WirePaths.Rounded` replaces the two corners of a small jog (a step shorter
than twice the corner radius between two runs heading the same way, perpendicular to the step) by one cubic whose
control points are the two corners, which gives an S with tangents along the runs; it starts and ends half of
`JogSpanFactor` (4) times the step before and after the step, limited to the run before the step (the whole run at the
start of the wire, otherwise half of it), the run after it (half of it, or the run minus the arrowhead clearance at the
tip), and is not used when that span would be shorter than the step or when either corner of the jog is a junction
point (see below). Only the drawn path changes: the stored route, the
displayed polyline, the hit target, the junction dots and the handles keep the square corners. The path is split into
dashes when the style has a dash pattern (the S and the hops are flattened like a corner curve, so dashes follow them).
The wire's junction points (its net's) are passed to `WirePaths.Rounded` as square corners, and the crossings it hops
over as hops. A selected wire with a stored route (not a transient display route) is remembered for its handles.

`AnalyzeWires`: groups the prepared wires into nets (`FindNets`: all wires with the same source port, joined through
inputs whose definition allows fan-in, so wires from different outputs that end at the same such input form one net;
in page order) and, for every net of at least two wires, asks `WireJunctions.Find` for the points where the net's wires
leave in three or more directions; every wire of the net receives all junction points of its net, and the owner wire
(first naming the point) and a radius (the theme's `WireJunctionRadius` scaled by the heaviest wire thickness of the net
over `WireThickness`, never smaller, so the dot shows on thick wires) are kept for the dot. When `WireCrossingJumps` is
on and `WireJumpRadius` is positive it then asks `WireCrossings.Find` for the crossings of all wires. Both searches see
every displayed wire, including wires outside the culling bounds, so the drawing does not depend on the visible area.
Wires of
different nets are never junction candidates for each other: nets that cross or touch make no dot. Two nets feeding one
input are different nets unless the input allows fan-in; then they are one net and the point where their routes merge
(a "T" left of the input) gets a dot and square corners like a fan-out branch, and the wires never hop each other.
Junction corner rule: where a wire bends at a junction point of its net the corner has radius zero (a crisp "T") and
takes no part in small-jog smoothing; a neighboring corner of the same wire may then use the whole shared segment rather
than half of it, which keeps wires of one net that share a trunk drawn identically. Overlapping trunks of one net are
drawn as identical overlapping strokes, not once (a dashed net can show phase differences after a square corner).

Crossing jumps (`WireCrossings.Find`, `WirePaths.Rounded`): a crossing exists where a horizontal segment of one wire and
a vertical segment of a wire of a different net meet at a point strictly inside both segments, at least the corner radius
from the ends of the vertical segment (so not on a corner curve), with no junction of the vertical wire's net exactly there
and no junction of the horizontal wire's net within the hop radius plus the dot radius. The horizontal wire hops: the
radius is the larger of `WireJumpRadius` times the hopping wire's thickness over `WireThickness` (at least one) and half
the sum of both thicknesses plus one. `WirePaths.Rounded` walks the finished path and, on each horizontal line, puts the
hops of that line in travel order and merges those whose spans overlap (crossings closer than the sum of their radii)
into one hop with a flat top between two quarter circles of the largest radius in the group; each group is drawn as two
cubic quarter arcs (control distance `0.5522847498` radius, rising toward negative Y), and is skipped, leaving the wires
to cross, when its span plus a one unit margin does not fit inside the straight line (too near a corner curve, the wire
start
or the arrowhead clearance before the tip). The vertical wire, the polyline, the hit targets and the handles are not
affected. Hops are cubics in the ordinary path commands, so both painters draw them the same and dashes follow them.

`AddJunctions`: draws, above the wires and below the nodes, a filled `RectPrimitive` circle at every junction point found
by `AnalyzeWires`, in the color of the owner wire. `WireJunctions.Find` (internal static) collects every vertex of every
wire as a candidate with
the directions of its two neighbors, adds both directions of any segment that passes strictly through the candidate
(a "T" where one wire bends on a trunk another runs along), and reports candidates whose direction set has at least three
members; two wires that run together and turn together leave in two directions and make no dot, and neither does a wire
that ends on another's trunk in line with it. A wire that ends on the side of a trunk, or a trunk that two wires leave in
opposite directions, is a junction. Nothing is drawn when the
theme sets `WireJunctionDots` to false or the radius to zero (the corners stay square).

`AddWireHandles`: after all other elements, for each selected wire adds a second polyline hit target for its segments (so
the segments of a selected wire win over nodes drawn later), then a square `RectPrimitive` and a rectangular hit target
(inflated by 2) with the bend index for every bend, then a circular handle with a source or target handle target for each
end. Handle size is the theme's `WireHandleSize`. The targets are appended last, so handles beat the wire's segments and
the segments beat nodes; unselected wires keep their ordinary place below nodes.

Wire styling resolution: the composer asks `Diagram.ResolveWireStyle(wire)` for each wire on every build, so the style
always reflects the current model and host state; wires with collapsed-group display routes take the same path. Each
member of the returned `WireStyle` that is null falls back to the theme (`WireColor`, `WireThickness`,
`WireArrowheads`); a null style, no styler, or a styler that throws all give the plain theme look. The arrowhead length
and width are the theme values multiplied by `max(1, (thickness + theme thickness) / (2 * theme thickness))`, so heavy
wires get a proportionately larger head while thin wires keep the theme head. The selection and hover underlay keeps
the highlight colors, is `thickness + 4` wide and stays solid even when the wire is dashed.

Per-page styling: `WireStylers.PerPage(factory)` returns a styler that keeps, per page (in a weak table), the styler the
factory built and the `Diagram.Version` it was built at. For each wire it takes the wire's page; when there is no entry
or the page's diagram version differs, it calls the factory (once, however many wires follow) and stores the result;
then it applies the stored styler to the wire. Since the version advances with every change, undo, redo and
`RefreshAppearance`, the per-page state can never be stale and the host needs no cache of its own. A wire without a page
gets no style; a factory that throws leaves the entry unchanged (so the next wire retries) and the exception is absorbed
by `ResolveWireStyle`; a factory that returns null is stored as "style nothing".

Node styling resolution: `AddNode` asks `Diagram.ResolveNodeStyle(node)` for each visible node on every build and
gives the result (or `NodeStyle.None` for null, no styler or a throwing styler) to the `NodeVisualContext`. Members
that are null fall back to the theme: `NodeFill`, `NodeBorder`, `NodeBorderThickness` and `NodeHeaderText`. The
opacity (null or 1 means opaque) multiplies the alpha of every color the standard visual draws (box, border, texts,
port triangles, halo) through `Faded`, and is carried to the icon as `IconPrimitive.Opacity`. The icon tint is carried
as `IconPrimitive.Tint` and means "paint the icon as a silhouette in this color" (see the painters). The glow is
`StandardNodeVisual.GlowRings` (6) unfilled rounded rectangles, each 2 units wide, drawn below the box. Ring number r
(counting from 0 outward) is the box inflated by `1 + 2r` units, with the glow color's alpha times one half times the
square of `1 - r/6`, so it fades outward and looks the same in every painter. The selection or hover outline is added
after the visual and is never styled. `NodeStylers.PerPage` works exactly as `WireStylers.PerPage` (a weak per-page
table of the styler and the `Diagram.Version` it was built at, with a failing factory retried and a null styler
meaning "style nothing"). Node palette prototypes are built from a private diagram without a styler, so they always
show the node type's default look.

Core-side dashing: `WirePaths.Dashed` flattens the rounded path (each corner curve into eight segments), then walks it from
the source end alternating the "on" and "off" lengths of the pattern (an odd pattern is repeated once, as in SVG). Every
"on" stretch becomes one open figure (`MoveTo` then `LineTo`s), and a dash that turns a corner stays a single figure.
The dashed path is still one `PathPrimitive`, so painters need nothing beyond their existing support for multi-figure
stroked paths, and every painter draws the same pattern. A pattern that would need more than 5,000 dashes leaves the
wire solid. Painters choose their own line caps (butt in the PNG painter, round in the Avalonia painter), which makes
the Avalonia dashes look about one wire thickness longer, with correspondingly shorter gaps.

`AddAnnotation`: draws the note box, wraps the text to the inner width and keeps the lines that fit the height, adds the
highlight, and, only while selected, the resize handle with its hit target.

`AddExpandedGroup` and `AddCollapsedGroup`: draw the frame or box, title bar, toggle and title; publish the group
layout; record hit targets (border band, header and toggle for expanded groups; box and toggle for collapsed ones).
Proxy ports mirror the node port look and are labelled with the inner port's path.

`StandardNodeVisual.Build`: adds the glow (when the style has one), the box, the title (wrapped, and shrunk in
half-unit steps down to a minimum font size while it would not fit), the icon, the name, the summary lines and the
port triangles and labels. It is public and not sealed so custom visuals can wrap or override its parts (`AddGlow`,
`AddBox`, `AddTitle`, `AddIcon`, ...); the box, title and icon parts read the node's resolved style.

Preconditions: the page is attached to a diagram. Postcondition: the model, stored routes and history are unchanged.

#### Error Handling

A null page throws `ArgumentNullException`. Elements are never rejected: hidden or culled elements are simply not built.
A router that returns null for a display route is treated as a straight line. Exceptions from a host's custom visual or
measurer propagate to the caller of `Build`, since they are host defects. A `Diagram.WireStyler` that throws is the one
exception: the failure is swallowed for that wire (any exception except out-of-memory) and the wire is drawn from the
theme, so a faulty styling callback never blanks the diagram; a `Diagram.NodeStyler` that throws is absorbed the same way
by `ResolveNodeStyle`. `WireStyle` rejects non-positive or non-finite thickness and dash lengths when it is created;
`NodeStyle` rejects a non-positive or non-finite border thickness and an opacity outside 0 to 1, and `IconPrimitive`
rejects an opacity outside 0 to 1.

#### Dependencies

- NodeLayout, TextMeasurement and DiagramTheme units
- DiagramScene unit (the product) and its internal hit targets
- Routing subsystem through `Diagram.Router` (display routes), and Model subsystem types
- Geometry unit

#### Callers

- PlexusRendering (`PlexusRenderer`) and PlexusAvalonia (`DiagramEditor`) build scenes for painting and interaction
- Hosts and tests building scenes directly
