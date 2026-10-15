## Model Subsystem Verification Design

Scope: Diagram document model: diagram root, pages, nodes, ports, wires, annotations and groups with undoable mutation
and change notification.

### Verification Approach

The Model subsystem is verified with integration tests that use the public API only and exercise the Diagram,
PageCollection, DiagramPage and the element units together, with the real undo history, router, connection rules and theme.
Nothing is mocked because every collaborator is in-memory and deterministic. Each scenario is a short editing session
on a diagram built from the shared test node types; it asserts on the resulting model, on error behavior, and, where
undo is part of the requirement, on the model after undo and redo. Unit-level behavior is verified by the unit tests
of each unit and is not reused as subsystem evidence.

### Test Environment

Standard xUnit v3 test runner on .NET (net8.0, net9.0 and net10.0). No external services, files or configuration.

### Acceptance Criteria

All scenarios below pass on every target framework and every test method named here is matched to its requirement by
the ReqStream trace matrix.

### Test Scenarios

#### Pages (`Plexus-Model-Pages`)

- **Model_Pages_AddInsertRenameMoveRemove_KeepsOrderAndUndoes**: pages are added, inserted, renamed, reordered and
  removed; the order is as expected and four undo steps and four redo steps restore the order.
- **Model_Pages_RemovedPage_RejectsEditsUntilUndo**: a removed page is detached, its content is not found, edits are
  rejected, and undo restores the same page ready for editing.

#### Elements (`Plexus-Model-Elements`)

- **Model_Elements_AddNodesAndAnnotation_UseUniqueNamesAndAreFindable**: generated names are unique across pages,
  explicit names and ids are kept and found from the diagram, annotations are found by id, duplicates and unknown
  types are rejected.

#### Rename (`Plexus-Model-Rename`)

- **Model_Rename_ValidationHostHookAndUndo_AppliesCorrectionsAndRejects**: a host handler corrects a trimmed name,
  another proposal is cancelled with a reason, empty and duplicate names are rejected, and undo restores the name and
  reports it.

#### Wires (`Plexus-Model-Wires`)

- **Model_Wires_ConnectByObjectsAndPath_RouteAndEdit**: wires are created by path and by reversed port objects, run
  from output to input, are routed, take a manual route, are re-routed by the router, and undo restores the manual
  route.

#### Connection rules (`Plexus-Model-ConnectionRules`)

- **Model_ConnectionRules_StructureAndHostRule_DenyWithReasons**: output-to-output, same-node, type-mismatch (host rule)
  and duplicate connections are denied with reasons before connecting, connecting a denied pair throws the reason, and
  an allowed pair connects.

#### Groups (`Plexus-Model-Groups`)

- **Model_Groups_NestCollapseMoveAndUngroup_MembersFollow**: nested groups are created, moving the outer group moves all
  nested members, collapse hides members and expand shows them, ungrouping hands members to the parent group, and undo
  restores the nesting.

#### Group frames as obstacles (`Plexus-Model-GroupObstacles`)

- **Model_Groups_UnrelatedGroupFrame_IsAnObstacleForStoredRoutes**: the stored route of a wire between two nodes outside
  a group that frames the free row between them never enters the frame with its clearance, while a wire to a node
  inside that group is routed orthogonally into the frame (the group of an end node is no obstacle).

#### Editing (`Plexus-Model-Editing`)

- **Model_Editing_MoveAndRemove_WiresFollowAndAreRemoved**: moving both ends translates a manual route; removing a node
  removes its wires; undo restores nodes and wires.
- **Model_Editing_MoveToPage_CarriesInternalWiresAndDropsOutsideWires**: moving nodes to another page carries the wire
  between them, removes the wire to the node left behind, and one undo restores everything.

#### Undo and redo (`Plexus-Model-UndoRedo`)

- **Model_UndoRedo_MixedEditingSession_RestoresEveryStateExactly**: ten different operations are applied, undo walks back
  through every recorded state exactly, and redo replays them exactly.

