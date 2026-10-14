### DiagramScene Unit Design

![Scene Structure](SceneView.svg)

#### Purpose

Is the built scene: it exposes the primitives, answers hit tests and region queries from the geometry that was drawn, and
publishes node, group and wire layouts.

#### Data Model

- `DiagramScene` (immutable after construction): `Page`, `Theme`, `Primitives` (painting order), `Bounds` (content,
  excluding the grid), a spatial index over primitives by their bounds, a spatial index over hit targets in draw order,
  and dictionaries of node layouts, displayed wire polylines and group layouts.
- `HitKind` (canvas, node, node name, port, wire, wire bend, wire source handle, wire target handle, annotation,
  annotation resize handle, group, group header, group collapse toggle, collapsed group) and `HitResult` (kind, element,
  port, wire segment index, bend index, plus `Node`, `IsHit` and `GetWireSegment`). Segment `i` joins points `i` and
  `i + 1` of the full polyline, source anchor to target anchor; bend `i` is the route point `i`, polyline point `i + 1`.
  The end handles carry the wire's port in `Port`.
- `HitTarget` (internal): a kind, element, optional port, optional bend index and area, either a rectangle, a border band
  or a polyline with a half thickness.
- `SpatialIndex<T>` (internal): items and bounds lists in insertion order, a dictionary from cell key to item indexes
  (128-unit cells by default), and a list of large items (more than 64 cells).

Invariants: hit targets are stored in draw order; the index returns ascending insertion indexes.

#### Key Methods

`HitTest(point, tolerance = 3)`: queries the target index for the square of side twice the tolerance around the point,
walks the candidates from last to first and returns the first target that accepts the point. A rectangle target
accepts a contained point. A border band accepts a point that is inside the area inflated by the band width (the
tolerance, at least 2) and outside the area deflated by it. A polyline target accepts a point whose distance to some
segment is at most the tolerance plus half the wire thickness and reports the nearest segment. No match returns
`HitResult.Canvas`.

Handle priority: the composer appends the targets of selected wires last, in the order segments, bend handles, end
handles, so the backwards walk meets handles first, then the selected wire's segments, then everything drawn earlier
(nodes, annotations, unselected wires). A rectangle handle target reports its kind with the bend index or the port.
Handle targets (wire bends, wire ends, the annotation resize handle) exist only for elements in `SceneOptions.Selection`,
so a wire that is not selected is hit as `Wire` even directly on a bend; this is documented on `DiagramScene.HitTest`,
`HitKind` and `SceneOptions.Selection`.

`Query(region)`: returns the primitives whose bounds intersect the region, in painting order.

`SpatialIndex.Add`: computes the range of cells touched by the bounds; items touching more than 64 cells go to the large
list, others are appended to each touched cell's bucket. `QueryIndexes`: visits the cells covering the region (or, when
that would be more cells than there are buckets, scans all buckets), keeps items whose bounds really intersect the
region, adds the large items that intersect, de-duplicates with a set and sorts the indexes. Coordinates are clamped
before conversion to cell indexes so extreme values cannot overflow.

`TryGetNodeLayout`, `TryGetGroupLayout`, `TryGetWirePoints`: dictionary lookups; false for elements not displayed
(hidden by a collapse, on another page, or culled).

Postcondition: a scene never changes after construction; queries are read-only and safe from several threads.

#### Error Handling

The lookup methods throw `ArgumentNullException` for a null element. The index constructor throws
`ArgumentOutOfRangeException` for a non-positive cell size. Hit tests and queries never throw for any finite or
extreme coordinates; a miss is `HitResult.Canvas` or an empty list.

#### Dependencies

- Model types (`DiagramPage`, `Node`, `Wire`, `Group`, `Port`, `DiagramElement`)
- NodeLayout and DiagramTheme units, Geometry unit
- Built by the SceneBuilder unit

#### Callers

- PlexusRendering and PlexusAvalonia painters (primitives) and the PlexusAvalonia editor and gestures (hit testing,
  queries and layouts)
- Hosts and automation
