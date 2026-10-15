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
and returns false, OK with an invalid value or a failing commit leaves the dialog open with the reason and writes
nothing, a missing owner or target is rejected before a window opens, the close-confirmed hook is raised once for a
close that goes ahead and never for a vetoed one, and the internal preview hook rejects a null preview and an invalid
size, docks on every side with the grid filling the rest, replaces a previous preview and exposes its automation
metadata.

### Test Scenarios

- **PropertyGridDialog_GetDefaultTitle_UsesDisplayNameOrTypeName** / **PropertyGridDialog_Constructor_CreatesStagedGridWithTitle**:
  default and given titles, the staged grid, and a null target rejected (error path).
- **PropertyGridDialog_ShowAsync_NullOwnerOrTarget_ThrowsArgumentNullException**: a null owner and a null target are each
  rejected with an argument-null exception and no dialog window is opened (error path).
- **PropertyGridDialog_CloseConfirmed_IsRaisedOnlyWhenTheCloseGoesAhead**: the internal hook is not raised when a
  `Closing` handler vetoes `Close`, and is raised exactly once when the Cancel button then closes the dialog
  (boundary).
- **PropertyGridDialog_Ok_CommitsAndReturnsTrue** / **PropertyGridDialog_Cancel_DiscardsAndReturnsFalse**: the awaited
  result and the object after OK and Cancel.
- **PropertyGridDialog_Ok_RunsCommitInsideScopeOnce**: two edits are written by one call of the commit scope.
- **PropertyGridDialog_Ok_WithInvalidValue_StaysOpen** /
  **PropertyGridDialog_Ok_WhenCommitFails_StaysOpenWithReason**: an out-of-range value and a setter that throws keep
  the dialog open and show the reason (error paths).
- **PropertyGridDialog_SetPreview_InvalidArguments_ThrowsAndShowsNothing**: the internal `SetPreview` hook rejects a null
  preview (argument-null) and a zero, negative, NaN or infinite size (argument-out-of-range), and no preview border is
  shown afterwards (error paths).
- **PropertyGridDialog_SetPreview_DocksOnEveryDirection**: left, right, top and bottom each dock the preview before the
  grid with the size as its width (left, right) or height (top, bottom), a separator line facing the grid, the grid still
  the last child and positioned on the opposite side after layout (theory over the four directions, boundary).
- **PropertyGridDialog_SetPreview_Replacement_ShowsOnlyTheLatestPreview**: a second call replaces the first preview, which
  is detached, leaving exactly one preview border in the new dock position.
- **PropertyGridDialog_SetPreview_ExposesAutomationMetadata**: the preview border has the automation id
  `PropertyGridDialog_Preview`, the name `Preview`, the content accessibility view and the group control type, and hosts
  the given control.

The visual check `PropertyGridDialog_Screenshot_Light` (renders the dialog with a rejected value for visual review) is
not part of this unit's test file: it lives in `PropertyGridScreenshotTests.cs`, which belongs to the PropertyGridControl
review set together with the other screenshot checks, and it is not cited as evidence for any requirement.
