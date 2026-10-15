### DiagramPage Unit Verification Design

Scope: One diagram page: containers for nodes, wires, annotations and groups, and the undoable operations that edit
them (add, connect, group, move, remove, layout).

#### Verification Approach

The DiagramPage unit is verified with unit tests that use a real diagram built from the shared test fixture (node types
with typed ports), real pages, undo history, router and connection rules; nothing is mocked because all collaborators
are in-memory and deterministic. Tests call the public page operations and assert on the content lists, element
positions, bounds and routes, the undo history depth and the notifications received. Operations that must be one
undoable step are checked with a round-trip helper that compares a full dump of the model (ids, list order, z-order,
bounds, routes, groups) before the change, after it, and after repeated undo and redo.

#### Test Environment

Standard xUnit v3 test runner on .NET (net8.0, net9.0 and net10.0). No external services, files or configuration.

#### Acceptance Criteria

All scenarios below pass on every target framework, and every test method named here is matched to its requirement by the
ReqStream trace matrix.

#### Test Scenarios

##### Name and removed page (`Plexus-Model-DiagramPage-Name`, `Plexus-Model-DiagramPage-RemovedGuard`)

- **DiagramPage_Name_Set_UndoRedoRoundTripAndValidation**: rename is one undo entry, unchanged name records nothing, blank
  rejected.
- **DiagramPage_Mutation_WhenRemoved_ThrowsInvalidOperationException**: a removed page rejects modifications.
- **DiagramPage_Remove_Element_MutationRejectedUntilUndo**: modification is rejected until the removal is undone.

##### Adding nodes and annotations (`Plexus-Model-DiagramPage-AddNode`, `-NodeIdentity`, `-AddAnnotation`)

- **DiagramPage_AddNode_ByTypeKey_CreatesNodeAtPosition**, **DiagramPage_AddNode_UnknownTypeKey_ThrowsArgumentException**,
  **DiagramPage_AddNode_UnregisteredNodeType_ThrowsArgumentException**, **DiagramPage_AddNode_NonFinitePosition_ThrowsArgumentException**:
  creation by key at a position and rejection of bad types and positions.
- **DiagramPage_AddNode_FewPorts_UsesThemeDefaultSize**, **DiagramPage_AddNode_ManyPorts_GrowsToFitPorts**,
  **DiagramPage_AddNode_TypeWithDefaultSize_UsesDeclaredSize**: node sizing.
- **DiagramPage_AddNode_DeclaredSizeWithoutProvider_KeepsDeclaredHeight**: a declared size shorter than the ports need
  is kept when the type has no port provider (boundary).
- **DiagramPage_AddNode_ExplicitNameAndId_UsesThem**, **DiagramPage_AddNode_DuplicateId_ThrowsArgumentException**,
  **DiagramPage_AddNode_DefaultId_IsUniqueGuidWithoutDashes**, **DiagramPage_AddNode_NoName_GeneratesUniqueNamesFromKeyLastSegment**,
  **DiagramPage_AddNode_NoName_ReusesFreedNumber**, **DiagramPage_AddNode_DuplicateOrEmptyName_ThrowsArgumentException**,
  **DiagramPage_AddNode_DuplicateNameWithUniquenessOff_Succeeds**: identity rules.
- **DiagramPage_AddAnnotation_CreatesAnnotationWithTextAndBounds**,
  **DiagramPage_AddAnnotation_InvalidArguments_Throw**: annotation creation and rejection of null text and invalid
  bounds.

##### Connections (`Plexus-Model-DiagramPage-CanConnect`, `-Connect`, `-ConnectByPath`, `-RerouteAll`)

- **DiagramPage_CanConnect_StructuralViolations_AreDeniedWithReasons**, **DiagramPage_CanConnect_PortsOnDifferentPages_IsDenied**,
  **DiagramPage_CanConnect_HostDenialWithoutReason_GetsDefaultReason**,
  **ConnectionRules_Default_AllowsAndHostRuleReceivesOutputThenInput**: structural denials, host rule and reasons.
- **DiagramPage_Connect_DefaultRules_RefuseFanInAndAllowFanOut**, **DiagramPage_Connect_InputAllowsMultipleWires_AcceptsSeveralWires**,
  **DiagramPage_Connect_HostRuleWithoutDefault_AllowsFanInEverywhere**: by default a second wire into an input is
  refused with the reason (`CanConnect` and the `Connect` exception agree) while fan-out is allowed, an opted-in input
  takes several wires, and a host rule without the default allows fan-in everywhere.
