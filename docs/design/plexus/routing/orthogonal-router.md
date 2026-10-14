### OrthogonalRouter Unit Design

![Routing Structure](RoutingView.svg)

#### Purpose

Produces the default wire route: orthogonal, obstacle-avoiding, with few bends and short straight stubs at both ports.
The unit also owns the routing contract types (`IWireRouter`, `RouteRequest`, `PortSide`) and the internal search
class `OrthogonalSearch` that implements the path search.

#### Data Model

- `OrthogonalRouter`: stateless; public settings `BendPenalty` (default 20, in the same units as segment length),
  `OverlapPenalty` (default 10, extra cost per unit of length run on top of a segment of another net; zero disables net
  separation) and `CrossingPenalty` (default 8, extra cost of crossing such a segment). Constants: a window padding of
  60 units for the first, bounded attempt and a 1e-9 coordinate tolerance.
- `RouteRequest`: init-only `Start`, `End`, `StartDirection`, `EndDirection`, `Obstacles`, `GridSize` (default 15),
  `StubLength` (default 15), `Net` (the net of the wire being routed, -1 when unknown) and `ExistingSegments` (the
  `RouteSegment` list of the wires already routed, empty by default). `RouteSegment` is a public record struct of two
  points and a net identity; a net is every wire from the same output port, numbered by the page.
- `ForeignSegments` (internal): the horizontal and vertical segments of the other nets taken from the request
  (own-net, diagonal and zero-length segments dropped) with the two penalties, or null when nothing is left.
- `RouteCleaner` (public static): `Clean(start, route, end)` returns the interior bends without duplicate points,
  without bends equal to an end point and without bends that lie on the straight run between their neighbors; genuine
  reversals are kept; idempotent and deterministic. It is built as an incremental stack (push a point, pop the middle of
  the last three while it is redundant).
- `RouteEditor` (public static): pure editing operations on (start, route, end) that return the new cleaned route or
  null: `MoveSegment` (perpendicular move, neighbors stretch), `MoveBend` (moves the bend's horizontal segment to the new
  Y and its vertical segment to the new X) and `InsertJog` (a bump of four bends, one step deep and at most two steps
  wide, kept clear of the segment ends). The ports are fixed: moving a segment next to a port inserts a jog of at most
  one stub length beside the port (the first stub always leaves to the right, the last always arrives from the left; a
  wire without bends gets a jog at both ends), and a vertical segment right behind a stub is kept one stub length from
  the port. Routes with a diagonal or zero-length segment, and indexes outside the route, give null.
- `PortSide`: `Left`, `Right`, `Top`, `Bottom`, mapped internally to direction indexes 0 right, 1 left, 2 down, 3 up so
  the opposite direction is `index ^ 1`.
- `OrthogonalSearch` (single use per attempt): sorted de-duplicated line coordinates `_xs` and `_ys`, the obstacle boxes
  selected for the window, and lazily built per-column and per-row lists of covering obstacles. A search state is a grid
  intersection plus the travel direction, packed into one integer. Invariant: every obstacle edge inside the window is a
  grid line, so an obstacle covers a column or row entirely or not at all. When foreign segments are present it also
  keeps them as per-row and per-column interval lists.

#### Key Methods

`Route(request)`: validates the request, moves the start and end anchors outward by the stub length to get the search
end points, computes the arrival direction (opposite of the end side), builds the foreign segments of the other nets,
runs `OrthogonalSearch.Find` bounded, then unbounded if the first attempt returns no path, falls back to
`SimpleRouter.Route` when both fail, and otherwise prepends the start anchor, appends the end anchor and removes
redundant points. Postcondition: the result lists the interior bend points, every segment of start, result, end is
axis-aligned, and no segment enters an obstacle interior (unless the fallback was used).

`OrthogonalSearch.Find(...)`: builds the window (the two end points, plus all obstacles when unbounded, inflated by the
padding), selects obstacles that overlap the window and shrinks any that cover an end point so the point lies on its
edge, collects grid lines, and runs A*. With foreign segments it additionally adds the middle channel plus three
parallel lines on each side (one grid step apart) and, for every foreign segment reaching into the window, the lines
at its coordinate and two grid steps on each side, so a wire has free tracks to move onto; without foreign segments the
grid is exactly as before.

`OrthogonalSearch.Run()`: A* over (intersection, direction) states with a priority queue ordered by (cost plus
estimate, estimate, insertion sequence). Moves go to the neighboring grid line in the four directions except straight
back. A move costs its Manhattan length; a change of direction adds the bend penalty plus a tiny center-bias term
(weight 1e-3) that prefers bends near the middle of the end points. Because both bends of a vertical step carry the
term, a small step between two rows slightly apart (for example half a port pitch) lands in the middle of the run
between the two stubs, snapped to the grid, rather than next to either port; this is deliberate and covered by tests.
A step that runs along a foreign segment adds the
overlap penalty per unit of overlapped length. "Along" is deliberately generous: a grid line within 4 units of a foreign
segment counts as the same track, and the segment is stretched by 20 units at both ends for this test (these two
numbers are the constants `NearCollinearTolerance` and `NearCollinearReach`), so a run that stops and another net's run
that starts a port pitch later on the same track, which would read as one wire with a kink, are kept apart. This only
applies to the overlap penalty; crossings still use the exact segment. Arriving at a point strictly inside a foreign
segment while crossing
it adds the crossing penalty. Penalties only ever add cost, so the estimate, the Manhattan distance, still never
overestimates and the result stays optimal for the combined cost. Stepping onto the goal in the wrong direction is
forbidden or costs an extra bend. A step is blocked when it crosses the open interior of an obstacle; obstacle edges are
traversable.

Preconditions: obstacles are finite boxes; `StubLength` below zero is treated as zero. Parameter types are as in the
data model.

#### Error Handling

A null request throws `ArgumentNullException`. Geometric failure is not an error: when no path exists the router
returns the simple elbow, so callers never see an exception or an empty failure value. Degenerate obstacles (empty or
outside the window) are skipped. The search terminates because a state is re-queued only when reached at a strictly
lower cost and the grid is finite.

#### Dependencies

- `SimpleRouter` (fallback)
- Geometry unit (`Point`, `Rect`)

#### Callers

- Model subsystem: `DiagramPage` connects, moves and re-routes wires through `Diagram.Router`, which defaults to this router.
- Scene subsystem: `SceneBuilder` routes transient display routes for wires that attach to collapsed groups.
- Hosts that call `IWireRouter` directly.
