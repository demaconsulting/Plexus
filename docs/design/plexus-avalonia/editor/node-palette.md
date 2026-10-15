### NodePalette Unit Design

![Editor Structure](EditorView.svg)

Scope: Palette control that lists node types, each drawn as it looks on the canvas, and lets the user drag or activate
them.

#### Purpose

`NodePalette` is the control a host places beside a `DiagramEditor` to let users pick the nodes to add. It works with
zero code: `<plexus:NodePalette Editor="{Binding #Editor}"/>` lists the node types of the editor's diagram under a
search box, follows the editor when another diagram is opened, drags a node type onto the editor (which accepts the
drop itself) and adds a node at the editor's center on double-click or Enter. Without an editor it works from a node
type registry alone (`<plexus:NodePalette Types="{Binding Registry}"/>`). Every type becomes an item that shows the node
exactly as a freshly added node of that type looks on the canvas. The unit also contains the helpers that make the look
identical: `NodePrototype` and `NodePrototypeCache`, which the editor's drop ghost uses as well, and the automation
peers that make the items scrollable and invocable for UI automation tools.

#### Data Model

| Field / property      | Meaning                                                                                      |
|-----------------------|----------------------------------------------------------------------------------------------|
| `Types`               | The registry, or any sequence of node types, to show; wins over the editor's types           |
| `Editor`              | The editor to follow and to add nodes to on activation                                       |
| `DiagramTheme`        | The theme to draw with; wins over the editor's theme; null means the editor's, else classic  |
| `Zoom`                | The scale items are drawn at, clamped to the editor's zoom range (default 1)                 |
| `Filter`              | Case-insensitive text that must appear in the display name, title, key or category           |
| `ShowFilter`          | Whether the search box is shown above the entries (default true)                             |
| `MaxColumns`          | The most columns of items in a category; zero (default) means as many as fit; negative is 0  |
| `MinColumnSpacing`    | The smallest horizontal space between two cells, in pixels (default 4; negative is 0)        |
| `GroupByCategory`     | Whether category headers are shown (default true)                                            |
| `UncategorizedHeader` | The header of types without a category when other types have one (default "Other")           |
| `SelectedNodeType`    | The selected type, two-way bindable                                                          |
| registry              | The registry the prototypes are built from; for a plain sequence a private registry          |
| prototype cache       | One `NodePrototype` per type, valid for one registry and theme                               |
| items                 | The `NodePaletteItem` controls currently shown, in display order, and the category headers   |
| panel                 | The `NodePalettePanel` that lays headers and items out, and knows each item's row and column |
| search box            | A `TextBox` (id `NodePalette_Filter`, prompt "Search nodes") over the scroll viewer          |
| followed editor       | The editor and diagram the palette currently listens to; set only while in the visual tree   |
| dirty flag            | Set by any property change; the entries are rebuilt once at the next layout or query         |

The effective types are `Types`, else the registry of the editor's current page's diagram, else none; the effective theme
is `DiagramTheme`, else the editor's effective theme, else the classic theme. Both are read when the entries are built.

A `NodePrototype` is the scene of one node of the type, built by the ordinary `SceneBuilder` on a scratch page of a
private diagram that shares the registry: the node sits at the origin, is named after the type's display name, and has
the default size and default property values a new node gets. It records the node box and the deterministic visual
bounds of the node (`NodeGeometry.VisualBounds`: name label, box, ports, port labels and summary), computed with the
scratch diagram's theme because port positions always follow the node's own diagram theme (a palette or editor theme
override changes only the look). A
`NodePaletteItem` paints that scene with the shared `ScenePainter` (and so the shared icon cache) under a transform of
the palette zoom. All items share one box column: an item is as wide as twice the largest horizontal reach
(`NodePrototype.HorizontalReach`: from the box center to the edge of the visual extent, on either side) of any shown
prototype plus padding; that is the width of one grid cell. The panel arranges the item to fill its cell, and the item
centers its box in the cell, rounded to a whole device-independent pixel, so every box center in a column coincides (within
half a pixel when box widths differ in parity) and input labels extend left and output labels right. The item's height
comes from the visual bounds plus fixed padding; the box starts on a whole device-independent pixel row that is the same
for every item (the highest label reach among the shown items sets it), so the box tops of a row line up and a row is as
tall as its tallest item. Category headers are not items and keep the full width.

#### Column Layout

The entries live in a `NodePalettePanel` instead of a stack. The panel treats every `NodePaletteItem` as a cell of the
same width (the shared item width) and every other child (a category header) as a section break. Under each header the
items fill rows left to right; the column count is
`max(1, floor((width + spacing) / (cellWidth + spacing)))`, capped by `MaxColumns` when that is positive, where
`width` is the width of the viewport less the palette's margins and `spacing` is `MinColumnSpacing`. The cells share
the width: each
is `floor((width - (n - 1) * spacing) / n)` wide, the first starts at the left edge, the last ends at the right edge and
the columns in between are spread evenly, in whole pixels, so every cell of a column has the same left edge and width. A
row is as tall as its tallest item, rows and headers are separated by a fixed vertical space, and a header closes the
open row of the section before it. A single column behaves like the former stack: the items fill the content width.

The panel cannot learn its width from the measure pass, because a scroll viewer that scrolls horizontally offers its
content unlimited width. The palette therefore passes the width it was given (less its margins) to the panel as
`AvailableWidth` in its own measure pass, and a change of palette width, zoom, filter, grouping or types rebuilds the
layout at the next pass. When even one cell is wider than the available width the panel is one cell wide and the scroll
viewer scrolls horizontally; otherwise the content never scrolls horizontally. The panel records the row and column of
every item in item order (rows are numbered across sections), which the keyboard handling uses.

