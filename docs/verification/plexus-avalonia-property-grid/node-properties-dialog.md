## NodePropertiesDialog Unit Verification Design

Scope: The `NodePropertiesDialog` helper: the ready-made modal properties dialog of a Plexus node, its options and live
preview, and the `NodePreviewContext`.

### Verification Approach

The dialog is opened with `ShowAsync` from a headless owner window over a real diagram node; the test finds it among the
owner's owned windows, types into the editors of its grid, and clicks OK, Cancel (routed click events) or calls `Accept`.
The results are checked on the node and on the undo history. No mocks. The tests are in `NodePropertiesDialogTests.cs`.

The live preview is verified in `NodePropertiesDialogPreviewTests.cs` with a preview factory that captures the
`NodePreviewContext` the dialog creates and returns a plain text block. Edits are typed into the grid (Enter commits them
to the session) or made through the dialog's session (`SetText`, `Reset`, `Cancel`); the test counts the context's
`Changed` notifications and reads the effective values, and checks the node and the undo history afterwards. The preview's
position is measured with `TranslatePoint` against the grid. The host-built-window path is verified with a context over
a `PropertyGrid` in a headless window (taken out of the window and put back to replace its session) and over a
`PropertyEditSession` with no UI at all.

### Test Environment

The headless Avalonia platform of the test project (see the PropertyGridControl verification design).

### Acceptance Criteria

A test passes when the title and rows are right, OK changes the node once in the undo history and completes with true,
Cancel changes nothing and completes with false, and an invalid or vetoed name leaves the dialog open with the reason.
For the preview, a test passes when the preview panel is found by its automation id at the requested side with the
expected dialog size, the context returns the staged value while the node keeps its own, every edit, reset, cancel and
session replacement is notified (and an error alone is not), OK and Cancel keep their meaning, and invalid options and
arguments are rejected with the documented exceptions.

### Test Scenarios

- **NodePropertiesDialog_ShowAsync_ShowsTheNodeInAStagedGrid**: the title "Osc - Properties", a staged grid, the name row
  first, the data rows present.
- **NodePropertiesDialog_Arguments_AreValidated**: null owner, node and title arguments are rejected (error paths).
- **NodePropertiesDialog_Ok_CommitsAsOneUndoEntry**: name, a number with a unit and a data value are applied as one undo
  entry; undo restores them.
- **NodePropertiesDialog_Cancel_ChangesNothing**: the node, its data object and the history are unchanged.
- **NodePropertiesDialog_Ok_InvalidNameShowsTheReasonAndStaysOpen**: a duplicate name is refused inline and in the status
  line, OK is refused, and fixing the name lets OK succeed (error path).
- **NodePropertiesDialog_Ok_VetoedRenameShowsTheReasonOnTheNameRow**: a handler veto keeps the dialog open with the reason
  on the name row and changes nothing (error path).
- **NodePropertiesDialog_Preview_IsShownBesideTheGridAndWidensTheDialog**: the `PropertyGridDialog_Preview` panel holds
  the factory's control, lies right of the grid, and the dialog is 880 by 640.
- **NodePropertiesDialog_Preview_TopPlacementIsAboveTheGrid**: a top preview lies above the grid and the dialog is 480 by
  860.
- **NodePropertiesDialog_Preview_SeesStagedValues**: a typed frequency and name are returned by the context while the node
  keeps 440 Hz and "Osc"; an unedited data row returns the node's value; an unknown path returns null.
- **NodePropertiesDialog_Preview_IsNotifiedOnEditResetAndCancel**: a typed edit, a reset (the default) and a cancel (the
  node's value) each raise `Changed`; an invalid text that only sets an error does not (boundary).
- **NodePropertiesDialog_PreviewOk_CommitsOneUndoEntry**: with a preview, OK applies a rename, a property and a data
  edit as one undo entry and completes with true.
- **NodePropertiesDialog_PreviewOk_IsNotifiedAfterCommit**: with a preview, OK raises `Changed` after the commit, so the
  last notification sees the committed frequency on the node.
- **NodePropertiesDialog_PreviewCancel_ChangesNothing**: with a preview, Cancel leaves the name, properties, data object
  and history unchanged, completes with false, and the context raises nothing after the dialog closed.
- **NodePropertiesDialog_Options_TitleAndSizeAreApplied**: a custom title and size are applied, a preview with only a
  width keeps the default height, and null or empty options give the plain 480 by 640 dialog.
- **NodePropertiesDialog_Options_ArgumentsAreValidated**: null owner and node, a zero, NaN or infinite size, and a factory
  that returns null are rejected; a factory exception propagates (error paths).
- **NodePreviewContext_Constructors_ValidateArguments**: null grid, session, path and property name, a grid with no
  content or an ordinary object, and a session over a reflection source are rejected (error paths).
- **NodePreviewContext_FromGrid_FollowsANewSession**: a grid taken out of its window and put back gets a new session; the
  context is notified and reads a value staged in the new session.
- **NodePreviewContext_FromSession_ReadsAndNotifies**: a context over a host-owned staged session reads the staged value,
  is notified on edit and cancel, and is silent after `Dispose` (called twice) while reads keep working.
