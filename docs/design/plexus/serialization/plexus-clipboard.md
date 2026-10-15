### PlexusClipboard Unit Design

![Serialization Structure](SerializationView.svg)

#### Purpose

Copies elements to portable text and pastes such text into a page as one undoable step, treating the text as untrusted
input; the unit also owns the importer and the duplicate operation that share this clone path.

#### Data Model

- `PlexusClipboard` (static): constants `FormatId` (`plexus.clipboard`), `FormatVersion` (1), `MaxPayloadBytes` (8 MiB)
  and `MaxElements` (10,000).
- `ClipboardEnvelope` (internal record): `Format`, `Version` and the element lists with the same shape as a page snapshot,
  plus the optional z-order; the `Content` accessor presents them to the importer as `ElementContent`.
- `ClipboardSelection` (internal static): selection expansion. `ImportOptions` (internal): `Restore`, `MaxElements`, `At`
  and the default offset `Dx`/`Dy`. `ElementImporter` (internal static, with a private builder): validation and
  construction of detached elements, limits `MaxCoordinate` 1e9 and group depth 64. `PreparedImport` (internal): success
  flag, error, missing types, warnings and the elements in attach order. `PasteResult` (public): `Success`, `Elements`,
  `MissingNodeTypes`, `Warnings`, `Error`.
- `DiagramPage.Duplicate(elements, dx, dy)` lives in the file `DiagramPage.Snapshot.cs` and is reviewed with this unit.

Invariants: a `PreparedImport` that reports success holds only valid detached elements; a failed result has no elements.

#### Key Methods

`Copy(elements)`: expands the selection (members of selected groups recursively; a wire only when both of its nodes are
carried; z-order of the page), captures the carried elements without data references, wraps them in an envelope and
serializes with the compact context. An empty selection yields an envelope with no elements.

`Paste(target, json, at)`: checks the target is attached and the position is finite and within 1e9 (argument
exceptions for caller errors); reads the envelope; prepares the import; and only then performs the single "Paste"
operation that attaches the elements. Reading the envelope rejects, in this order, empty text, text above 8 MiB
(character count, then UTF-8 bytes), text that is not valid JSON of the envelope shape or nests deeper than 64 levels,
a missing or foreign
`format` marker and an unsupported version, each with a message in a failed `PasteResult`.

`ElementImporter.Prepare(target, content, options)`: refuses content above `MaxElements` before per-element work,
rejects a node entry that is null or has a null, empty or white-space type key ("A node has no node type key."; checked
before the missing-type check, so such a node is never reported as an unknown type), reports all unknown node types
together, validates every rectangle, collapsed position and route point (finite, within 1e9, sizes non-negative),
computes the translation (the target position applied to the top-left of the union of node, annotation and collapsed
group boxes, else the default offset), then creates detached nodes (claiming ids: fresh GUIDs when cloning, the stored
ids when restoring, rejecting empty and repeated ids; resolving names; loading properties by coercion; reading payloads,
where a node without any payload gets `NodeType.CreateData` when cloning and keeps no payload when restoring), then
recomputing the node's ports with its type's port provider (`Node.InitializePorts`, after the properties and payload and
before any wire; the stored bounds are kept; every provider failure except `OutOfMemoryException` - an exception of any
type, a null result or an invalid port set - becomes a "the ports of node ... could not be computed: reason" import
problem that keeps the provider's exception as its inner exception), annotations (validating fill colors) and groups,
links group membership after checking for unknown members, members in two groups, cycles and depth beyond 64, creates
wires (checking nodes, port names against the recomputed ports and directions, self connections and duplicate port
pairs, translating routes) and fixes the z-order (a stored z-order must name every element exactly once; otherwise
groups, nodes, annotations, wires). Any problem aborts with a message and nothing is applied.

`PreparedImport.Apply(page, plan, exactZOrder)`: when restoring, attaches the elements in order; when cloning, attaches
everything except wires first and then each wire after asking the page whether the connection rules allow it, skipping
rejected wires with a warning. All attachments are steps of one operation plan, so undo and redo are one step.

`DiagramPage.Duplicate(elements, dx, dy)`: validates arguments and offsets, expands the selection, requires all
elements on this page, captures them keeping payload references for types without a serializer, prepares the import
with the offset and applies it as one "Duplicate" step. Payloads of declared types are deep-copied through the codec;
others are shared by reference.

Preconditions: the target page belongs to a diagram. Postcondition: after a failed paste the page, the diagram and the
history are unchanged.

`PasteResult.Message`: computed from the result, never stored: `Pasted n element(s).` plus `Warnings: ...` on success,
`Paste failed: this application does not know the node type(s) ...` when `MissingNodeTypes` is not empty, otherwise
`Paste failed:` followed by the `Error` text.

#### Error Handling

Bad clipboard text is never an exception: the paste returns `PasteResult` with `Success` false, an error message and, for
unknown node types, the complete list of keys. Warnings (property kind mismatches, unavailable choices, rejected wires,
unreadable payloads) accompany a successful result. Caller errors throw: `ArgumentNullException` and `ArgumentException`
for null, detached or multi-page selections, `ArgumentOutOfRangeException` for non-finite or huge positions and offsets,
and `InvalidOperationException` when the target page has been removed, a duplicate's own payload no longer
serializes or a port provider fails for a copy ("The elements could not be duplicated: ..."). Exceptions of host
payload serializers while reading are never propagated: any of them except `OutOfMemoryException` makes the payload
unreadable (a warning on paste, an error on restore).

#### Dependencies

- Snapshot unit (`SnapshotWriter`, codecs, `PlexusJson` compact context, snapshot records)
- Model subsystem (`DiagramPage`, `Diagram.Perform`, elements, connection rules through `CanConnect`), Types and Properties
  subsystems (registry, definitions, coercion), History subsystem (operation plans)
- System.Text.Json (part of the .NET platform)

#### Callers

- PlexusAvalonia `DiagramClipboard` and `DiagramEditor` (copy, cut, paste, duplicate gestures)
- The Snapshot unit's `Diagram.FromSnapshot` (uses the importer in restore mode)
- Hosts that offer copy and paste or duplication
