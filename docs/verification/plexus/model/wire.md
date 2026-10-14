### Wire Unit Verification Design

Scope: Directed connection from an output port to an input port with a persisted route.

#### Verification Approach

The unit is verified with unit tests that use the real Model classes through the public API (no mocks): wires are
created with the page connect operation between nodes of the shared test node types. Route editing and removal use the
shared round-trip helper, which compares a full-state dump of the diagram after each undo and redo. Tests live in
`WireTests.cs`, `WireEditingTests.cs`, `ConnectionTests.cs` and `ElementConvenienceTests.cs`.

#### Test Environment

Standard xUnit v3 runner on .NET with no external services, files or configuration.

#### Acceptance Criteria

Every scenario below passes on all target frameworks: observed routes, points and bounds equal the expected values,
one undoable history entry exists per change, none for a no-op, and undo and redo restore the exact prior state.

#### Test Scenarios

**Endpoints** (`Plexus-Model-Wire-Endpoints`)

- `Wire_SourceAndTarget_ReversedConnect_RunFromOutputToInput`: connecting input to output still yields output as source.

**Route** (`Plexus-Model-Wire-Route`)

- `Wire_Route_SuppliedList_IsCopied`: later changes to the supplied list do not alter the route.
- `DiagramPage_Connect_RouteSuppliedOrNull_UsesSuppliedRouteOrRouter`: a supplied route is stored as given.

**Points** (`Plexus-Model-Wire-Points`)

- `Wire_GetPoints_IncludesAnchorsAndBendPoints`: the polyline is anchor, bends, anchor.
- `Wire_GetPoints_EndNodeMoved_AnchorsFollowPorts`: end points follow a moved node.

**Bounds** (`Plexus-Model-Wire-Bounds`)

- `Wire_GetBounds_CoversFullPolyline`: the box covers every point.
- `Wire_GetBounds_StraightWire_HasZeroHeight`: a straight wire has zero height.
- `Wire_GetBounds_ThroughBaseType_EqualsPolylineBox`: the base class contract reports the same box.

**Set route** (`Plexus-Model-Wire-SetRoute`)

- `Wire_SetRouteAndReroute_UndoRedoRoundTrip`: editing is undoable, an identical route records nothing, null throws.
- `Wire_SetRoute_SuppliedList_IsCopied`: the stored route is a copy.
- `Wire_SetRouteAndReroute_RemovedWire_ThrowsInvalidOperationExceptionUntilUndo`: a removed wire rejects edits.

**Reroute** (`Plexus-Model-Wire-Reroute`)

- `Wire_Reroute_ManualRoute_RestoresRouterRouteAndUndoes`: the router route replaces a manual one; undo restores it.
- `Wire_SetRouteAndReroute_UndoRedoRoundTrip`: rerouting is one undoable entry.

**Remove** (`Plexus-Model-Wire-Remove`)

- `Wire_Remove_UndoRedoRoundTrip`: removal is undoable and restores the wire with its route and identifier.
- `Wire_Remove_AttachedWire_DetachesFromPageAndPorts`: the wire leaves the page and port lists; nodes stay.

**Change notification** (`Plexus-Model-Wire-ChangeNotification`)

- `Wire_SetRoute_RouteChanged_RaisesPropertyChangedForSetAndUndo`: a notification and a diagram event per change.

**Route normalization** (`Plexus-Model-Wire-RouteNormalization`)

- `Wire_SetRoute_CollinearAndZeroLengthBends_AreNormalized`: a collinear, a duplicated and an end-point bend vanish.
- `DiagramPage_Connect_RouteWithCollinearBend_StoresNormalizedRoute`: a route supplied when connecting is normalized too.
- `Wire_SetRoute_SameShapeAfterNormalizing_RecordsNothing`: a route that normalizes to the current one records no entry.

**Reconnect** (`Plexus-Model-Wire-Reconnect`)

- `Wire_Reconnect_ValidTarget_MovesEndKeepsIdAndReroutes`: new target, same object and id, route ends at the new port.
- `Wire_Reconnect_ValidSource_MovesSourceEnd`: the source end moves and the port wire lists follow.
- `Wire_Reconnect_UndoRedo_RestoresEndsAndRouteAsOneEntry`: one entry; undo and redo restore ports and route exactly.
- `Wire_Reconnect_SamePorts_RecordsNothing`: current ports (or two nulls) record nothing.
- `Wire_Reconnect_InvalidMoves_ThrowWithReasonAndChangeNothing`: self-connection, wrong direction, duplicate and a host
  rule each throw with their reason; the wire and the history are untouched.
- `DiagramPage_CanReconnect_ExcludesTheWireItselfFromDuplicateCheck`: the wire's own connection is allowed, a real duplicate
  is denied with its reason.
- `Wire_Reconnect_RemovedWire_ThrowsInvalidOperationException`: a removed wire rejects the call.

**Add jog** (`Plexus-Model-Wire-AddJog`)

- `Wire_AddJog_StraightSegment_AddsBumpAsOneUndoEntry`: a straight wire gets four bends as one entry; undo removes them.
- `Wire_AddJog_UnknownSegment_ReturnsFalse`: a bad segment index returns false and records nothing.
