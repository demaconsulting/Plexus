### DiagramPage Unit Design

![Model Structure](ModelView.svg)

Scope: One diagram page: containers for nodes, wires, annotations and groups, and the undoable operations that edit
them (add, connect, group, move, remove, layout).

#### Purpose

DiagramPage is one canvas of a diagram. It holds the nodes, wires, annotations and groups placed on it and provides every
undoable operation that creates, connects, groups, moves, re-routes and removes them; nodes and wires cannot be created
or deleted any other way. The unit comprises the partial class files for the creation API (`DiagramPage.Elements.cs`), port
lookup and connection by path (`DiagramPage.Ports.cs`), groups (`DiagramPage.Groups.cs`), removal
(`DiagramPage.Removal.cs`) and layout (`DiagramPage.Layout.cs`), plus the three internal commands that perform the
changes: element presence, group membership and layout. Duplicating elements is part of the Snapshot unit.

#### Data Model

- **Content lists**: read-only observable lists of nodes, wires, annotations and groups, each in creation order, and a
  list of all elements in z-order (first is bottom-most). A new element is appended to both its typed list and the
  z-order list.
- **Name and state**: the page name (change is undoable) and an attached flag that is true while the page belongs to the
  diagram's page list. A removed page keeps its content but rejects modification.
- **Element positions**: element presence commands capture the index in the typed list and the z-order list the first
  time they run and reuse them, so undo and redo restore exact positions.
- **Layout command**: one mergeable command holding old and new bounds of nodes and annotations, old and new collapsed
  positions of groups and old and new routes of wires, stored in first-seen order. Merging two commands for the same page
  keeps the first old and the last new value for every target, which makes a drag a single compact entry.
- **Notifications**: the page raises a property change for its name; element additions and removals raise the diagram's
  `Changed` event after the element is fully attached or detached.

#### Key Methods

- **AddNode(type key or type, position, name, id)**: checks the page is attached, the type is the registered object and
  the position is finite; resolves the id (unique, generated as a dash-free GUID when null) and the name (trimmed and
  validated, or generated as the lowest unused `{key last segment}{n}`); sizes the node from the type's declared size or
  the theme default grown to fit its ports; stores the type's default payload. Only for a type with a port provider does
  it let the provider replace the declared ports (`Node.InitializePorts`) and grow the height (never the width) to
  `NodeGeometry.PortColumnHeight` of those ports, even over a declared size. A type without a provider keeps exactly
  `NodeGeometry.DefaultNodeSize`, so a declared size is authoritative and the palette ghost matches the dropped node. A
  provider that returns null (`InvalidOperationException`) or builds an invalid `PortSet` (`ArgumentException`) makes
  AddNode throw before anything is recorded. Finally it executes an element presence command "Add node".
- **AddAnnotation(text, bounds, id)**: validates text and bounds (finite, non-negative size) and executes "Add annotation".
- **CanConnect(a, b)**: returns a decision without changing anything. Denials, in order: page detached, a port not on a
  node of this page, equal directions, a port that a port provider took off its node (`Port.IsOnNode` false; a host may
  still hold it), same node, an existing wire between the same ports. Then it calls the
  diagram's connection rule with the output as source and the input as target and guarantees a reason for a denial
  without one. The default rule (`ConnectionRules.Default`) also refuses a second wire into an input whose definition
  does not allow fan-in; a host that assigns a rule without it decides fan-in itself.
  **CanReconnect(wire, newSource, newTarget)**: the same checks for the wire's new pair of ports (a null argument keeps
  that end), with the wire itself ignored in the duplicate check and a preliminary direction check (only an output can
  be a source, only an input a target); it backs `Wire.Reconnect` and the editor's drop feedback. Naming the wire's own
  current ports (or passing null for both) is "no change" and is allowed without asking the rule. While the rule runs
  the wire under check is published as the public `WireBeingReconnected` so wire-counting rules leave it out
  (`Port.CountWiresIgnoringReconnect` does so): moving a
  wire onto an input that holds another wire is refused, keeping it on its own target is not.
- **Connect(from, to, route, id)**: requires an allowed connection (else throws with the reason), orders the ports as
  output then input, computes the route with the diagram's router when none is given (a supplied route is normalized by
  the route cleaner), and executes "Connect".
  **Connect(path, path)**: resolves each path preferring an output for the first and an input for the second, then
  delegates. **FindPort(path, direction)**: tries every dot as split point from the right, resolving the node by name and
  then by id, and looks up the port; ambiguity without a direction throws.
- **ComputeRoute / BuildObstacles / BuildTraffic** (internal): build a route request (anchors, sides, obstacle boxes
  from visible nodes, grid size, stub length, the net of the wire's source port and the segments of the other wires
  held in a `WireTraffic`) and ask the router; the result is normalized by the route cleaner and a router returning null
  yields an empty route. A net is every wire with the same source port, joined through inputs that allow fan-in
  (wires ending at the same such input form one net with the wires of their sources); `WireTraffic` numbers the
  ports, merges nets with a small union-find and collects wire segments in page order. When
  `DiagramTheme.GroupsAreObstacles` is on (the default) `BuildGroupObstacles` prepares the frame of every visible group
  once per operation (bounds inflated by the obstacle margin; groups hidden in a collapsed group skipped) and
  `ComputeRoute` adds, per wire, every frame whose group contains neither end node at any depth (`GroupObstacles`,
  internal). Hypothetical node bounds are not applied to the frames: a frame that would change contains a moved node,
  whose wires are exempt from it anyway. Moving a group re-routes nothing unrelated; `RerouteAll` does.
