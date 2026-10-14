### NodeTypeRegistry Unit Design

![Types Structure](TypesView.svg)

#### Purpose

NodeTypeRegistry is the Software Unit that holds the host's declarations of what can be placed on a diagram: node types,
the port definitions they contain, and the port types those ports refer to. It validates and freezes each node type at
registration and offers lookup, removal and ordered enumeration. It also carries the optional hooks a node type may
declare: a custom summary function, a host payload type or serializer (`INodeDataSerializer`) and a custom node
visual. The unit covers the files `NodeType`, `NodeTypeRegistry`, `PortDefinition`, `PortDirection`, `PortType`,
`PortTypeRegistry`, `FreezableList` and `INodeDataSerializer`.

#### Data Model

- **NodeTypeRegistry**: a dictionary of node types by ordinal key, a list in registration order and a `PortTypes`
  registry. `Count` is the number of registered node types. It implements `IEnumerable<NodeType>` in registration order.
- **NodeType**: `Key` (non-blank, fixed at construction), `Title` (explicit, or the last dot-separated segment of the
  key), `DisplayName` (explicit and non-empty, otherwise `Title`), `Category` (optional group name for palettes and
  menus; null means uncategorized), `Icon` (optional `IconSource`), `Inputs`, `Outputs` and `Properties` (frozen
  lists), `DefaultSize` (optional), `IsResizable`, `Summary` (optional function), `DataType`,
  `DataSerializer` (takes precedence over `DataType`; `DataType` is optional when a serializer is set), `CreateData`
  (optional `Func<object>` that produces the initial payload of every new node; init-only) and `Visual` (optional
  `INodeVisual`, declared in a separate
  partial-class file). Most members are init-only so a type is described with an object initializer.
- **PortDefinition**: `Name`, `TypeKey` (both non-blank), `Label` (defaults to the name) and `AllowsMultipleWires`
  (init-only, false by default: whether an input accepts more than one wire; ignored for outputs, which always fan
  out; `Port.AllowsMultipleWires` exposes it and reports true for outputs). Direction is not stored; it
  is decided by which list of the node type holds the definition. `PortDirection` names the two directions (input on
  the left edge, output on the right edge).
- **PortType**: a non-blank `Key` and the `Color` that ports of this type are drawn with.
- **PortTypeRegistry**: a dictionary of port types by ordinal key plus an ordered list; unknown keys resolve to a neutral
  gray (`#9A9A9A`) type.
- **FreezableList of T**: an internal list of non-null elements with a frozen flag; once frozen every mutating
  operation throws and `IsReadOnly` is true.

Invariants: keys are unique within each registry; a registered node type has no duplicate input names, no duplicate
output names and no duplicate property names; a registered node type's lists never change afterwards.

#### Key Methods

- **NodeTypeRegistry.Add(type)**: precondition is a non-null type whose key is not registered. It then calls
  `NodeType.ValidateAndFreeze` and only after that inserts the type, so a failure leaves the registry unchanged.
  Postcondition: the type is retrievable by key, last in enumeration order, and its lists are frozen.
- **NodeType.ValidateAndFreeze** (internal): groups inputs by name and outputs by name using ordinal comparison and
  throws on the first duplicate, does the same for properties, and finally freezes the three lists.
- **NodeTypeRegistry.Remove(key)**: removes the type from both the dictionary and the ordered list and returns
  whether it existed. It is not undoable; existing nodes keep their type object.
- **NodeTypeRegistry.Get(key)**, **TryGet(key, out type)**, **Contains(key)**: lookup by ordinal key; `Get` throws for
  an unknown key.
- **NodeType.FindProperty(name)**: returns the declared property or null.
- **NodeType.GetSummaryLines(node)**: returns the custom `Summary` output when set; otherwise one line per property
  flagged for the summary, in declaration order, formatted by the property definition.
- **PortTypeRegistry.Add(type)**, **Get(key)**, **TryGet**, **Contains**: `Add` rejects a duplicate key; `Get` returns
  the registered type or a neutral gray type with the requested key without registering it.

#### Error Handling

Declaration mistakes are programming errors by the host and are reported by exceptions at the point of declaration:
`ArgumentException` for blank keys, names or type keys and for duplicate keys, port names or property names (the
message names the key or member); `ArgumentNullException` for null arguments; `KeyNotFoundException` from
`NodeTypeRegistry.Get`; `NotSupportedException` when a frozen list is modified. The `TryGet` and `Contains` forms
and the neutral port type fallback provide non-throwing alternatives. The registry does no logging and catches
nothing.

#### Dependencies

Depends on Geometry (`Color` for port types, `Size` for the default size), on `PropertyDefinition` from the Properties
subsystem for the property list and summary formatting, on `Node` for the summary function signature, on the Scene
interface `INodeVisual` for the optional custom visual, and on `IconSource` (see IconSource Design) for the icon.
There are no external dependencies.

#### Callers

The Model subsystem owns a registry per diagram: `Diagram` exposes it, `DiagramPage.AddNode` looks node types up by key,
`Node` derives its ports from a type's input and output definitions, and `DiagramPage.AddNode` takes the default size
from the type. Scene layout reads title, icon, ports, property summary and visual. Serialization resolves type keys and
uses the payload type or serializer. The Avalonia editor looks types up for drag and drop. Hosts fill the registry at
start-up.
