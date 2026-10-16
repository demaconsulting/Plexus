### Snapshot Unit Verification Design

#### Verification Approach

Unit tests in `SnapshotTests` and `DiagramJsonTests` capture, serialize, parse and restore real diagrams built from
the shared fixtures (`SerializationFixtures`: standard types plus a type with a declared data type, a type with a
custom `INodeDataSerializer`, and a type with no way to serialize a payload). Results are compared through the full
model dump or through parsed JSON (`JsonDocument`) for the shape. Tampered snapshots are built with record `with`
expressions. The history and model units are real; nothing is mocked because the snapshot is meant to be proven
against the real model.

#### Test Environment

Standard xunit runner; no files, network or clipboard.

#### Acceptance Criteria

All scenarios pass on every target framework. Restored diagrams dump equal to the original; serializing a restored
diagram gives the same text byte for byte; every invalid input produces the documented exception type with a message
naming the problem.

#### Test Scenarios

Round trips:

- **Rich diagram through the snapshot**
  (`Snapshot_RoundTrip_RichDiagram_RestoresEverythingExactly`) and **through compact and indented JSON**
  (`Snapshot_RoundTrip_ThroughJson_RestoresEverythingExactly`, theory over both forms).
- **Property kinds**
  (`Snapshot_RoundTrip_PropertiesOfEveryKind_KeepKindAndValue`): value and CLR type of every kind, including ad hoc
  and extreme values.
- **Payloads** (`Snapshot_RoundTrip_TypedAndCustomData_AreRestored`), **z-order**
  (`Snapshot_RoundTrip_ZOrder_IsPreserved`) and **nested collapsed groups**
  (`Snapshot_FromSnapshot_NestedCollapsedGroups_AreRestored`).
- **Diagram JSON shorthand**
  (`Diagram_ToJsonFromJson_RoundTripsContent`, `Diagram_ToJson_MatchesPlexusJsonAndIndentFlag`).
- **Page capture**
  (`Snapshot_PageToSnapshot_MatchesDiagramEntry`).
- **Opaque data omitted**
  (`Snapshot_ToSnapshot_OpaqueData_IsOmitted`).

Restored state:

- **Clean history** (`Snapshot_FromSnapshot_HistoryIsEmptyAndClean`), **editable**
  (`Snapshot_FromSnapshot_RestoredDiagramIsEditable`) and **stored routes kept**
  (`Snapshot_FromSnapshot_RoutesAreNotRerouted`).

Rejection:

- **Missing types** (`Snapshot_FromSnapshot_MissingNodeTypes_ListsAllOfThem`) and **unsupported version**
  (`Snapshot_UnsupportedVersion_IsRejected`).
- **Invalid content** (`Snapshot_FromSnapshot_InvalidContent_ThrowsSnapshotException`), **duplicate ids across pages**
  (`Snapshot_FromSnapshot_DuplicateIdAcrossPages_Throws`) and **property kind mismatch**
  (`Snapshot_FromSnapshot_PropertyKindMismatch_Throws`).
- **Node without a type key** (`Plexus-Serialization-Snapshot-RejectInvalidContent`;
  `Snapshot_FromJson_NodeWithoutTypeKey_ThrowsSnapshotException`, theory over a null, an empty and a white-space key):
  the load throws a snapshot exception saying the node has no node type key, lists no missing type, and does the same
  when unknown node types are skipped (boundary between invalid content and unknown type).
- **Unreadable payload** (`Plexus-Serialization-Snapshot-PayloadReadFailure`;
  `Snapshot_Load_SerializerThrowsUnexpectedException_ThrowsSnapshotException`): a payload serializer throwing a host
  exception type the library does not know makes the load throw a snapshot exception carrying its message (error
  path).
- **Malformed JSON** (`PlexusJson_Deserialize_Malformed_ThrowsJsonException`, theory over several malformed texts),
  **too deep** (`PlexusJson_Deserialize_TooDeep_ThrowsJsonException`) and **invalid diagram JSON**
  (`Diagram_FromJson_InvalidInput_Throws`).

JSON shape:

- **Stable shape**
  (`PlexusJson_Serialize_ProducesStableShape`): member names, kinds, color text, embedded payloads, no type names.