- **Routing caches** (internal): the obstacle boxes and group frames of the current layout and the traffic of every
  visible wire (in page order) are cached per page, so routing many wires does not rebuild them for each wire.
  `OnElementChanging` runs before any notification of a change, attach or detach: a change of a node, annotation or
  group discards all three, a change of a wire discards the traffic, and a wire attached at the end of the page is
  added to the cached traffic exactly as a fresh build would add it. A theme change discards the caches of every page
  (other diagram settings leave them), and a page discards its caches when it is removed from or put back into the
  diagram, so a
  page restored by undo never routes with a theme that changed while it was out. A re-routed wire, and a new wire that
  would merge two nets at a fan-in input, route against a fresh traffic so the cache never holds a merge for a wire
  that does not exist. `WireTraffic.Segments` returns a snapshot view of an append-only list, rebuilt only after a
  merge, and `GroupObstacles.For` returns the obstacles followed by the applicable frames as a view instead of a copy.
  Results are identical to building everything for each wire.
- **RerouteAll()**: within one transaction sets the route of every wire to a freshly computed route, routing the wires
  in page order and adding each new route to the traffic the next wire keeps clear of (stale old routes never count);
  unchanged routes record nothing and an unchanged page creates no entry.
- **Move(elements, dx, dy)**: validates, expands groups to members, drops wires and duplicates, computes new bounds and
  collapsed positions and calls the layout commit. **ApplyBounds** (internal): used by the node and annotation bounds
  setters. **CommitLayout**: builds a layout command with the changed bounds and positions, plans the wire updates and
  executes it when not empty. **PlanWireUpdates**: wires with an unchanged route (untouched, or translated because both
  nodes moved by the same delta without resizing) form the traffic first; then every other wire with a changed end is
  routed against the hypothetical new bounds and that traffic, in page order. An optional set of forced re-routes adds
  wires whose end nodes keep their bounds but whose anchors moved (a port update); with no changed bounds the cached
  obstacles of the current layout are used.
- **PlanPortUpdate(plan, node)** (internal, `DiagramPage.PortUpdates.cs`): plans everything a recomputation of a node's
  ports implies into the open plan of the triggering property or data change, in an order whose reverse restores the
  model exactly: (1) asks for the change (`NodePortsCommand.TryCreate`; null means nothing to do), (2) removes the
  wires of
  removed ports in page order, (3) remembers the anchors of the node's remaining wires, (4) runs the port swap, (5)
  checks each remaining wire on a retyped port with the connection rules as a reconnect of itself (so it does not count
  against its own port) and removes it when refused, (6) grows the node to the height its longer port column needs
  (never shrinking) and re-routes, through one layout command, every remaining wire of the node whose anchor moved,
  like after a resize. Removed wires are therefore reported through `Diagram.Changed` before the node reports `Ports`.
- **CreateGroup(members, title, id)**: ignores wires, requires members on this page that are ungrouped or share one
  parent, and as one multi-step operation adds the group, replaces the members in the parent by the new group, adds the
  members and records the collapsed position. **Ungroup / ReleaseGroup**: members take the frame's slot in the parent or
  become top-level, then the frame is removed. **SetGroupCollapsed / AddToGroup / RemoveFromGroup** (internal): back the
  group's collapse and membership members with undoable commands and the cycle and page checks.
- **Remove / RemoveWithMembers(elements)**: resolves the full set (members of groups when requested, all wires of removed
  nodes), then as one multi-step operation removes wires, releases nodes and annotations from their groups and removes
  them, and dissolves group frames innermost first. Undo restores objects, positions and membership.
- **AttachCore / DetachCore** (internal): used by the presence command; update the typed list, the z-order list and the
  diagram's element index, and raise `Changed` only when the page is completely updated.

#### Error Handling

Null arguments throw argument-null exceptions. A detached page throws an invalid-operation exception from every
modifying operation. Unknown or unregistered node types, non-finite coordinates, invalid bounds, duplicate or blank ids,
invalid names, elements of another page and empty member lists throw argument exceptions; a denied connection and mixed
group membership throw invalid-operation exceptions with the reason. All validation happens before the first change, and
multi-step operations run through the diagram's `Perform`, which reverts partial work if a step fails, so a failed call
leaves the model unchanged.

#### Dependencies

- Diagram: owner; provides types, theme, router, connection rule, name and id helpers, the element index, `Perform` and
  the shared undo history.
- History: executes presence, membership, layout and set-value commands and transactions.
- Node, Wire, Annotation and Group: the element types created and changed here; they call back into the page for
  bounds, route, collapse and membership changes.
- Geometry and Scene node geometry: positions, default node sizes, port anchors, the port column height and routing
  obstacles (computed from each node's current ports).
- Node (`NodePortsCommand`, `InitializePorts`): the port change planned by `PlanPortUpdate`.
- Routing: the router interface used to compute routes.
- ConnectionRules: the decision type returned by `CanConnect`.

#### Callers

Host applications and the editor control call the public operations. Nodes, ports, wires and groups call the internal
members for bounds, routes, ports and group changes (`Node.ExecuteAffectingPorts` calls `PlanPortUpdate`); the
diagram's move-to-page operation uses the presence and
membership commands; the Snapshot unit rebuilds pages through the public API.
