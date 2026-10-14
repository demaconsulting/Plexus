### NodeLayout Unit Design

![Scene Structure](SceneView.svg)

#### Purpose

Computes where everything of a node goes: the text-independent geometry shared by the model, the routers and the
painters (`NodeGeometry`), the full per-node layout used for drawing and hit testing (`NodeLayout`), and the wrapping
of annotation text (`TextWrapper`).

#### Data Model

- `NodeGeometry` (static): default node size (a size declared by the node type wins, else the theme's width and a height
  that fits the longer port column), required height for a port column, port edge point (left edge for inputs, right
  edge for outputs, at the row; given the column's port count, because centered rows depend on it), port label width
  (estimated by the approximate measurer, widened by a 5% safety factor
  and rounded up), port reach (triangle plus gaps plus label), port anchor (the outer end of triangle and label where wires
  attach), the node's visual bounds, and the routing obstacles (the inflated node box plus one strip per labelled port
  whose label reaches beyond the margin).
- Port rows follow `DiagramTheme.PortAlignment`. With `Center` (the default) each column (all inputs, all outputs) is
  a stack centered vertically on the box: row `i` of `n` lies at `Top + Height/2 + (i - (n-1)/2) * PortPitch`, so an odd
  count puts the middle port on the box center and an even count straddles it on half-grid positions (nodes still
  snap by their top-left corner). With `Top` row `i` lies at `Top + HeaderHeight + FirstPortOffset + i * PortPitch`, the
  previous behavior. The required height of a column of `n` ports is, for `Top`, `HeaderHeight + FirstPortOffset +
  (n-1) * PortPitch + PortBottomPadding`, and for `Center` twice the larger of (`HeaderHeight + FirstPortOffset`,
  `PortBottomPadding`) plus `(n-1) * PortPitch`, so the topmost centered anchor never rises above the header plus the
  first-port offset; the default height is the larger of the theme's default height and the required height of the
  longer column. Sizes declared on a type are not adjusted. Hit areas, triangles and labels are derived from the same
  row, and wires between two nodes whose ports have the same count and whose boxes have the same height and top are
  exactly straight.
- `NodeLayout` (immutable): `Node`, `Box` (equal to the node's bounds), `Bounds` (the union of everything drawn),
  `Title` (text and header band), `IconBounds` (nullable), `Name`, `SummaryLines` and `Ports`.
- `PortLayout`: `Port`, `Anchor`, `Triangle`, `HitBounds` and `Label`; `LayoutText` pairs a string with its rectangle.
- `TextWrapper` (internal static): no state.

Invariant: a port's `Anchor` equals `Port.Anchor` of the model, whichever measurer was used.

#### Key Methods

`NodeLayout.Compute(node, theme, measurer)`: the title is the header band across the top of the box; the icon, only
when the type has one, is a square of the theme's icon size shrunk to fit the body, centered below the header; the name
is centered above the box; summary lines stack downward from below the box, each centered; ports are laid out inputs
first, then outputs. For each port the edge point and anchor come from `NodeGeometry` (never from the measurer), the
triangle sits outside the edge pointing right, the hit rectangle spans from just inside the edge out to the anchor (so it
covers triangle and label), and the label is measured with the supplied measurer and placed beside the triangle,
vertically centered on the row. Postcondition: `Bounds` contains the box, name, summary lines, port triangles and labels.
Proxy ports of a collapsed group (`SceneComposer`) follow the same alignment: with `Center` each proxy column is centered
on the collapsed box (which grows to at least the title-bar height plus one pitch per proxy row), with `Top` the rows
start below the title bar as before.

`TextWrapper.Wrap(text, maxWidth, style, measurer)`: splits at line breaks, fills each line greedily with words that
still measure within the width and splits a single word wider than a line by character, returning at least one line.

Preconditions: arguments are not null.

#### Error Handling

`Compute` throws `ArgumentNullException` for a null node, theme or measurer. `NodeGeometry` members reject null
arguments in the same way. Text that does not fit is not an error: wrapping splits it, and labels may overlap on very
crowded nodes (a cosmetic effect only).

#### Dependencies

- DiagramTheme unit (metrics, text styles)
- TextMeasurement unit (`ITextMeasurer`, `ApproximateTextMeasurer`)
- Model subsystem types (`Node`, `Port`, `NodeType`)
- Geometry unit

#### Callers

- Model subsystem (`Port.Anchor`, node sizing, routing obstacles, group extents through `NodeGeometry`)
- `SceneBuilder` (layouts, wrapping), `StandardNodeVisual` and custom visuals through `NodeVisualContext`
- Painters and hosts reading layouts through `DiagramScene.TryGetNodeLayout`
