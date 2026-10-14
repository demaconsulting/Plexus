### Group Unit Design

![Model Structure](ModelView.svg)

Scope: Nestable, collapsible group of elements with computed bounds.

#### Purpose

The Group unit models a titled frame around nodes, annotations and other groups that moves, collapses and nests as a
unit. An element belongs to at most one group. While expanded, the frame bounds are computed from the members; while
collapsed, the group is a fixed-size box and its members are hidden. Groups are created and dissolved through the page
API (see DiagramPage Design); this unit owns the group state and the bounds computation.

#### Data Model

- `Members`: the direct members in order, as a read-only observable collection over a private list; each member's
  `Parent` points back to the group. Members are nodes, annotations and groups of the same page, never wires.
- `Title`: the title shown in the title bar, never null.
- `IsCollapsed` and `CollapsedPosition`: the collapsed flag and the top-left corner of the box while collapsed. The
  position is set when the group is created and when collapsing, and it moves with the group.
- `Bounds` (and `GetBounds()`): computed, never stored. Expanded with members: the union of the member extents inflated
  by the theme group padding, with the title bar height added on top. Collapsed or empty: a box of the theme's
  collapsed width and height at `CollapsedPosition`.
- A node contributes its full visual extent (name above, summary lines below, port labels at the sides) rather than
  its box, an annotation its bounds, and a nested group its own computed bounds, so bounds are computed recursively.
- Invariant: a group is never its own ancestor.

#### Key Methods

- `AddMember(element)`: through the page; rejects wires, elements of other pages, and the group itself or an ancestor.
  An element already in this group is ignored; otherwise one history entry removes the element from its old group (if
  any) and appends it to this one.
- `RemoveMember(element)`: through the page; the element must be a direct member and becomes ungrouped.
- `AllMembers()`: iterator that yields each member and, after a nested group, that group's own members, depth first.
- `Title` setter: an undoable value command "Rename group"; an equal title is ignored.
- `IsCollapsed` setter: through the page; an unchanged state is ignored. Collapsing remembers the current expanded
  top-left corner as the collapsed position; members keep their own positions, so expanding restores the layout
  exactly. History entries are named "Collapse group" and "Expand group".
- `ExpandedBounds()`: the expanded computation, also used to pick the collapsed position.
- Internal command helpers `InsertMemberCore`, `RemoveMemberCore`, `SetCollapsedCore` and `SetCollapsedPositionCore`
  apply state and raise notifications; `NotifyBoundsChanged` reports a bounds change on the group and on every ancestor
  group because their bounds depend on it.

#### Error Handling

- A null title or element throws `ArgumentNullException`; a wire, a foreign element or a non-member for removal throws
  `ArgumentException`; adding the group to itself or to a descendant, or changing a removed group, throws
  `InvalidOperationException`.
- Exceptions are propagated and leave the group unchanged. A member of an unsupported kind makes the bounds computation
  throw `InvalidOperationException`; the page API prevents this state.

#### Dependencies

- DiagramPage (membership, collapse and layout commands) and the undo history; Node (visual extent through the Scene node
  geometry), Annotation, the diagram theme for padding, title height and collapsed size, and Geometry (`Rect`).
  DiagramPage reads `Bounds` of every visible group (through the internal `GroupObstacles`) to offer group frames to routers
  as obstacles; groups themselves do not route.

#### Callers

DiagramPage creates, dissolves, moves and removes groups and updates membership; the Scene builder draws frames and hides
collapsed members; hit testing, serialization, duplicate and clipboard walk members; the Avalonia editor reads and
edits groups through the public members.
