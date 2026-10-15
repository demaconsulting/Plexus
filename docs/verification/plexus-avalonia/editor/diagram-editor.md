### DiagramEditor Unit Verification Design

Scope: The Avalonia editor control: view state, selection, keyboard, rename, annotation editing, history commands,
host events and automation peer.

#### Verification Approach

The control is verified as a unit by driving it in a headless Avalonia window through its public surface and through
simulated input, with the real core model behind it. The core, Avalonia, the painter, the measurer and the gestures
are real dependencies: the control cannot be separated from Avalonia, and the gestures are internal parts of its
behavior that are verified in isolation by the Gestures unit tests. Test doubles are used only for the clipboard (an
in-memory clipboard, or one that throws) so that tests do not depend on the machine's clipboard. The tests are in the
`DiagramEditor*Tests.cs` files of the PlexusAvalonia test project, one file per concern, sharing the fixtures described
in the PlexusAvalonia system verification design. Each test starts a fresh host, so tests share no state.

#### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing and the Fluent theme, started once for
the test run by the shared fixtures. Rendering tests write pictures for visual inspection into the temporary folder of
the operating system.

#### Acceptance Criteria

A unit test passes when the control's observable state (model, history, selection, view, events, return values, the
inline editors and their text boxes) is as specified for the scenario, and argument errors raise the documented
exceptions.

#### Test Scenarios

**View and defaults** (`DiagramEditorViewTests.cs`)

- **DiagramEditor_Defaults_NewEditor_HasDocumentedValues**, **DiagramEditor_AutomationId_Default_IsDiagramEditor**,
  **DiagramEditor_Assembly_DeclaresPlexusXmlNamespace**: a new editor has the documented defaults and the XML
  namespace is declared.
- **DiagramEditor_Zoom_OutOfRange_IsClamped**, **DiagramEditor_ScreenToDiagram_WithZoomAndOffset_RoundTrips**,
  **DiagramEditor_GetPortScreenPosition_ReturnsAnchorInScreenSpace**,
  **DiagramEditor_ZoomToFit_FarApartNodes_BringsBothIntoView**: zoom limits (boundary values), conversion in both
  directions, port positions and fit-to-view.
- **DiagramEditor_ZoomToFit_BeforeFirstLayout_AppliesAfterLayout**, **DiagramEditor_ZoomToFit_WithoutPage_AppliesWhenPageIsSet**,
  **DiagramEditor_ZoomToFit_Pending_IsAppliedOnlyOnce** (`DiagramEditorPageTests.cs`): a fit requested before the first
  layout or before a page exists does nothing at once, is applied when the size and page exist, and is not applied again
  after the user zooms and the window is resized.
- **DiagramEditor_CtrlWheel_ZoomsAroundCursor**, **DiagramEditor_Wheel_WithoutModifiers_Scrolls**,
  **DiagramEditor_MiddleDrag_PansView**: wheel and middle button navigation.
- **DiagramEditor_IsReadOnly_Drag_DoesNotMoveNodes**: read-only editors select but do not move.

**Scene and painting** (`DiagramEditorRenderTests.cs`)

- **DiagramEditor_Scene_ModelChange_IsRebuilt**, **DiagramEditor_Scene_BurstOfChanges_RebuildsOnce**,
  **DiagramEditor_Scene_UndoAndRedo_AreReflected**: rebuild on change, coalescing, undo and redo.
- **DiagramEditor_Scene_FarAwayNode_IsCulled**, **DiagramEditor_Scene_ScrollToFarNode_BuildsIt**: culling and its
  boundary when scrolling.
- **DiagramEditor_DiagramTheme_Override_IsUsedByScene**: the theme override.
- **DiagramEditor_Render_SmallDiagram_PaintsNodesWiresAndText**,
  **DiagramEditor_Render_GroupsAndConnectPreview_SavesPicture**: rendered frames contain the expected colors, groups
  and the connection preview.

