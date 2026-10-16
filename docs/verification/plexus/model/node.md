### Node Unit Verification Design

Scope: Diagram element base class, nodes and their ports.

#### Verification Approach

The unit is verified with unit tests that use the real Model classes through the public API (no mocks): a diagram is
built from the shared test node types and nodes are created with the page API, because nodes cannot be constructed
directly. Port tests cover declared order, lookup and shared names; the element tests exercise visibility, removal and
notification through nodes, annotations and groups. Undo and redo are checked with the shared round-trip helper, which
compares a full-state dump of the diagram after each step (the dump includes every port's direction, row, name, type,
label and description). Per-node ports use the shared `acme.Bus` (a `Channels` property decides its inputs, `Kind` their
type) and `acme.Router` (its payload names its outputs) test types, whose port providers are real. Tests live in
`NodeTests.cs`, `NodePortsTests.cs`, `PortPathTests.cs`, `SharedPortNameTests.cs`, `ElementConvenienceTests.cs` and
the files named in the scenarios.

#### Test Environment

Standard xUnit v3 runner on .NET with no external services, files or configuration.

#### Acceptance Criteria

Every scenario below passes on all target frameworks: the observable values equal the expected ones, one undoable
history entry exists per change, no entry exists for a no-op, and undo and redo restore the exact prior state.

#### Test Scenarios

**Ports** (`Plexus-Model-Node-Ports`)

- `Node_Ports_InputsThenOutputs_ReportOwnerDirectionAndIndex`: a four-input node lists inputs then the output.
- `Node_Port_ByName_ReturnsCachedPortWithDefinitionData`: the same port object is returned and carries its data.
- `PortDefinition_Label_CustomLabelIsExposedOnPort`: a custom label is exposed.
- `Port_Anchor_NodeMoved_AnchorFollowsNode`: the anchor moves with the node.

**Port lookup** (`Plexus-Model-Node-PortLookup`)

- `Node_TryGetPort_UniqueName_FindsPort`: unique names resolve with and without a direction.
- `Node_TryGetPort_MissingOrWrongDirection_ReturnsFalse`: unknown names and wrong directions fail; null throws.
- `Node_InputAndOutput_WrongDirectionOrMissing_Throw`: the throwing lookups reject the same cases.

**Shared port names** (`Plexus-Model-Node-SharedPortNames`)

- `Node_InputAndOutput_SameName_ReturnPortsOfTheRequestedDirection`: the direction picks the port.
- `Node_Port_AmbiguousName_ThrowsInvalidOperationWithHint`: the ambiguity message names the explicit lookups.
- `Node_TryGetPort_AmbiguousName_NeedsDirection`: the non-throwing lookup fails without a direction.

**Connected wires** (`Plexus-Model-Node-ConnectedWires`)

- `Port_ConnectTo_OutputToInput_WireIsListedOnPortsAndNodes`: wires appear on both ports and nodes in page order.
- `Node_Wires_RemovedNode_IsEmptyUntilUndo`: a removed node lists no wires; undo restores the lists.

**Bounds** (`Plexus-Model-Node-Bounds`)

- `Node_MoveToAndBounds_MoveNodeAndRerouteWires`: move and resize are undoable and re-route wires.
- `Node_Bounds_SameOrInvalid_NoEntryOrThrows`: an unchanged box records nothing; an invalid box is rejected.
- `DiagramElement_GetBounds_NodeAnnotationGroup_ReturnTheirBounds`: the element contract returns the box.

**Data** (`Plexus-Model-Node-Data`)

- `Node_Data_Set_UndoRedoRoundTrip`: payload replacement is undoable.
- `Node_Data_SetEqualValue_RecordsNothing`: an equal payload records nothing.

**Visibility** (`Plexus-Model-Node-Visibility`)

- `DiagramElement_IsVisible_FalseWhenAnyAncestorGroupIsCollapsed`: hidden at any depth, visible after expanding.

**Removal** (`Plexus-Model-Node-Removal`)

- `DiagramElement_Remove_RemovesFromPage`: the shortcut removes undoably; a second removal throws.
- `DiagramElement_Id_RemoveAndUndo_StaysTheSame`: the identifier survives removal and undo.
- `Node_BoundsAndData_Set_OnRemovedNode_ThrowsInvalidOperationException`: detached nodes reject changes.
- `DiagramPage_Remove_Element_MutationRejectedUntilUndo`: a removed annotation rejects changes until undo.
- `Node_Name_Set_OnRemovedNode_ThrowsInvalidOperationException`: a removed node rejects renaming.

**Change notification** (`Plexus-Model-Node-ChangeNotification`)

