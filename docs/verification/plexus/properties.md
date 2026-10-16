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

#### Declaration (`Plexus-Properties-Declaration`, `Plexus-Properties-ChoiceDeclaration`)

- **Properties_Declaration_DeclaredTypes_ExposeKindsDefaultsAndChoices**: kinds are inferred for all seven declared
  properties (boolean, both integer sizes, double, string, color), the choice list makes the string property a choice
  property, choices, default and unit are exposed, and undeclared names have no declaration.

#### Declaration validation (`Plexus-Properties-DeclarationValidation`)

- **Properties_Declaration_InvalidDeclarations_AreRejected**: blank names, unsupported or non-finite defaults and bad
  choice lists (default not listed, duplicates, a null entry) are rejected.

#### Display (`Plexus-Properties-Display`)

- **Properties_Display_SummaryOfNodeValues_UsesNamesUnitsAndFormats**: summary lines combine display name, current
  value, unit and a custom formatter.

#### Presentation hints (`Plexus-Properties-PresentationHints`)

- **Properties_PresentationHints_DeclaredHints_AreExposedWithoutRestrictingValues**: display name, description,
  category, unit, editor hint, summary and read-only hints declared on a node type are exposed through the node's
  property declaration, and a value no hint mentions is still accepted (the read-only hint is not enforced).

#### Display order (`Plexus-Properties-DisplayOrder`)

- **Properties_DisplayOrder_DeclaredOrders_KeepDeclarationOrder**: properties declared A, B, C with orders 3, 1, 2 and D
  at the default expose those orders (D: 0), while the node type's property list and the node's property enumeration
  stay A, B, C, D.

#### Text parsing and edit text (`Plexus-Properties-TextParsing`, `Plexus-Properties-EditText`)

- **Properties_TextEditing_TypedText_ParsesAndRoundTrips**: typed text converts to values of exactly the declared kinds
  (a number with its unit, white space around an integer, a color, a choice in other letter case returning the declared
  spelling); a fractional number for an integer property is refused with a reason; the edit text of a stored double
  (0.1 + 0.2) parses back to the identical value.

#### Defaults (`Plexus-Properties-Defaults`)

- **Properties_Defaults_NewNode_HoldsEveryDeclaredDefault**: a new node holds exactly the seven declared defaults.

#### Validation, numeric conversion and ad-hoc values

Requirements: `Plexus-Properties-Validation`, `Plexus-Properties-NumericConversion` and `Plexus-Properties-AdHoc`.

- **Properties_Validation_Assignments_ConvertRejectAndAllowAdHoc**: compatible numbers convert to the declared kinds,
  invalid values (wrong kind, fractional for an integer, value outside the choices, color as text) are rejected without
  a history entry and without changing the value, and ad-hoc properties accept portable kinds only.

#### Batch edit (`Plexus-Properties-BatchEdit`)

- **Properties_BatchEdit_OneInvalidValue_ChangesNothing**: a batch with one invalid value changes no value, records no
  entry and reports the invalid property; the corrected batch applies all values as one undo entry that undoes all of
  them together.

#### Access, enumeration and reset (`Plexus-Properties-Access`, `Plexus-Properties-Enumeration`, `Plexus-Properties-Reset`)

- **Properties_Access_ReadEnumerateAndReset_BehaveAsDocumented**: typed reads convert or fail, enumeration lists declared
  then ad-hoc properties in order, and reset restores a default or removes an ad-hoc property.

#### Undo and notification

Requirements: `Plexus-Properties-UndoNotification`, `Plexus-Properties-NoOpChange`,
`Plexus-Properties-MergeInTransaction`, `Plexus-Properties-ChangeNotification` and `Plexus-Properties-RemovedNode`.

- **Properties_UndoNotification_EditSession_UndoesRedoesAndNotifies**: real changes create one entry each, a no-op
  records nothing, undo and redo restore values, notifications carry names, a slider-style edit merges into one entry,
  and a removed node rejects edits.

#### Port recomputation (`Plexus-Properties-PortRecompute`, `-PortRecomputeReset`, `-PortRecomputeBatch`, `-PortRecomputeAdHoc`)

- **Properties_PortRecompute_PropertyEdits_UpdatePortsInSameEntry**: a set, a batch edit (count and port type), a
  reset, and adding and removing an ad-hoc value of a node whose type has a port provider each change the ports in one
  exact undo entry.
