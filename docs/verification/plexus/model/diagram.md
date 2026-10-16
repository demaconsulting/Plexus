### Diagram Unit Verification Design

Scope: Diagram root: node-type registry, pages, history, theme, router, connection rules, change notification, rename
pipeline and move-to-page.

#### Verification Approach

The Diagram unit is verified with unit tests that drive the real Diagram together with the real node-type registry
fixture, pages, undo history, theme, router and connection rules; nothing is mocked because all collaborators are
in-memory and deterministic. Tests create a diagram through the shared test fixture, perform operations through the
public API and assert on returned values, the model state (including a full state dump where undo must restore
everything), the undo history depth and the events received. JSON snapshot behavior is verified by the Snapshot unit.

#### Test Environment

Standard xUnit v3 test runner on .NET (net8.0, net9.0 and net10.0). No external services, files or configuration.

#### Acceptance Criteria

All scenarios below pass on every target framework, and every test method named here is matched to its requirement by the
ReqStream trace matrix.

#### Test Scenarios

##### Construction (`Plexus-Model-Diagram-NodeTypes`)

- **Diagram_Constructor_NullTypes_ThrowsArgumentNullException**: a null registry is rejected.
- **Diagram_Constructor_Registry_IsExposedAndDiagramStartsEmpty**: the registry is exposed; no pages and no history.

##### Lookup (`Plexus-Model-Diagram-Lookup`, `Plexus-Model-Diagram-PortLookup`)

- **Diagram_FindNode_SearchesAllPages**: a node is found by name on any page; unknown name or id gives null.
- **DiagramPage_AddNode_ExplicitNameAndId_UsesThem** and **DiagramPage_Remove_Node_RemovesAttachedWiresAndRoundTrips**:
  an element is found by id while attached and not found after removal.
- **Diagram_FindPort_NameOrId_ResolvesPort**: a path resolves with a node name or an element id.
- **Diagram_FindPort_UnknownOrMalformed_ReturnsNull**: unknown or malformed paths give null.
- **Diagram_FindPort_AmbiguousWithoutDirection_ThrowsWithHint**: a shared port name needs a direction.
- **Diagram_FindPort_NodeNameWithDots_SplitsAtMatchingDot**: dots inside node and port names are handled.
- **Diagram_FindPort_SearchesAllPages_PageFindPortStaysLocal**: the diagram searches all pages in order.

##### Settings (`Plexus-Model-Diagram-ConnectionRulesSetting`, `Plexus-Model-Diagram-Settings`)

- **Diagram_Settings_Default_AreClassicThemeOrthogonalRouterAndUniqueNames**: documented defaults.
- **ConnectionRules_Default_AllowsAndHostRuleReceivesOutputThenInput**: the host rule receives output then input; null
  is rejected.
- **Diagram_Settings_Assigned_AreAppliedButNotUndoable**: assigned settings take effect and record no history.
- **Diagram_Changed_SettingsChange_RaisesSettingsChanged**: each setting raises a settings change naming it; null router
  rejected.
- **Diagram_Theme_CustomMetrics_AffectNewNodes**: a new theme changes new node sizes; null theme rejected.

##### History and notification (`Plexus-Model-Diagram-SharedHistory`, `Plexus-Model-Diagram-ChangeNotification`)

- **Diagram_History_EditsOnSeveralPages_ShareOneUndoStack**: edits on two pages share one history.
- **Diagram_Changed_PageAddRemoveUndoRedo_RaisesPageEvents**: page add, remove and re-add via undo and redo.
- **Diagram_Changed_PageRenameAndMove_RaiseDedicatedKinds**: page rename and reorder kinds.
- **Diagram_Changed_AddAndRemoveElement_RaisesElementEvents**: element added and removed events carry page and element.
- **Diagram_Changed_UndoRedoOfPropertyChange_RaisesElementChanged**: property change, undo and redo each notify.
- **Diagram_Changed_NodeRename_RaisesNameChange**: a rename reports the name property.
- **Diagram_Changed_MoveToPageAndUndo_RaisesRemovalAndAdditionEvents**: a move and its undo notify every step.

