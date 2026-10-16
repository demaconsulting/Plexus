### PropertyBag Unit Design

![Properties Structure](PropertiesView.svg)

Scope: Per-node property storage with typed values and automatic undo.

#### Purpose

PropertyBag stores the current property values of one node: the declared properties of the node type, starting at their
defaults, plus ad-hoc values under undeclared names. It validates and converts every assignment, makes every change an
undoable history entry, and announces changes. It is exposed as a read-only dictionary of names to values with an
indexer that also accepts assignments.

#### Data Model

- **Owner**: the node the bag belongs to; it supplies the node type (declarations) and the page and diagram (history and
  notifications).
- **Values**: a dictionary from property name (ordinal comparison) to the current canonical value; every declared
  property always has an entry.
- **Ad-hoc order**: the names of ad-hoc values in insertion order.
- **Invariants**: a declared property always holds a value legal for its kind and choices; an ad-hoc value is always one
  of the canonical portable kinds; enumeration yields declared properties in declaration order followed by ad-hoc
  values in insertion order; the property count equals the number of declared plus ad-hoc values.
- **Notification name**: changes are reported with the property name and with the indexer name used by the
  property-changed convention. While the node is on a page both notifications are raised through the diagram history's
  notification gate, so a throwing handler cannot interrupt an undoable change; its exception is rethrown after the
  change was recorded (see Diagram Design).

#### Key Methods

- **Constructor (internal)**: copies the default value of each declared property of the node type.
- **Indexer get / TryGetValue / ContainsKey / Get&lt;T&gt;**: read by name. Get&lt;T&gt; returns the value when it is
  already of the requested type, otherwise converts between the numeric kinds (int, long, double) and primitive
  targets only when the converted value represents exactly the stored one (the converted value is converted back and
  must equal the stored value); a value that does not fit, loses precision (1.5 as int, a large long as double) or is a
  non-numeric mismatch fails.
- **Indexer set (Set)**: precondition: the node is attached to a page, the name is not empty and the value is not null.
  The value is coerced to the declaration (declared names) or normalized (ad-hoc names). When the name exists and the
  canonical value differs from the current value, a set-value command is executed through the diagram history; when the
  value is equal nothing is recorded; when the name is new, an add command is executed that inserts the value and its
  insertion position. Postcondition: the stored value is canonical and one history entry exists per real change.
- **Reset(name)**: precondition: the node is attached and the name exists. A declared property is set back to its
  default through the same command (nothing when already default); an ad-hoc property is removed by a command that
  remembers its position so undo restores it exactly there.
- **GetDefinition(name)**: returns the node type's declaration, or null for ad-hoc names.
- **Enumeration (Keys, Values, GetEnumerator)**: declared properties first, then ad-hoc ones.
- **Apply and raise (private)**: storing a value, whether by do, undo or redo, raises the bag's property-changed event
  twice (property name, then indexer name) and reports a property change to the node, which in turn raises the
  diagram's element-changed event.
- **Merging**: the set-value command identifies its target by the bag and the name, so repeated assignments of one
  property inside a transaction collapse into one entry that remembers the first old and the last new value.
- **Port recomputation**: every command (set, reset, ad-hoc add and remove) is run through the node's
  `ExecuteAffectingPorts` instead of the history directly. For a type with a `NodeType.PortProvider` the node's ports
  are recomputed after the value is stored and recorded in the same entry (see Node Design and DiagramPage Design); for
  other types, and when the ports do not change, the command alone is recorded as before, so merging is unaffected.
  Inside `TrySetAll` the commands are executed with the node's port recomputation deferred (`Node.DeferPortUpdates`), so
  the provider runs once, for the final state of the batch, and not after each value; a batch edit is still one entry,
  and a provider failure rolls the batch back and propagates.

`TrySetAll(changes, out errors)`: resolves every entry first (a string for a property that is not a string property is
read with `PropertyDefinition.TryParse`; other values are coerced like an assignment; undeclared names are normalized as
ad-hoc values; null and empty names are errors), collects a message per rejected name and returns false without touching
the bag when any entry failed; otherwise it applies all entries inside `History.Transact("Edit properties", ...)` with
the port recomputation deferred, so the result is one undo entry (none when nothing changes) and the ports are
recomputed once, for the final values.

#### Error Handling

- Reading a missing name raises a key-not-found error; an impossible typed conversion raises an invalid-cast error
  (including numeric overflow and a conversion that would lose precision).
- A null name raises an argument-null error; an empty name on write raises an argument error; a null value raises an
  argument-null error; a value that is not legal for the declaration raises the argument error produced by the shared
  value rules. In all these cases the bag and the history are unchanged because validation happens before any command is
  executed.
- Writing or resetting while the node is not attached to a page raises an invalid-operation error (the user must undo
  the removal first).
- Errors thrown while executing a command propagate through the history, which records nothing for a failed command.

#### Dependencies

The bag cannot exist without a node, so it depends on units outside the Properties subsystem:

- **Node (Model)**: the owner; the bag reads its node type, requires it to be attached to a page, and reports changes
  through it. Unit tests and subsystem tests therefore create a node through a diagram, a page and a node type
  registry.
- **NodeType (Types)**: supplies the ordered declarations and lookup by name.
- **UndoHistory (History)**: reached through the page's diagram; every change is executed as a command, including the
  internal set-value command.
- **PropertyDefinition and the shared value rules** (same subsystem): validation, coercion and defaults.

#### Callers

- The Model subsystem creates one bag per node and exposes it as the node's property collection.
- The Serialization subsystem reads the values for snapshots and the clipboard and assigns them when importing.
- Host applications and editors read and assign properties, reset them, and observe the notifications; the Scene
  subsystem reads values through the node type's summary lines.
