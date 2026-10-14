### OrthogonalRouter Unit Verification Design

#### Verification Approach

Unit tests in `OrthogonalRouterTests`, `NetSeparationTests`, `RouteCleanerTests` and `RouteEditorTests` call
`OrthogonalRouter.Route`, `RouteCleaner.Clean` and `RouteEditor` directly with hand-built `RouteRequest` values and
points, so
the unit is tested in isolation from the page model. The fallback is observed against a real `SimpleRouter` (the
dependency is trivial and deterministic, so it is not mocked). The shared `RouteAssertions` helpers check
orthogonality and that no segment enters the interior of an obstacle.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework. Routes are exact where the expected shape is specified (centered Z
shape, no bend points); otherwise the structural properties (orthogonal, clear of obstacles, stub lengths) hold.

#### Test Scenarios

- **Unobstructed forward target**
  (`OrthogonalRouter_Route_NoObstacles_ForwardTarget_ProducesCenteredZShape`): two bend points with the vertical run
  centered between the stubs and snapped to the grid.
- **Same row**
  (`OrthogonalRouter_Route_SameRow_ReturnsNoBendPoints`): aligned ports give an empty route.
- **Small row offset**
  (`OrthogonalRouter_Route_SmallRowOffset_PlacesStepInMiddleOfRun`,
  `OrthogonalRouter_Route_SmallRowOffsetBetweenNodes_StepInMiddleAndDeterministic`): for ports a fraction of a grid
  apart, in either direction and over short and long runs, the route has one vertical step within half a grid of the
  middle between the two stubs, also between two flanking margin boxes, and repeated routing is identical.
- **Obstacle between ports**
  (`OrthogonalRouter_Route_ObstacleBetweenPorts_AvoidsObstacleInterior`): the route detours, is orthogonal and avoids
  the interior of the box.
- **Target behind source**
  (`OrthogonalRouter_Route_TargetBehindSource_UsesStubsOutAndIn`): the first segment runs right by at least a stub
  and the last enters from the left.
- **Bottom start side**
  (`OrthogonalRouter_Route_BottomStartSide_LeavesDownward`): the first segment runs straight down by at least a stub
  for a start side of bottom and an end side of top.
- **Obstacle covers a stub point**
  (`OrthogonalRouter_Route_ObstacleCoversStubPoint_StillRoutes`): a path is found and it is not the fallback elbow.
- **End walled in**
  (`OrthogonalRouter_Route_EndWalledIn_FallsBackToSimpleRouter`): four walls around the end stub give the simple
  router's route.
- **Determinism**
  (`OrthogonalRouter_Route_SameRequest_IsDeterministic`): three routes of one request, two by new instances, are
  equal.
- **Null request**
  (`OrthogonalRouter_Route_NullRequest_ThrowsArgumentNullException`): an argument-null exception.

Net separation (`NetSeparationTests.cs`, `Plexus-Routing-OrthogonalRouter-NetSeparation`, `-SameNetTrunk`):

- **Baseline**
  (`OrthogonalRouter_Route_WithoutExistingSegments_TwoNetsShareTrunk`): without existing segments two wires with the
  same middle channel do share a stretch, so the following scenarios measure something.
- **Other net**
  (`OrthogonalRouter_Route_ExistingOtherNet_DoesNotRunAlongIt`): with the first wire's segments in the request the
  second wire is orthogonal and shares no collinear stretch with it.
- **Same net**
  (`OrthogonalRouter_Route_ExistingSameNet_MayShareTrunk`, `OrthogonalRouter_Route_RunLiningUpWithSameNet_KeepsMiddleChannel`):
  wires of one net still share the trunk and may line up.
- **Lining up**
  (`OrthogonalRouter_Route_RunLiningUpWithOtherNet_MovesToItsOwnTrack`): a vertical run that would sit on the middle
  channel one port pitch (15) beyond the end of another net's run there moves to another track, while one 105 beyond it
  stays on the middle channel.
- **Crossing**
  (`OrthogonalRouter_Route_MustCrossOtherNet_CrossesWithoutOverlap`): a long foreign wire between start and end is
  crossed, not run along.
- **Determinism and switch**
  (`OrthogonalRouter_Route_WithExistingSegments_IsDeterministic`,
  `OrthogonalRouter_Route_ZeroOverlapPenalty_IgnoresOtherNets`): repeated routing is identical, and a zero penalty
  gives the old shared-trunk behavior.

Route cleaning (`RouteCleanerTests.cs`, `Plexus-Routing-OrthogonalRouter-RouteCleaning`): a collinear bend
(`RouteCleaner_Clean_CollinearBend_IsMerged`), a cascade of collinear bends
(`RouteCleaner_Clean_ChainOfCollinearBends_CollapsesToStraightLine`), duplicate and end-point bends
(`RouteCleaner_Clean_ZeroLengthSegments_AreRemoved`), a kept reversal (`RouteCleaner_Clean_Reversal_IsKept`),
idempotence (`RouteCleaner_Clean_AlreadyCleanRoute_IsUnchangedAndStable`) and a null route
(`RouteCleaner_Clean_NullRoute_ThrowsArgumentNullException`).

Route editing (`RouteEditorTests.cs`, `Plexus-Routing-OrthogonalRouter-RouteEditing`): interior segment move
(`RouteEditor_MoveSegment_InteriorSegment_MovesAndNeighborsStretch`), first and last stub jogs
(`RouteEditor_MoveSegment_FirstStub_InsertsJogNextToSourcePort`, `RouteEditor_MoveSegment_LastStub_InsertsJogNextToTargetPort`),
a straight wire (`RouteEditor_MoveSegment_StraightWire_InsertsJogAtBothEnds`), a stub moved onto its own line
(`RouteEditor_MoveSegment_StubToItsOwnLine_LeavesRouteUnchanged`), flattening a bump
(`RouteEditor_MoveSegment_OntoNeighborLine_MergesCollinearBends`), corner moves
(`RouteEditor_MoveBend_Corner_MovesBothAdjacentSegments`, `RouteEditor_MoveBend_NextToStub_InsertsJog`,
`RouteEditor_MoveBend_TowardPort_KeepsOneStubLength`), invalid input
(`RouteEditor_Edits_BadIndexOrDiagonalRoute_ReturnNull`) and jogs
(`RouteEditor_InsertJog_HorizontalSegment_AddsCenteredBump`,
`RouteEditor_InsertJog_NearSegmentEnd_IsClampedAwayFromTheEnd`, `RouteEditor_InsertJog_VerticalSegment_StepsSideways`).

Boundary values: zero bend points, minimum stubs, covered stub point, enclosed end. Error path: null request.