**Selection** (`DiagramEditorSelectionTests.cs`)

- **DiagramEditor_Click_OnNode_SelectsIt**, **DiagramEditor_Click_OtherNodeThenCanvas_ReplacesThenClears**,
  **DiagramEditor_ShiftAndCtrlClick_AddAndToggle**, **DiagramEditor_Click_OnSelectedNodeAmongSeveral_NarrowsSelection**,
  **DiagramEditor_Click_OnWire_SelectsWire**: the click selection rules.
- **DiagramEditor_RubberBand_OverNodes_SelectsTouchedNodes**, **DiagramEditor_RubberBand_WithShift_AddsToSelection**,
  **DiagramEditor_RubberBand_DoesNotCreateUndoEntries**: banding.
- **DiagramEditor_Selection_ElementRemovedByUndo_IsDropped**, **DiagramEditor_Select_ElementOnOtherPage_IsIgnored**,
  **DiagramEditor_Page_Changed_ClearsSelection**: selection hygiene.
- **DiagramEditor_MoveToPage_SelectedElements_LeaveSelection** (`DiagramEditorPageTests.cs`): elements moved to another
  page drop out of the selection while selected elements that stay remain.

**Page following** (`DiagramEditorPageTests.cs`)

- **DiagramEditor_PageRemoved_ShownLastPage_ShowsPreviousPageAndClearsSelection**,
  **DiagramEditor_PageRemoved_ShownMiddlePage_ShowsPageAtSameIndex**, **DiagramEditor_PageRemoved_OnlyPage_ShowsNothing**:
  removing the shown page switches to the previous page (index clamped), to the page that took its index, or to none, and
  clears the selection.
- **DiagramEditor_PageRemoved_OtherPage_KeepsShownPageAndSelection**: removing another page changes nothing.
- **DiagramEditor_UndoAddPage_ShownPage_FollowsAndDoesNotJumpOnRedo**: undoing the addition of the shown page follows to
  the old page and the redo that brings the page back does not switch to it.
- **DiagramEditor_PageRemoved_BoundPage_WritesBackThroughTwoWayBinding**: the automatic switch reaches the source of a
  two-way binding on `Page`.

**Drag gestures through the control** (`DiagramEditorDragTests.cs`)

- **DiagramEditor_DragMove_Node_SnapsToGridWithOneUndoEntry**, **DiagramEditor_DragMove_SnapDisabled_MovesByExactDelta**,
  **DiagramEditor_DragMove_SelectedNodes_MoveTogether**, **DiagramEditor_DragMove_ConnectedNode_RewiresLive**,
  **DiagramEditor_DragMove_GroupHeader_MovesMembers**, **DiagramEditor_Click_WithoutDrag_RecordsNoUndoEntry**: moving.
- **DiagramEditor_DragMove_EscapeCancels_RestoresPositionAndHistory**: Escape.
- **DiagramEditor_CtrlDrag_Node_DuplicatesAndMovesCopy**: Ctrl-drag copy.
- **DiagramEditor_PortDrag_OutputToInput_ConnectsWithOneUndoEntry**,
  **DiagramEditor_PortDrag_InputToOutput_ConnectsInCorrectOrder**,
  **DiagramEditor_PortDrag_IncompatibleTypes_IsRejected**, **DiagramEditor_PortDrag_ReleasedOnCanvas_DoesNothing**:
  connecting, including both directions, the rejection path and a release on the canvas.
- **DiagramEditor_WireSegmentDrag_MovesSegmentWithOneUndoEntry**,
  **DiagramEditor_ResizeHandleDrag_Annotation_ResizesWithOneUndoEntry**,
  **DiagramEditor_Click_GroupCollapseToggle_CollapsesAndExpands**: wire segment, resize handle and group toggle.

**Wire editing** (`Editor/DiagramEditorWireEditingTests.cs`, driven with real pointer input on a selected wire)

