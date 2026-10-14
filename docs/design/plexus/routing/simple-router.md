### SimpleRouter Unit Design

![Routing Structure](RoutingView.svg)

#### Purpose

Provides a cheap, obstacle-ignoring route: a Z-shaped elbow for targets ahead and a loop for targets behind. It is the
predictable alternative that hosts can assign to `Diagram.Router`, and the fallback of the orthogonal router.

#### Data Model

Stateless; no fields other than a 1e-9 tolerance constant. Inputs come from `RouteRequest` (see the OrthogonalRouter
design): `Start`, `End`, `StartDirection`, `EndDirection`, `GridSize`, `StubLength`. Obstacles are never read. Only
horizontal sides are meaningful: the sign of a side is -1 for left and top and +1 for right and bottom.

#### Key Methods

`Route(request)`: derives the stub end x-coordinates (`Start.X + sign * stub` and `End.X + sign * stub`). When the end
stub lies ahead of the start stub (opposite signs and travel in the start direction), it returns no points for aligned
ports, otherwise the two points of a vertical run at the grid-snapped midpoint of the stubs. Otherwise it returns a
four-point loop: out through the start stub, along a horizontal channel and in through the end stub. The channel is
the snapped middle row of the two ports, or two stub lengths below the row when both ports are on the same row.

`Snap` rounds a coordinate to the nearest grid line (away from zero at midpoints; disabled when the grid is not
positive). `SnapBetween` applies the snap only when the snapped value stays between the two stubs, because a run
outside the stubs would fold the wire back through the node it leaves.

Postcondition: the result is deterministic, the segments of start, result, end are axis-aligned, and the route is
identical for identical end points whatever the obstacles.

#### Error Handling

A null request throws `ArgumentNullException`. There are no other failure modes: every combination of end points
yields a route, and negative stub lengths are treated as zero.

#### Dependencies

- Geometry unit (`Point`)

#### Callers

- `OrthogonalRouter` (fallback when no obstacle-free route exists)
- Hosts that assign it to `Diagram.Router`
