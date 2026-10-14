## PropertyGridDialog Unit Verification Design

Scope: The `PropertyGridDialog` helper: a modal dialog that edits an object in a staged grid with OK and Cancel.

### Verification Approach

The dialog is created directly or through `ShowAsync` from a headless owner window; the test finds the dialog among the
owner's owned windows, edits through its grid and clicks OK or Cancel (routed click events). The test harness checks the
returned task, the edited object, the commit scope calls and the status line. No mocks. The tests are in
`PropertyGridDialogTests.cs`.

### Test Environment

The headless Avalonia platform of the test project (see the PropertyGridControl verification design).

### Acceptance Criteria

A test passes when OK writes the staged edits once inside the scope and returns true, Cancel leaves the object untouched
and returns false, and OK with an invalid value or a failing commit leaves the dialog open with the reason and writes
nothing.

### Test Scenarios

- **PropertyGridDialog_GetDefaultTitle_UsesDisplayNameOrTypeName** / **PropertyGridDialog_Constructor_CreatesStagedGridWithTitle**:
  default and given titles, the staged grid, and a null target rejected (error path).
- **PropertyGridDialog_Ok_CommitsAndReturnsTrue** / **PropertyGridDialog_Cancel_DiscardsAndReturnsFalse**: the awaited
  result and the object after OK and Cancel.
- **PropertyGridDialog_Ok_RunsCommitInsideScopeOnce**: two edits are written by one call of the commit scope.
- **PropertyGridDialog_Ok_WithInvalidValue_StaysOpen** /
  **PropertyGridDialog_Ok_WhenCommitFails_StaysOpenWithReason**: an out-of-range value and a setter that throws keep
  the dialog open and show the reason (error paths).
- **PropertyGridDialog_Screenshot_Light**: renders the dialog with a rejected value for visual review.
