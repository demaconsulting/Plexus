# Plexus System Verification Design

This document describes how the system requirements of the Plexus core library (`DemaConsulting.Plexus`) are verified.
The subsystem and unit verification designs describe the lower levels.

## Verification Approach

The system requirements are verified by system tests that exercise the library only through its public API, the way a
host application does, and cross several subsystems in one scenario. They live in
`test/DemaConsulting.Plexus.Tests/PlexusTests.cs` and use xUnit v3. Each system requirement is linked to one or more
scenarios below by test method name; ReqStream matches the names in the TRX results of the test run.

- **Level separation.** System tests may use any subsystem or unit. They assert user-visible outcomes (names, wires,
  routes, scene primitives, hit results, JSON text, paste results, history state) rather than internal state. The
  subsystem tests (one `{Subsystem}Tests.cs` file per subsystem) and the unit tests prove the lower-level requirements.
- **Exact-state comparison.** Where a scenario must prove that undo or redo restores *everything*, it compares a full
  textual dump of the model (`TestModel.Dump`: ids, list and z-order, bounds, routes, properties, data, group
  structure) taken before and after, instead of spot checks.
- **No mocks.** The library has no external services; the real model, history, router, scene builder and serializers
  are used. Host extension points (router, text measurer, node visual, rename hook, connection rule) are exercised with
  small test implementations declared in the test class.
- **Platform requirements.** `Plexus-Platform-*` requirements are verified by running the same hello-diagram test on
  each platform and runtime; the requirement links use source filters (`windows@`, `ubuntu@`, `macos@`, `net8.0@`,
  `net9.0@`, `net10.0@`) so that only results from that platform or runtime count as evidence.

## Test Environment

- The standard xUnit v3 runner (`dotnet test`) on .NET 8, .NET 9 and .NET 10; the test project multi-targets these
  runtimes, and each scenario runs once per runtime.
- CI runs the suite on Windows, Linux (Ubuntu) and macOS, producing one TRX file per platform and runtime.
- No external services, network access, files, fonts or UI framework are required. Every scenario builds its own
  diagram in memory from the shared test registry (`TestModel.CreateTypes`: port types `f32` and `i32` and node types
  `acme.Producer`, `acme.Consumer`, `acme.IntConsumer`, `acme.Gain`, `acme.Mixer`) or from a registry declared in the
  test.
- Tests are independent: no shared mutable state, no ordering assumptions, safe to run in parallel.

## Acceptance Criteria

The Plexus system passes verification when:

- every scenario below passes on all runtimes of the run, with no skipped test;
- every system requirement in `docs/reqstream/plexus.yaml` and `docs/reqstream/plexus/platform-requirements.yaml` is
  linked to at least one passing test, as shown by the ReqStream trace matrix (platform requirements need passing
  results from the matching platform or runtime);
- the subsystem and unit requirements decomposed from the system requirements are likewise covered and passing.

## Test Scenarios

Each heading names a requirement; the bullets name the test methods and what they prove.

### Plexus-Core-NodeTypes

- **Plexus_NodeTypes_DeclareAndRegister_RejectsInvalidDeclarationsAndFreezesTypes**: a registry with a port type and a
  fully declared node type (title, icon, ports, property) returns the type by key; duplicate keys, duplicate port
  names and duplicate property names are rejected and leave the registry unchanged; a registered type cannot be
  modified.
- **Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram**: two declared node types are enough to build and
  connect a diagram.

### Plexus-Core-Properties

- **Plexus_Properties_TypedValues_DefaultsValidationAndReset**: a new node holds the declared defaults of each kind;
  assigned values are stored; a value of the wrong kind or a choice outside the options is rejected; reset restores the
  default and leaves other properties untouched.

### Plexus-Core-Pages

- **Plexus_Pages_AddInsertRenameMoveRemove_KeepsOrderAndUndoesRemoval**: add, insert, rename and move produce the
  expected page order; removing a page works and its undo restores the page at the same place.
