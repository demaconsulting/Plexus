## History Subsystem Verification Design

Scope: Command-based undo/redo history in which every operation is undoable.

### Verification Approach

The History subsystem is verified with subsystem-level tests that drive the history only through its public API,
together with the two public command interfaces, and without a diagram. Each test combines several behaviors the way a
host does: a sequence of edits with undo and redo, a drag gesture with merging and nested rollback, a document load
under suspension, a save-and-diverge editing session, a capacity-limited session and a notification count across a
full workflow. The test commands are small in-memory commands that change a counter-like document, so a passing test
proves the collaboration of the stacks, transactions, merging, suspension, dirty tracking and capacity logic. The
individual behaviors are verified in isolation by the UndoHistory unit tests (see UndoHistory Verification Design).

### Test Environment

- Standard xUnit test runner on the supported .NET runtimes, executed through the repository test project.
- No external services, files or UI; the tests are deterministic and single-threaded.
- Test file: the History subsystem test class in the History folder of the test project.

### Acceptance Criteria

- All subsystem tests pass.
- Every subsystem requirement is linked to at least one passing subsystem test, and all requirements of the UndoHistory
  unit are satisfied.

### Test Scenarios

#### Undo and redo (`Plexus-History-UndoRedo`)

- **History_UndoRedo_ExecuteUndoRedoWorkflow_ReplaysInOrderAndGuardsOperations**: three changes are undone and redone
  in order with correct descriptions, a new change discards the redo entry, a failing undo keeps its entry, and undo and
  clear are refused while a transaction is open.

#### Transactions (`Plexus-History-Transactions`)

- **History_Transactions_DragWithNestedRollback_ProducesOneUndoEntry**: merged commands, a rolled-back inner transaction
  and a following command produce one entry named after the outer transaction, which undoes and redoes as a whole.
- **History_Transactions_RolledBackTransaction_LeavesStateAndHistoryUntouched**: rolling back a transaction restores the
  state and leaves the earlier history entry as it was.

#### Suspension (`Plexus-History-Suspend`)

- **History_Suspend_LoadWorkflow_DropsStaleEntriesAndRestoresDirtyState**: nested suspension applies changes without
  recording, drops stale entries at the end of the outermost scope and keeps a dirty history dirty, while a clean
  history stays clean.

#### Dirty tracking (`Plexus-History-DirtyTracking`)

- **History_DirtyTracking_SaveUndoDiverge_TracksSavedState**: dirty after a change, clean after marking, dirty after
  undo, clean after redo, dirty after diverging, and clean with empty stacks after clearing.

#### Capacity (`Plexus-History-Capacity`)

- **History_Capacity_LimitedHistory_DropsOldestAndRejectsInvalidValues**: only the newest entries stay undoable, the lost
  clean state keeps the history dirty, shrinking applies at once and a capacity of zero is rejected.

#### State notification (`Plexus-History-StateNotification`)

- **History_StateNotification_Workflow_RaisesChangedOncePerStateChange**: execute, undo, redo, mark clean, transaction
  commit, end of suspension and capacity trim each raise exactly one notification; commands inside a transaction raise
  none.
