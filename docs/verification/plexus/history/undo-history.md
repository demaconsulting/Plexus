### UndoHistory Unit Verification Design

Scope: Per-diagram undo and redo stacks, transactions, mergeable commands and dirty tracking.

#### Verification Approach

The unit is verified with isolated unit tests that construct the history directly and execute small lambda-based and
mergeable test commands against a trivial in-memory counter. Each test follows arrange, act and assert on the stacks,
the descriptions, the counter state, the dirty flag and the thrown errors. Error paths are verified by asserting the
exact error type and that the history remains consistent afterwards. One test builds a diagram under suspension to
prove the load scenario end to end; it is not used as requirement evidence.

#### Test Environment

- Standard xUnit test runner on the supported .NET runtimes, executed through the repository test project.
- No external services, files or UI; the tests are deterministic and single-threaded.
- Test file: the UndoHistory test class in the root of the test project.

#### Acceptance Criteria

- All UndoHistory tests pass.
- Every UndoHistory requirement is linked to at least one passing test.

#### Test Scenarios

##### Execute (`Plexus-History-UndoHistory-Execute`)

- **UndoHistory_Execute_LambdaCommand_AppliesAndRecords**: the change is applied and recorded with its description.
- **UndoHistory_Execute_DoThrows_NothingRecorded**: a failing command propagates its error and is not recorded.
- **UndoHistory_Execute_NullArguments_ThrowArgumentNullException**: every null argument of both overloads is rejected.

##### Undo and redo (`Plexus-History-UndoHistory-UndoRedo`)

- **UndoHistory_UndoRedo_RoundTripsState**: undo and redo move entries between the stacks and restore the state.
- **UndoHistory_UndoRedo_EmptyStacks_ReturnFalse**: undo and redo on empty stacks return false.
- **UndoHistory_Execute_AfterUndo_ClearsRedo**: executing after an undo discards the redo entries.

##### Transactions (`Plexus-History-UndoHistory-Transactions`)

- **UndoHistory_Transaction_Commit_CreatesSingleEntry**: several commands form one entry with the transaction text.
- **UndoHistory_Transaction_Empty_RecordsNothing**: an empty transaction records nothing and stays clean.
- **UndoHistory_Transaction_Nested_FlattensIntoOuterEntry**: nested transactions form one outer entry.

##### Rollback (`Plexus-History-UndoHistory-Rollback`)

- **UndoHistory_Transaction_Rollback_RevertsAndRecordsNothing**: rollback reverts the commands and records nothing.
- **UndoHistory_Transaction_InnerRollback_KeepsOuterCommands**: an inner rollback leaves the outer commands.
- **UndoHistory_Transaction_InnerRollback_DoesNotMergeIntoOuterCommand**: an inner rollback never reaches commands
  merged before it began.

##### Merging (`Plexus-History-UndoHistory-Merging`)

- **UndoHistory_Transaction_MergeableCommands_MergeIntoOne**: repeated mergeable commands in a transaction form one entry
  that undoes and redoes the combined effect.
- **UndoHistory_Execute_MergeableOutsideTransaction_DoesNotMerge**: outside a transaction commands stay separate.

##### Guards (`Plexus-History-UndoHistory-Guards`)

- **UndoHistory_Transaction_DisposedOutOfOrder_ThrowsInvalidOperationException**: the outer transaction cannot complete
  before the inner one, and a retry succeeds.
- **UndoHistory_Transaction_CompletedTwice_RollbackThrowsDisposeIgnored**: a second dispose is ignored, a rollback after
  completion is an error.
- **UndoHistory_UndoRedoClear_InsideTransaction_ThrowInvalidOperationException**: undo, redo and clear are rejected while
  a transaction is open.
- **UndoHistory_Execute_DuringUndo_ThrowsInvalidOperationException**: recording from inside an undo is rejected.
- **UndoHistory_Undo_UndoThrows_EntryStaysOnUndoStack**: a failing undo keeps its entry and the history stays usable.

##### Suspension (`Plexus-History-UndoHistory-Suspend`)

- **UndoHistory_Suspend_ChangesAppliedButNotRecorded**: suspended changes apply without being recorded.
- **UndoHistory_Suspend_Nested_ResumesAfterLastScope**: recording resumes after the last scope; double disposal is safe.
- **UndoHistory_Suspend_CleanHistory_StaysClean**: a clean history stays clean and empty.
- **UndoHistory_Suspend_DirtyHistory_StaysDirtyAndClearsStacks**: a dirty history stays dirty with empty stacks.
- **UndoHistory_Suspend_Nested_OnlyOutermostScopeClearsHistory**: only the outermost scope drops the entries and
  restores the dirty state.

