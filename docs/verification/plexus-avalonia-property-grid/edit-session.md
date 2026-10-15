## EditSession Unit Verification Design

Scope: Applies edits to the edited object immediately or staged until commit, with per-property errors, a commit scope
hook and collection editing through a working copy.

### Verification Approach

Sessions are created over reflection sources of the original `Person` test object (and custom items that record the
order of their writes), and the target object is inspected after each step, because the behavior that matters is when
and in what order the target changes. Commit scopes are small delegates that record when they run. A frozen collection
test double makes a collection write fail. No mocks are used. The tests are in `EditSessionTests.cs` (values, modes,
scope, errors, paths), `EditSessionCollectionTests.cs` (collection editing) and `EditSessionUiSupportTests.cs`
(replacing the working copy and reporting an editor's error).

### Test Environment

The standard test runner.

### Acceptance Criteria

A test passes when the target changes exactly when documented (immediately in live mode, only at commit in staged mode,
never on cancel), edits are applied in the documented order, failures are returned as results and recorded as errors
without exceptions, a failed edit stays pending, and the commit scope wraps the writes as documented.

### Test Scenarios

Values, modes, scope and errors (`EditSessionTests.cs`):

- **PropertyEditSession_Live_SetValueWritesImmediately**: a live edit writes at once, nothing is pending, one applied
  event.
- **PropertyEditSession_Live_SetTextRecordsAndClearsErrors**: invalid text is an error until a later edit succeeds.
- **PropertyEditSession_Live_ReadOnlyInvalidAndThrowingSettersFail**: read-only, invalid and throwing edits fail with
  messages (error paths).
- **PropertyEditSession_Live_CommitScopeWrapsEachWrite**: each live write runs inside the scope.
- **PropertyEditSession_Live_CommitScopeFailuresAreReported**: a scope that skips or throws fails the edit (error path).
- **PropertyEditSession_Live_ResetRestoresTheDefault**: live reset, and refusal for an item without a default.
- **PropertyEditSession_Live_RefreshPropertiesAllRequestsRefresh**: the refresh-all flag of the change event.
- **PropertyEditSession_Staged_SetValueBuffersWithoutTouchingTheTarget**: pending value shown, target untouched.
- **PropertyEditSession_Staged_CommitAppliesAndClearsTheEdits**: commit writes, clears and reports.
- **PropertyEditSession_Staged_CancelDiscardsEverything**: cancel drops edits and errors.
- **PropertyEditSession_Staged_CommitAppliesInFirstEditOrder**: write order is the order of first edit.
- **PropertyEditSession_Staged_SettingBackToTheCurrentValueRemovesTheEdit**: no pending edit for an unchanged value.
- **PropertyEditSession_Staged_ValidationErrorsAreNotBuffered**: invalid edits are not buffered (error paths).
- **PropertyEditSession_Staged_CommitFailureKeepsTheFailedEditPending**: a failing edit stays pending, others apply, a
  retry fails again (error path).
- **PropertyEditSession_Staged_CommitScopeIsCalledOnceForAllEdits**: one scope call per commit, none when nothing pending.
- **PropertyEditSession_Staged_CommitScopeFailureKeepsAllEditsPending**: a throwing or skipping scope commits nothing
  (error path).
- **PropertyEditSession_Staged_NestedPropertyPathsAreBuffered**: nested and struct child edits are buffered and applied.
- **PropertyEditSession_Staged_CollectionElementPathsAreBuffered**: element edits are buffered and applied.
- **PropertyEditSession_Staged_ResetIsBufferedAndApplied**: staged reset shows the default and applies.
- **PropertyEditSession_Staged_ValueEditReplacesAPendingReset**: a value edit replaces a pending reset.
- **PropertyEditSession_Staged_DiscardRemovesOneItem**: discard affects one item only.
- **PropertyEditSession_Staged_CommitOfRefreshPropertyRequestsRefresh**: commit asks for a full refresh.
- **PropertyEditSession_FindItem_ResolvesPathsAndRejectsBadOnes**: path resolution and bad paths (error paths).
- **PropertyEditSession_Arguments_AreValidated**: null arguments are rejected (error paths).

Collection editing (`EditSessionCollectionTests.cs`):

- **CollectionEditor_Staged_ListEditsApplyInPlaceOnCommit**: add, remove, move and replace in a working copy, target
  untouched until commit, then the same list instance refilled.
- **CollectionEditor_Staged_SameEditorWhilePendingAndInactiveAfterCommit**: the same editor while pending; inactive after
  commit (error path).
- **CollectionEditor_Staged_CancelEndsTheEditor**: cancel ends the editor (error path).
- **CollectionEditor_Staged_BackToTheTargetRemovesThePendingEdit**: an unchanged working copy is not pending.
- **CollectionEditor_Add_CreatesDefaultElements**: default elements for classes, strings and value types.
- **CollectionEditor_Add_UsesTheFactoryWhenThereIsNoDefaultConstructor**: missing factory, factory and throwing factory
  (error paths).
- **CollectionEditor_Array_ReadOnlyPropertyAllowsOnlyInPlaceChanges**: reorder in place, no resize (error paths).
- **CollectionEditor_Array_SettablePropertyReceivesANewArray**: a longer array is assigned.
- **CollectionEditor_ReadOnlyList_RejectsEveryChange**: a read-only list rejects changes (error paths).
- **CollectionEditor_GenericCollection_IsRefilledInPlace**: a set is refilled in place.
- **CollectionEditor_NullCollection_IsReplacedByANewList**: a null collection gets a new list.
- **CollectionEditor_InvalidArguments_AreRejected**: bad indices, null and wrongly typed elements (error paths).
- **CollectionEditor_Staged_ValueEditReplacesThePendingCollectionEdit**: a value edit replaces the collection edit.
- **CollectionEditor_Staged_ElementEditsAndStructuralEditsCommitTogether**: element and structural edits commit together.
- **CollectionEditor_Live_ChangesApplyImmediately**: live changes write at once.
- **CollectionEditor_Live_FailureRestoresTheWorkingCopy**: a failed live write restores the working copy (error path).
- **CollectionEditor_SetItems_Live_WritesOnceInsideScope** / **CollectionEditor_SetItems_Staged_RecordsPendingEdit**: the
  whole working copy is replaced in one live write inside one scope call, or recorded as one pending edit.
- **CollectionEditor_SetItems_ValidatesSizeAndElementType**: a fixed-size array refuses another length but reorders, an
  element of the wrong type and a null list are rejected (error paths). These tests are in `EditSessionUiSupportTests.cs`.
- **PropertyEditSession_ReportError_RecordsAndClearsError**: an editor's own rejection is recorded, raises the error
  change and clears on discard; null arguments are rejected (error paths).

Source commit scope and refusals (`CommitScopeTests.cs`; a test source records the steps its scope performs):

- **PropertyEditSession_Commit_SourceScopeIsOutsideTheHostScope**: a staged commit runs the source's scope once around
  the host's scope around the write.
- **PropertyEditSession_LiveEdit_RunsTheSourceScopePerEdit**: each live edit runs the source's scope once, without a host
  scope.
- **PropertyEditSession_Commit_RefusedExceptionMarksTheNamedProperty** /
  **PropertyEditSession_LiveEdit_RefusedExceptionIsAnInlineError**: a `PropertyCommitRefusedException` records the
  reason as the named property's error, commits nothing and keeps the edit pending (error paths).
- **PropertyEditSession_Commit_SourceScopeThatSkipsTheActionFails**: a source scope that never runs the writes is
  reported (error path).
- **PropertyCommitRefusedException_Constructor_KeepsPathAndMessage**: the exception carries the path and rejects a blank
  path (error path).
- **PropertyEditSession_Commit_RefusedExceptionForAnUneditedRowIsListedAsFailed**
  (`PlexusAvaloniaPropertyGrid-EditSession-CommitFailures`): a refusal naming a row that was not edited is listed in
  `CommitResult.Failed` with its path and message, and the edit that was made stays pending
  (`PropertyEditSession_Commit_RefusedExceptionMarksTheNamedProperty` checks the same list, `Applied` 0 and the item).
- **PropertyEditSession_Commit_RefusalAfterWrites_RestoresTheTargetAndKeepsEdits**
  (`PlexusAvaloniaPropertyGrid-EditSession-CommitRestore`): two value edits and a collection edit are written, then the
  scope refuses; the name, the age and the list are back to their old values, `Applied` is 0, `Failed` lists the refused
  row and all three edits are still pending (error path).
- **PropertyEditSession_Commit_RefusalAfterPartialFailedWrite_RestoresTheTarget**
  (`PlexusAvaloniaPropertyGrid-EditSession-CommitRestoreFailedWrite`): the setter of the first edit writes its field and
  then throws, and the scope refuses the second edit after the writes ran; both fields are back to their old values,
  `Applied` is 0 and both edits are still pending (error path).
- **PropertyEditSession_Commit_RefusalAfterScopeRolledBack_WritesNothingAgain**: a scope that rolled its write back
  itself before refusing causes no second write (the setter log shows one write).
- **PropertyEditSession_Commit_RefusalAfterWrites_ReportsAValueThatCouldNotBeRestored**: when the setter rejects the
  restoring write, `CommitResult.Error` names the property (error path).
- **PropertyEditSession_SetText_ArgumentExceptionMessage_HasNoParameterSuffix**: an argument exception from a setter or
  a
  converter produces a message without " (Parameter 'value')" (error path).
- **PropertyEditSession_FindItem_PrefixedRootPaths_AreResolvedByTheirFullPath**
  (`PlexusAvaloniaPropertyGrid-EditSession-PrefixedRoots`): a root item is found by its whole path, unknown and empty
  paths give null.
