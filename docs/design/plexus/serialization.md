## Serialization Subsystem Design

![Serialization Structure](SerializationView.svg)

### Overview

The Serialization subsystem moves diagram content in and out of the model as plain data: complete diagrams as versioned
JSON snapshots, and selections of elements as clipboard text, plus the in-process duplicate operation that shares the
clipboard's clone path. It contains two units:

- **Snapshot**: the snapshot records (`DiagramSnapshot`, `PageSnapshot`, `NodeSnapshot`, `WireSnapshot`,
  `AnnotationSnapshot`, `GroupSnapshot`, `PropertySnapshot`, `RectSnapshot`, `PointSnapshot`), `PlexusJson`,
  `SnapshotException`, the capture side (`SnapshotWriter`), the property and payload codecs, the JSON converters and the
  `ToSnapshot`, `FromSnapshot`, `ToJson` and `FromJson` entry points of `Diagram` and `DiagramPage`.
- **PlexusClipboard**: `PlexusClipboard.Copy` and `Paste`, the envelope, selection expansion, the importer that
  validates and builds elements, the prepared import that applies them, and `DiagramPage.Duplicate`.

Core performs no file or clipboard I/O: hosts exchange the JSON text with their own storage and clipboard. The
subsystem does not store history, themes, routers or connection rules, which are session or configuration state.

### Interfaces

Exposed:

- `Diagram.ToSnapshot`, `Diagram.FromSnapshot`, `Diagram.ToJson`, `Diagram.FromJson` and `DiagramPage.ToSnapshot`.
- `PlexusJson.Serialize(snapshot, indented)` and `PlexusJson.Deserialize(json)`.
- `PlexusClipboard.Copy(elements)` returning text, `PlexusClipboard.Paste(page, json, at)` returning a `PasteResult`
  (success, created elements, missing node types, warnings, error), the constants `FormatId`, `FormatVersion`,
  `MaxPayloadBytes` and `MaxElements`.
- `DiagramPage.Duplicate(elements, dx, dy)`.
- `SnapshotException` (with `MissingNodeTypes`) and the host extension point `INodeDataSerializer` (Types subsystem).

Consumed: the Model subsystem (elements, `DiagramPage`, `Diagram.Perform` for undoable operations), the Types subsystem
(node type registry, data type and serializer declarations), the Properties subsystem (definitions, value coercion) and
the History subsystem (suspension while loading, one undoable plan per paste).

### Design

**One clone path.** Snapshot loading, paste and duplicate are variants of one pipeline: *capture* (`SnapshotWriter`,
reading live elements into records, bottom-most first) and *import* (`ElementImporter`, turning records into detached
elements). The records of a clipboard envelope have the same shape as a page snapshot, so there is one description of
element state and one validator. Options distinguish the variants: restoring reproduces the stored ids, names,
positions and z-order exactly and treats inconsistency as an error, while cloning generates fresh ids, makes names
unique, translates geometry and degrades gracefully with warnings.

**Validation before mutation.** The importer builds only *detached* elements while it validates; it never touches the
target page or history. Only when the whole content has passed does a `PreparedImport.Apply` attach the elements as
steps of one operation plan (one undoable "Paste", "Duplicate" or "Load page" step). A failure at any point, even at
the last wire, therefore leaves page, diagram and history untouched, and a failed result is returned instead of an
exception. Failures inside the importer are reported through an internal control-flow exception caught at the boundary
and converted to messages.

**Snapshot JSON shape.** The document is `{ "formatVersion": 1, "pages": [ ... ] }`. A page holds `name`, `nodes`,
`wires`, `annotations`, `groups` and an optional `zOrder` listing every element id bottom-most first. A node has `id`,
`typeKey`, `name`, `bounds` (`x`, `y`, `width`, `height`), `properties` and an optional `data`. A property is
`{ "kind": "double", "value": 1.5 }` with short kind names (`bool`, `int`, `long`, `double`, `string`, `choice`,
`color`); storing the kind makes round trips exact (an `int` stays an `int`) and lets loaders detect a mismatch with
the definition. Colors are `"#AARRGGBB"` with always eight digits. Node `data` is embedded raw JSON, not an escaped
string, so documents stay readable. A wire has source and target node ids and port names and its stored `route` of
bend points; a group has `memberIds`, `isCollapsed` and `collapsedPosition` (expanded bounds are computed, not stored).
Names are camelCase and null members are omitted. Reading ignores unknown members, so newer minor additions do not break
older readers, and rejects any other format version.

**No type names.** Serialization uses source-generated System.Text.Json metadata for a closed list of types, so no
reflection resolves a type by name from the text. Node payloads are read as exactly the type that the node type declares
(`DataType`) or through its `INodeDataSerializer`; undeclared payloads are not serialized at all. Untrusted text can
therefore never select what is instantiated.

**Resource limits for untrusted input.** Paste rejects text above 8 MiB (a character-count check first as a cheap lower
bound, then the UTF-8 byte count), more than 10,000 elements, JSON nesting above 64 levels (reader depth, also applied to
embedded payloads and to custom serializer output) and group nesting above 64 levels (which also breaks membership
cycles). Every coordinate must be finite and within 1e9, sizes non-negative, offsets within 1e9. The limits bound
memory, time and stack for hostile input while leaving ample room for real diagrams; they are applied before per-element
work where possible.

**Copy.** `ClipboardSelection.Expand` turns a selection into the exact carried set: a selected group brings its members
recursively, a wire is carried only when both of its nodes are (a selected wire alone has nothing to attach to), and the
result is returned in the page's z-order so pasted content stacks as it did. The envelope adds the format marker
`plexus.clipboard` and a version.

**Paste placement and names.** With a target position, the translation moves the top-left corner of the union of the
pasted node, annotation and collapsed-group boxes to that point (names, labels and routes do not count), keeping relative
layout; without one, the content is offset by one grid step. Wire routes are translated with their nodes and the router
is not consulted. When the diagram requires unique node names, a clashing name gets the next free numeric suffix
(`Axis`, `Axis1`, `Axis2`), computed against all nodes of all pages.

**Degrading gracefully.** Property values whose kind does not fit the target definition fall back to the default with a
warning (compatible numeric kinds convert silently); a payload that cannot be read becomes a warning and a node without
data; a wire that the connection rules reject is skipped with a warning. While restoring, the same conditions are errors,
because a snapshot of this diagram must reproduce it exactly.

**Loading a diagram.** `FromSnapshot` checks the version, reports every missing node type at once before building
anything, then loads each page with history suspended and finally clears the history, so a freshly loaded diagram has an
empty, clean history. Wires are restored with their stored routes.

**Per-node ports.** Snapshots, trees, JSON and clipboard content never contain port lists: wires name their ports. The
importer builds each node in the order properties, payload, ports (recomputed with the type's port provider), and only
then, once every node exists, groups and wires, so wires on computed ports resolve; stored bounds are kept. A provider
failure or a wire to a port that is not recomputed fails the load (`SnapshotException`) or the paste (failed
`PasteResult`) like any other invalid content. See PlexusClipboard Design and Snapshot Design.
