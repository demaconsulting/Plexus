## Routing Subsystem Design

![Routing Structure](RoutingView.svg)

### Overview

The Routing subsystem computes the interior bend points of a wire from its two end anchors and the boxes that stand in
its way. It consists of the `IWireRouter` contract (with `RouteRequest` and `PortSide`) and two implementations:

- **OrthogonalRouter** (default): finds a cheap orthogonal path around the obstacle boxes.
- **SimpleRouter**: a constant-time elbow that ignores obstacles, offered as the predictable alternative and used by
  the default router as the last-resort fallback.

The subsystem is pure computation. It reads no model state, holds no state between calls and performs no I/O, so
routing is deterministic, reproducible in tests and in headless rendering, and thread-safe. The page model (Model
subsystem) owns the decision of when to route; the Scene subsystem owns the obstacle geometry
(`NodeGeometry.RoutingObstacles`) and calls the router for transient display routes of collapsed groups.

Boundaries: the subsystem does not decide which wires exist, does not move nodes and does not store routes. Stored
wire routes belong to the model and are restored by the Serialization subsystem without consulting any router.

### Interfaces

Exposed:

- `IWireRouter.Route(RouteRequest)`: returns the interior bend points from start to end; endpoints are not included and
  an empty list means a straight segment. Implementations must be deterministic, must not mutate the request and must
  not depend on UI state.
- `RouteRequest`: an immutable snapshot with `Start` and `End` anchors, `StartDirection` and `EndDirection`
  (`PortSide`, defaulting to right for outputs and left for inputs), the `Obstacles` (node boxes already inflated by
  the theme's obstacle margin and including the boxes of the wire's own end nodes), the `GridSize` routers may snap
  bend points to and the `StubLength`, plus the optional `Net` of the wire and the `ExistingSegments`
  (`RouteSegment` values) of the wires already routed.
- `OrthogonalRouter` (with the `BendPenalty`, `OverlapPenalty` and `CrossingPenalty` settings) and `SimpleRouter`, both
  public sealed classes; `RouteCleaner` and `RouteEditor`, public static helpers for normalizing and editing routes.

Consumed: the geometry types `Point` and `Rect` of the Geometry unit. `Diagram.Router` (Model subsystem) holds the
active router, which defaults to an `OrthogonalRouter`.

### Design

**Contract choices.** The request carries obstacles that already include the end nodes' own boxes. Routers therefore
cannot "cheat" by passing through the nodes at either end and must leave and enter ports through stub segments of
`StubLength`. Passing the obstacles pre-inflated keeps all clearance policy in one place (the theme) and keeps routers
free of model knowledge. Because the request is a plain data holder, routers are testable without a diagram.

**Orthogonal routing.** The default router searches a *sparse orthogonal visibility grid* built from the coordinates
that can matter (obstacle edges, the two stub end points, a grid-snapped middle channel and the borders of the search
window), instead of a uniform grid with one cell per grid step. The search runs A* over states of (grid intersection,
direction of travel) so that the cost function can charge a *bend penalty* for every change of direction; the penalty
makes wires prefer a few long segments over a staircase. A tiny tie-break term prefers bends near the middle of the two
end points, which resolves otherwise equal routes (a centered Z shape wins over an L shape hugging the source node).
The sparse grid is chosen because routing a wire among hundreds of nodes on a uniform grid costs a number of cells
proportional to the diagram area, whereas the sparse grid costs a number of lines proportional to the nearby
obstacles. Whether a step between two neighboring lines is free is answered lazily from the obstacles covering that
column or row, so only the explored part of the diagram is ever examined.

**Windowing.** The first attempt searches only a window around the two ports (padded by a fixed distance plus the stub
length); only when that fails does a second attempt widen the window to cover every obstacle. A short wire among many
distant nodes is thus routed from the handful of obstacles near it. The two-stage approach trades a possible slightly
different route for a long wire (a detour may exist outside the first window) for the speed of the common case.

**Stubs and arrival direction.** The search runs between the ends of the two stubs. The wire must arrive at the end stub
traveling into the end node, so reaching it in any other direction costs an extra bend, and reaching it traveling away
from the end node is not allowed.
After the search, the stub points are joined to the path and redundant points (duplicates and collinear points) are
removed, leaving exactly the bend points a wire stores.

**Overlapping clearance boxes.** When nodes are closer together than twice the margin, a neighbor's inflated box can
cover a stub end point. Rather than failing, such an obstacle is shrunk just enough to leave the point on its edge:
the real node box stays protected while the wire can get out.

**Fallback.** When no path exists in either window (a port that is walled in), the orthogonal router returns the route of
the simple router for the same request, so a wire is always produced. A wire through a node is preferable to an
exception during interactive editing; the user then moves the nodes.

**Simple routing.** The simple router computes a closed-form route from the stub end points: a single vertical run at
the (grid-snapped) midpoint when the target is ahead, and a four-point loop through a horizontal channel when it is
behind. Snapping is applied only if it keeps the run between the stubs, so the wire never doubles back through the
node it leaves.

**Determinism.** Both routers break ties by insertion order and use no randomness, time or shared mutable state. The
request is never modified.

**Net separation.** A *net* is every wire that starts at the same output port; the page numbers the source ports and
passes the wire's net and the segments of the wires already routed (`RouteRequest.Net`, `ExistingSegments`). The router
keeps the segments of other nets only (own-net segments may be shared like a bus trunk), and the search charges a high
cost per unit of length run along one (`OverlapPenalty`) and a small cost for crossing one (`CrossingPenalty`). For
this to work the sparse grid is enriched, only when other nets are present, with parallel tracks one grid step apart
beside the middle channel and beside each foreign segment, so nets that must not overlap can sit side by side; with
no foreign segments the grid and the result are exactly as before. The page routes wires in page order and adds each
new route to the traffic of the next, so a wire never avoids the stale route of a wire that is about to be replaced and
the result of re-routing a page is repeatable. Penalties only add cost, so the search stays optimal for the combined
cost and its estimate stays admissible.

**Groups as obstacles.** Group frames are not part of the router's own logic: the page (and the scene builder, for
display routes of collapsed groups) simply adds them to `RouteRequest.Obstacles`, so routers need no group knowledge.
Per wire the frame of every visible group (an expanded group's bounds including the title bar, a collapsed group's box,
each inflated by the obstacle margin) is added unless one of the wire's two end nodes lies inside that group at any
depth; a group hidden inside a collapsed group is not drawn and not offered. The option
`DiagramTheme.GroupsAreObstacles` (default on) switches this off. Routes are still computed only when a wire is created
or reconnected, when one of its end nodes moves, and by `DiagramPage.RerouteAll`: moving a group does not re-route unrelated
wires, so hosts call `RerouteAll` afterwards when those wires should avoid the new position. The existing fallback
applies unchanged when no obstacle-free route exists.

**Route cleaning and editing.** `RouteCleaner` normalizes a route (collinear bends and zero-length segments vanish,
the drawn shape never changes) and every stored route passes through it. `RouteEditor` holds the pure geometry of manual
wire editing: moving a segment or a bend perpendicular to itself while its neighbors stretch, inserting a jog beside a
port when a stub moves (ports never move) and adding a bump to a segment. Keeping this in the Routing subsystem, free
of model and UI types, lets the Avalonia gestures recompute the route from the start of a drag on every pointer move
and lets the rules be tested without a diagram.

**Performance.** The default router is designed so that routing one wire among hundreds of nodes takes well under a
millisecond for the usual short wire. The requirement bounds are deliberately generous (a total for two hundred wires
and for a whole connected page) so that they fail only when scalability is lost, not because of machine noise.
