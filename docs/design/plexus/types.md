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
define property behavior (the Properties subsystem owns `PropertyDefinition`; a node type merely lists them).

### Interfaces

Exposed to hosts and to other Plexus items, all in the `DemaConsulting.Plexus` namespace:

- `NodeTypeRegistry` with `Add`, `Remove`, `Get`, `TryGet`, `Contains`, `Count`, enumeration and the `PortTypes`
  registry; a `Diagram` is created with one.
- `PortTypeRegistry` and `PortType` for port colors, with a neutral gray fallback for unregistered keys.
- `NodeType` (key, title, display name, icon, input and output port lists, property list, default size, resizable flag,
  summary function, payload type and serializer, custom visual) and `PortDefinition`, `PortDirection`.
- `INodeDataSerializer`, the host hook that converts a node payload to and from JSON.
- `IconSource` with `FromSvg`, `FromSvgFile` and `FromSvgResource`.

Consumed: `PropertyDefinition` (Properties), `Color` and `Size` (Geometry), and the `INodeVisual` interface and `Node`
type that the optional summary and visual hooks refer to.

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
