## NodePropertiesDialog Unit Verification Design

Scope: The `NodePropertiesDialog` helper: the ready-made modal properties dialog of a Plexus node.

### Verification Approach

The dialog is opened with `ShowAsync` from a headless owner window over a real diagram node; the test finds it among the
owner's owned windows, types into the editors of its grid, and clicks OK, Cancel (routed click events) or calls `Accept`.
The results are checked on the node and on the undo history. No mocks. The tests are in `NodePropertiesDialogTests.cs`.

### Test Environment

The headless Avalonia platform of the test project (see the PropertyGridControl verification design).

### Acceptance Criteria

A test passes when the title and rows are right, OK changes the node once in the undo history and completes with true,
Cancel changes nothing and completes with false, and an invalid or vetoed name leaves the dialog open with the reason.

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