- **DiagramEditor_HitTest_SelectedWire_ReportsBendAndEndHandles**: the bend and both end handles are reported with
  their bend index and kinds.
- **DiagramEditor_DragBendHandle_MovesBendOrthogonallyAsOneUndoEntry**, **DiagramEditor_DragBendHandle_EscapeCancels**,
  **DiagramEditor_DragBendHandle_ReadOnly_DoesNothing**: the dragged bend lands on the snapped target, the wire stays
  orthogonal, one entry is recorded and undone completely; Escape rolls the drag back; a read-only editor ignores it.
- **DiagramEditor_DragPortStub_InsertsJogAsOneUndoEntry**: pulling the stub that leaves the source adds bends, keeps both
  anchors, stays orthogonal and is one entry.
- **DiagramEditor_DoubleClickSegment_InsertsJogAsOneUndoEntry**: a double-click on an unselected wire's segment adds four
  bends, selects the wire and records exactly one entry.
- **DiagramEditor_DragJogSegment_ReshapesAndFlattens**: the jog's middle piece is draggable and dragging it back removes
  the bump through route cleaning.
- **DiagramEditor_DragTargetHandle_OverValidPort_ReconnectsWire**,
  **DiagramEditor_DragSourceHandle_OverValidPort_ReconnectsWire**: the end moves to the port under the pointer, the wire
  keeps its identity, undo and redo move it back and forth.
- **DiagramEditor_DragEndHandle_OverInvalidPort_ChangesNothing**, **DiagramEditor_DragEndHandle_DroppedOnCanvas_Cancels**:
  a type-mismatched input, an output and empty canvas leave the wire and the history untouched.

**Keyboard and history** (`DiagramEditorKeyboardTests.cs`, `DiagramEditorHistoryCommandTests.cs`)

- **DiagramEditor_Delete_SelectedNode_RemovesNodeAndWires**, **DiagramEditor_CtrlZAndCtrlY_UndoAndRedo**,
  **DiagramEditor_CtrlShiftZ_Redoes**, **DiagramEditor_CtrlA_SelectsAllElements**,
  **DiagramEditor_CtrlD_DuplicatesSelection**, **DiagramEditor_F2_SelectedNode_StartsRename**,
  **DiagramEditor_Delete_ReadOnly_DoesNothing**, **DiagramEditor_SpaceDrag_PansView**: the shortcuts.
- **DiagramEditor_UndoAndRedo_ReturnWhetherTheyActed**, **DiagramEditor_UndoAndRedo_ReadOnlyEditor_ReturnFalse**: return
  values and read-only.
- **DiagramEditor_Undo_DuringDrag_OnlyCancelsGesture**, **DiagramEditor_Redo_DuringDrag_OnlyCancelsGesture**,
  **DiagramEditor_CtrlZ_DuringDrag_OnlyCancelsTheDrag**, **DiagramEditor_Undo_AfterCancelledDrag_UndoesPreviousEntry**,
  **DiagramEditor_Undo_DuringRename_OnlyCancelsRename**, **DiagramEditor_Redo_DuringAnnotationEdit_OnlyCancelsEdit**:
  Undo and Redo during gestures and inline edits only cancel, and the next request reverts the previous entry.
- **DiagramEditor_Delete_GivenElements_RemovesThemAndLeavesSelectionAlone**,
  **DiagramEditor_Delete_SelectedElement_DropsItFromSelection**, **DiagramEditor_Delete_Groups_FollowsCollapsedGroupRule**,
  **DiagramEditor_Delete_StaleOrForeignElements_AreIgnored**, **DiagramEditor_Delete_ReadOnlyOrNull_DoesNothingOrThrows**:
  the delete command including its error paths.

**Rename and host events** (`DiagramEditorRenameAndEventTests.cs`)

