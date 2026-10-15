## Types Subsystem Design

![Types Structure](TypesView.svg)

### Overview

Types holds everything a host declares before it creates nodes: port types, node types and icons. Plexus has no
built-in knowledge of any application domain, so each node is an instance of a host-declared node type, and every
node type, port type and icon the rest of Plexus uses comes from this subsystem. Types contains two units:

- **NodeTypeRegistry** (see NodeTypeRegistry Design): node types, port definitions, port types, their registries and
  the payload and visual hooks of a node type.
- **IconSource** (see IconSource Design): the immutable SVG icon descriptor a node type can carry.

Boundaries: Types stores and validates declarations only. It does not create nodes (the Model subsystem does), it does
not decide how a node looks (the Scene subsystem does, from the declared title, ports, icon and size) and it does not
define property behavior (the Properties subsystem owns `PropertyDefinition`; a node type merely lists them). Two of
its declarations refer to other subsystems by design: node-type callbacks (`PortProvider`, `ToolTip`, the summary
function and `CreateData`'s product) receive a Model `Node`, which the Model creates and passes in, and a node type may
hold a Scene `INodeVisual`, a contract Scene owns and calls; Types only stores the callbacks and the reference, and
the callbacks must not change the model.

### Interfaces

Exposed to hosts and to other Plexus items, all in the `DemaConsulting.Plexus` namespace:

- `NodeTypeRegistry` with `Add`, `Remove`, `Get`, `TryGet`, `Contains`, `Count`, enumeration and the `PortTypes`
  registry; a `Diagram` is created with one.
- `PortTypeRegistry` and `PortType` for port colors, with a neutral gray fallback for unregistered keys.
- `NodeType` (key, title, display name, icon, input and output port lists, property list, default size, resizable flag,
  summary function, payload type and serializer, custom visual) and `PortDefinition`, `PortDirection`.
- `INodeDataSerializer`, the host hook that converts a node payload to and from JSON.
- `NodeType.PortProvider` (`Func<Node, PortSet>?`) and `PortSet` (the validated, immutable input and output
  declarations a provider returns; an empty list in either direction is valid, a null result is a provider failure).
- `NodeType.Description`, `NodeType.ToolTip` (`Func<Node, string?>?`) and `NodeType.GetToolTip(node)`,
  `PortDefinition.Description` and `PortType.DisplayName` for tooltip text.
- `IconSource` with `FromSvg`, `FromSvgFile` and `FromSvgResource`.

Consumed: `PropertyDefinition` (Properties), `Color` and `Size` (Geometry), the `Node` type (Model) that the summary,
port provider and tooltip callbacks receive, and the `INodeVisual` interface (Scene) that the optional visual hook
refers to.

### Design

**Declare, then freeze.** A host builds a `NodeType` with an object initializer and adds it to a `NodeTypeRegistry`.
Registration validates the key and the member names, then freezes the port and property lists. Freezing matters
because live nodes derive and cache their ports from the type when they are created; if the lists could change, those
caches would silently disagree with the declaration. The lists are exposed as plain `IList` so collection initializers
work, and an internal freezable list rejects changes once frozen and rejects null elements.

**Validate before inserting.** `Add` checks the key and runs the member validation before touching its dictionary and
ordered list, so a rejected type leaves the registry unchanged and is not frozen, and the host can fix it and retry.
Names must be unique within a direction; an input and an output may share a name because lookup by "Node.Port" path
resolves the direction separately.

**Registries are configuration, not content.** Registering or removing a type is not undoable and does not raise a
diagram change event. Removal makes a key unavailable for new nodes (the Model subsystem looks the key up when a node
is added) while existing nodes keep their type object. The registry keeps registration order in a list beside the
dictionary so palettes list types in a stable order.

**Forgiving port types, strict node types.** Port types are optional. `PortTypeRegistry.Get` returns a neutral gray
port type carrying the requested key when none is registered, so a diagram that mentions an undeclared port type still
renders. Node types, by contrast, are required for creating nodes, so `NodeTypeRegistry.Get` fails for unknown keys.

**Content-keyed icons.** An `IconSource` reads its SVG eagerly (from text, file or resource) and derives a key from a
hash of the content. Painters use the key as a cache key, and equality of icons follows the key, so one decoded image
is shared by every node type that uses the same artwork. The icon never decodes the SVG itself, which keeps the core
library free of any graphics dependency.

**Collaboration.** A node type refers to an icon (`NodeType.Icon`) and to port types by key (`PortDefinition.TypeKey`);
the keys are resolved late, through `NodeTypeRegistry.PortTypes`, by whoever draws or checks a port. The Model
subsystem consumes a registered node type to create a node with its declared ports and default size.

**Per-node ports and descriptions.** A node type may declare a `PortProvider` that returns a validated `PortSet` for
a node from its properties and payload; the declared `Inputs` and `Outputs` stay the type's defaults. The Model
subsystem calls the provider (through `NodeType.ResolvePorts`) when a node is created, loaded or changed. Node types
also carry an optional `Description` and `ToolTip` function, port declarations a `Description` and port types a
`DisplayName`. `NodeType.GetToolTip(node)` returns the `ToolTip` function's result unchanged when a function is declared
(null or empty meaning no tooltip; the description is then not used as a fallback) and the `Description` otherwise; the
function is called with the node on every request, so it reflects the current properties and payload, and an exception
it throws propagates unchanged. A provider result of null is a failure (`InvalidOperationException` naming the type),
whereas an empty `PortSet` is a valid result giving a node without ports in that direction. The subsystem only stores
these declarations: it neither recomputes ports nor shows text (see NodeTypeRegistry Design).