- **Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram**: a page can be added to a new diagram.

### Plexus-Core-Elements

- **Plexus_Elements_AddNodesAndAnnotations_UniqueNamesAndIdsAreFindable**: generated names are numbered per type
  (`Gain1`, `Gain2`), explicit names are kept, ids are distinct, annotations keep text and bounds, elements are found
  from the diagram by name and id, and a duplicate name or unknown type key is rejected.
- **Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram**: nodes receive automatic names without
  configuration.

### Plexus-Core-Rename

- **Plexus_Rename_HostHookCorrectsOrCancels_AppliedRenameIsUndoable**: a host hook corrects a proposed name, a hook
  cancellation and a duplicate name are refused with a reason and leave the name unchanged, and undo of the applied
  rename restores the old name.

### Plexus-Core-Wires

- **Plexus_Wires_ConnectByObjectsAndPaths_AreOrderedRoutedAndEditable**: a wire connected from port objects and a wire
  connected from paths with reversed ends both run from the output to the input and are routed; a manually set route
  is stored, and rerouting replaces it with the router's route.
- **Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram**: the shortest connect scenario produces a routed
  wire with the right ends.

### Plexus-Core-ConnectionRules

- **Plexus_ConnectionRules_StructuralAndHostRules_DenyWithReasonBeforeAndOnConnect**: with the type-compatibility and
  single-input rules installed, a type mismatch, a second wire to an input and an output-to-output connection are
  denied with reasons, an allowed pair is reported as allowed, asking changes nothing, and connecting a denied pair
  throws with the same reason.

### Plexus-Core-Groups

- **Plexus_Groups_NestCollapseMoveAndUngroup_MembersFollowTheGroup**: a group nested in a group lists all members;
  collapsing hides its members; moving the outer group moves every member by the same offset; ungrouping removes the
  frame and keeps the members.

### Plexus-Core-Editing

- **Plexus_Editing_MoveRemoveAndMoveToPage_KeepsWiresConsistent**: wires stay attached to a moved node; moving a node to
  another page removes the wire to the node left behind; removing a node removes its remaining wires.

### Plexus-Core-UndoRedo

- **Plexus_EditingSession_UndoAllThenRedoAll_RestoresExactStates**: a long session covering every kind of mutation is
  undone to the empty diagram and redone to the exact final state.
- **Plexus_Transaction_WholeGesture_UndoesAsOneStep**: a transaction around several edits is one history entry with the
  transaction's description.
- **Plexus_LoadingWithSuspendedHistory_LeavesCleanEmptyHistory**: content loaded with history suspended leaves an empty,
  clean history.

### Plexus-Core-ChangeNotification

- **Plexus_ChangeNotification_EditUndoRedo_RaisesChangedForEveryChange**: adding a page and a node, undo, redo and a
  property change raise the expected sequence of change kinds, and the page list raises its collection event.

### Plexus-Core-Geometry

- **Plexus_Geometry_ColorsRectsAndPoints_DescribeAndPositionElements**: both hexadecimal color forms parse and format
  back, a rectangle built from reversed corners is normalized and used as annotation bounds, containment and center
  behave, and invalid color text is rejected.

### Plexus-Core-Routing

- **Plexus_Routing_DefaultAvoidsNodesAndCustomRouterIsUsed**: the default router produces only horizontal and vertical
  segments that do not cross a node standing between the ports; a host router assigned to the diagram supplies the
  route of the next wire.
- **Plexus_WireNetsAndEditing_SeparateNetsJunctionsAndHandles_WorkThroughThePublicApi**: two producers feed a mixer
  and a consumer; wires of different nets share no segment, the branching net shows a junction dot, a selected wire
  reports a bend handle, and a jog and a reconnect (the wire keeps its identity and moves only its target) are undone
  one entry at a time. It covers `Plexus-Core-NetSeparation` and `Plexus-Core-WireEditing`.

