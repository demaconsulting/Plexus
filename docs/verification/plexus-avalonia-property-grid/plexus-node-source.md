## PlexusNodeSource Unit Verification Design

Scope: The Plexus adapter sources that show and edit a node, an annotation or a group in the property grid, with every
commit as one undo entry of the diagram.

### Verification Approach

The tests build a real diagram (a node type with a double with a unit, a choice, a boolean, a color, an uncategorized
string, a read-only string and a hinted integer, and an annotated host data class), create the source for a node, and drive
it through a real `PropertyEditSession` in staged and live mode. The results are checked on the node itself (name,
properties, data object identity and content) and on the diagram's undo history (`UndoCount`, `Undo`). A handler of
`NodeRenaming` provides the host veto. No mocks. The tests are in `NodePropertySourceTests.cs`; the grid-level test
(`PropertyGrid_SelectedObjectNode_LiveEditsAreUndoEntriesAndFollowUndo`) is in `NodePropertiesDialogTests.cs` and runs in
the headless Avalonia platform. The fixtures `NodeFixture.cs` hold the node type and the host data class.

### Test Environment

The test project of the system; the grid test uses the headless Avalonia platform (see the PropertyGridControl
verification design).

### Acceptance Criteria

A test passes when the rows match the definitions and annotations, typed text is validated with the documented messages,
a staged commit changes the node exactly once in the undo history and leaves the previous data object untouched, a cancel
or a refused commit changes nothing, live edits are one entry each, and outside changes are followed.

### Test Scenarios

- **NodePropertySource_GetItems_ListsNameAndDeclaredPropertiesWithMetadata**: categories and order (General first, name
  first), the display name with the unit, the description, read-only, the editor key and the type of each row.
- **NodePropertySource_GetItems_ChoiceAndColorRowsUseTheMatchingEditorTypes**: a choice is an exclusive list and a
  color is an Avalonia color that is not bold at its default.
- **NodePropertySource_GetItems_ListsHostDataProperties** / **NodePropertySource_GetItems_NodeWithoutDataHasNoDataRows**:
  data rows with their annotations (hidden ones left out), and none for a node without data.
- **NodePropertySource_Constructor_DetachedNode_Throws**: a detached node and a null node are refused (error paths).
- **NodePropertySource_SetText_ValidatesWithThePropertyDefinition** /
  **NodePropertySource_SetText_NameIsValidatedWhileTyping**: "abc" is refused as "'abc' is not a number.", "880 Hz" is
  accepted, an empty and a duplicate name are refused with the rename messages (error paths).
- **NodePropertySource_StagedCommit_IsOneUndoEntry**: rename, choice, color, number and data edits are one undo entry;
  undo restores everything including the data object.
- **NodePropertySource_StagedCancel_ChangesNothing** / **NodePropertySource_StagedCommit_UneditedDataIsNotReplaced**:
  cancel leaves node, data and history alone; a commit that only renames keeps the data object.
- **NodePropertySource_StagedCommit_CollectionEditsPersistAndUndo**: a collection edited through a collection editor
  persists on commit and undoes.
- **NodePropertySource_StagedCommit_RenameVetoedRefusesTheCommitAndMarksTheNameRow**: a vetoed rename refuses the commit
  with the reason on the name row, nothing changes and the edits stay pending (error path).
- **NodePropertySource_LiveEdits_EachEditIsItsOwnUndoEntry**: four live edits are four entries and a data edit
  publishes a copy; undo steps back one edit.
- **NodePropertySource_ExternalChange_RaisesChangedAndRereads** /
  **NodePropertySource_ExternalChange_DataRemovedDropsTheDataRows**: a property change and an undo raise `Changed`; removing
  the data drops the data rows.
- **PropertySourceFactory_Create_PlexusElements_UsesThePlexusSources**: the factory chooses the three sources.
- **AnnotationPropertySource_StagedCommit_IsOneUndoEntry** / **GroupPropertySource_StagedCommit_IsOneUndoEntry**: text and
  fill (and clearing the fill), title and collapsed state, each commit one undo entry.
- **PropertyGrid_SelectedObjectNode_LiveEditsAreUndoEntriesAndFollowUndo**: a live grid given a node makes each edit one
  entry and shows the old value after undo.
