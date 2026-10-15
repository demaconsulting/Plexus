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
  `Outputs` and `Ports` (inputs first; read-only views over private lists, so they are live), the `Properties` bag
  (see Property Bag Design), `Position` (computed from the bounds) and `Wires` (computed from the page, in page order,
  empty while removed).
- `Port`: `Node`, `Definition`, `Direction` and `Index` (row within its column, inputs and outputs counted
  separately), plus `Name`, `Label`, `TypeKey` and `Description` forwarded from the definition, and the internal
  `IsOnNode` (whether the port is still in its node's column). `Anchor` and `Wires` are derived from the node's bounds
  and the page. The constructor creates the ports of the type's declared `Inputs` and `Outputs`; for a type with a
  `NodeType.PortProvider`, `InitializePorts` replaces them once the properties and data are known.
- Port identity invariant: within a direction, the port name is the identity of a port. When ports are recomputed a
  surviving name keeps its `Port` object (only `Definition` and `Index` change, through the internal `Port.Update`),
  so ports can be compared by reference and kept by hosts and wires. A port whose name disappears leaves the node's
  lists (its `IsOnNode` becomes false) and returns, as the same object, when the change is undone. Ports of a type
  without a provider never change.
- `NodePortsCommand` (internal): the old and new state of a port change, each a list of (port, definition, row) slots
  per direction, plus `RemovedPorts` and `RetypedPorts` (kept ports whose type key or fan-in flag changes).

#### Key Methods

- `Node.Bounds` setter and `Node.MoveTo(x, y)`: delegate to the page layout pipeline, which validates the box,
  ignores an unchanged box, and records one undoable layout command that also updates attached wire routes (see DiagramPage
  Design). The stored box changes only inside that command.
- `Node.Data` setter: checks the value against `NodeType.DataType` first (an instance of an incompatible type throws
  `ArgumentException`; a derived type, null and any value for a type without `DataType` pass), then replaces the payload
  through an undoable value command (run by `ExecuteAffectingPorts`, below); an equal value is ignored.
  `DiagramPage.AddNode` stores the payload produced by
  `NodeType.CreateData` (validated against `DataType`, else `InvalidOperationException`) before the add command runs.
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
- `DiagramElement.NotifyChanged(name)`: drops the page's routing caches, raises `PropertyChanged` (through the
  history's notification gate while the element is on a page, so a throwing handler cannot interrupt an undoable
  change) and reports an element-changed event to the diagram.

- `Node.InitializePorts()` (internal): for a type with a provider, asks the provider (`NodeType.ResolvePorts`) and
  rebuilds the port lists without history, reusing ports by name. Called for a detached node by `DiagramPage.AddNode`
  (after the default payload) and by the importer (after the loaded properties and data, before any wire).
- `Node.ExecuteAffectingPorts(command)` (internal): the single entry point for property and data changes. Without a
  provider it is `UndoHistory.Execute(command)`. With one it opens a `CommandPlan`, runs the change, asks the page to
  plan the port update (`DiagramPage.PlanPortUpdate`) and records the plan as one entry; when the plan holds only the
  change (the ports did not change) the change itself is recorded, so mergeable value commands keep merging. The plan
  runs as one history operation (`UndoHistory.BeginOperation`/`EndOperation`), so a throwing change handler does not
  interrupt it and its exception is rethrown after the entry was recorded. Any exception of the plan itself aborts it
  with `CommandPlan.Abort(cause, history)` (reverting every applied step, the change included, even when one revert
  fails, under the history's replay guard) and
  propagates; handler exceptions captured meanwhile are dropped.
- `Node.DeferPortUpdates(action)` (internal): runs an action, normally inside an undo transaction, during which
  `ExecuteAffectingPorts` only executes each command through the history (no provider call, no planning) and remembers
  that the ports may be stale. A nesting counter makes the outermost scope the only one that recomputes: when it ends
  normally and a command ran, a plan holding only `DiagramPage.PlanPortUpdate` runs as one history operation (like
  above) and is recorded as one more command when it is not empty. When the action throws nothing is recomputed; when
  the provider or a rule throws, the plan is aborted and the exception propagates, so the enclosing transaction rolls
  every change back. Used by `PropertyBag.TrySetAll` (the ports follow the final values of the batch) and by the node
  property source of the property grid (the ports follow the final properties and data of one commit).
- `NodePortsCommand.TryCreate(node)`: asks the provider; when every new definition is equivalent
  (`PortDefinition.IsEquivalentTo`) to the current one in the same row it returns null. Otherwise it builds each new
  column by reusing the current port of the same name or creating a new port, and records removed and retyped ports.
  `Do`/`Undo` apply a state: update every slot's port (definition, row), replace the node's lists and drop the page's
  routing caches; they raise no notification.
- `NodePortsCommand.CreateNotificationSteps(node)`: returns two marker steps sharing a "settled" flag. The opening step
  (recorded first in the plan, before the triggering change) does nothing when applied and, when reverted after the
  closing step has run at least once, reports the change; the closing step (run last) sets the flag and reports the
  change, and does nothing when reverted. Reporting means `PropertyChanged("Ports")` on the node followed by a `Bounds`
  change of every ancestor group, whose frames follow the node's visual extent. So the node reports `Ports` exactly once
  per change, undo and redo, after every wire removal or re-add, bounds and route change and (on undo) the restored
  value; a recomputation rolled back before its closing step ran reports nothing.

`CloneData()`: serializes the payload with `NodeDataCodec` (the data serializer or exact data type of the node type) and
deserializes it again, so the copy is exactly what saving and loading would produce; null data gives null, and data of
a type that declares no way to copy it gives an `InvalidOperationException` that names the node type. Committing the
edited copy together with property changes is one `Transact` call that assigns `Data` and calls `TrySetAll`.

#### Error Handling

- Mutating an element that is removed (its page is null or detached) throws `InvalidOperationException`; the same
  setters work again after an undo re-attaches the element.
- Null arguments throw `ArgumentNullException`; unknown port names throw `ArgumentException`; ambiguous names throw
  `InvalidOperationException`; invalid bounds are rejected by the page with `ArgumentException`.
- A port provider that returns null makes `NodeType.ResolvePorts` throw `InvalidOperationException` naming the type and
  node; an invalid `PortSet` throws `ArgumentException` from its constructor; an empty `PortSet` is valid and gives the
  node no ports in that direction. During a property or data change any exception of the provider (or of a connection
  rule while wires are re-checked) aborts the plan, so the change is reverted, nothing is recorded, no `Ports` change is
  reported and the exception propagates unchanged; when a revert fails too, an `AggregateException` carrying the
  original exception first is thrown. When adding a node, the node is not added.
- Errors are propagated to the caller; apart from aborting the plan the unit does not catch exceptions.

#### Dependencies

- DiagramPage, Diagram and the command classes of the Model subsystem for every mutation and for removal.
- Property Bag (`Properties`), Node Type Registry (`NodeType`, `PortDefinition`, `PortSet`, the port provider) and
  Geometry (`Rect`, `Point`).
- History (`CommandPlan`, `UndoHistory`) for the single undo entry of a change that recomputes ports.
- The Scene node geometry for port anchors and the diagram theme (reached through the page's diagram).

#### Callers

DiagramPage and Diagram create nodes and resolve ports (and `DiagramPage.PlanPortUpdate` plans around
`NodePortsCommand`); Property Bag calls `ExecuteAffectingPorts` for every property change; the importer calls
`InitializePorts`; Wire and Group build on `DiagramElement`; the Scene builder, hit
testing, serialization and the Avalonia editor read nodes, ports and elements; host applications use the public
members.
