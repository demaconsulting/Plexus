### Diagram Unit Design

![Model Structure](ModelView.svg)

Scope: Diagram root: node-type registry, pages, history, theme, router, connection rules, change notification, rename
pipeline and move-to-page.

#### Purpose

Diagram is the root object of a Plexus document. It owns the node-type registry supplied by the host, the ordered page
collection, the single undo history shared by all pages and the diagram-wide settings (theme, wire router, host
connection rule and the unique-node-name option). It indexes every attached element by id, offers lookup of elements,
nodes and ports across pages, raises the one `Changed` event through which painters and controls learn about every
change, implements the node rename pipeline and moves elements between pages. The unit comprises `Diagram.cs`, the
partial files for renaming and moving to a page, the change event data and kind enumeration, and the two rename event
data classes. JSON snapshots are further partial members of the same class that belong to the Snapshot unit.

#### Data Model

- **Types, Pages, History**: read-only properties set by the constructor; `Pages` is created with a reference back to the
  diagram and `History` is a new undo history.
- **Settings**: `Theme` (default classic theme), `Router` (default orthogonal router), `ConnectionRules` (default
  `ConnectionRules.Default`, which refuses a second wire into an input whose port does not allow multiple wires and
  allows every other structurally valid connection; a host composes it with its own rule through
  `ConnectionRules.All` to keep that behavior, or replaces it to allow fan-in everywhere) and `RequireUniqueNodeNames`
  (default true). They are plain fields, not part of the history.
- **Wire styler**: `WireStyler`, an optional `Func<Wire, WireStyle?>` (default null, theme-only wires) in the partial file
  `Diagram.Appearance.cs`. It is configuration like the settings: not undoable, not serialized.
- **Node styler**: `NodeStyler`, the node equivalent, an optional `Func<Node, NodeStyle?>` (default null, theme-only
  nodes) in the same partial file, with the same rules.
- **Version**: `Version`, a `long` that starts at zero and is incremented by `RaiseChanged`, before `Changed` is raised.
  Every change notification (edits, undo, redo, settings, `RefreshAppearance`) therefore advances it, handlers already see
  the new value, and reads or scene builds never touch it. It is a cache key for values a host derives from the diagram;
  `WireStylers.PerPage` and `NodeStylers.PerPage` build on it.
- **Element index**: a dictionary from element id to element containing exactly the elements attached to pages of the
  diagram. Pages register and unregister elements as they attach and detach.
- **Events**: `Changed` carries a change kind (page added, removed, moved or renamed; element added, removed or
  changed; settings changed), the affected page and element, and the property name. `NodeRenaming` carries the node, the
  old name, the proposed name, a settable new name and a cancel flag with reason. `NodeRenamed` carries the node, the old
  name and the new name.
- **Single-threaded**: the model is not thread-safe and all calls must come from the owning thread.

#### Key Methods

- **Constructor(types)**: rejects null; creates the history and the page collection.
- **Settings setters**: reject null (except the boolean), store the value and raise `Changed` with the settings kind and
  the setting name. They record nothing in the history.
- **WireStyler setter**: stores the callback (null allowed) and raises `Changed` with the settings kind and the name
  `WireStyler`. **RefreshAppearance()**: raises `Changed` with the settings kind and the name `Appearance`, so editors
  and painters rebuild when the styler depends on host state outside the diagram; nothing is recorded in the history.
  **ResolveWireStyle(wire)** (internal): called by the scene builder for each wire on every build; returns null
  without a styler and also when the styler throws (any exception except out-of-memory), so a faulty styler never
  breaks rendering. **NodeStyler setter** and **ResolveNodeStyle(node)** (internal) behave the same way for nodes (settings
  change named `NodeStyler`; null without a styler and when the styler throws). **RefreshAppearance()** serves both
  stylers.
- **FindElement(id)**: dictionary lookup returning null for unknown ids. **FindNode(name)**: first node in page order
  whose name equals the argument case-sensitively. **FindPort(path, direction)**: asks each page in order for the port
  and returns the first match; the path rules (name or id, dots in names, direction) are those of the page lookup.
- **RaiseChanged / Register / Unregister** (internal): used by pages and the page collection to publish changes and keep
  the index current. `RaiseChanged` drops the routing caches when the theme changed, advances `Version` (always, also
  when a handler throws) and raises `Changed` through the history's notification gate, so during an undoable change a
  throwing handler is captured and rethrown after the change.
- **Perform(description, plan)** (internal): runs a multi-step operation through a command plan as one history
  operation. If a step throws, the plan reverts all applied steps (`Abort(cause)`; when a revert fails too an
  `AggregateException` with the original exception first is thrown), handler exceptions captured meanwhile are dropped
  and the exception propagates; otherwise the steps are recorded as one history entry, or nothing when no step ran, and
  then the first captured handler exception is rethrown.
