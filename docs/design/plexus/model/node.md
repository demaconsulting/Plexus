### Node Unit Design

![Model Structure](ModelView.svg)

Scope: Diagram element base class, nodes and their ports.

#### Purpose

The Node unit provides the three classes every element on a page is built on: `DiagramElement`, the abstract base of
nodes, wires, annotations and groups (identity, page and group links, visibility, bounds contract, removal and change
notification), `Node`, a block that is an instance of a node type with a name, a bounding box, a host data payload,
properties and ports, and `Port`, a lightweight connection point derived from the node type. Elements are only created
through the page API (see DiagramPage Design), so none of the classes has a public constructor.

#### Data Model

- `DiagramElement`: `Id` (immutable, stable across removal and undo), `Page` (null while the element is removed),
  `Parent` (the containing group or null), `IsVisible` (computed: false when any ancestor group is collapsed). It
  implements `INotifyPropertyChanged`. The `Page`, `Parent` and the internal `NotifyChanged` hook are set only by the
  commands of the Model subsystem.
- `Node`: `Type` (immutable node type), the stored name, bounds and data payload, the immutable port lists `Inputs`,
  `Outputs` and `Ports` (inputs first), the `Properties` bag (see Property Bag Design), `Position` (computed from the
  bounds) and `Wires` (computed from the page, in page order, empty while removed).
- `Port`: `Node`, `Definition`, `Direction` and `Index` (row within its column, inputs and outputs counted
  separately), plus `Name`, `Label` and `TypeKey` forwarded from the definition. A port has no mutable state; `Anchor`
  and `Wires` are derived from the node's bounds and the page. Invariant: ports are created once in the node
  constructor from the frozen node type, so a port can be compared by reference and kept by hosts.

#### Key Methods

- `Node.Bounds` setter and `Node.MoveTo(x, y)`: delegate to the page layout pipeline, which validates the box,
  ignores an unchanged box, and records one undoable layout command that also updates attached wire routes (see DiagramPage
  Design). The stored box changes only inside that command.
- `Node.Data` setter: replaces the payload through an undoable value command; an equal value is ignored.
- `Node.Name` setter: delegates to the diagram rename pipeline (see Diagram Design); the unit only stores the
  validated name and raises the change notification.
- `Node.Port(name)`, `Node.Input(name)`, `Node.Output(name)`, `Node.TryGetPort(name, direction, out port)`: find a
  port by ordinal name comparison within one column or both. When no direction is given and an input and an output have
  the same name, `Port` throws `InvalidOperationException` whose message names `Input` and `Output`, and
  `TryGetPort` returns false; with a direction the lookup is always unambiguous. `Port` and the direction-specific
  methods throw `ArgumentException` when nothing matches. The internal `LookupPort` reports the ambiguity to callers
  that build their own messages, such as path resolution in the diagram.
- `Port.Anchor`: computes the attachment point from the node bounds, direction, row, label and the theme.
- `Port.ConnectTo(other)`: shorthand for the page connect operation; the page fixes the direction and applies the
  connection rules.
- `DiagramElement.Remove()`: shorthand for removing the element through its page; `RequirePage()` returns the
  attached page or throws, and every mutator calls it first.
- `DiagramElement.NotifyChanged(name)`: raises `PropertyChanged` and reports an element-changed event to the diagram.

`CloneData()`: serializes the payload with `NodeDataCodec` (the data serializer or exact data type of the node type) and
deserializes it again, so the copy is exactly what saving and loading would produce; null data gives null, and data of
a type that declares no way to copy it gives an `InvalidOperationException` that names the node type. Committing the
edited copy together with property changes is one `Transact` call that assigns `Data` and calls `TrySetAll`.

#### Error Handling

- Mutating an element that is removed (its page is null or detached) throws `InvalidOperationException`; the same
  setters work again after an undo re-attaches the element.
- Null arguments throw `ArgumentNullException`; unknown port names throw `ArgumentException`; ambiguous names throw
  `InvalidOperationException`; invalid bounds are rejected by the page with `ArgumentException`.
- Errors are propagated to the caller; the unit does not catch exceptions.

#### Dependencies

- DiagramPage, Diagram and the command classes of the Model subsystem for every mutation and for removal.
- Property Bag (`Properties`), Node Type Registry (`NodeType`, `PortDefinition`) and Geometry (`Rect`, `Point`).
- The Scene node geometry for port anchors and the diagram theme (reached through the page's diagram).

#### Callers

DiagramPage and Diagram create nodes and resolve ports; Wire and Group build on `DiagramElement`; the Scene builder, hit
testing, serialization and the Avalonia editor read nodes, ports and elements; host applications use the public
members.