- **DiagramEditor_Rename_EnterWithValidName_CommitsAndCloses**,
  **DiagramEditor_Rename_EnterWithDuplicateName_StaysOpenWithReason**,
  **DiagramEditor_Rename_HostHandlerCancels_ShowsHostReason**, **DiagramEditor_Rename_Escape_CancelsWithoutChange**,
  **DiagramEditor_Rename_Committed_CanBeUndone**, **DiagramEditor_DoubleClick_NodeName_StartsRenameOnly**: the rename
  pipeline.
- **DiagramEditor_DoubleClick_NodeBody_RaisesNodeDoubleClicked**,
  **DiagramEditor_RightClick_OnNode_RaisesContextRequestedWithNode**,
  **DiagramEditor_RightClick_OnCanvas_RaisesContextRequestedWithPosition**,
  **DiagramEditor_HitTest_ReportsWhatIsUnderThePoint**: host events and hit testing.

**Annotation editing** (`DiagramEditorAnnotationEditTests.cs`)

- **DiagramEditor_BeginEditAnnotation_OpensTextBoxOverAnnotation**, **DiagramEditor_DoubleClick_Annotation_BeginsEdit**,
  **DiagramEditor_F2_SelectedAnnotation_BeginsEdit**: opening the editor.
- **DiagramEditor_AnnotationEdit_CtrlEnter_CommitsWithOneUndoEntry**, **DiagramEditor_AnnotationEdit_PlainEnter_DoesNotCommit**,
  **DiagramEditor_AnnotationEdit_Escape_CancelsWithoutChange**, **DiagramEditor_AnnotationEdit_FocusLoss_Commits**,
  **DiagramEditor_AnnotationEdit_ClickOnCanvas_Commits**, **DiagramEditor_AnnotationEdit_UnchangedText_RecordsNothing**,
  **DiagramEditor_AnnotationEdit_Commit_NormalizesLineBreaks**,
  **DiagramEditor_AnnotationEdit_ViewChanges_BoxFollowsAnnotation**: commit, cancel and layout.
- **DiagramEditor_BeginRename_WhileEditingAnnotation_ReplacesTheEdit**,
  **DiagramEditor_AnnotationEdit_EditorBecomesReadOnly_Cancels**, **DiagramEditor_AnnotationEdit_AnnotationRemoved_Cancels**,
  **DiagramEditor_CommitEditAndCancelEdit_WorkForRename**, **DiagramEditor_CommitEdit_RejectedRename_ReturnsFalse**,
  **DiagramEditor_CommitEditAndCancelEdit_NothingOpen_AreNoOps**, **DiagramEditor_F2_MultipleSelected_DoesNothing**,
  **DiagramEditor_DoubleClick_AnnotationInReadOnlyEditor_DoesNothing**,
  **DiagramEditor_BeginEditAnnotation_ReadOnlyEditor_DoesNothing**, **DiagramEditor_BeginEditAnnotation_Null_Throws**:
  lifecycle, read-only and error paths.

**Clipboard commands** (`DiagramEditorClipboardTests.cs`)

- **DiagramEditor_CopyPaste_ConnectedPair_RoundTrips**, **DiagramEditor_Paste_PointerOverEditor_PlacesContentAtPointer**,
  **DiagramEditor_PasteAt_ExplicitPosition_PlacesContentThere**, **DiagramEditor_CutThenPaste_MovesNodes**,
  **DiagramEditor_Paste_FromOtherEditorInstance_Works**, **DiagramEditor_CtrlCAndCtrlV_CopyAndPasteViaKeyboard**,
  **DiagramEditor_CopyPaste_SystemClipboard_RoundTripsWhenAvailable**: copy, cut and paste paths.
- **DiagramEditor_Paste_NotPlexusData_RaisesPasteFailed**, **DiagramEditor_Paste_UnknownNodeType_ListsMissingTypes**,
  **DiagramEditor_CtrlC_ClipboardThrows_RaisesClipboardFailed**: failure paths.