##### Dirty tracking (`Plexus-History-UndoHistory-DirtyTracking`)

- **UndoHistory_IsDirty_TracksCleanPointThroughUndoRedo**: leaving and returning to the clean point toggles the flag.
- **UndoHistory_IsDirty_NewCommandAfterUndoPastCleanPoint_StaysDirty**: diverging from the clean point stays dirty.
- **UndoHistory_IsDirty_CleanPointTrimmedByCapacity_StaysDirty**: a clean point lost to the capacity limit stays dirty.

##### Clear (`Plexus-History-UndoHistory-Clear`)

- **UndoHistory_Clear_EmptiesStacksAndMarksClean**: both stacks are emptied and the history is clean.

##### Capacity (`Plexus-History-UndoHistory-Capacity`)

- **UndoHistory_Capacity_Exceeded_DropsOldestEntries**: only the newest entries remain undoable.
- **UndoHistory_Capacity_Lowered_TrimsAndValidates**: lowering trims at once, the default is 1000, zero is rejected.

##### Change notification (`Plexus-History-UndoHistory-Changed`)

- **UndoHistory_Changed_RaisedForStateChanges**: execute, undo, redo, mark clean and clear each raise the event.
- **UndoHistory_Suspend_Disposed_RaisesChanged**: ending a suspension raises the event once.
- **UndoHistory_Capacity_Lowered_RaisesChanged**: a capacity trim raises the event and a non-trimming change does not.

##### Transact (`Plexus-History-UndoHistory-Transact`)

- **UndoHistory_Transact_Success_RecordsOneEntry**: several edits become one entry with the given description.
- **UndoHistory_Transact_Throws_RollsBackAndRethrows**: a refused edit after an accepted one leaves the model dump, the
  undo count and the clean state exactly as before, and the original exception reaches the caller.
- **UndoHistory_TransactOfT_ReturnsResultAndRollsBackOnFailure**, **UndoHistory_Transact_Nested_FlattensIntoOuterEntry**
  and **UndoHistory_Transact_EmptyAndNullArguments** (`UndoHistoryTransactTests.cs`): the function overload, nesting
  with a caught inner failure, an empty action and null arguments.

##### Aborting a multi-step operation (`Plexus-History-UndoHistory-PlanAbort`)

- **CommandPlan_Abort_RevertsEveryStepNewestFirstAndClears**: three applied steps are reverted in the order c, b, a and
  the plan is empty afterwards.
- **CommandPlan_Abort_InverseThrows_RevertsRemainingStepsAndReportsAllFailures**: when the middle step cannot be
  reverted the other two are still reverted; an `AggregateException` carries the original error first and the revert
  failure second, and the plan is empty (error path).

##### History changes during replay

Requirements: `Plexus-History-UndoHistory-ClearDuringReplay`, `Plexus-History-UndoHistory-StateChangeDuringReplay` and
`Plexus-History-UndoHistory-SuspendEndDuringReplay`.

- **UndoHistory_Clear_FromUndoCallback_ThrowsAndKeepsStacks** and **UndoHistory_Clear_FromRedoCallback_ThrowsAndKeepsStacks**:
  `Clear` called from an undo (respectively redo) action throws `InvalidOperationException`; the undo or redo still moves
  its entry normally and the earlier entry stays undoable (error path).
- **UndoHistory_MarkCleanCapacityAndSuspend_FromUndoCallback_AreRejected**: `MarkClean`, setting `Capacity` and
  `Suspend` from an undo action each throw `InvalidOperationException`; capacity, suspension and the dirty flag are those
  of a plain undo (error path).
- **UndoHistory_SuspendScopeDisposedFromUndoCallback_TakesEffectAfterUndo**: disposing the outermost suspension from an
  undo action does not throw; the stacks are unchanged inside the action and cleared right after the undo.

##### Failure-atomic grouped replay (`Plexus-History-UndoHistory-ReplayAtomic`, `Plexus-History-UndoHistory-ReplayCompensationFailure`)

A transaction of three commands adding 1, 10 and 100 to a counter, whose middle command can be told to fail:

- **UndoHistory_Redo_MiddleCommandFails_RestoresStateAndKeepsEntry**: a failing redo re-reverts the first command
  (counter 0), keeps the entry on the redo stack, and the retry gives 111 (error path).
- **UndoHistory_Undo_MiddleCommandFails_RestoresStateAndKeepsEntry**: a failing undo re-applies the last command
  (counter 111), keeps the entry on the undo stack, and the retry gives 0 (error path).
- **UndoHistory_Undo_MiddleCommandAndCompensationFail_ReportsAllFailures**: when re-applying the last command fails as
  well, an `AggregateException` carries the middle command's failure first and the compensation failure second, and the
  entry stays on the undo stack (error path).
