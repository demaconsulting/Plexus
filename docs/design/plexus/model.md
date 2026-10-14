## Model Subsystem Design

![Model Structure](ModelView.svg)

Scope: Diagram document model: diagram root, pages, nodes, ports, wires, annotations and groups with undoable mutation
and change notification.

### Overview

The Model subsystem is the document model of Plexus. A `Diagram` is the root of a document; it owns the node-type
registry supplied by the host, an ordered, observable `PageCollection`, one shared undo history and the diagram-wide
settings. Each `DiagramPage` is a canvas holding nodes (with their ports), wires, annotations and nestable groups. The model
is UI-free, single-threaded and the only place where document content is created, changed and removed. Every mutation
is expressed as an undoable command that runs through the history of the diagram, and every applied change, including
undo and redo, is announced by the diagram's `Changed` event.

The subsystem contains seven units: Diagram, PageCollection and DiagramPage (documented in their own designs) and the element
units Node (with its ports), Wire, Annotation and Group. All elements derive from a common element base with an id, the
page that holds it, the group that contains it, a bounds query and a property-changed event.

### Interfaces

- **Diagram**: construction from a node-type registry; the `Pages`, `History`, `Types` properties; the theme, router,
  connection rule and unique-name settings; lookup of elements, nodes and ports; rename of nodes; move of elements to
  another page; and the events `Changed`, `NodeRenaming` and `NodeRenamed`.
- **PageCollection**: undoable add, insert, remove and move of pages, a read-only list view and collection-changed
  notifications.
- **DiagramPage**: undoable creation of nodes, annotations and wires, connection checks, groups, move, remove and re-routing,
  and the observable content lists.
- **Elements**: nodes expose properties, ports and bounds; wires expose their ports and route; annotations expose text,
  fill and bounds; groups expose title, members, collapse state and bounds. Their property setters call back into the
  owning page or diagram so that the change is undoable and announced.
- **Dependencies of the subsystem**: Types (node types and registry), Properties (node property bags), History (undo
  history and commands), Geometry, ConnectionRules (host connection decision), Routing (wire router) and Scene (theme and
  node geometry used for default sizes and port anchors).

### Design

- **Everything through the history.** Pages and elements never change their own state directly. A public operation
  validates its arguments first and then executes a command; the command's do and undo actions call internal "core"
  members that change state and raise notifications. As a result undo, redo and the original operation use exactly the
  same code path, which is why every change, including undo and redo, is observable and why a failed validation can
  never leave a partial change behind.
- **Exact restoration.** Commands remember positions (typed list index, z-order index, member index) and object
  identity, so undo restores the very same objects in the same places. Hosts and controls may therefore hold element
  references across undo and redo.
- **One entry per user action.** Multi-step operations (create group, remove, move to page, re-route) are built as
  command plans or transactions and recorded as one composite entry. A failing step reverts the earlier ones. Moves
  are mergeable so a drag is one entry.
- **Consistent notifications.** Attach and detach complete before any notification is raised, so observers never see
  a half-attached element or page. One diagram-level event serves painters and controls; the observable collections
  serve data binding.
- **Settings are configuration, not content.** Theme, router, connection rule and name uniqueness raise a settings
  change but are not undoable, so undo never reverts host configuration.
- **Wires follow nodes.** Wires are not moved directly: when nodes move, a wire whose two ends moved alike has its
  route translated, otherwise it is re-routed; wires never span pages and are removed with their nodes. A re-routed
  wire keeps clear of the wires of other nets (a net is every wire from one output port).
- **Wire editing.** Every stored route is normalized (collinear bends and zero-length segments removed). A wire can
  add a jog (`AddJog`) and move one or both ends to other ports (`Reconnect`, validated like a connection but
  ignoring the wire itself, keeping the wire's identity and re-routing it); each is one undo entry.
- **Identity.** Elements have unique ids (generated GUID text unless supplied); nodes also have names, unique by default
  across all pages. The diagram keeps an index of attached elements for lookup by id.
- **Threading.** The model is not thread-safe; all access must come from the owning thread.

**Per-node ports.** For a node whose type declares a port provider, the Node unit recomputes the ports after every
property or payload change (`Node.ExecuteAffectingPorts`, called by the Property Bag and by the `Data` setter) and the
DiagramPage unit plans the consequences in the same `CommandPlan`: wires of removed ports are removed, the ports are
swapped by name (`NodePortsCommand`), wires of retyped ports are checked again with the connection rules, the node grows
to fit and the wires whose anchors moved are re-routed (`DiagramPage.PlanPortUpdate`). The whole change is one undo
entry, and hosts observe it through the usual element notifications: wire removals first, then the node's `Ports`
change. A port taken off its node is refused by connection checks. See Node Design and DiagramPage Design.