- **DiagramEditor_ReadOnly_PasteAndCut_DoNothing**, **DiagramEditor_DuplicateSelection_Empty_ReturnsNothing**,
  **DiagramEditor_DeleteSelection_CollapsedGroup_RemovesMembers**: read-only, empty selection and collapsed groups.

**Drag-and-drop** (`DiagramEditorNodeDropTests.cs`)

- **DiagramEditor_CreateNodeTypeDataTransfer_CarriesTheTypeKey**,
  **DiagramEditor_Drop_NodeType_CreatesCenteredSelectedNodeWithOneUndoEntry**, **DiagramEditor_Drop_NodeType_UsesViewTransform**,
  **DiagramEditor_Drop_NodeType_SnapsToGrid**, **DiagramEditor_Drop_TwiceInARow_CreatesTwoDistinctNodes**: dropping.
- **DiagramEditor_Drop_UnknownKeyReadOnlyOrForeignPayload_CreatesNothing**, **DiagramEditor_Drop_NoPage_CreatesNothing**,
  **DiagramEditor_EnableNodeTypeDrag_ArgumentsAndPlainClick**, **DiagramEditor_StartNodeTypeDragAsync_NullArguments_Throw**:
  ignored drops and argument errors.
- **DiagramEditor_AddNodeAtCenter_AddsOrRefuses** (`NodePaletteEditorTests.cs`): the editor adds a selected node at the
  visible center, and returns null for an unknown type, a read-only editor and no page; a null type throws.

**Drop ghost** (`NodeDropGhostTests.cs`)

- **DiagramEditor_Ghost_DragEnterAndOver_ShowsGhostAtSnappedPosition**: the ghost has the dragged type, sits on the grid
  within half a cell of the centered pointer position, and moves with the pointer.
- **DiagramEditor_Ghost_DragLeave_RemovesGhost**: leaving removes the ghost and creates nothing.
- **DiagramEditor_Ghost_Drop_NodeLandsExactlyWhereGhostWas**: for three zoom and scroll settings the dropped node's
  position equals the ghost position and the ghost is gone.
- **DiagramEditor_Ghost_NotAcceptedPayloads_ShowNoGhost**: an unknown key, a text payload, a read-only editor and a
  missing page show no ghost.
- **DiagramEditor_Ghost_IsPaintedTranslucent_AndGoneAfterLeave**: in a rendered frame a pixel inside the ghost box is a
  blend between canvas and node fill, and is the canvas again after the drag left (the frame is saved for inspection).

##### Selection and view commands (`PlexusAvalonia-Editor-DiagramEditor-SelectionCommands`, `-ViewCommands`)

All in `DiagramEditorSelectionCommandTests.cs`, in a headless window.

- **DiagramEditor_GroupSelection_...**, **DiagramEditor_UngroupSelection_...** and
  **DiagramEditor_ToggleCollapseSelection_...**: the result, the selection afterwards, exactly one undo entry (and its
  undo), wires ignored, a selected member standing for its group, and no effect (null or zero, no history entry) for
  an empty selection, a lone wire, a read-only editor or no page.
- **DiagramEditor_MoveSelectionToPage_...**: one undo entry, the selection cleared, the shown page kept; false for an
  empty selection, the shown page, a removed page, a page of another diagram and a read-only editor.
- **DiagramEditor_ViewCenter_FollowsZoomAndOffset**, **DiagramEditor_ZoomInAndOut_StepAroundTheCenterAndClamp** (step
  factor, fixed center, `MinZoom` and `MaxZoom`), **DiagramEditor_SnapPoint_FollowsSnapToGrid** and
  **DiagramEditor_AddAnnotationAtCenter_...**: grid-aligned, centered on the view, selected, one undo entry; nothing
  when read-only.

##### Tooltips