#### Key Methods

- **Entry building** (`EnsureItems`) — when stale, disposes the old items' drag helpers, resolves the registry (a
  registry as given; for a plain sequence a private registry that holds the types, with a neutral grey for each port
  type key), filters the types, groups them by category (categories in the order of their first type in the registry,
  uncategorized types last, headers only when some visible type has a category and grouping is on) and creates one item
  per type from the cached prototype.
- **Prototype creation** (`NodePrototype.Create`) — builds the private diagram, adds the node, builds the scene with
  the palette's theme and an `AvaloniaTextMeasurer` and records box and visual bounds. The private diagram has no
  `NodeStyler`, so prototypes always show the node type's default look, never the editor diagram's dynamic node styling.
- **Prototype cache** (`NodePrototypeCache.Get`) — returns the cached prototype, emptying the cache first when the
  registry or either theme differs from the one the cache was built for. `Refresh` and a new `Types` value empty it.
- **Item press** — a left press selects the item, takes keyboard focus and, for the second press of a double-click,
  activates the type.
- **Activation** (`Activate`) — raises `NodeTypeActivated` with a `Handled` flag; unless a handler set it and when an
  editor is set, calls the editor's `AddNodeAtCenter` (centered on the visible area, snapped, selected, one undo entry,
  cascading by a grid cell while the place is taken) and gives the editor the keyboard focus. Double-click, Enter, the
  Invoke automation pattern and hosts all go through it, so there is one default behavior.
- **Reveal and select by type** (`ScrollIntoView`, `Select`) — find the shown item of a type (the very instance, else the
  type with the same key), update the layout and bring the item into view; `Select` also sets `SelectedNodeType`. Both
  return false for a type that is not shown.
- **Following the editor** — while the palette is in the visual tree it listens to the editor's property changes
  (`Page`, `DiagramTheme`) and to the `Changed` event of the editor's diagram (a replaced `Theme`). A different diagram
  or theme marks the entries dirty; switching to another page of the same diagram changes nothing, so the scroll position
  is kept. Leaving the tree removes both subscriptions (so a discarded palette is never kept alive by the editor);
  returning marks the entries dirty so changes made meanwhile are picked up.
- **Search box** — a `TextBox` above the scroll viewer in a dock panel. Typing copies the text to `Filter`; setting
  `Filter` from code copies it into the box (each direction compares first, so there is no feedback loop). From the box
  only Down, Up and Enter reach the palette's keyboard handling; Home, End and the editing keys stay with the box.
- **Automation ids** (`GetAutomationId`) — the public rule that gives an item's id: `NodePalette_` plus the key with
  every character other than an ASCII letter, digit or underscore replaced by an underscore. Items use it; hosts, tests
  and tools call it (the Puppet restates it and a test asserts that both agree).
- **Automation peers** — the item peer is a list item that implements the Invoke pattern (select and activate) and, like
  every Avalonia peer, supports the ScrollItem pattern, whose scroll request reaches the item's bring-into-view; the
  palette peer is a list.
- **Keyboard** — Down, Up, Home and End move the selection and scroll it into view; Enter on the selected item
  activates it. Before handling a key the palette brings the layout up to date. Left and Right (only while there is more
  than one column) move to the neighbor in the same row and stay at the ends of the row; Up and Down move to the previous
  or next row, across category headers, to the item in the same column or, when that row is shorter, its last item.
  With no selection Down and Right select the first item, Up and Left the last. In a single column Up and Down are
  simply the previous and next item and Left and Right are not handled. From the search box only Down, Up and Enter
  reach the handler, as before.
- **Drag** — each item carries a `NodeTypeDragSource` for its type key whose starter is the editor's
  `StartNodeTypeDragAsync`, so a press followed by movement past the threshold starts the standard node-type drag.
- **Property changes** — `MaxColumns` and `MinColumnSpacing` go straight to the panel; `Types`, `Refresh` and theme
  changes rebuild prototypes; a different `Editor` or followed
  diagram marks the entries dirty (the prototype cache empties itself when the registry or theme differs); filter,
  grouping, header text and zoom only rebuild the items from cached prototypes; the selection only updates the highlight.

#### Error Handling

No input is rejected with an error: no types show an empty palette, a filter that matches nothing shows nothing, an
out-of-range zoom is clamped and NaN becomes 1, and activation does nothing beyond the event when there is no editor,
the editor is read-only or its diagram lacks the type. Null arguments of the public methods and of the event-argument
constructor are rejected (`ArgumentNullException`). A platform that cannot run a drag leaves activation as the
alternative, because the drag helper swallows platform failures.

#### Dependencies

- **Core** — `NodeTypeRegistry`, `NodeType` (display name, category, icon, ports, properties, visual), `Diagram` and
  `DiagramPage` (for the scratch page and for following the editor's diagram), `SceneBuilder`, `DiagramScene`,
  `NodeGeometry.VisualBounds` and `DiagramTheme`.
- **ScenePainter**, **AvaloniaTextMeasurer**, **PlexusIcons** — painting, measuring and icons shared with the editor.
- **NodeTypeDragSource** — the drag behavior of each item.
- **DiagramEditor** — the static drag start and the drag payload format, `AddNodeAtCenter`, the effective theme and the
  `Page` and `DiagramTheme` property changes.
- **Avalonia** (OTS) — control, scroll viewer, text box, input, drawing and automation (including the Invoke provider).

#### Callers

Host applications (through XAML or code) and, for `NodePrototype` and `NodePrototypeCache`, the `DiagramEditor` drop
ghost.
