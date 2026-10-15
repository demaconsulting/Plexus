## Properties Subsystem Design

![Properties Structure](PropertiesView.svg)

Scope: Typed, undoable property definitions and per-element property bags.

### Overview

The Properties subsystem models the editable state of nodes. A host declares, per node type, a list of property
definitions (name, value kind, default, optional choices, presentation hints). Every node created from that type owns
a property bag that holds the current values: each declared property starting at its default, plus any number of
ad-hoc values the host attaches under undeclared names. All values are limited to a small portable set of kinds
(boolean, 32-bit integer, 64-bit integer, double, string and color, plus string-constrained choices), which keeps
snapshots and the clipboard able to round-trip every value without type-name resolution.

The subsystem contains two units: PropertyDefinition (with the value-kind enumeration and the shared value rules; see
PropertyDefinition Design) and PropertyBag (see PropertyBag Design).

### Interfaces

- **PropertyDefinition** is consumed by the Types subsystem: a node type owns an ordered list of definitions, checks
  them for duplicate names when it is registered and freezes the list, and offers lookup by name and default summary
  lines built from the definitions.
- **PropertyBag** is exposed by every node as its property collection. It offers an indexer, typed reads, lookup of the
  declaration, reset, enumeration as a read-only dictionary, and a property-changed notification. Its batch edit
  `TrySetAll(changes, out errors)` validates every requested value first (reading text for typed properties) and then
  applies all of them as one undo entry, or none of them when any is invalid (every rejected property gets a message);
  for a node whose type has a port provider the ports are recomputed once inside that same entry.
- **Display-order hint**: `PropertyDefinition.Order` is an `int` hint (default 0, any value including negative, not
  validated) for property editors: a categorized editor lists the properties of a category by ascending `Order`, equal
  values by display name (case-insensitive, culture-invariant); an alphabetical view ignores it. It never changes the
  order of the node type's property list, the bag's enumeration, node summaries or saved documents. The Plexus property
  grid is the consumer (it maps the hint to its item rank).
- **Consumes**: the owning node (its type and its page), the page's diagram history for undo, and the diagram change
  notification. These are the documented dependencies on the Model, Types and History subsystems; see PropertyBag
  Design for how each is used.

### Design

- **Declarative kinds.** The value kind of a definition is inferred from the type of its default value, and a choice
  list turns a string property into a choice property. Validation of defaults and choices therefore happens when the
  declaration is written, so a node can never be created with an illegal value.
- **One set of value rules.** An internal helper normalizes arbitrary supported values to canonical types, coerces a
  value to a declared kind and formats values for display. Both units use it, so definitions, bags and serialization
  agree on which values are legal and how numbers convert. Numbers convert only when the value is preserved exactly
  (a 64-bit integer converts to a double only when the double converts back to the same integer).
- **Hints never restrict.** Presentation members of a definition (display name, category, unit, editor hint,
  read-only flag, summary flag) are for user interfaces only; programmatic changes through the bag are governed by the
  kind and choices alone.
- **Changes are history entries.** Every bag assignment is executed through the diagram's undo history, using a
  set-value command that merges repeated assignments of one property inside a transaction. A no-op assignment records
  nothing, and a bag whose node is removed from its page refuses changes because no history is available for it.
- **Bag test setup.** Because a bag cannot exist outside a node, the subsystem tests build a registry with one node
  type, a diagram and a page, and add a node; no other Model features are exercised.

**Port recomputation.** Property changes of a node whose type declares a port provider also recompute the node's ports
in the same undo entry: the Property Bag hands each command to `Node.ExecuteAffectingPorts` (Model subsystem) instead
of executing it on the history directly. See PropertyBag Design.