(Requirement IDs are prefixed `PlexusAvalonia-Editor-DiagramEditor`: `-ToolTipContent`, `-ToolTipNodeContent`,
`-ToolTipWireContent`, `-ToolTipOtherContent`, `-ToolTipRequested`, `-ToolTipDisplay`, `-ToolTipClear`,
`-ToolTipPressSuppression`, `-ToolTipGestureHiding`, `-ShowToolTips`.) All in `DiagramEditorToolTipTests.cs`, in a
headless window; the tests read the editor's `ToolTip.Tip` and `IsOpen` instead of waiting for the tooltip delay.

- **DiagramEditor_GetToolTipContent_Port_ShowsLabelTypeAndDescription** (`-ToolTipContent`): `"label (type)"` and the
  description on a new line; the port type display name, and its fallback to the key for an unregistered type.
- **DiagramEditor_GetToolTipContent_Node_UsesTypeToolTipThenDescription**,
  **DiagramEditor_GetToolTipContent_NodeFunctionReturnsEmpty_IsNull** (`-ToolTipNodeContent`): the tooltip function,
  else the description, else none; a function returning an empty string or null gives none although the type has a
  description (boundary).
- **DiagramEditor_GetToolTipContent_Wire_ShowsSourceToTarget** (`-ToolTipWireContent`): `"A.value → B.value"`.
- **DiagramEditor_GetToolTipContent_Annotation_IsNull**,
  **DiagramEditor_GetToolTipContent_GroupWithHandlerContent_ReturnsHandlerContent** (`-ToolTipOtherContent`): no
  default for an annotation and a group, a handler's content for a group, and a null element (error path).
- **DiagramEditor_ToolTipRequested_Handler_OverridesAndSuppresses** (`-ToolTipRequested`): a handler receives the
  element, the port and the default, replaces node text and cancels wire text; null and empty content count as none
  (`DiagramEditor_GetToolTipContent_Annotation_IsNull` for the null element) and the content query still raises the event
  with tooltips off (`DiagramEditor_ShowToolTips_False_NeverSetsTip`).
- **DiagramEditor_Hover_OverNode_SetsTip**,
  **DiagramEditor_ToolTip_OpenThenMoveToAnotherElement_ShowsNewContent** (`-ToolTipDisplay`): hovering sets the node
  text; an open tooltip shows the next element's text and stays open.
- **DiagramEditor_Hover_OverCanvas_ClearsTip**, **DiagramEditor_ToolTip_PointerLeaves_ClearsTip** (`-ToolTipClear`):
  empty canvas and leaving clear the tooltip.
- **DiagramEditor_ToolTip_RightClickPress_ClearsTipUntilPointerMovesAway** (`-ToolTipPressSuppression`): a right-click
  press clears the tooltip, which stays away while the pointer moves over the clicked element and returns after it moves
  away and back; the context request is still raised once.
- **DiagramEditor_ToolTip_DragOrRename_ClearsTip** (`-ToolTipGestureHiding`): a drag (the dragged node shows no tooltip
  until the pointer moved elsewhere) and an inline rename clear the tooltip.
- **DiagramEditor_ShowToolTips_False_NeverSetsTip** (`-ShowToolTips`): turning tooltips off clears the tooltip, hovering
  then raises no request, and the content query still works and raises the event; the default is on
  (`DiagramEditor_Defaults_NewEditor_HasDocumentedValues`).

##### Navigation

(Requirement IDs are prefixed `PlexusAvalonia-Editor-DiagramEditor`: `-BringIntoView`, `-BringIntoViewPage`,
`-BringIntoViewCollapsed`, `-BringIntoViewDeferred`, `-BringIntoViewInvalid`, `-ZoomToFitElements`,
`-ZoomToFitElementsCollapsed`, `-ZoomToFitElementsPages`, `-ZoomToFitElementsEmpty`, `-ZoomToFitElementsDeferred`,
`-ViewRequestOrder`, `-ZoomToFitElementsInvalid`.) All in `DiagramEditorNavigationTests.cs`, in an 800 by 600 headless
window.

