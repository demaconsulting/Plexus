# Plexus System Design

![Plexus Structure](PlexusView.svg)

Plexus is the UI-free core library of the repository, delivered as the `DemaConsulting.Plexus` package. It provides the
document model, undo history, node-type vocabulary, typed properties, wire routing, a painter-independent scene with
hit testing, and JSON snapshot and clipboard serialization for node-based diagrams. It performs no drawing, windowing,
file or clipboard I/O: those belong to the host or to the PlexusRendering and PlexusAvalonia systems.

## Architecture

The system consists of two units directly under it and seven subsystems. Geometry, History and Properties are the
lowest layers, Types and ConnectionRules build on them, the Model builds on all of these plus Routing and the theme and
node geometry of Scene, and Serialization and the scene builder read the model through its public operations; each
subsystem design lists its exact dependencies.

| Item            | Kind      | Responsibility                                                                   |
|-----------------|-----------|----------------------------------------------------------------------------------|
| Geometry        | Unit      | Immutable `Point`, `Size`, `Rect` and RGBA `Color` value types                   |
| ConnectionRules | Unit      | Host connection decision (`ConnectionValidator`) and the stock rules             |
| History         | Subsystem | Undo history: commands, transactions, suspension, dirty tracking                 |
| Properties      | Subsystem | Property definitions (kinds, choices, presentation) and per-node property bags   |
| Types           | Subsystem | Node types, ports, port types, icons and the registry the host declares          |
| Model           | Subsystem | Diagram, pages, nodes, ports, wires, annotations and groups with undoable edits  |
| Routing         | Subsystem | `IWireRouter`, the obstacle-avoiding orthogonal router and the simple router     |
| Scene           | Subsystem | Theme, node layout, text measurement contract, scene building, hit testing       |
| Serialization   | Subsystem | JSON snapshot of a diagram, clipboard copy/paste and duplicate                   |

**Collaboration.** The host declares a `NodeTypeRegistry` (Types, using Properties and Geometry) and creates a
`Diagram` (Model) with it. The diagram owns one `UndoHistory` (History); every public mutation of pages and elements is
validated and then executed as an undoable command, so undo, redo and the original edit share one code path and one
notification path (`Diagram.Changed`). New wires are routed by the diagram's `IWireRouter` (Routing) using node
geometry from Scene, and are checked by the diagram's connection rule (ConnectionRules). A `DiagramScene` is built
from a page by `SceneBuilder` (Scene) and is consumed by painters and by hit testing. Serialization reads and writes the
model through its public operations (snapshot capture and import, clipboard paste as one undoable plan).

**Principles shared by all subsystems.**

- *Everything through the history.* The model changes state only inside history commands. This yields uniform undo
  and redo, uniform notifications, and prevents partial changes after a failed operation.
- *Validate before mutating.* Public operations check arguments, state and rules first; clipboard and snapshot
  imports build detached elements and attach them only after the whole input has been validated.
- *One geometry.* The theme carries every metric; node geometry, routers, the scene and hit testing derive from it, so
  the picture, the routes and the hit areas agree.
- *Painter independence.* The scene is a closed set of primitives; text measurement is an injected interface.
- *Stable identity.* Elements keep their ids and object identity across undo and redo.
- *Host-defined vocabulary.* Node types, port types, connection rules, routers, themes, node visuals and data
  payload serializers are all supplied by the host; the library has defaults for each.

## External Interfaces

The interfaces of the library are its public .NET API. Hosts interact with the system in the following ways.

- **Registry declarations** (`NodeTypeRegistry`, `NodeType`, `PortType`): in; .NET objects declared by the host before
  diagrams are built; registered node types are frozen.
- **Model operations** (`Diagram`, `DiagramPage` and the elements): in and out; method and property calls, with exceptions
  for invalid arguments; single-threaded, and every mutation is undoable.
- **Change notification** (`Diagram.Changed`, observable collections): out; .NET events carrying the kind of change,
  the page and the element; raised on the calling thread after the change has been applied.
- **Undo history** (`UndoHistory`): in and out; `Undo`, `Redo`, transactions, `Suspend`, `MarkClean` and state
  properties; bounded capacity (default 1000 entries).
- **Host extension points** (`ConnectionValidator`, `IWireRouter`, `ITextMeasurer`, `INodeVisual`,
  `INodeDataSerializer`): in; host-implemented callbacks that the library calls synchronously.
