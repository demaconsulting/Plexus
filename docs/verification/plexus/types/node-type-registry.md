### NodeTypeRegistry Unit Verification Design

This document describes how the NodeTypeRegistry unit (node types, port definitions, port types and their registries)
is verified. Requirements are defined in the NodeTypeRegistry Unit Requirements document.

#### Verification Approach

The unit tests use the real registry, node type, port definition and port type classes; nothing is mocked. Summary line
tests use a real node from a small test diagram because the summary function takes a node. The tests live in several
files that each hold part of the evidence: `Types/NodeTypeRegistryTests.cs`, `TypesAndGeometryTests.cs`,
`PortPathTests.cs` (removal), `SharedPortNameTests.cs` (shared port names) and `PropertyBagTests.cs` (summary lines).

#### Test Environment

No setup beyond the standard xUnit v3 test runner on .NET. No external services or files are needed.

#### Acceptance Criteria

All NodeTypeRegistry unit tests pass. Lookups return the registered objects, rejected declarations raise
`ArgumentException` and leave the registry unchanged, and frozen lists raise `NotSupportedException`.

#### Test Scenarios

##### Registration and Lookup (`Plexus-Types-NodeTypeRegistry-Registration`)

- **NodeTypeRegistry_AddGetTryGetContains_WorkAndRejectDuplicates**: add, get, try-get, contains and count work; an
  unknown key fails in `Get`; null is rejected.
- **NodeTypeRegistry_Enumerate_ReturnsRegistrationOrder**: enumeration follows registration order, also after removal.

##### Duplicate Keys (`Plexus-Types-NodeTypeRegistry-DuplicateKeys`)

- **NodeTypeRegistry_AddGetTryGetContains_WorkAndRejectDuplicates**: a second type with the same key is rejected.
- **NodeTypeRegistry_Add_DuplicateKey_KeepsOriginalType**: the message names the key and the original stays.

##### Duplicate Members (`Plexus-Types-NodeTypeRegistry-DuplicateMembers`)

- **NodeTypeRegistry_Add_DuplicatePortOrPropertyNames_ThrowsArgumentException**: duplicate inputs and duplicate
  properties are rejected and the registry stays empty.
- **NodeTypeRegistry_Add_DuplicateNameWithinOutputs_Throws**: duplicate outputs are rejected.
- **NodeTypeRegistry_Add_SameNameOnInputAndOutput_IsAllowed**: an input and an output may share a name.
- **NodeTypeRegistry_Add_RejectedDeclaration_CanBeCorrectedAndRegistered**: a rejected type is not frozen and can be
  corrected and registered.

##### Freezing (`Plexus-Types-NodeTypeRegistry-Freeze`)

- **NodeType_AfterRegistration_ListsAreFrozen**: input, property and output lists reject changes after registration.

##### Removal (`Plexus-Types-NodeTypeRegistry-Removal`)

- **NodeTypeRegistry_Remove_RegisteredKey_RemovesTypeAndReturnsTrue**: removal reports whether the type existed.
- **NodeTypeRegistry_Remove_ThenAddAgain_Works**: a removed key can be registered again; null is rejected.
- **NodeTypeRegistry_Remove_RemovedTypeCannotBeAdded**: a removed type can no longer be added to a page.
- **NodeTypeRegistry_Enumerate_ReturnsRegistrationOrder**: the remaining types keep their order.

##### Naming (`Plexus-Types-NodeTypeRegistry-Naming`)

- **NodeType_Title_DefaultsToLastKeySegment**: the title defaults to the last key segment; an explicit title wins.
- **NodeType_DisplayName_DefaultsToTitle**: the display name falls back to the title when unset, null or empty.
- **NodeType_Constructor_BlankKey_ThrowsArgumentException**: empty and white-space keys are rejected.

##### Port Declarations (`Plexus-Types-NodeTypeRegistry-PortDeclarations`)

- **NodeType_Ports_PreserveDeclarationOrderPerDirection**: names and type keys keep their declared order.
- **PortDefinition_Label_DefaultsToNameUnlessSet**: the label defaults to the name.
- **PortDefinition_Constructor_BlankNameOrTypeKey_ThrowsArgumentException**: four blank combinations are rejected.
- **NodeTypeRegistry_Add_SameNameOnInputAndOutput_IsAllowed**: direction comes from the list a port is declared in.

##### Port Fan-In (`Plexus-Types-NodeTypeRegistry-PortFanIn`)

- **Port_AllowsMultipleWires_DefaultsToFalseForInputsAndTrueForOutputs**: the definition and input ports default to one
  wire, outputs always report that they accept several.
- **DiagramPage_Connect_InputAllowsMultipleWires_AcceptsSeveralWires**: a declaration that opts in is reported on the node's
  port and accepts several wires.

##### Presentation Hints (`Plexus-Types-NodeTypeRegistry-PresentationHints`)

- **NodeType_Presentation_NotDeclared_IsUnspecified**: size, icon, summary, visual and payload hooks are null and the
  type is not resizable by default.
- **NodeType_Presentation_Declared_IsExposed**: declared size, resizable flag, icon and payload type are exposed.
- **NodeType_Category_NotDeclared_IsNull**: a type without a declared category reports none.
- **NodeType_Category_Declared_IsExposedAndKeptByRegistry**: a declared category is exposed and survives registration.

##### Summary Lines (`Plexus-Types-NodeTypeRegistry-SummaryLines`)

- **NodeType_GetSummaryLines_DefaultAndCustom**: flagged properties are listed in order with their units; a custom
  summary function replaces the default.

##### Port Types (`Plexus-Types-NodeTypeRegistry-PortTypeColors`, `Plexus-Types-NodeTypeRegistry-PortTypeDuplicates`)

- **PortTypeRegistry_Get_RegisteredAndUnknownKeys**: registered colors resolve, unknown keys give neutral gray, and a
  duplicate key is rejected.
- **PortTypeRegistry_Get_UnknownKey_ReturnsNeutralGrayWithoutRegistering**: the fallback does not register anything and
  an empty key is rejected.
- **PortTypeRegistry_Add_DuplicateKey_KeepsFirstType**: the first definition stays and the message names the key.

##### Initial Data (`Plexus-Types-NodeTypeRegistry-CreateData`, `NodeDataTests.cs`)

- **NodeType_CreateData_AddNode_GivesEveryNodeFreshData**: each added node gets its own payload; other types are
  untouched.
- **NodeType_CreateData_UndoRedoAdd_KeepsThePayload**: redo restores the same payload instance without calling the factory.
- **NodeType_CreateData_WrongTypeFromFactory_ThrowsInvalidOperation** and
  **NodeType_CreateData_ReturningNull_StartsWithoutData**: a factory result of the wrong type is refused and adds nothing
  (error path); null means no data.
- **NodeType_CreateData_PasteAndDuplicate_FillOnlyMissingData**: pasted and duplicated nodes without data get fresh data,
  those with data get a copy.
- **NodeType_CreateData_Load_KeepsStoredDataIncludingNone**: loading never calls the factory.