- **Indentation**
  (`PlexusJson_Serialize_IndentedOptionControlsWhitespace`).
- **Unknown members ignored**
  (`PlexusJson_Deserialize_UnknownMembers_AreIgnored`), including a type-name member.
- **Six- and eight-digit colors** (`Plexus-Serialization-Snapshot-ColorText`;
  `Snapshot_FromJson_SixAndEightDigitColors_AreRead`): the writer emits `"#FF445566"` for an opaque and `"#80112233"`
  for a translucent color; after the opaque one is rewritten as `"#445566"` both load as the original colors.

Boundary values: extreme numeric values, nested groups, empty history. Error paths: all rejection scenarios.

Format-neutral tree, streams and skipping unknown types (`DiagramTreeTests`, `DiagramStreamTests`, `UnknownNodeTypeTests`):

- **Tree** (`Plexus-Serialization-Snapshot-Tree`): `Diagram_ToTree_HoldsOnlyPlainValues` walks the whole tree and fails
  on any value outside the vocabulary; `Diagram_ToTree_UsesPlainKindsColorsAndNestedData` and
  `Diagram_ToTree_WholeDoubleProperty_IsADouble` check the documented value forms;
  `Diagram_ToTreeFromTree_RoundTripsLikeJson` compares the model dump and the JSON text after a round trip;
  `Diagram_ToTree_IsACopy` shows the diagram is unaffected by changes to the tree;
  `Diagram_FromTree_ToleratesForeignNumbersMapsAndLists` and
  `Diagram_FromTree_PairSequencesAndJsonElements_AreAccepted` feed int, float, decimal, ushort, a non-generic
  dictionary, read-only dictionaries, arrays, bare key/value sequences and `JsonElement` values;
  `Diagram_FromTree_InvalidTree_ThrowsSnapshotException` covers an unsupported type, NaN, a non-string key, a wrongly
  shaped tree, a bad version, nesting beyond the limit and null arguments; `Diagram_FromTree_MinimalTrees_Load`.
- **Streams** (`Plexus-Serialization-Snapshot-Streams`): a round trip that keeps the stream open (a stream that
  records disposal), the same text as `ToJson`, a byte-order mark, and the error cases.
- **Skipping** (`Plexus-Serialization-Snapshot-SkipUnknownTypes`): the strict default still lists the types; skipping
  drops nodes, wires and memberships and fills `LoadReport` (types, counts and message); a group with remaining
  members stays; groups that become empty disappear repeatedly; nothing missing gives the empty report and an
  identical diagram; the tree loader skips too.

- **Per-node ports** (`Plexus-Serialization-Snapshot-DynamicPorts`):
  `Snapshot_RoundTrip_WireOnDynamicPort_RestoresWireJsonAndTree` restores wires on computed ports through JSON and the
  tree identically (ports are not written to the JSON); `Snapshot_FromSnapshot_WireOnDynamicPort_RestoresWire` does
  the same directly from the snapshot object (`P.value -> Bus.in2`, `R.z -> C.in`).
- **Stored bounds kept** (`Plexus-Serialization-Snapshot-DynamicPortBounds`;
  `Snapshot_Load_DynamicPortNode_KeepsStoredBounds`): a bus the user made 300 units taller than its three ports need
  loads with exactly that size and still three inputs.
- **No port lists** (`Plexus-Serialization-Snapshot-NoStoredPorts`; `Snapshot_ToJson_DynamicPortNode_StoresNoPortLists`):
  no node entry of the JSON has a port, inputs or outputs member.
- **Missing computed port** (`Plexus-Serialization-Snapshot-DynamicPortMissing`;
  `Snapshot_Load_WireToPortProviderDoesNotProduce_ThrowsSnapshotException`): a document whose wire names an input the
  loading registry's provider does not produce is refused, naming the port (error path).
- **Provider failure** (`Plexus-Serialization-Snapshot-DynamicPortProviderFailure`):
  `Snapshot_Load_ProviderThrows_ThrowsSnapshotException` (an invalid-operation exception),
  `Snapshot_Load_ProviderThrowsUnlistedException_ThrowsSnapshotException` (a host exception type the library does not
  know) and `Snapshot_Load_ProviderReturnsNull_ThrowsSnapshotException` (a null port set) each turn into a snapshot
  error naming the node (error paths).