- **Scene** (`SceneBuilder.Build`, `DiagramScene`): out; an immutable list of scene primitives with bounds, layouts and
  `HitResult` values; rebuilt by the caller after the model changes.
- **Snapshot** (`Diagram.ToJson`, `Diagram.FromJson`): out and in; versioned JSON text; restoring requires the same
  node types.
- **Clipboard** (`PlexusClipboard.Copy`, `PlexusClipboard.Paste`): out and in; versioned JSON text in a
  `plexus.clipboard` envelope and a `PasteResult`; the input is untrusted and subject to size, depth and element
  limits.
- **Duplicate** (`DiagramPage.Duplicate`): in; a selection and an offset, returning the new elements as one undo step.
No interface uses the network, the file system, the operating-system clipboard or reflection-based serialization of
host-supplied types other than through the host's declared data type or `INodeDataSerializer`.

## Dependencies

Plexus has no OTS or shared-package runtime dependencies: it is implemented against the .NET Base Class Library only,
including `System.Text.Json` (source-generated context) for snapshot and clipboard text. Build-time analyzers and
SBOM and source-link packages are private build assets and are not dependencies of consumers. Documentation and
quality tools are OTS items documented in the OTS design (see *OTS Integration Design*); they are not linked into the
package. The PlexusRendering and PlexusAvalonia systems depend on Plexus; Plexus depends on neither.

## Risk Control Measures

Plexus is a general-purpose software library; no segregation is required for risk control in the sense of IEC 62304
section 5.3.3 and no item is classified as a risk-control measure. The following measures nevertheless protect
the host application from the library's one exposure to untrusted data and from model corruption:

- **Untrusted clipboard and snapshot text.** The Serialization subsystem parses text with explicit, versioned,
  source-generated JSON contracts; it never uses binary or type-name-driven deserialization (type-name members in
  the payload are ignored), and enforces limits (clipboard: payload size, nesting depth and element count;
  snapshot: nesting depth). Content is fully validated before any model change, and failures are reported as an error
  result (clipboard) or `SnapshotException` (snapshot) with the model untouched.
- **Atomic mutation.** Multi-step edits are composite history entries: a failing step reverts the earlier steps, so a
  failure never leaves a partially applied edit.
- **Rule enforcement at the boundary.** Connection rules and node-type availability are enforced inside the model, so
  no front end can introduce an invalid wire or a node of an unknown type.

## Data Flow

1. **Declaration.** The host fills a `NodeTypeRegistry`; registering a node type validates and freezes it.
2. **Editing.** Host code or an editor control calls a model operation. The operation validates its input, then
   executes an undoable command on the diagram's history. The command changes the model, re-routes affected wires
   through the router, updates indexes and raises `Diagram.Changed` and collection events. Undo and redo replay the same
   commands in the reverse or forward direction and raise the same events.
3. **Presentation.** On a change, a painter asks `SceneBuilder.Build` for a `DiagramScene` of a page. The builder
   computes node layouts (theme and text measurer), drawing order, culling, wires (from their routes), selection and
   hover state, and the hit targets. The painter draws the primitives; an editor control uses `HitTest` to translate
   pointer positions into model elements and then calls model operations (back to step 2).
4. **Persistence and exchange.** `Diagram.ToJson` captures the model as a snapshot document; `Diagram.FromJson`
   validates the document against the node types and rebuilds the diagram with history suspended, leaving an empty,
   clean history. `PlexusClipboard.Copy` captures a selection (expanding groups, keeping internal wires) as envelope
   text; `Paste` validates the text, builds detached elements with fresh ids and unique names, and attaches them as one
   undoable step. `DiagramPage.Duplicate` uses the same clone path within a page.

## Design Constraints

- **Platforms.** Targets net8.0, net9.0 and net10.0; builds and runs on Windows, Linux and macOS (see the platform
  requirements). No platform-specific APIs are used.
- **No UI or drawing dependency.** The library must be usable in servers, tests and command-line tools; it contains no
  reference to a UI framework, drawing library, file or clipboard API.
- **Threading.** The model, history and scene are not thread-safe; all use is from one thread. A built scene is
  an immutable result that does not track later model changes.
- **Determinism.** Routing, layout, naming, serialization and ordering are deterministic for equal input, so tests,
  snapshots and rendered images are reproducible.
- **Performance.** Routing is expected to stay interactive for diagrams of hundreds of nodes, and scene queries and hit
  tests use a spatial index so that cost does not grow with the whole page. Speed is assessed by profiling rather than
  by timed tests, because test machines are shared and their speed varies.
