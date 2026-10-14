### DiagramTheme Unit Design

![Scene Structure](SceneView.svg)

#### Purpose

Holds the metrics, colors and fonts that the model, the routers and every painter share, so that a node's port anchors,
obstacle boxes and drawn boxes always agree, and provides the classic default look.

#### Data Model

`DiagramTheme` is a sealed partial record with init-only properties, split across two files:

- Geometry metrics: grid size, default node size, header height, port pitch, port alignment (`PortAlignment`: `Center`,
  the default, centers each port column vertically on the node box; `Top` starts it below the header, see NodeLayout),
  first-port offset (the header clearance), port bottom
  padding, icon box size, wire corner radius, obstacle margin, wire stub length, group padding, group title height and
  collapsed group size, and `GroupsAreObstacles` (default true), which makes the frames of groups that contain neither
  end node of a wire routing obstacles for it (see Routing design, "Groups as obstacles").
- Appearance: colors (canvas, grid dots, node fill and border, texts, wire, selection, hover, annotation, group),
  border and wire thicknesses, font family and per-role font sizes, port triangle size, hit radius, label gap, arrowhead
  settings, wire junction dots (`WireJunctionDots`, default true, and `WireJunctionRadius`, default 3), small-jog
  smoothing (`WireSmoothSmallJogs`, default true: a step shorter than twice `WireCornerRadius` is drawn as one
  S-curve), crossing jumps (`WireCrossingJumps`, default true: a horizontal wire hops over a vertical wire of another
  net, and `WireJumpRadius`, default 4), the size of the handles of a selected wire (`WireHandleSize`, default 8), text
  gap, annotation padding,
  resize handle size and group toggle size.
- Derived `TextStyle` values (`HeaderTextStyle`, `NameTextStyle`, `SummaryTextStyle`, `PortLabelTextStyle`,
  `GroupTitleTextStyle`, `AnnotationTextStyle`) computed from the font and color settings; `TextStyle` pairs a font
  family, size, color, weight and `TextAlignment`.
- `Classic`: one shared, never modified instance created once.
- `Dark`: one shared, never modified instance created once. Only colors differ from `Classic` (canvas, grid dots, node
  fill and border, instance name, summary, port label, wire, selection and hover, annotation fill, border and text,
  group fill, border, title bar and title text), chosen so that texts reach a 4.5:1 contrast ratio and lines and
  outlines a
  3:1 ratio against their background; every metric equals the classic value so the two can be exchanged without moving
  anything.

Invariants: instances are immutable; a customized copy is made with a `with` expression, which leaves the original
unchanged.

#### Key Methods

The unit is data. The text style properties construct a new `TextStyle` from the current settings on each read, so a
customized copy always reports styles consistent with its settings. `Classic` returns the shared instance.

#### Error Handling

N/A - the theme validates nothing and throws nothing; it is a plain value holder. Consumers that divide by or loop over
a metric (for example a grid size of zero or less) treat non-positive values as "disabled" or reject them locally.

#### Dependencies

- Geometry unit (`Color`)

#### Callers

- Model subsystem (`Diagram.Theme`, node sizing, anchors and obstacles)
- Scene subsystem units (`NodeLayout`, `SceneBuilder`, `DiagramScene`)
- PlexusRendering and PlexusAvalonia painters