##### Rename (`Plexus-Model-Diagram-RenameValidation`, `-RenameHook`, `-RenameUndo`)

- **Diagram_Rename_WhitespaceAroundName_IsTrimmed**, **Diagram_Rename_EmptyName_IsRejected**,
  **Diagram_Rename_DuplicateName_IsRejectedWithReason**, **Diagram_Rename_DuplicateWithUniquenessOff_Succeeds**,
  **Diagram_Rename_SameName_IsNoOp**: default validation and the reason reported by the try variant.
- **Diagram_Rename_InvalidArguments_Throw** and **Node_Name_Set_OnRemovedNode_ThrowsInvalidOperationException**: null
  arguments and detached nodes throw.
- **Diagram_Rename_HandlerCorrectsName_AppliesCorrection**, **Diagram_Rename_RenamingArgs_CarryOldAndProposedName**,
  **Diagram_Rename_HandlerCorrectionDuplicates_IsRejected**, **Diagram_Rename_HandlerCancels_RejectsWithReason**,
  **Diagram_Rename_CancelWithoutReason_ReportsDefaultReason**: the host hook corrects or cancels and is re-validated.
- **Node_Name_Set_RenamesAndRoundTripsThroughUndo**, **Diagram_Rename_Events_RenamedFiresOnUndoRedoButRenamingDoesNot**,
  **Diagram_Rename_FreesOldName_ForReuse**: one undo entry, renamed event on undo and redo, old name reusable.

##### Move to page (`Plexus-Model-Diagram-MoveToPage`, `Plexus-Model-Diagram-MoveToPageWires`)

- **Diagram_MoveToPage_Group_BringsMembersAndStructure**: a group moves with its members and nesting.
- **Diagram_MoveToPage_MemberOfStayingGroup_LeavesGroupAndUndoRestoresMembership**: leaving a staying group and undo.
- **Diagram_MoveToPage_Annotation_MovesWithBounds**: an annotation keeps its bounds.
- **Diagram_MoveToPage_ElementsAlreadyOnTargetOrWires_AreIgnored**: wires and target-page elements are skipped.
- **Diagram_MoveToPage_InvalidArguments_Throw**: null arguments and foreign pages or elements throw.
- **Diagram_MoveToPage_FromSeveralPages_IsOneEntryAndRoundTrips**: one undo entry round-trips the whole state.
- **Diagram_MoveToPage_KeepsNamesAndIdsFindable**: names and ids are kept and findable.
- **Diagram_MoveToPage_NodesTogether_CarryWiresWithRoutes**: a wire between moved nodes moves with its route.
- **Diagram_MoveToPage_NodeWithOutsideWires_RemovesWiresAndUndoRestores**: wires to staying nodes are removed and undo
  restores them.

##### Wire styler (`Plexus-Model-Diagram-WireStyler`)

- **Diagram_WireStyler_Default_IsNullAndWiresUseTheme**: no styler by default and wires use the theme.
- **Diagram_WireStylerAndRefreshAppearance_RaiseChangedWithoutHistory**: assigning the styler and refreshing raise
  settings changes named `WireStyler` and `Appearance`, and the undo count is unchanged.

##### Node styler (`Plexus-Model-Diagram-NodeStyler`)

- **Diagram_NodeStyler_Default_IsNullAndNodesUseTheme**: no styler by default and nodes use the theme.
- **Diagram_NodeStyler_Assign_RaisesChangedWithoutHistory**: assigning the styler and refreshing raise settings changes
  named `NodeStyler` and `Appearance`, and the undo count is unchanged.
- **SceneBuilder_Build_NodeStylerThrows_FallsBackToTheme**: a styler that throws is treated as returning no style.

##### Version (`Plexus-Model-Diagram-Version`)

- **Diagram_Version_AdvancesWithEveryChangeIncludingUndoRedoAndRefresh**: reads and scene builds leave the version
  alone; adding a page or a node, undo, redo, a property edit, a theme change and `RefreshAppearance` each increase it.
- **Diagram_Version_IsCurrentInsideChangedHandlers**: a `Changed` handler already reads the new version (`DiagramVersionTests.cs`).

