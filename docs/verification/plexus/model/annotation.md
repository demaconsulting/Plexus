### Annotation Unit Verification Design

Scope: Sticky-note and text-box element with resizable bounds and optional fill.

#### Verification Approach

The unit is verified with unit tests that use the real Model classes through the public API (no mocks): annotations
are created with the page API on a diagram with the shared test node types. Undo and redo are checked with the shared
round-trip helper, which compares a full-state dump of the diagram after each step. Tests live in
`AnnotationTests.cs`, `PageCollectionTests.cs`, `DiagramPageMoveTests.cs`, `DiagramPageCreationTests.cs` and `DiagramPageRemoveTests.cs`.

#### Test Environment

Standard xUnit v3 runner on .NET with no external services, files or configuration.

#### Acceptance Criteria

Every scenario below passes on all target frameworks: values equal the expected ones, one undoable history entry
exists per change, none for a no-op, and undo and redo restore the exact prior state.

#### Test Scenarios

**Initial state** (`Plexus-Model-Annotation-Initial`)

- `Annotation_Initial_NewAnnotation_HasTextBoundsAndNoFill`: text and bounds are kept; no fill; not grouped.
- `DiagramPage_AddAnnotation_CreatesAnnotationWithTextAndBounds`: the page API creates the annotation as specified.

**Text** (`Plexus-Model-Annotation-Text`)

- `Annotation_TextAndFill_Set_UndoRedoRoundTrip`: text edits are undoable; null throws.
- `Annotation_Text_SameValue_RecordsNothing`: an unchanged text records nothing.
- `Annotation_Text_EmptyString_IsAccepted`: an empty text is accepted.

**Fill** (`Plexus-Model-Annotation-Fill`)

- `Annotation_TextAndFill_Set_UndoRedoRoundTrip`: setting and clearing the fill is undoable.
- `Annotation_Fill_SameValue_RecordsNothing`: an unchanged fill records nothing.

**Bounds** (`Plexus-Model-Annotation-Bounds`)

- `Annotation_Bounds_Set_UndoRedoRoundTrip`: resizing is undoable and described as a resize.
- `Annotation_Bounds_SameOrInvalid_NoEntryOrThrows`: an unchanged box records nothing; invalid boxes are rejected.
- `Annotation_Bounds_MoveOrResize_DescribedAndReported`: a pure move is described as a move; the box is reported.

**Detached** (`Plexus-Model-Annotation-Detached`)

- `Annotation_Setters_RemovedAnnotation_ThrowInvalidOperationException`: all three setters reject a removed annotation.
- `DiagramPage_Remove_Element_MutationRejectedUntilUndo`: changes work again after undo.

**Change notification** (`Plexus-Model-Annotation-ChangeNotification`)

- `Annotation_PropertyChanged_TextFillAndBounds_RaisedForSetUndoAndRedo`: notifications for set, undo and redo.
