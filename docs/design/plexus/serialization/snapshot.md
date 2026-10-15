### Snapshot Unit Design

![Serialization Structure](SerializationView.svg)

#### Purpose

Defines the persisted form of a diagram and converts between it and live models and JSON text: capture, restore and the
JSON reader and writer.

#### Data Model

- Records (all immutable): `DiagramSnapshot` (`FormatVersion` constant 1, `Pages`), `PageSnapshot` (`Name`, `Nodes`,
  `Wires`, `Annotations`, `Groups`, optional `ZOrder`), `NodeSnapshot` (`Id`, `TypeKey`, `Name`, `Bounds`, `Properties`,
  `Data` as raw JSON text, and an internal never-serialized `DataReference` used only by in-process duplicate),
  `WireSnapshot` (`Id`, `SourceNodeId`, `SourcePort`, `TargetNodeId`, `TargetPort`, `Route`), `AnnotationSnapshot`,
  `GroupSnapshot` (`MemberIds`, `IsCollapsed`, `CollapsedPosition`), `PropertySnapshot` (kind and JSON value) and
  `RectSnapshot` and `PointSnapshot` (plain numbers).
- `PlexusJson`: two shared contexts (indented and compact) built from source-generated metadata with camelCase names, null
  omission, a maximum depth of 64 and relaxed escaping so non-ASCII text stays readable.
- `PropertyCodec`: the seven portable kinds and their exact JSON forms; `NodeDataCodec`: payload serialization
  through the declared `DataSerializer` or `DataType`; `RawJsonStringConverter` (embeds payload text as a JSON value) and
  `PropertyKindJsonConverter` (short kind names).
- `SnapshotWriter` (internal static): the capture half of the clone path. `SnapshotException`: message and the list of
  missing node type keys.

Invariants: the snapshot holds no type names; `Data` is valid JSON or null; a property's `Value` matches its `Kind`.

#### Key Methods

`Diagram.ToSnapshot` / `DiagramPage.ToSnapshot`: capture each page through `SnapshotWriter.CapturePage`, which reads elements
bottom-most first into lists by kind plus the z-order, serializing node payloads and properties and leaving the diagram
unchanged.

`PlexusJson.Serialize(snapshot, indented)`: writes the snapshot with the indented or compact context. `Deserialize(json)`:
parses with the compact context (depth-limited), rejects a null document and rejects any format version other than the
current one with a `SnapshotException`; JSON syntax and shape problems surface as `JsonException`.

`Diagram.FromSnapshot(snapshot, types)`: checks the version; collects every unknown node type across all pages and
throws one `SnapshotException` listing them all; creates a diagram, loads each page with history suspended through the
shared importer in restore mode (exact ids, names, bounds, stored routes, z-order, groups, properties and payloads) and
clears the history. Port lists are never stored: the importer recomputes each node's ports from its restored
properties and payload (with the type's port provider, if any) before resolving the wires, keeping the stored bounds,
so wires on computed ports load and a wire naming a port that is not recomputed fails like any unknown port. Every
non-fatal provider failure (any exception except `OutOfMemoryException`, a null result or an invalid port set) becomes a
`SnapshotException` naming the node. A node entry whose type key is null, empty or white space is invalid content: it
is rejected with "A node has no node type key." before the missing-type check, and `SnapshotFilter` does not treat it
as an unknown type, so it is never skipped.
`ToJson`/`FromJson` are the shorthands combining these steps.

`PropertyCodec.Write` / `TryRead`: write values with round-trip precision and colors as `#AARRGGBB` (eight digits, alpha
first, also when opaque); read strictly by token type so a malformed value is reported, never coerced; color text is
accepted with six digits (opaque) or eight digits (alpha first). `NodeDataCodec.Serialize` / `TryDeserialize`: use the
exact declared type (never a name from the text), verify that serializer output is valid JSON of limited depth, and
convert every reader failure - a null result or any exception of the host serializer or System.Text.Json except
`OutOfMemoryException` - into a problem message (the exception message).

Preconditions: nodes whose payload type is declared hold a payload of that type.

`Diagram.ToTree` / `FromTree` (`DiagramTree`, internal): the format-neutral tree has the schema of the JSON format and is
produced from it, so there is one schema. `ToTree` serializes the snapshot compactly, parses it with `JsonDocument` and
converts objects to `Dictionary<string, object?>`, arrays to `List<object?>` and numbers to `long` (when integral) or
`double`; the value of every property of kind `double` is then made a `double` so that formats that distinguish `2.0`
from `2` keep the kind visible. `FromTree` writes the tree to JSON with a `Utf8JsonWriter` (accepting every CLR number
type, `JsonElement`, any `IEnumerable<KeyValuePair<string, object?>>` or non-generic `IDictionary` as an object and any
other `IEnumerable` as a list; refusing other types, non-finite numbers, non-string keys and nesting beyond the JSON
depth limit with a `SnapshotException` that names the path) and then follows the normal `FromJson` path, so every
validation applies unchanged; a `JsonException` of that path is rethrown as `SnapshotException`. The parameter is the
sequence of key/value pairs because a dictionary-typed parameter would be ambiguous for types that implement both
dictionary interfaces.

`Diagram.ToJson(Stream)` / `FromJson(Stream, ...)`: `PlexusJson.SerializeTo` and `DeserializeFrom` use the same
source-generated contexts with `JsonSerializer` stream overloads (UTF-8, byte-order mark accepted when reading) and
never close the stream.

`DiagramOptions` and loading: `FromSnapshot(snapshot, types, options)` builds the diagram with
`new Diagram(types, options)` so the configuration is installed before any content exists. With
`UnknownNodeTypes = Skip`, `SnapshotFilter` first reduces the page snapshots: nodes of unknown types are removed, as
are the wires that touch them, their ids in group member lists and in the z-order, and groups left without members
(repeating until stable, because a group can be the only member of another); the reduced snapshot is then loaded
through the strict path, and the counts are stored in `Diagram.LoadReport`.

#### Error Handling

- Malformed JSON, the wrong shape or excessive depth: `JsonException` from `Deserialize`.
- Unsupported version, missing node types, duplicate or missing ids, dangling references, a property value of the wrong
  kind, a node without a type key, an unreadable payload while restoring (any non-fatal serializer failure), a port
  provider that fails in any non-fatal way, a wire to a port the provider does not produce: `SnapshotException`
  (missing types listed in `MissingNodeTypes`).
- A payload that does not match its declared type, or a serializer that returns null or invalid JSON, while capturing:
  `InvalidOperationException` naming the node, because it is a host defect.
- Null arguments: `ArgumentNullException`.
Loading never leaves a partial diagram: the new diagram is only returned when every page loaded.

#### Dependencies

- Model, Types and Properties subsystems (elements, node type registry and declarations, property definitions)
- History subsystem (suspension and clearing during load)
- The importer of the PlexusClipboard unit for the restore half
- System.Text.Json (part of the .NET platform, no third-party dependency)

#### Callers

- Hosts and PlexusAvalonia/PlexusRendering for saving, loading and exporting
- The PlexusClipboard unit (`SnapshotWriter`, codecs and `PlexusJson` contexts)