##### Options (`Plexus-Model-Diagram-Options`)

- **Diagram_Constructor_WithOptions_InstallsEverythingSilently** and
  **Diagram_Constructor_NullOrEmptyOptions_KeepsDefaults**: every member is installed without a change notification
  (the version stays 0); null or empty options keep every default.
- **Diagram_Loaders_WithOptions_InstallThemOnTheLoadedDiagram**: `FromJson`, `FromSnapshot` and `FromTree` install the
  options.
- **Diagram_Apply_ReplacesOnlySetMembers_NotUndoable** and **Diagram_Apply_FullOptions_InstallsEveryMember**: only
  members that are set are replaced, each raises a `SettingsChanged` notification, nothing enters the history and null
  is refused (`DiagramOptionsTests.cs`).
- **Diagram_Constructor_WithInitialize_InvokesItOnceWithTheConfiguredDiagram**,
  **Diagram_Loaders_WithInitialize_InvokeItAfterTheLoadAndAttachPerInstanceHandlers** and
  **Diagram_Apply_WithInitialize_InvokesItAfterTheSettings** (`Plexus-Model-Diagram-Initialize`): the callback runs once
  per created or loaded diagram after the configuration (and, for loaders, after the content is loaded and the history
  is clean), a rename veto attached in it is live on every loaded diagram, and `Apply` calls it last on every call.

##### Change handlers that throw (`Plexus-Model-Diagram-HandlerFailureKeepsChange`)

All in `DiagramChangedEventTests.cs`:

- **Diagram_Changed_HandlerThrowsOnAdd_NodeIsAddedRecordedAndExceptionRethrown**: a `Changed` handler that always
  throws: the node is added, findable and recorded as one entry, and undo (handler detached) removes it again.
- **Diagram_Changed_HandlerThrowsDuringMultiStepOperation_IsOneCompleteEntry**: removing a node with two wires while a
  handler throws on every removal removes the node and both wires as one entry; one undo restores the full model dump.
- **Diagram_PropertyChanged_HandlersThrow_ChangesAreRecorded**: in turn, a throwing element `PropertyChanged`,
  property-bag `PropertyChanged`, page `Nodes` collection change, page-list collection change, `NodeRenamed` and page
  `PropertyChanged` handler: each change (move, value, add node, add page, rename node, rename page) is applied and
  recorded as one entry, and the handler's exception reaches the caller.
- **Diagram_Changed_HandlerThrows_VersionStillAdvances**: the version advances by exactly the number of notifications
  although the last handler of each throws.

##### Reported handler exception (`Plexus-Model-Diagram-HandlerFailureReported`)

- **Diagram_Changed_HandlerThrowsOnAdd_NodeIsAddedRecordedAndExceptionRethrown**: the exception instance thrown by the
  handler is the one the caller receives, after the change was recorded.
- **Diagram_Changed_TwoHandlerFailures_FirstIsRethrown**: when the element handler and then the diagram handler of one
  rename throw, the first exception is reported, the later `NodeRenamed` handler still runs and the rename is complete.

##### Undo and redo (`Plexus-Model-Diagram-HandlerFailureUndoRedo`)

- **Diagram_Changed_HandlerThrowsOnUndoAndRedo_EntryMoves**: with a handler that always throws, undo removes the node
  and moves the entry to the redo stack, and redo adds it back and moves the entry back, each reporting the handler's
  exception afterwards.

##### Transactions (`Plexus-Model-Diagram-HandlerFailureTransaction`)

- **Diagram_Changed_HandlerThrowsInsideTransact_TransactionIsRolledBack**: a handler exception raised by the add inside
  a `Transact` that first renamed a node rolls back the rename and the add; the handler's exception propagates, the
  model dump and the undo count are unchanged and no transaction stays open.

##### Failing change (`Plexus-Model-Diagram-HandlerFailureOperationFails`)

- **Diagram_Changed_OperationFailsAndHandlerThrows_OperationExceptionPropagates**: a property change whose port
  provider throws while a handler throws on every notification propagates the provider's exception type; the value is
  rolled back and nothing is recorded (error path).

**Expected**: each test passes, proving the Diagram requirements.