- `Node_PropertyChanged_NameBoundsAndData_RaisedForSetUndoAndRedo`: property notifications for set, undo and redo.
- `Node_Changed_BoundsSet_DiagramReportsElementChanged`: the diagram receives an element-changed event.

**Data copy** (`Plexus-Model-Node-CloneData`, `PropertyEditingTests.cs`)

- `Node_CloneData_ReturnsIndependentCopy`: typed and custom payloads are equal but not the same object; no data gives null.
- `Node_CloneData_TypeWithoutSerializer_Throws`: the message names the node type.
- `Node_CloneData_CommittedWithPropertiesInOneTransaction_IsOneUndoEntry`: the documented pattern is one undo entry that
  restores both the data and the property.

**Data type check** (`Plexus-Model-Node-DataTypeCheck`, `NodeDataTests.cs`)

- `Node_Data_IncompatibleType_ThrowsArgumentException`: a payload of another type throws at the assignment, the message
  names both types and neither the payload nor the history changes (error path).
- `Node_Data_DeclaredDerivedOrNull_IsAccepted`: the declared type, a derived type and null pass.
- `Node_Data_SerializerOnlyOrUndeclared_AcceptsAnyObject`: a type with only a serializer, or with nothing, accepts any
  object.

**Per-node ports** (`Plexus-Model-Node-DynamicPorts`, `NodePortsTests.cs`)

- `Node_Ports_PortProvider_NewNodeUsesProviderPorts`: a new node has the provider's ports (with their descriptions).

**Recomputation** (`Plexus-Model-Node-DynamicPortRecompute`, `NodePortsTests.cs`)

- `Node_Ports_DataChange_RecomputesPorts`: a payload change recomputes the outputs, as one exact undo entry.
- `Node_Ports_CountIncreased_AddsPortsKeepingExistingInstancesAndWires`: a property change recomputes the inputs.

**Port identity** (`Plexus-Model-Node-DynamicPortIdentity`, `NodePortsTests.cs`)

- `Node_Ports_CountIncreased_AddsPortsKeepingExistingInstancesAndWires`: the old ports are the same objects and keep
  their wires.
- `Node_Ports_RenamedByName_ReattachesWiresByName`: a port that moves to another row keeps its object and wire, takes
  its new row, and the wire is re-routed to the new anchor.
- `Node_Ports_SurvivingPortRetyped_KeepsWiresStillAllowed`: a kept port takes its new type and keeps its wire.

**New names** (`Plexus-Model-Node-DynamicPortNewNames`, `NodePortsTests.cs`)

- `Node_Ports_CountIncreased_AddsPortsKeepingExistingInstancesAndWires`: the new names `in2` and `in3` are new port
  objects without wires.

**Static ports** (`Plexus-Model-Node-StaticPorts`, `NodePortsTests.cs`)

- `Node_Ports_NoPortProvider_KeepsTypePorts`: property and data changes of a type without a provider keep the same port
  object and definition and record one entry each.

**Empty port set** (`Plexus-Model-Node-EmptyPortSet`, `NodePortsTests.cs`)

- `Node_Ports_ProviderReturnsEmptySet_RemovesEveryPortAndWire`: a provider that returns no inputs and no outputs
  (boundary: empty set) leaves the node without ports, removes both of its wires, and is one exact undo entry.

**Wires of removed ports** (`Plexus-Model-Node-DynamicPortWires`, `NodePortsTests.cs`)

- `Node_Ports_CountDecreased_RemovesPortsAndTheirWires`: removed ports take their wires; the removed port reports that
  it is no longer on its node.

**Re-checked wires** (`Plexus-Model-Node-DynamicPortRevalidation`, `NodePortsTests.cs`)

- `Node_Ports_SurvivingPortRetyped_RemovesWiresTheRulesRefuse`: with a same-type rule, a retyped port loses its wire;
  undo and redo restore the wire and the port type exactly.
- `Node_Ports_SurvivingPortRetyped_KeepsWiresStillAllowed`: with the default rules, the wire stays.
- `Node_Ports_SurvivingPortFanInRemoved_RemovesWiresTheRulesRefuse`: turning fan-in off keeps a single wire on the
  input (the same port object) and is one exact undo entry.

**Re-check order** (`Plexus-Model-Node-DynamicPortRevalidationOrder`, `NodePortsTests.cs`)

- `Node_Ports_FanInRemoved_KeepsWireLatestInPageOrder`: with three wires on a fan-in input, turning fan-in off keeps
  exactly the third (latest in page order) wire; the diagram reports the removal of the first, then the second wire,
  then the node's `Ports` change.

**Node size** (`Plexus-Model-Node-DynamicPortSize`, `NodePortsTests.cs`)

