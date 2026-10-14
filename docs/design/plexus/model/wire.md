### Wire Unit Design

![Model Structure](ModelView.svg)

Scope: Directed connection from an output port to an input port with a persisted route.

#### Purpose

The Wire unit models one connection between an output port and an input port. It stores only the interior bend points
(the route); the two end points always come from the current port anchors, so a wire can never be left detached when
a node moves or is resized. Wires are created by the page connect operation (see DiagramPage Design) and are never
members of a group.

#### Data Model

- `Source` (output port) and `Target` (input port): read-only properties backed by private fields. They are fixed at
  creation (the page connect operation orders the two ports it receives so that the output is the source) and change
  only through `Reconnect`, which writes both fields together with the route (see `ApplyEnds`).
- `Route`: the interior bend points as an array copied from the caller and normalized by the route cleaner (see
  OrthogonalRouter Design), exposed as a read-only list. End points are not part of the route.
- Inherited from `DiagramElement` (see Node Design): `Id`, `Page`, `Parent` (always null), `IsVisible`.
- Computed values: `GetPoints()` is the source anchor, the bend points and the target anchor; `GetBounds()` is the
  bounding box of that polyline (zero width or height for a straight wire).

#### Key Methods

- `SetRoute(route)`: precondition: the wire is attached and `route` is not null. Normalizes the list with
  `RouteCleaner.Clean` (collinear bends and zero-length segments are removed, the drawn shape never changes); if the
  result equals the current route nothing happens; otherwise executes an undoable value command named "Edit wire
  route" whose apply and revert delegate write the route through `SetRouteCore`, which raises the change notification
  for `Route`. Consecutive calls inside one transaction merge, so a drag is one entry.
- `Reroute()`: asks the page to compute a route between the two ports with the diagram's router (against the traffic
  of all other wires), then applies it with `SetRoute`. Manual edits are discarded; if the computed route equals the
  current one nothing is recorded.
- `AddJog(segmentIndex, near)`: asks `RouteEditor.InsertJog` for the route with a bump of one grid step added to the
  segment and stores it as an undoable entry named "Add wire jog"; returns false and records nothing when the editor
  reports null (bad index, diagonal route).
- `Reconnect(newSource, newTarget)`: asks `DiagramPage.CanReconnect` (the connection rules with this wire excluded from
  the duplicate check, plus a direction check: only an output can be a source and only an input a target) and throws
  `InvalidOperationException` with the reason when denied; a call that changes neither port returns without recording;
  otherwise it computes the new route with the page (excluding this wire's own segments from the traffic) and executes
  one `ReconnectCommand`, whose `Do` and `Undo` call `ApplyEnds` to swap ports and route together and raise the
  change notifications for `Source`, `Target` and `Route`. The wire object and its identifier never change.
- `GetPoints()` and `GetBounds()`: pure computations from the current anchors and route; bounds start from the first
  point and union every following point.
- `Remove()` (inherited): removes the wire from the page through the page remove operation; undo restores the same
  object with its route and identifier.

#### Error Handling

- `SetRoute(null)` throws `ArgumentNullException`.
- `SetRoute`, `Reroute`, `AddJog`, `Reconnect` and `Remove` on a wire that is removed throw `InvalidOperationException`,
  because the wire has no attached page.
- `Reconnect` with a denied pair throws `InvalidOperationException` carrying the denial reason.
- Exceptions are propagated; nothing is caught. A rejected call leaves the wire unchanged.

#### Dependencies

- DiagramPage (route computation, connection check, removal, history access through the diagram) and the undo history value
  command.
- Port and Node (anchors), Geometry (`Point`, `Rect`), and the routing contract through the diagram's router; the
  Routing unit's `RouteCleaner` and `RouteEditor`.

#### Callers

DiagramPage creates, removes and re-routes wires (for example when nodes move or on route recalculation of a whole page);
Node and Port list their wires; the Scene builder draws wires from `GetPoints()`; hit testing, serialization and the
Avalonia editor read and edit routes through the public members.
