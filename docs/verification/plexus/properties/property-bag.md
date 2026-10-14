### PropertyBag Unit Verification Design

Scope: Per-node property storage with typed values and automatic undo.

#### Verification Approach

The unit is verified with unit tests that create a bag through its owning node: a registry with the standard test node
types (one type declares a property of every kind and a choice property), a diagram, a page and a node. Tests assert the
stored values and their canonical types, the thrown errors, the number of history entries, the undo and redo round trip
of the complete model state, and the raised notifications. Errors are verified together with the absence of side effects
on the values and the history.

#### Test Environment

- Standard xUnit test runner on the supported .NET runtimes, executed through the repository test project.
- No external services, files or UI.
- Test setup depends on the Model (diagram, page, node), Types (registry, node type) and History subsystems as described
  in PropertyBag Design; shared test helpers create the diagram and check undo and redo round trips.
- Test file: the PropertyBag test class in the root of the test project.

#### Acceptance Criteria

- All PropertyBag tests pass.
- Every PropertyBag requirement is linked to at least one passing test.

#### Test Scenarios

##### Defaults (`Plexus-Properties-PropertyBag-Defaults`)

- **PropertyBag_Initial_ContainsDeclaredDefaults**: a new node holds the seven declared defaults.

##### Assignment (`Plexus-Properties-PropertyBag-Assignment`)

- **PropertyBag_Set_AllKinds_StoreValues**: every kind is stored and read back.
- **PropertyBag_Set_CompatibleNumericTypes_Convert**: compatible numbers convert to the declared kind.

##### Validation (`Plexus-Properties-PropertyBag-Validation`)

- **PropertyBag_Set_IncompatibleValues_ThrowArgumentException**: wrong kinds, lossy and out-of-range numbers, infinity
  and null are rejected without changing the value or the history.
- **PropertyBag_Set_ChoiceOutsideOptions_ThrowsArgumentException**: a value outside the choices is rejected and the
  message names the choices.

##### Ad-hoc properties (`Plexus-Properties-PropertyBag-AdHoc`)

- **PropertyBag_Set_AdHocProperty_AllowsPortableKindsOnly**: portable values are accepted without a declaration and
  other values are rejected.

##### Read (`Plexus-Properties-PropertyBag-Read`)

- **PropertyBag_Read_MissingKeysAndConversions_BehaveAsDocumented**: lookup, missing names and typed conversions.
- **PropertyBag_Get_OverflowingConversion_ThrowsInvalidCastException**: an overflowing conversion is an invalid cast.

##### Enumeration (`Plexus-Properties-PropertyBag-Enumeration`)

- **PropertyBag_Enumeration_DeclaredFirstThenAdHocInInsertionOrder**: declaration order, then insertion order.

##### Reset (`Plexus-Properties-PropertyBag-Reset`)

- **PropertyBag_Reset_DeclaredProperty_RestoresDefaultAndRoundTrips**: a declared property returns to its default and
  the reset round-trips through undo and redo.
- **PropertyBag_Reset_AlreadyDefaultOrUnknown_NoEntryOrThrows**: an already-default value records nothing and an
  unknown name is an error.
- **PropertyBag_Reset_AdHocProperty_RemovesAndRestoresPositionOnUndo**: an ad-hoc property is removed and undo restores
  its value and position.

##### Undo (`Plexus-Properties-PropertyBag-Undo`)

- **PropertyBag_Set_DeclaredAndAdHoc_UndoRedoRoundTrip**: assignments of declared and ad-hoc values undo and redo
  exactly as one entry each.

##### No-op (`Plexus-Properties-PropertyBag-NoOp`)

- **PropertyBag_Set_SameValue_RecordsNothing**: assigning the current value, also through a convertible type, records
  nothing.

##### Merge (`Plexus-Properties-PropertyBag-Merge`)

- **PropertyBag_Set_RepeatedInTransaction_MergesIntoOneEntry**: twenty assignments in a transaction form one entry that
  undoes to the original value.

##### Notification (`Plexus-Properties-PropertyBag-Notification`)

- **PropertyBag_PropertyChanged_RaisedWithNameAndIndexerOnSetAndUndo**: the name and the indexer name are reported on
  set and on undo.
- **PropertyBag_Set_RaisesNodeAndDiagramChangedEvents**: the node and the diagram report the change.

##### Removed node (`Plexus-Properties-PropertyBag-RemovedNode`)

- **PropertyBag_Set_OnRemovedNode_ThrowsInvalidOperationException**: assignment on a removed node is rejected.

##### Batch edit (`Plexus-Properties-PropertyBag-BatchEdit`, `PropertyEditingTests.cs`)

- **PropertyBag_TrySetAll_AllValid_AppliesAsOneUndoEntry**: texts and typed values for declared and ad-hoc properties
  apply as one entry that one undo reverts.
- **PropertyBag_TrySetAll_AnyInvalid_ChangesNothingAndReportsAll**: one valid and four invalid entries (text, choice,
  null and a foreign object) change nothing, add no entry and report a message for each rejected name.
- **PropertyBag_TrySetAll_NoEffectiveChange_RecordsNothing** and **PropertyBag_TrySetAll_EmptyName_IsReported**; a
  removed node and null arguments are refused.

##### Port recomputation (`Plexus-Properties-PropertyBag-PortRecompute`)

- **PropertyBag_Set_ProviderProperty_RecomputesPortsInSameEntry**: one entry; undoing it restores the value and the
  ports.
- **PropertyBag_TrySetAll_ProviderProperties_IsOneUndoEntry**: a batch of text and choice values that removes a wired
  input and retypes the remaining one is one exact entry, the wire included.
- **PropertyBag_Reset_ProviderProperty_RestoresDefaultPorts**: resetting the count restores the default inputs.
- **PropertyBag_Set_AdHocProviderValue_RecomputesPortsInSameEntry**: adding an ad-hoc value the provider reads turns
  fan-in on in one exact undo entry; the value stays ad-hoc (no definition).
- **PropertyBag_Reset_AdHocProviderValue_RecomputesPortsInSameEntry**: removing that ad-hoc value turns fan-in off in
  one exact undo entry.
