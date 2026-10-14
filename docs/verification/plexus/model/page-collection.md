### PageCollection Unit Verification Design

Scope: Ordered, observable collection of pages with undoable add, insert, remove and move.

#### Verification Approach

The PageCollection unit is verified with unit tests that use a real diagram created from the shared test fixture, because
the collection cannot exist without its owning diagram, history and pages and all of them are in-memory and
deterministic. Tests call the public operations and assert on the page order, the page attached state, the diagram's
element lookup, the undo history, and the notifications received, including the state observed from inside a
notification handler.

#### Test Environment

Standard xUnit v3 test runner on .NET (net8.0, net9.0 and net10.0). No external services, files or configuration.

#### Acceptance Criteria

All scenarios below pass on every target framework, and every test method named here is matched to its requirement by the
ReqStream trace matrix.

#### Test Scenarios

##### Add, insert, remove, move (`Plexus-Model-PageCollection-AddInsert`, `-Remove`, `-Move`)

- **PageCollection_AddAndInsert_PlacePagesAndRoundTrip**: add appends, insert places at the index.
- **PageCollection_AddAndInsert_InvalidArguments_Throw**: blank names and out-of-range indexes throw.
- **PageCollection_Remove_DetachesContentAndUndoRestoresIt**: removal detaches the page and its content.
- **PageCollection_Remove_PageNotInDiagram_ReturnsFalse**: an unknown page yields false.
- **PageCollection_Move_ReordersPagesAndRoundTrips**: reordering, no-op for the same position, invalid page or index
  rejected.

##### Undo (`Plexus-Model-PageCollection-Undo`)

- **PageCollection_AddAndInsert_PlacePagesAndRoundTrip**, **PageCollection_Remove_DetachesContentAndUndoRestoresIt** and
  **PageCollection_Move_ReordersPagesAndRoundTrips**: each operation is one history entry and undo and redo restore the
  exact order and, for removal, the same page object and content.

##### Notification (`Plexus-Model-PageCollection-Notification`, `-NotificationOrder`)

- **PageCollection_CollectionChanged_RaisedForAddRemoveMove**: add, remove and move raise notifications with the
  collection as sender.
- **PageCollection_CollectionChanged_ReportsAddMoveRemoveWithListInSync**: the reported change matches the list content.
- **PageCollection_Remove_PageIsDetachedBeforeNotifications**: a handler sees the removed page detached.
- **PageCollection_UndoRemove_PageIsAttachedBeforeNotifications**: a handler sees the restored page attached.

##### List binding (`Plexus-Model-PageCollection-ListBinding`)

- **PageCollection_AsIList_ReflectsPagesAndIsReadOnly**: the non-generic list reflects the pages and is read-only.
- **PageCollection_AsIList_Mutators_ThrowNotSupported**: every non-generic mutator throws.

**Expected**: each test passes, proving the PageCollection requirements.
