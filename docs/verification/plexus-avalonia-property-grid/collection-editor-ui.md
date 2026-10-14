## CollectionEditorUi Unit Verification Design

Scope: The collection editor view and dialog: a list of members with Add, Remove and Move buttons and a nested property
grid for the selected member.

### Verification Approach

The view is created over the session of a hosted grid and driven through its public members and its buttons; the dialog
is shown on the headless Avalonia platform and its OK and Cancel buttons are clicked. The tests check the working list,
the member texts in the list box, the nested grid, and what the owning object and session hold after OK or Cancel. Test
objects are the sample `Widget`, a `Person` (fixed-size arrays, elements without a default constructor), a `Catalog`
whose entries hold collections, and a holder of structure elements. No mocks. The tests are in
`CollectionEditorUiTests.cs`.

### Test Environment

The headless Avalonia platform of the test project (see the PropertyGridControl verification design).

### Acceptance Criteria

A test passes when the working list changes as the buttons say, the owning collection changes only after OK (live: at
once; staged: after the host's commit), Cancel leaves it untouched, and refused operations return a failed result with a
message.

### Test Scenarios

- **CollectionEditorView_AddRemoveMove_ChangeWorkingListUntilApplied**: add, move and remove change the list, not the
  collection, until `Apply`.
- **CollectionEditorView_Buttons_EnableByCapabilityAndSelection**: the buttons call the operations and enable by selection.
- **CollectionEditorView_Add_UsesCollectionItemFactory**: an element without a default constructor fails to add (error
  path) until the grid supplies a factory.
- **CollectionEditorView_NestedGrid_EditsSelectedMember** / **..._ValueElements_UseValueRowAndFormatter** /
  **..._FormatItem_UsesToStringThenFallbacks** / **..._StructElements_ExpandInValueRow**: object members edit in the
  nested grid, value members through a Value row, structures expand, and member texts follow the formatter and fallbacks.
- **CollectionEditorView_FixedArray_RefusesResizeButAllowsReorder** / **..._NotACollection_Throws**: a fixed array refuses
  Add with a message and still reorders; a non-collection and a null session are rejected (error paths).
- **CollectionEditorDialog_OkAndCancel_Live** / **CollectionEditorDialog_Ok_Staged_RecordsPendingEdit** /
  **PropertyGrid_CollectionButton_OpensDialogThatApplies**: OK applies, Cancel does not, staged OK is a pending edit
  that a commit applies, and the grid's button opens the dialog and completes the awaited task.
- **CollectionEditorView_NestedCollections_WorkRecursively**: a member's collection is edited through the member grid's
  own session and the nested button asks for another editor.
- **CollectionEditor_Screenshot_Dark**: renders the dialog in the dark theme for visual review (members list, buttons,
  nested grid).
