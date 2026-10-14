### SimpleRouter Unit Verification Design

#### Verification Approach

Unit tests in `SimpleRouterTests` call `SimpleRouter.Route` with hand-built `RouteRequest` values and compare the
result with exact expected points computed from the documented rules (stub end points, grid snapping). No
collaborators are involved, so nothing is mocked.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework with exactly the expected bend points.

#### Test Scenarios

- **Target ahead**
  (`SimpleRouter_Route_TargetAhead_ProducesSnappedElbow`): the stubs end at 115 and 385, so the vertical run is at
  the snapped midpoint 255.
- **Same row ahead**
  (`SimpleRouter_Route_SameRowAhead_ReturnsNoBendPoints`): an empty route.
- **Target behind**
  (`SimpleRouter_Route_TargetBehind_LoopsThroughBothStubs`): four bend points leaving through the start stub, running
  along the snapped middle row and entering through the end stub; the polyline is orthogonal.
- **Target behind on the same row**
  (`SimpleRouter_Route_TargetBehindOnSameRow_UsesChannelBelowTheRow`): the channel is two stub lengths below the row.
- **Snapping disabled**
  (`SimpleRouter_Route_GridSizeZero_DoesNotSnap`): the run is exactly at the midpoint.
- **Snapping would leave the stub range**
  (`SimpleRouter_Route_SnappingWouldLeaveStubRange_KeepsUnsnappedMidpoint`): the run stays at the unsnapped midpoint
  between the stubs.
- **Obstacles ignored**
  (`SimpleRouter_Route_Obstacles_AreIgnored`): equal routes with and without obstacles.
- **Determinism**
  (`SimpleRouter_Route_SameRequest_IsDeterministic`): two instances give equal results.
- **Null request**
  (`SimpleRouter_Route_NullRequest_ThrowsArgumentNullException`): an argument-null exception.

Boundary values: zero grid size, midpoint outside the stub range, shared row. Error path: null request.