### Plexus-Core-Scene

- **Plexus_Scene_BuildPage_ContainsGridNodesAndWires**: a scene of a small page contains grid, node text and wire path
  primitives attributed to their elements, honors the grid switch, and has bounds covering every node.

### Plexus-Core-SceneCustomization

- **Plexus_SceneCustomization_ThemeMeasurerAndVisual_AreHonored**: a derived theme changes node colors, a host text
  measurer changes the node layout, and a node type with a custom visual draws its own primitive.

### Plexus-Core-WireStyling

- **Plexus_WireStyling_StylerFollowsConnectionsAndRefreshes**: a combined styler colors a wire by its port type, dashes
  it when a property of the connected node changes, thickens it after the host refreshes the appearance, and a
  throwing styler leaves the theme look.

### Plexus-Core-NodeStyling

- **Plexus_NodeStyling_StylerFollowsPropertiesAndRefreshes**: a combined styler gives a node a fill and halo from one
  of its properties and a border from its connections, follows a property edit and its undo, fades after the host
  refreshes the appearance, and a throwing styler leaves the theme look.
- **Scene_NodeStyling_StyleFollowsNodesAndFallsBackToTheme** and **Model_NodeStyler_Assign_NotifiesWithoutUndoEntries**
  verify the scene and model children.

### Plexus-Core-HitTest

- **Plexus_HitTest_NodePortWireAndCanvas_AreIdentified**: points on a node body, on a port anchor, on a wire and on empty
  canvas are identified as node, port, wire and canvas with the right model objects.

### Plexus-Core-Snapshot

- **Plexus_Snapshot_ToJsonFromJson_RestoresContentWithCleanHistory**: a diagram with nodes, a property, a wire, an
  annotation and a group is restored from its JSON with identical content, an empty history and a clean state; an
  unsupported document is rejected.

### Plexus-Core-Duplicate

- **Plexus_Duplicate_NodesAndWire_CloneAtOffsetAsOneUndoStep**: duplicating two connected nodes creates two nodes and a
  wire at the offset with new ids and names, and one undo restores the previous state.

### Plexus-Core-Clipboard

- **Plexus_Clipboard_CopyPasteAcrossDiagrams_RecreatesElementsAtomically**: copying two nodes of a chain and pasting
  into another diagram at a position recreates the nodes with their properties and only the internal wire; pasting
  into a diagram that lacks the node types fails, lists every missing type and changes nothing.

### Plexus-Core-ClipboardSafety

- **Plexus_ClipboardSafety_HostileAndMalformedText_IsRejectedWithoutChange**: non-JSON text, foreign formats, an oversize
  payload and excessive nesting are each rejected with an error and no elements; the page and history are unchanged and
  a valid payload still pastes afterwards.

### Plexus-Core-DynamicPorts

- **Plexus_DynamicPorts_PropertyChange_UpdatesPortsWiresAndUndoesInOneStep**: a host type whose `Channels` property
  decides its inputs is fed on two channels; lowering the count removes the last input and its wire, keeps the first
  input object and its wire and the node height, in one undo entry; undo restores the four inputs and both wires.
- **Plexus_DynamicPorts_SaveLoadAndPaste_RestoreWiresOnDynamicPorts**: a diagram with wires on a computed input and a
  computed output loads from JSON identical to the original (ports included) and pastes into another diagram with both
  wires on the computed ports.

### Plexus-Core-ElementDescriptions

- **Plexus_Descriptions_PortsAndNodeTypes_ProvideToolTipText**: a node type's tooltip function shows a live property
  value, a type without one falls back to its description, a port carries its description and a port type its display
  name.

### Platform requirements (Plexus-Platform-*)

- **Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram**: runs on every platform and runtime of the CI
  matrix; the Windows, Linux, macOS, .NET 8, .NET 9 and .NET 10 requirements each accept only the result from their own
  platform or runtime.