- **DiagramEditor_BringIntoView_FarNode_CentersItWithoutChangingZoom**,
  **DiagramEditor_BringIntoView_ElementLargerThanView_ZoomsOut** (`-BringIntoView`): a far node is centered at the
  unchanged zoom; only an element too large for the view minus the margin makes the editor zoom out (boundary).
- **DiagramEditor_BringIntoView_NodeOnOtherPage_SwitchesPage** (`-BringIntoViewPage`), and
  **DiagramEditor_BringIntoView_ElementOfOtherDiagram_ThrowsAndKeepsView** (also `-BringIntoViewInvalid`): the page of
  the element is shown; an element of another diagram is refused while a page is shown but accepted when the editor
  shows none.
- **DiagramEditor_BringIntoView_NodeInCollapsedGroup_CentersGroup**,
  **DiagramEditor_BringIntoView_WireWithHiddenEnd_ShowsVisibleEndAndGroup** (`-BringIntoViewCollapsed`): the outermost
  collapsed group is shown; a wire whose target is in a far collapsed group shows its visible source and that group,
  zooming out to fit both.
- **DiagramEditor_BringIntoView_BeforeFirstLayout_AppliesAfterLayout** (`-BringIntoViewDeferred`): nothing happens
  before the first layout, the node is in view after it.
- **DiagramEditor_BringIntoView_DetachedElement_ThrowsArgumentException** (`-BringIntoViewInvalid`): a removed node, a
  node of another diagram and null (error paths) with the page unchanged; the other-diagram test above also asserts
  that zoom, offset and selection stay as they were.
- **DiagramEditor_ZoomToFitElements_SubsetOfNodes_FitsOnlyThose**,
  **DiagramEditor_ZoomToFitElements_Margin_IsKeptOnEachSide** (`-ZoomToFitElements`): only the subset is shown at a zoom
  of at most 1; for an element larger than the view the box lies inside the view shrunk by the margin and a larger margin
  gives a smaller zoom (margin 0 and 100).
- **DiagramEditor_ZoomToFitElements_NodeInCollapsedGroup_FitsGroup** (`-ZoomToFitElementsCollapsed`): a node hidden in a
  large collapsed group is fitted as the small collapsed box, at actual size and without the far-away other node.
- **DiagramEditor_ZoomToFitElements_OtherPage_SwitchesToFirstElementsPage** (`-ZoomToFitElementsPages`): the first
  element's page is shown and the element of the other page ignored (one node fits at actual size).
- **DiagramEditor_ZoomToFitElements_Empty_DoesNothing** (`-ZoomToFitElementsEmpty`): offset and zoom stay (boundary);
  also a null sequence throws.
- **DiagramEditor_ZoomToFitElements_BeforeFirstLayout_AppliesAfterLayout** (`-ZoomToFitElementsDeferred`): applied once
  the window is shown, zooming out to show a far node.
- **DiagramEditor_ZoomToFit_LatestRequestWins_AcrossOverloads** (`-ViewRequestOrder`): a page fit followed by a
  bring-into-view before the first layout leaves only the latter applied.
- **DiagramEditor_ZoomToFitElements_InvalidArguments_ThrowWithoutChangingView** (`-ZoomToFitElementsInvalid`): a null
  sequence, a null entry, a removed node, an element of another diagram (alone or mixed), and margins -1, NaN, +infinity
  and -infinity (also with an empty set) throw the documented exception and leave page, zoom, offset and selection
  unchanged.

##### Per-node ports

(Requirement IDs `PlexusAvalonia-Editor-DiagramEditor-DynamicPorts` and `-DynamicPortRemoved`, in
`DiagramEditorDynamicPortsTests.cs`.)

- **DiagramEditor_Connect_DragToPortAddedByPropertyChange_CreatesWire** (`-DynamicPorts`): a drag onto a port added by a
  property change connects to it.
- **DiagramEditor_HitTest_PortRemovedByPropertyChange_IsNotHit** (`-DynamicPortRemoved`): the position of a removed port
  no longer hits it, while the remaining port is still hit.