- **TryRename(node, proposed, reason)**: checks the node is attached to this diagram, then (1) trims and validates the
  name (non-empty; unique unless disabled), (2) returns success without a change when the name equals the current one,
  (3) raises `NodeRenaming` so the host can correct or cancel, (4) re-validates a corrected name, and (5) executes an
  undoable set-name command. **Rename(node, proposed)** throws an argument exception carrying the reason instead of
  returning false.
- **ApplyNodeName** (internal): used by the command for do, undo and redo; sets the name, raises the node's property
  change (which becomes `Changed`) and then `NodeRenamed`, both through the history's notification gate. `NodeRenaming`
  (the veto hook, raised before anything changes) is not gated: its exception aborts the rename before any change.
- **GenerateNodeName / EnsureIdAvailable / ValidateNodeName** (internal): generate the lowest unused `{TypeKey
  last segment}{n}` name, reject blank or used ids, and apply the default name rules; used by pages when adding nodes.
- **MoveToPage(elements, target)**: validates the target and elements; expands groups to their members; drops wires and
  elements already on the target; then performs one multi-step operation that detaches every affected wire (keeping the
  ones whose two nodes both move), releases elements from groups that do not move as a whole, detaches the elements from
  their source pages in z-order, and attaches them to the target in the same relative order followed by the carried
  wires. Presence and membership commands remember positions, so undo restores every list position and membership.

`DiagramOptions` (immutable record: theme, router, connection rule, wire and node stylers, unique-names flag, the
unknown-node-type policy used by loaders and the `Initialize` callback) and `LoadReport` (what a skipping load left
out): `Diagram(types, options)`
writes the members that are set straight into the fields, because a constructor raises no change notification;
`Diagram.Apply(options)` assigns each member that is set through the public setter, so it raises `SettingsChanged` per
member and is, like every setting, not undoable. A null member always means "keep the default or the current value"; to
remove a styler assign null to the property. `Initialize` (`Action<Diagram>`) lets a host attach per-instance event
handlers in one place: the constructor invokes it last, the loaders (`FromJson`, `FromSnapshot`, `FromTree`) build the
diagram with a copy of the options whose `Initialize` is null and invoke it after the whole document is loaded and the
history is cleared (so a handler never sees a half-built diagram), and `Apply` invokes it after the settings, on every
call.

#### Error Handling

Null arguments throw argument-null exceptions. A rename of a node that is not attached to the diagram, a move to a page
or of elements that are not part of the diagram, a blank or duplicate id, and a rejected `Rename` throw argument
exceptions; `TryRename` reports rename rejections through its return value and reason instead. Failing multi-step
operations are rolled back by `Perform`, so an exception never leaves the model partially changed. Event handlers are
invoked synchronously on the calling thread:

- During an undoable change (an execute, a multi-step operation, an undo, a redo or a rollback), an exception thrown by
  a handler of `Changed`, `NodeRenamed`, an element's, page's or property bag's `PropertyChanged`, a page content
  collection or the page list (any exception except running out of memory) does not interrupt the change: the remaining
  notifications are raised, the change is completed and recorded (or its entry moved), and the first handler exception
  is then rethrown to the caller with its original stack trace; later ones of the same change are dropped.
- Inside `UndoHistory.Transact` the rethrown exception reaches the transaction delegate, so the whole transaction is
  rolled back and the exception propagates; handler exceptions raised while a failure is rolled back are dropped. An
  explicit `HistoryTransaction.Rollback()` rethrows the first one after the rollback completed.
- When the operation itself fails (validation, provider failure, failing command) its own exception propagates, the
  model is reverted, nothing is recorded and handler exceptions are dropped.
- Outside a history operation (the settings setters and `RefreshAppearance`, which are not undoable) a handler
  exception propagates immediately.

#### Dependencies

- Types: the node-type registry held by the diagram and used to create nodes.
- History: the undo history, set-value and command-plan commands.
- Scene: the diagram theme and node geometry used by pages (the theme setting), and `WireStyle` and `NodeStyle` (the
  stylers' results).
- Routing: the wire router setting and its default orthogonal router.
- ConnectionRules: the connection validator type and its default rule.
- DiagramPage and PageCollection: pages hold the content, register elements in the index and use `Perform`, name and id
  helpers.

#### Callers

Host applications create and configure the diagram and call its lookup, rename and move-to-page operations. Pages and the
page collection call the internal helpers. Painters, the scene builder and the editor control subscribe to `Changed`,
`NodeRenaming` and `NodeRenamed`; the Snapshot unit reads and fills the diagram through its public members.
