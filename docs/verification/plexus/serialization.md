## Serialization Subsystem Verification Design

### Verification Approach

The Serialization subsystem is verified by integration tests in `SerializationTests` that drive the two units together
over a real model: a rich two-page diagram (`SerializationFixtures.CreateRichDiagram`) with properties of every kind,
typed and custom node payloads, manual routes, nested and collapsed groups, annotations and a non-default z-order.
State is compared with a full deterministic dump (`TestModel.Dump`) that includes ids, order, bounds, routes,
properties and data, so a single string comparison proves exactness. Hostile content is built as literal text or
generated strings. No mocks are used; node types come from real registries, with one equivalent registry per diagram.
Unit-level behavior is verified in the Snapshot and PlexusClipboard unit verification designs.

### Test Environment

Standard xunit runner of `DemaConsulting.Plexus.Tests` on net8.0, net9.0 and net10.0 on Windows, Linux and macOS. No
files, network or clipboard are used. The limits tests allocate strings of about 8 MiB and a document of 10,001
annotations.

### Acceptance Criteria

All scenarios pass on every target framework. Round trips reproduce the dump exactly; every rejection is a failed
result or the documented exception type, never an unrelated exception; after every rejected paste the dump and undo
count equal their values before the attempt.

### Test Scenarios

- **JSON round trip of a rich diagram**
  (`Serialization_DiagramJson_RichDiagram_RoundTripsExactly`): saving and loading reproduce the dump exactly, with
  empty undo and redo history.
- **Versioned format without type names**
  (`Serialization_Json_IsVersionedAndContainsNoTypeNames`): the snapshot carries its format version and the clipboard
  its format marker and version, and neither text contains `$type` or .NET type names.
- **Type-name members ignored when reading**
  (`Serialization_TypeNameMembers_AreIgnoredWhenReading`): type-name members at the document, envelope and payload
  level do not change what is created; payloads load as the declared type and the paste has no warnings.
- **Invalid snapshots**
  (`Serialization_InvalidSnapshots_ThrowTheDocumentedExceptions`): malformed JSON gives a JSON exception, an
  unsupported version and a wire to an unknown node give a snapshot exception, a valid snapshot loads.
- **Opaque payloads**
  (`Serialization_OpaquePayload_IsOmittedFromSnapshotAndClipboard`): neither the snapshot nor the clipboard text
  holds the payload and a paste creates the node without data.
- **Missing node types**
  (`Serialization_MissingNodeTypes_AreAllReportedByBothEntryPoints`): loading and pasting with a registry lacking two
  types report both keys and change nothing.
- **Copy and paste across diagrams**
  (`Serialization_CopyPaste_AcrossDiagrams_ReproducesContentAsOneUndoStep`): the same numbers of nodes, wires,
  annotations and groups appear with new ids as one undo entry that undo removes completely.
- **Duplicate and paste share behavior**
  (`Serialization_Duplicate_AndCopyPaste_ShareTheCloneBehavior`): duplicating with a one-grid-step offset and
  copy-then-paste at the default offset give the same names, bounds and routes.
- **Invalid clipboard content**
  (`Serialization_InvalidClipboardContent_FailsWithoutChangingTheDiagram`): non-JSON, an empty object, an unknown
  type, out-of-range geometry and a dangling wire all fail without exception and without changing dump or history.
- **Hostile limits**
  (`Serialization_HostileClipboardLimits_AreRejectedWithoutExceptions`): oversize text, 500 nesting levels and 10,001
  elements are rejected with error messages and the page stays empty with unchanged history.

- **Per-node ports on load, paste and duplicate** (`Plexus-Serialization-DynamicPorts`,
  `Plexus-Serialization-DynamicPortsPaste`, `Plexus-Serialization-DynamicPortsDuplicate`;
  `Serialization_DynamicPorts_RoundTripAndPaste_RecomputePortsBeforeWires`): a diagram with wires on computed ports
  loads from JSON identical to the original, pastes into another diagram with both wires, and duplicates in place with
  both wires.
- **No stored ports** (`Plexus-Serialization-NoStoredPorts`; `Serialization_DynamicPorts_Save_StoresNoPortLists`): no
  node entry of the saved JSON or of the copied clipboard text (eight entries) has a port, inputs or outputs member.
- **Wire to a port that is not recomputed** (`Plexus-Serialization-DynamicPortUnknownPort`;
  `Serialization_DynamicPorts_WireToPortNotRecomputed_IsRejected`): content wired to `Bus.in2`, read with a bus that
  only has two inputs, makes the load throw a snapshot exception and the paste fail, both naming the missing input; the
  target page stays empty (error path).
- **Provider failure** (`Plexus-Serialization-DynamicPortProviderFailure`;
  `Serialization_DynamicPorts_ProviderFails_ReportsImportError`): a provider throwing `NotSupportedException` makes the
  load throw a snapshot exception and the paste fail, both with the provider's message; page and history unchanged
  (error path).

Boundary conditions covered: limits just exceeded (oversize text, element count, nesting), empty and minimal
documents, equivalent but separate registries. Error paths: every rejection above.