- **DiagramPage_CanReconnect_OntoOccupiedInput_FollowsFanInRule** (`Plexus-Model-Wire-Reconnect`): moving a wire onto an
  occupied input is refused unless the input allows fan-in, while moving only the source or keeping the target is allowed.
- **DiagramPage_Connect_OutputToInput_CreatesWire**, **DiagramPage_Connect_ReversedPorts_AutoOrdersOutputToInput**,
  **DiagramPage_Connect_Denied_ThrowsInvalidOperationExceptionWithReason**,
  **DiagramPage_Connect_RouteSuppliedOrNull_UsesSuppliedRouteOrRouter**: connecting by objects.
- **DiagramPage_ConnectByPath_SharedPortName_PrefersOutputThenInput**, **DiagramPage_ConnectByPath_IsOneUndoEntryAndAcceptsIdAndRoute**,
  **DiagramPage_ConnectByPath_UnknownPathOrDeniedConnection_Throws**: connecting by path.
- **DiagramPage_RerouteAll_RecomputesAllWiresAsOneUndoEntry**, **DiagramPage_RerouteAll_NothingChanges_RecordsNoHistory**,
  **DiagramPage_RerouteAll_UsesCurrentRouter_DiscardingManualRoutes**: re-routing.
- **Groups as obstacles** (`Plexus-Model-DiagramPage-GroupObstacles`, `GroupObstacleTests`): **DiagramPage_Connect_GroupBetweenPorts_RoutesAroundGroupFrame**
  (a frame spanning the free row is avoided), **DiagramPage_Connect_EndNodeInsideGroup_ThatGroupIsNotAnObstacle**,
  **DiagramPage_Connect_TargetInsideGroup_MayCrossItsFrame**, **DiagramPage_Connect_NestedGroups_ExemptionFollowsMembershipPerGroup**
  and **DiagramPage_Connect_EndNodeNestedTwoLevelsDeep_AllAncestorsAreExempt** (the exemption is per group, at any
  depth, checked on the obstacle list a recording router receives),
  **DiagramPage_Connect_CollapsedGroupBetweenPorts_RoutesAroundCollapsedBox**,
  **DiagramPage_Connect_GroupInsideCollapsedGroup_IsNotAnObstacle**,
  **DiagramPage_RerouteAll_AfterGroupMoved_RoutesAroundGroup** (moving a group leaves wires alone, `RerouteAll` avoids
  it and is undoable), **DiagramPage_Move_EndNode_ReroutesAroundGroupFrame**,
  **DiagramPage_RerouteAll_WithGroups_IsDeterministic** and **DiagramPage_Connect_NoGroups_ObstaclesAreOnlyNodeBoxes**.

##### Groups (`Plexus-Model-DiagramPage-CreateGroup`, `-Ungroup`, `-MoveGroup`)

- **DiagramPage_CreateGroup_LinksMembersAndRoundTrips**, **DiagramPage_CreateGroup_InvalidInput_Throws**,
  **DiagramPage_CreateGroup_WiresInMemberList_AreIgnored**, **DiagramPage_CreateGroup_MembersOfSameGroup_NestsInsideThatGroup**.
- **DiagramPage_Ungroup_RemovesFrameKeepsMembersAndRoundTrips**, **DiagramPage_Ungroup_NestedGroup_MembersTakeFramePlaceInParent**,
  **DiagramPage_Ungroup_InvalidArguments_Throw**.
- **DiagramPage_Move_Group_MovesAllMembersAndRoundTrips**, **DiagramPage_Move_Group_TranslatesInternalWiresAndReroutesCrossingWires**,
  **DiagramPage_Move_CollapsedGroup_MovesBoxAndMembers**.

##### Move (`Plexus-Model-DiagramPage-Move`, `-MoveWires`)

- **DiagramPage_Move_NodesAndAnnotations_ShiftsBoundsAndRoundTrips**, **DiagramPage_Move_ZeroDeltaOrEmptySet_RecordsNothing**,
  **DiagramPage_Move_InvalidArguments_Throw**, **DiagramPage_Move_WireInSet_IsIgnored**,
  **DiagramPage_Move_ManyMovesInTransaction_CreateOneEntry**, **DiagramPage_Move_SeparateCalls_CreateSeparateEntries**,
  **DiagramPage_Move_MergedWithRouteEditInTransaction_RestoresOriginalOnUndo**, **DiagramPage_Move_RaisesBoundsChangedNotifications**.
