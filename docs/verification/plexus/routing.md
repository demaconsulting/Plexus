## Routing Subsystem Verification Design

### Verification Approach

The Routing subsystem is verified by integration tests in `RoutingTests` that exercise the routers together with the
page model that calls them (`Diagram.Router`, `DiagramPage.Connect`) and the shared route assertions (`RouteAssertions`:
orthogonality and obstacle-clearance checks). The geometry types are real; no mocks are used. One recording router
implementing the public `IWireRouter` contract stands in for a host router to observe the request the model builds.
Unit-level behavior of each router is verified separately in the unit verification designs (OrthogonalRouter and
SimpleRouter), with unit requirements linked below the subsystem requirements in the ReqStream hierarchy.

### Test Environment

The standard xunit test runner of the `DemaConsulting.Plexus.Tests` project on net8.0, net9.0 and net10.0 on Windows,
Linux and macOS. No files, network or fonts are used.

### Acceptance Criteria

All tests below pass on every target framework. Every routed wire of every scenario must be orthogonal, and the
default router's wires must not cross the box of any node other than at its own ports.

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
  the request holds the port anchors, sides, the theme's stub length and obstacles covering both nodes.
- **Determinism**
  (`Routing_SameRequest_BothRouters_AreDeterministic`): two instances of each router give equal results for one
  request with obstacles.
- **Dense page**
  (`Routing_DenseConnectedPage_RoutesEveryWireOrthogonally`): a lattice of sixty nodes gets all ninety-five connected
  wires, and every one is orthogonal.
- **Null request**
  (`Routing_NullRequest_BothRouters_ThrowArgumentNullException`): both routers reject a null request.
- **Net separation**
  (`DiagramPage_Connect_TwoNetsToAdjacentInputs_DoNotRunAlongEachOther`, `DiagramPage_RerouteAll_TwoNets_KeepsNetsApartAndIsRepeatable`):
  two producers feeding adjacent inputs of one mixer get wires that share no collinear stretch, also after re-routing
  the whole page, which is repeatable.
- **Wire by wire equals re-routing the page**
  (`DiagramPage_ConnectWireByWire_MatchesRerouteAll`): a lattice of thirty mixers connected wire by wire, with six
  wires merging at one fan-in input, keeps every route when the whole page is re-routed, and re-routing records no
  undo entry.
- **Route cleaning and editing**
  (`Wire_SetRoute_CollinearAndZeroLengthBends_AreNormalized`, `Wire_AddJog_StraightSegment_AddsBumpAsOneUndoEntry`):
  the page-level effect of the cleaner and the editor; their geometry is verified in the OrthogonalRouter unit tests.
- **Groups as obstacles**
  (`DiagramPage_Connect_GroupBetweenPorts_RoutesAroundGroupFrame`, `GroupObstacleTests`): a group frame spanning the
  free row between two nodes is avoided, is not an obstacle for a wire with an end node inside it (at any depth),
  collapsed boxes block, groups hidden in a collapsed group do not, the theme option switches it off, and moving a
  group alone leaves wires untouched until `RerouteAll`; the details are in the DiagramPage, DiagramTheme and
  SceneBuilder verification designs.

Boundary conditions covered: targets ahead and behind, aligned and unaligned ports, enclosed ports, dense crossings.
Error paths: null request, request without a free path.
