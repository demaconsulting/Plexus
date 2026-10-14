## Properties Subsystem Verification Design

Scope: Typed, undoable property definitions and per-element property bags.

### Verification Approach

The Properties subsystem is verified with subsystem-level tests that exercise property definitions and property bags
together. Each test registers a node type that declares one property of every kind, creates a diagram with a page and a
node, and then works only through the public property API: reading declarations, defaults and values, assigning
valid and invalid values, enumerating, resetting, undoing, redoing and observing notifications. The registry, diagram
and page are the host a bag needs (see Properties Design); no other model behavior is verified here. The individual
behaviors of each unit are verified in isolation by the PropertyDefinition and PropertyBag unit tests (see their
verification designs).

### Test Environment

- Standard xUnit test runner on the supported .NET runtimes, executed through the repository test project.
- No external services, files or UI; the tests are deterministic and single-threaded.
- Test file: the Properties subsystem test class in the Properties folder of the test project.

### Acceptance Criteria

- All subsystem tests pass.
- Every subsystem requirement is linked to at least one passing subsystem test, and all requirements of the
  PropertyDefinition and PropertyBag units are satisfied.

### Test Scenarios

#### Declaration (`Plexus-Properties-Declaration`)

- **Properties_Declaration_DeclaredTypes_ExposeKindsDefaultsAndChoices**: kinds are inferred for all seven declared
  properties, choices, default and unit are exposed, and undeclared names have no declaration.
- **Properties_Declaration_InvalidDeclarations_AreRejected**: blank names, unsupported or non-finite defaults and bad
  choice lists are rejected.

#### Display (`Plexus-Properties-Display`)

- **Properties_Display_SummaryOfNodeValues_UsesNamesUnitsAndFormats**: summary lines combine display name, current
  value, unit and a custom formatter.

#### Defaults (`Plexus-Properties-Defaults`)

- **Properties_Defaults_NewNode_HoldsEveryDeclaredDefault**: a new node holds exactly the seven declared defaults.

#### Validation (`Plexus-Properties-Validation`)

- **Properties_Validation_Assignments_ConvertRejectAndAllowAdHoc**: compatible numbers convert to the declared kinds,
  invalid values are rejected without a history entry, and ad-hoc properties accept portable kinds only.

#### Access (`Plexus-Properties-Access`)

- **Properties_Access_ReadEnumerateAndReset_BehaveAsDocumented**: typed reads convert or fail, enumeration lists declared
  then ad-hoc properties in order, and reset restores a default or removes an ad-hoc property.

#### Undo and notification (`Plexus-Properties-UndoNotification`)

- **Properties_UndoNotification_EditSession_UndoesRedoesAndNotifies**: real changes create one entry each, a no-op
  records nothing, undo and redo restore values, notifications carry names, a slider-style edit merges into one entry,
  and a removed node rejects edits.

#### Port recomputation (`Plexus-Properties-PortRecompute`)

- **Properties_PortRecompute_PropertyEdits_UpdatePortsInSameEntry**: a set, a batch edit (count and port type), a
  reset, and adding and removing an ad-hoc value of a node whose type has a port provider each change the ports in one
  exact undo entry.