#### Wire styler (`Plexus-Model-WireStyler`)

- **Model_WireStyler_AssignAndRefresh_NotifyWithoutUndoEntries**: assigning a styler and refreshing the appearance each
  raise a settings change and leave the undo history unchanged.

#### Node styler (`Plexus-Model-NodeStyler`)

- **Model_NodeStyler_Assign_NotifiesWithoutUndoEntries**: assigning a node styler raises a settings change and leaves
  the undo history unchanged.

#### Change notification (`Plexus-Model-ChangeNotification`)

- **Model_ChangeNotification_EditsUndoAndRedo_RaiseChangedForEveryChange**: page, element, rename, settings, undo and redo
  changes raise the expected sequence of change kinds.
- **Model_ChangeNotification_ObservableCollections_ReportAddMoveRemove**: the page collection and a page's node list
  report add, move and remove.

#### Per-node ports

Requirements: `Plexus-Model-DynamicPorts`, `Plexus-Model-DynamicPortIdentity`, `Plexus-Model-DynamicPortWireRemoval` and
`Plexus-Model-DynamicPortUndo`.

- **Model_DynamicPorts_PropertyAndDataChanges_RecomputePortsInOneUndoEntry**: raising a bus's channel count and
  changing a router's payload are each one exact undo entry (twice over); the bus grew and kept its wire (same port),
  the router lost its second output and the wire on it.

#### Re-checked wires (`Plexus-Model-DynamicPortRuleRevalidation`)

- **Model_DynamicPorts_RetypedPort_RemovesOnlyRefusedWires**: with a same-type rule, retyping the bus inputs removes the
  f32 wire on its input and keeps the wire on its unchanged output; the edit is one exact undo entry.

#### Node size (`Plexus-Model-DynamicPortResize`)

- **Model_DynamicPorts_PortCountChange_FitsHeightAndReroutes**: ten channels make the bus taller with the same position
  and width; the wire equals a fresh route of its moved anchor; going back to two channels restores the original
  bounds (boundary: back to the default height).

#### Provider failure (`Plexus-Model-DynamicPortProviderFailure`)

- **Model_DynamicPorts_ProviderFails_RollsBackAndRecordsNothing**: a payload whose provider result has a duplicate
  output name makes the edit throw; the model dump (payload, ports, wire) and the history are unchanged (error path).

#### Ports notification (`Plexus-Model-DynamicPortNotification`)

- **Model_DynamicPorts_Notifications_ReportPortsChangeAfterModelSettles**: dropping a four-channel wired bus to one
  channel, its undo and its redo each report the bus's `Ports` change exactly once, and the model dump taken in the
  handler equals the final dump of that step.

#### Removed port held by a host (`Plexus-Model-DynamicPortDetached`)

- **Model_DynamicPorts_RemovedPortHeldByHost_CannotBeConnected**: a port kept by the host after the provider removed it
  is refused with a reason, and connecting to it throws without adding a wire (error path).

#### New node of a provider type (`Plexus-Model-DynamicPortAddNode`)

- **Model_DynamicPorts_AddNode_UsesProviderPortsAndFitsHeight**: a type with a declared 120 x 60 size whose default
  payload names five outputs gets those outputs, the declared width and a taller height that contains every anchor.

#### Change handlers that throw

Requirements: `Plexus-Model-HandlerFailureKeepsChange`, `Plexus-Model-HandlerFailureReported`,
`Plexus-Model-HandlerFailureUndoRedo` and `Plexus-Model-HandlerFailureTransaction`.

- **Model_HandlerFailure_EditsUndoRedoAndTransactions_KeepModelAndHistoryConsistent**: with a handler that throws on
  every notification, a property edit is applied and recorded, undo and redo restore the exact dumps and move the entry,
  a `Transact` with a rename and a move is rolled back as a whole, and once the handler is quiet one undo returns to the
  starting dump.

**Expected**: each test passes, proving the Model requirements.
