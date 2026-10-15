## Routing Subsystem Verification Design

### Verification Approach

The Routing subsystem is verified by integration tests in `RoutingTests` (every scenario below is in `RoutingTests.cs`)
that exercise the routers together with the
page model that calls them (`Diagram.Router`, `DiagramPage.Connect`) and the shared route assertions (`RouteAssertions`:
orthogonality and obstacle-clearance checks). The geometry types are real; no mocks are used. One recording router
implementing the public `IWireRouter` contract stands in for a host router to observe the request the model builds.
Unit-level behavior of each router is verified separately in the unit verification designs (OrthogonalRouter and
SimpleRouter), with unit requirements linked below the subsystem requirements in the ReqStream hierarchy.

### Test Environment

The standard xunit test runner of the `DemaConsulting.Plexus.Tests` project on net8.0, net9.0 and net10.0 on Windows,
Linux and macOS. No files, network or fonts are used.

### Acceptance Criteria

All tests below pass on every target framework. Every routed wire of every scenario must be orthogonal. Wires of the
default router avoid every node box (other than at their own ports) whenever an obstacle-free route exists; when none
exists the route is the simple elbow, which may cross nodes (verified separately by the no-free-path scenario).

### Test Scenarios

- **Default router is orthogonal**
  (`Diagram_Router_Default_IsOrthogonalRouter`): a new diagram routes with the obstacle-avoiding router.
- **Connected wires are orthogonal under either router**
  (`Routing_ConnectedWires_UnderEitherRouter_AreOrthogonal`): a wire to a target behind and one to a target ahead,
  connected through the page, consist of axis-aligned segments for the default and the simple router.
- **Wire routes around a node placed between its ports**
  (`DiagramPage_Connect_NodeBetweenPorts_RoutesAroundIt`): the routed wire is orthogonal, has bend points and does not
  cross the blocker's box.
- **Nodes changed after earlier wires**
  (`DiagramPage_Connect_NodeMovedOrAddedAfterEarlierWires_IsAvoided`): after a wire is routed, a node moved into the
  way of a new wire and a node added in the way of another are each avoided at their current position.
- **Theme changed after wires were routed**
  (`DiagramPage_Connect_AfterThemeChange_UsesTheNewTheme`): on a page whose wires were routed with the default theme,
  the obstacle margin is widened, once while the page stays in the diagram and once while it is removed (the removal is
  then undone); a new wire on either page takes the route of a page that had the wide margin from the start, which
  differs from the default-margin route.
- **Wires leave and enter through horizontal stubs**
  (`Routing_ConnectedWires_LeaveAndEnterPortsHorizontallyThroughStubs`): the first and last segments of connected
  wires are horizontal and at least one stub long, for both routers, ahead and behind.
- **No obstacle-free path still yields a route**
  (`Routing_NoObstacleFreePath_StillReturnsAnOrthogonalRoute`): a request whose end stub is enclosed by walls returns
  the simple elbow, which is orthogonal.
- **Host router receives the request**
  (`Routing_HostRouter_ReceivesRequestForNewWires`): the wire carries the bend points the host router returned, and
  the request holds the port anchors, sides, the theme's stub length, the theme's grid size (the diagram theme uses a
  grid size of 20, not the default 15, so the value is observably passed on) and obstacles covering both nodes.
- **Determinism**
  (`Routing_SameRequest_BothRouters_AreDeterministic`): two instances of each router give equal results for one
  request with obstacles.
- **Dense page**
  (`Routing_DenseConnectedPage_RoutesEveryWireOrthogonally`): a lattice of sixty nodes gets all ninety-five connected
  wires, and every one is orthogonal.
- **Null request**
  (`Routing_NullRequest_BothRouters_ThrowArgumentNullException`): both routers reject a null request.
- **Net separation**
  (`Routing_TwoNets_UseSeparateTracksAfterConnectAndReroute`): two producers feeding adjacent inputs of one mixer, and a
  branch of the first net to a consumer, get wires of different nets that share no collinear stretch when connected
  and after re-routing the whole page; every wire stays orthogonal.
- **Wire by wire equals re-routing the page**
  (`DiagramPage_ConnectWireByWire_MatchesRerouteAll`): a lattice of thirty mixers connected wire by wire, with six
  wires merging at one fan-in input, keeps every route when the whole page is re-routed, and re-routing records no
  undo entry.
- **Route cleaning**
  (`Routing_StoredRoutes_AreNormalized`): a padded route (collinear bend, duplicated bend, bend on the target anchor)
  normalizes to two bends, and a route stored on a connected wire is normalized the same way without changing its shape.
- **Route editing**
  (`Routing_RouteEditing_KeepsRoutesOrthogonalWithFixedPorts`): moving a segment, moving a bend and inserting a jog on
  a routed wire each give a route that is orthogonal from the source anchor to the target anchor; the moved segment
  lands at the requested coordinate.
- **Groups as obstacles**
  (`Routing_GroupFrameBetweenPorts_IsRoutedAround`): a group frame spanning the free row between two nodes, containing
  neither end node, is avoided by a new wire with the obstacle clearance. Which group frames are supplied (end-node
  exemption, collapsed boxes, hidden groups, the theme option, re-routing) is verified by the Model, DiagramPage,
  DiagramTheme, Scene and SceneBuilder verification designs.

Boundary conditions covered: targets ahead and behind, aligned and unaligned ports, enclosed ports, dense crossings.
Error paths: null request, request without a free path.