- **DiagramPage_Move_BothEndpointsMoved_TranslatesRoute**, **DiagramPage_Move_OneEndpointMoved_ReroutesWire**,
  **DiagramPage_Move_UnrelatedNode_LeavesWireUntouched**, **Node_MoveToAndBounds_MoveNodeAndRerouteWires**: wire consistency.

##### Remove (`Plexus-Model-DiagramPage-Remove`)

- **DiagramPage_Remove_Node_RemovesAttachedWiresAndRoundTrips**, **DiagramPage_Remove_Node_UndoRestoresSameObjectsAtSamePositions**,
  **DiagramElement_Remove_RemovesFromPage**, **DiagramPage_Remove_Multiple_IsSingleEntryAndRoundTrips**,
  **DiagramPage_Remove_EmptyOrInvalid_NoEntryOrThrows**: removal of elements and their wires.
- **DiagramPage_Remove_Group_RemovesFrameOnlyAndRoundTrips**, **DiagramPage_RemoveWithMembers_Group_RemovesMembersWiresAndRoundTrips**,
  **DiagramPage_RemoveWithMembers_NestedGroups_RemovesEverythingAndRoundTrips**,
  **DiagramPage_Remove_GroupMember_UpdatesGroupAndRestoresSlotOnUndo**, **DiagramPage_Remove_NestedGroupFrame_MembersFlowToParent**,
  **DiagramPage_Remove_GroupAndOneMemberTogether_RoundTrips**: group removal variants.

##### Undo and notification (`Plexus-Model-DiagramPage-Undo`, `Plexus-Model-DiagramPage-Notification`)

- **DiagramPage_Creation_AddNodeAnnotationAndWire_UndoRedoRoundTrip**,
  **DiagramPage_RemoveThenUndo_RestoresExactState**: exact restoration of ids, order and z-order.
- **DiagramPage_ContentLists_AreObservable**: the content lists raise collection notifications.
- **DiagramPage_Remove_ElementIsDetachedBeforeNotifications**, **DiagramPage_UndoRemove_ElementIsAttachedBeforeNotifications**:
  handlers see a settled model.

##### Reconnect context (`Plexus-Model-DiagramPage-ReconnectContext`, `ReconnectRulesTests.cs`)

- **DiagramPage_CanReconnect_NoChange_IsAllowedWithoutAskingTheRule**: passing no ports or the wire's own ports is
  allowed and the rule is not asked; a real move is still judged.
- **DiagramPage_WireBeingReconnected_IsTheMovingWireOnlyDuringTheCheck**: the property is null for a plain check, the moving
  wire inside a reconnect check and null again afterwards.
- **ConnectionRules_HostRule_AlwaysReceivesOutputThenInput**: connect with reversed ports, `CanConnect` and
  `CanReconnect` all call the rule with the output first.

##### Per-node ports (`Plexus-Model-DiagramPage-DetachedPorts`)

- **DiagramPage_CanConnect_PortRemovedByProvider_IsDenied**: a port a provider removed is refused with a reason by
  `CanConnect`, and `Connect` throws (error path).

##### New node ports (`Plexus-Model-DiagramPage-AddNodePorts`)

- **DiagramPage_AddNode_ProviderNeedsMoreHeight_GrowsNewNode**: the new node has the outputs `x` and `y` that the
  provider returns for the default payload.

##### New node height (`Plexus-Model-DiagramPage-AddNodeHeight`)

- **DiagramPage_AddNode_ProviderNeedsMoreHeight_GrowsNewNode**: a type that declares a 60-unit height but whose default
  payload yields two outputs starts at the height two rows need, with its declared width (boundary: declared size).

##### Provider failure when adding (`Plexus-Model-DiagramPage-AddNodeProviderFailure`)

- **DiagramPage_AddNode_ProviderBuildsInvalidPortSet_ThrowsAndAddsNothing**: a provider that builds a port set with two
  inputs of the same name makes AddNode throw, leaving no node and no history entry (error path).
- **DiagramPage_AddNode_ProviderReturnsNull_ThrowsAndAddsNothing**: a provider that returns no set makes AddNode throw
  an invalid-operation error naming the type, leaving no node and no history entry (error path).

**Expected**: each test passes, proving the DiagramPage requirements.
