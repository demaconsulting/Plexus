## PropertyEditors Unit Verification Design

Scope: The editors of the value column of a property grid row, the editor context that connects them to the edit
session, and the editor registry of the grid.

### Verification Approach

Each editor is exercised inside a hosted `PropertyGrid` on the headless Avalonia platform: the test finds the editor by
the automation id of its row, changes it the way a user would (typing text and pressing Enter, toggling a check box,
picking a list entry, moving a slider) and checks the edited object, the visual state of the row (error border and
tooltip, bold) and the session (pending edits, errors). Live and staged modes are both covered. A slider registered under
the key of an annotation is the sample custom editor. No mocks are used. The tests are in `PropertyGridEditingTests.cs`
and `ColorEditorTests.cs`.

### Test Environment

The headless Avalonia platform of the test project (see the PropertyGridControl verification design); the culture of the
text assertions is the invariant-looking integers and hex strings that do not depend on the culture.

### Acceptance Criteria

A test passes when the value reaches the object at the documented moment (Enter or focus loss in live mode, commit in
staged mode), rejected input leaves the object unchanged and is shown with the reason until fixed or cancelled, and each
editor shows the current value after a refresh.

### Test Scenarios

- **PropertyEditors_Text_Live_WritesOnEnter** / **..._WritesWhenFocusLeaves** / **..._Staged_BuffersUntilCommit**: text
  editing in both modes; staged edits wait for commit, and cancel restores the text.
- **PropertyEditors_Staged_CommitRunsInsideTheCommitScope**: the grid's commit scope wraps the commit.
- **PropertyEditors_InvalidNumber_ShowsErrorUntilFixed** / **PropertyEditors_OutOfRangeValue_Staged_ShowsValidationMessage**:
  a value that does not convert and a value outside its range are rejected (error paths) with a red border and the reason;
  fixing the text clears the error.
- **PropertyEditors_Escape_CancelsTypedText** / **PropertyEditors_Escape_OnRowCancelsRejectedEdit**: Escape abandons typed
  text and a rejected edit, from the editor and from the row.
- **PropertyEditors_Bool_TogglesWithCheckBox**, **..._Enum_SelectsFromDropDown**, **..._FlagsEnum_TogglesMembers**,
  **..._StandardValues_UseEditableDropDown**, **..._PasswordText_IsMasked**: the choice editors and the masked text.
- **PropertyEditors_Color_UsesSwatchAndHexText**, **ColorSupport_FormatAndParse_RoundTripBothColorTypes**,
  **ColorEditor_EditsBothColorTypes**: the swatch follows the value, hex text and names parse, text that is not a
  color is rejected (error path), System.Drawing and optional colors work and empty text clears an optional color.
- **PropertyEditors_NestedObjects_WriteThroughToOwner**: child rows of a reference object and of a structure write through
  and the parents' summary text follows.
- **PropertyEditors_Collection_ShowsSummaryAndOpensEditor**: summary text with singular and plural, the button raises the
  request and a handler can take it over.
- **PropertyEditors_CustomEditor_SliderEditsAnnotatedProperty** /
  **PropertyEditors_EditorRegistry_FallsBackAndHonorsTypeRegistrations** /
  **PropertyEditors_ResetMenu_RestoresDefaultValue**: a custom editor registered under an annotation key, the fallback to
  the type's editor without the registration, an editor registered for a type, and the reset menu.
