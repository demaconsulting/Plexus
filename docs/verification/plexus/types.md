## Types Subsystem Verification Design

This document describes how the Types subsystem (host-declared node types, port definitions, port types and icons) is
verified as a whole. Requirements are defined in the Types Subsystem Requirements document; the individual units are
verified in the NodeTypeRegistry and IconSource verification documents.

### Verification Approach

The subsystem tests in `Types/TypesTests.cs` exercise the NodeTypeRegistry and IconSource units together through the
public API, as a host would use them: they declare port types, node types and icons, register them, and read them
back. Where a requirement concerns how a declaration is used (default ports and size, availability of a type), a test
creates a `Diagram` over the registry and adds a node, which makes the Model subsystem a real collaborator. Nothing is
mocked. Unit tests are not reused as subsystem evidence.

### Test Environment

No setup beyond the standard xUnit v3 test runner on .NET. The tests use in-memory declarations only, with no external
services or files.

### Acceptance Criteria

All Types subsystem tests pass: declarations read back exactly as declared, invalid declarations are rejected without
changing the registry, registered types cannot be modified, icons with equal content are equal, and nodes created from
a type reflect its ports and size.

### Test Scenarios

#### Declaration (`Plexus-Types-Declaration`)

- **Types_Declaration_PortTypesNodeTypesAndIcon_AreRetrievable**: a port type, a node type with an icon, ports and a
  property are registered and read back, including the derived title, the label, the port color and the enumeration.

#### Validation (`Plexus-Types-Validation`)

- **Types_Validation_DuplicateKeyAndMembers_AreRejectedLeavingRegistryUnchanged**: a duplicate node type key, a
  duplicate input name, a duplicate output name, a duplicate property name and a duplicate port type key are each
  rejected, and the original declarations remain.
- **Types_Validation_RegisteredType_CannotBeModified**: after registration (and with a live node) the input, output
  and property lists reject changes.

#### Icons (`Plexus-Types-Icons`)

- **Types_Icons_SameSvgOnTwoNodeTypes_SharesKeyAndEquality**: two node types with icons made separately from the same
  SVG have equal icons and keys; a different SVG differs; a type without an icon reports none.

#### Node Defaults (`Plexus-Types-NodeDefaults`)

- **Types_NodeDefaults_AddNode_UsesDeclaredPortsAndSize**: a node added from a type with a declared size gets that size
  and its ports in declaration order with their types, labels and directions.

#### Availability (`Plexus-Types-Availability`)

- **Types_Availability_RemoveAndReRegister_ControlsNodeCreation**: after a type is removed nodes can no longer be
  created from it, and after it is registered again they can.

#### Port provider (`Plexus-Types-PortProvider`)

- **Types_PortProvider_DeclaredOnType_SuppliesPerNodePorts**: two nodes of one type with a port provider have different
  ports after configuring one of them, while the type's declared ports and its provider are unchanged.

#### Descriptions (`Plexus-Types-Descriptions`)

- **Types_Descriptions_PortNodeAndPortType_AreExposed**: a port description, a node type description (as the node's
  tooltip text) and a port type display name are returned as declared.