- **Security.** Clipboard and snapshot input is untrusted (see *Risk Control Measures*); the library does not execute
  or instantiate types named by input data.
- **Quality gates.** The project builds with warnings as errors and code analyzers enabled, and every public member is
  documented.
- **Compatibility.** Snapshot and clipboard formats are versioned; readers reject unsupported versions.

## Key Design Decisions

Each decision is recorded with the reason it was taken; the behavior itself is specified in the subsystem and unit
designs.

- **The host owns persistence; the core offers I/O-free helpers.** The core performs no file I/O and prescribes no
  document format, because storage and file formats are product decisions that differ between hosts. It nevertheless
  exposes `Diagram.ToJson`, `Diagram.FromJson` and the snapshot records, using the same versioned JSON as the
  clipboard, because every host otherwise had to rewrite the same model-to-JSON mapping (this was found while building
  the demo application). Hosts may embed the JSON in their own format, convert the snapshot tree to another format, or
  ignore the helpers.
- **Explicit, versioned JSON; no framework serialization.** `BinaryFormatter` and `[Serializable]` are rejected: they
  were removed from current .NET and are a well-known deserialization attack surface. Snapshot and clipboard text use
  explicit, source-generated contracts, safe value types for properties (boolean, 32-bit and 64-bit integer, double,
  string, choice and color) and a host serialize and deserialize hook for node payloads. Text from another application
  is untrusted input and is validated completely before anything changes (see *Risk Control Measures*).
- **Node-type keys are opaque, host-chosen strings.** The core attaches no meaning to a key beyond identity; hosts
  namespace them by convention (for example `com.acme.Motor`) so that independent applications and plug-ins do not
  collide. A key is matched exactly. Because keys are written to files and to the clipboard, hosts must keep them
  stable. A paste that names any unknown key is rejected as a whole and reports every missing type, so a diagram is
  never pasted with silently missing nodes or dangling wires; loading a snapshot is all-or-nothing by default and may
  be told to skip unknown types.
- **Stored routes, with a deliberate re-route policy.** Wire routes are part of the model so that a diagram looks the
  same in every painter and after every load. A route is recomputed with obstacle avoidance when the wire is created and
  when exactly one endpoint node moves, because the old shape no longer fits; this discards manual segment edits. When
  both endpoints move together (multi-selection move, group move, paste) the stored route is translated, so manual
  shapes survive. Port anchors depend only on theme metrics and a deterministic text-width estimate, so routing never
  depends on a painter or a font.
- **No wires between pages.** A page is the unit of display, routing and hit testing; a wire to another page would
  need off-page connectors and cross-page routing, which are not provided. Moving nodes to another page keeps the wires
  among the moved nodes and removes the wires to nodes left behind, as one undoable command that undo fully restores.
- **Collapsed groups are a view, not a model change.** A collapsed group is drawn as a summary box with one read-only
  proxy port per inner port that has a wire crossing the group boundary. The display routes to the proxies are
  transient and the stored routes are untouched, so expanding the group restores the layout exactly and collapse
  remains an ordinary undoable edit. Expanded frames enclose the full visual extent of their members (names,
  summaries and port labels), so a frame never clips what it contains.
- **Hit testing and queries use a simple spatial index.** The scale target is hundreds of nodes and wires; a uniform
  grid index and visible-region culling meet it with little code and no allocation churn (see the Scene subsystem
  design for the comparison with a tree).

## Known Limitations and Future Considerations

- **Clipboard format evolution.** Only format version 1 exists and a reader rejects any other version. How a future
  version 2 would be negotiated between applications of different ages is not decided, and neither is the handling of
  two applications that declare different definitions (ports, properties) under the same node-type key: a paste
  currently accepts the key and degrades the content to the receiving definition with warnings (see the
  PlexusClipboard unit design).
- **No connections to proxy ports.** The proxy ports of a collapsed group are display-only; a wire cannot be started
  from or connected to them. A user expands the group to change its connections.
- **No cross-page wires or off-page connectors.** See *Key Design Decisions*.
- **No virtualization or level-of-detail rendering.** The scene is built for the visible region plus a margin, but every
  element in that region is built and drawn in full. Diagrams of thousands of nodes are outside the scale target.
- **No shipped node-type catalog or icon library.** Hosts declare their node types and supply their own SVG icons.
- **No property panel in the core.** The core only raises events and offers rename and property APIs; the ready-made
  property grid is the separate PlexusAvaloniaPropertyGrid system, and hosts without Avalonia build their own UI.
