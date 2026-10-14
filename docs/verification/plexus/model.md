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

**Expected**: each test passes, proving the Model requirements.