- `Node_Ports_MorePortsThanHeight_GrowsNodeAndReroutesWires`: eight inputs grow the node to exactly the height they need
  (position and width unchanged).
- `Node_Ports_FewerPorts_ShrinksToFitNotBelowDefaultHeight`: going from eight to three inputs shrinks the node to the
  height three need, and on to one input shrinks it to the type's default height and no further (boundary: the default
  height is the floor); position and width are kept and the attached wire equals a fresh route.
- `Node_Ports_DeclaredSize_ShrinksNotBelowDeclaredHeight`: a node of a type with a declared size grows for five outputs
  and shrinks back for one, never below the declared height, keeping the declared width and its position.
- `Node_Ports_ProviderReturnsEmptySet_RemovesEveryPortAndWire`: with zero port rows a node with a taller hand-set size
  takes the type's default height (boundary: no rows).

**Re-routing** (`Plexus-Model-Node-DynamicPortReroute`, `NodePortsTests.cs`)

- `Node_Ports_MorePortsThanHeight_GrowsNodeAndReroutesWires`: the wire on the moved anchor equals a fresh route.
- `Node_Ports_RenamedByName_ReattachesWiresByName`: the wire of a port that moved to another row equals a fresh route.

**Group frames** (`Plexus-Model-Node-DynamicPortGroupFrame`, `NodePortsTests.cs`)

- `Node_Ports_NodeInGroup_GroupFrameFollowsNewPorts`: the enclosing group reports a bounds change and still contains the
  node.

**Unchanged port count** (`Plexus-Model-Node-DynamicPortKeepsSize`, `NodePortsTests.cs`)

- `Node_Ports_SameCount_KeepsHandSetSize`: retyping the ports (same number of inputs and outputs) keeps a size the host
  set, both taller and shorter than the fitted height.

**One undo entry** (`Plexus-Model-Node-DynamicPortUndo`, `NodePortsTests.cs`)

- `Node_Ports_PropertyChange_IsOneUndoEntryAndRoundTripsExactly`: removing wired ports and growing the node are each one
  entry that undo and redo restore exactly, twice over, with the same port objects.
- `Node_Ports_Shrink_UndoRedoRestoreExactBounds`: shrinking a wired eight-input node to two inputs is one entry; undo
  restores the exact previous bounds and redo the exact shrunk bounds.

**Unchanged ports** (`Plexus-Model-Node-UnchangedPorts`, `NodePortsTests.cs`)

- `Node_Ports_UnchangedPorts_RaisesNoPortsChangeAndOneEntry`: an edit that leaves the ports alone keeps the definitions,
  raises no `Ports` notification and records only the plain property entry.

**Provider failure** (`Plexus-Model-Node-ProviderFailure`, `NodePortsTests.cs`)

- `Node_Ports_ProviderDuplicateNames_ThrowsAndRevertsAssignment`: a provider producing duplicate names throws; the model
  and history are unchanged (error path).
- `Node_Ports_ProviderThrows_RevertsAssignmentAndPropagates`: the provider's own exception (same instance, an
  unusual exception type) reaches the caller; the model dump and the history are unchanged (error path).
- `Node_Ports_RuleThrowsDuringRevalidation_RollsBackEverything`: a connection rule that throws while a wire is re-checked
  makes the edit throw; the model dump and the history are unchanged and no `Ports` change is reported (error path).

**Null provider result** (`Plexus-Model-Node-ProviderNull`, `NodePortsTests.cs`)

- `Node_Ports_ProviderReturnsNull_ThrowsInvalidOperation`: a provider returning nothing fails the edit with the type
  named and the value reverted (error path).

**Notification when applied** (`Plexus-Model-Node-DynamicPortNotification`, `NodePortsTests.cs`)

- `Node_Ports_Change_ReportsWireRemovalsBeforePortsChanged`: the diagram reports the property change, then the wire
  removal, then the node's `Ports` change.
- `Node_Ports_RetypedPortRefusedWire_RemovedBeforePortsChanged`: when the `Ports` change is reported, a wire the rules
  refused after a retype is already off the page.
- `Node_Ports_Resize_BoundsAndRoutesSettledWhenPortsChanged`: growing to eight channels reports `Ports` exactly once,
  and at that moment the node's bounds and the wire's route already equal their final values.

**Notification on undo and redo** (`Plexus-Model-Node-DynamicPortUndoNotification`, `NodePortsTests.cs`)

- `Node_Ports_Undo_ReportsPortsChangeOnceAfterRestore`: undoing and redoing a shrink that removed two wires each report
  `Ports` exactly once, and the full model dump taken in the handler equals the dump after the undo (respectively redo)
  completed: wires, bounds, routes and the property value are settled.
