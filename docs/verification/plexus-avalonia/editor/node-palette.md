### NodePalette Unit Verification Design

Scope: Palette control that lists node types, each drawn as it looks on the canvas, and lets the user drag or activate
them.

#### Verification Approach

The unit is verified with real controls in a headless Avalonia window with Skia drawing: a palette beside an editor
over a registry of five varied types (a source with a summary line, a mixer with display name, icon, two inputs, an
output and a summary, a type with a custom node visual, a sink and an uncategorized gain). Pointer, keyboard and
drag-and-drop input is simulated with the headless helpers, and assertions read the palette's items, headers and
events and the editor's model. The headless platform cannot run an operating-system drag, so tests replace the
palette's drag starter with a function that records the key (and builds the standard payload) or delivers the drag
events to the editor; that the platform starts the drag is verified by the NodeTypeDragSource tests. The zero-code mode
is verified with a palette that was given only the editor (the `followEditor` option of the host fixture); items that
must scroll are provided by a high zoom, and the UI Automation patterns are exercised through the item's automation
peer (the same objects the Windows UI Automation bridge calls).

The unit's central claim, that an item looks exactly like the node on the canvas, is verified by pixels: a palette
that shows one type and an editor that received the same type by a drop are rendered in one window at the same zoom
and theme; the node is renamed to the type's display name, deselected, and scrolled so that its box lies at the same
fractional screen position as the item's box; the node's visual extent is cropped from both places and every pixel is
compared with a tolerance of two per channel. The frames are saved to the `plexus-palette` folder in the temporary
folder so that a person can look at them. The tests are in `NodePaletteTests.cs`, `NodePaletteEditorTests.cs`,
`NodePaletteColumnTests.cs` and `NodePalettePixelTests.cs`, and share the `PaletteHost` and `PictureReader` fixtures of
the test project.

The multi-column layout is verified by resizing the palette column of the host fixture (`SetPaletteWidth`, what a
splitter drag does) to widths computed from the measured cell width, so that exactly k cells fit and one pixel less fits
k - 1; the number of columns is read from the palette and cross-checked against the distinct left edges of the items,
and box centers and tops are read from the items' translated box origins. Keyboard tests send real key events to the
window. Scrolling tests use a plain list of 48 simple types in four categories at a zoom that makes the content far taller
than the view.

#### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing and the Fluent theme, started once per
test run by the shared fixtures. The window is 1240 by 900 pixels with the palette in the left 340 pixels unless a test
sets another palette width.

#### Acceptance Criteria

A test passes when the items, headers, selection, events and model are as specified, and for the pixel tests when no
pixel of the cropped node differs by more than the tolerance, the crop shows a node, and the selected themes (the
classic theme and a light theme with other colors, corner radius and font size) and zooms (0.75, 1, 1.5 and 2) all
agree.

#### Test Scenarios

**Items and automation** (`NodePaletteTests.cs`)

- **NodePalette_Types_Registry_ShowsAnItemForEveryType**: one item per registered type in registration order, the
  display name as accessible name, `NodePalette_<key>` as automation id, a real size, a focusable palette.
- **NodePalette_Types_PlainSequence_ShowsItems**: a list of types works like a registry.
- **NodePalette_Types_Null_IsEmpty**: no types, no items.
- **NodePalette_GetAutomationId_SanitizesKey**: characters other than letters, digits and underscores become
  underscores, for a node type and for a bare key, and null arguments throw.
- **NodePalette_Refresh_PicksUpAddedTypes**: a type added to the registry appears only after `Refresh`.

**Default look** (`NodeStylePixelTests.cs`)

- **Editor_NodeStyler_AssignAndRefresh_RepaintsAndPaletteStaysDefault**: with a diagram styler that fills every node, a
  palette that follows the editor still shows its prototype with the theme fill.

**Looks like the canvas** (`NodePaletteTests.cs`, `NodePalettePixelTests.cs`)

- **NodePalette_Items_UseTheCanvasScene_WithNameSummaryPortsAndCustomVisual**: the item scenes hold the display name,
  the title, the default-value summary, port labels, the icon and the primitives of a custom visual.
- **NodePalette_Item_IsPixelIdenticalToTheDroppedNode**: seven combinations of type, zoom and theme render identically
  in the palette and on the editor (see the approach above).
- **NodePalette_Zoom_ScalesItemsAndIsClamped**: the item's minimum width is twice the palette-wide horizontal reach at
  the zoom plus padding, and zoom is clamped to the editor's range.

**Box column** (`NodePaletteTests.cs`)

- **NodePalette_Items_ShareOneBoxColumn**: for types with very different boxes and port labels, the box centers of all
  items coincide within half a pixel, sit in the middle of the shared item width, and the category headers have the same
  full width.
- **NodePalette_Items_KeepTheSharedBoxColumnWhenContentOverflows**: at the maximum zoom the items are wider than the
  palette, and the box centers are still one column in the middle of each item.

**Columns** (`NodePaletteColumnTests.cs`)

- **NodePalette_Columns_FollowThePaletteWidth**: widths that hold exactly one to four cells give one to four columns, one
  pixel less gives one fewer, and the items' distinct left edges agree with the count.
- **NodePalette_Columns_CellsAreEqualAndInsideThePalette**: all cells have the same width, lie inside the palette's
  margins, do not overlap and keep the minimum spacing.
- **NodePalette_Columns_BoxesAlignInAGrid**: in three columns the box centers are equal down each column and a cell
  apart across a row, and the box tops are equal along each row.
- **NodePalette_Columns_HeadersSpanTheFullWidthAndSectionsStartNewRows**: the four headers have the full content width,
  the two processing types share a row, and each section starts below the previous one and below its header.
- **NodePalette_Columns_ArrowKeysMoveByCellAndRow**: Right and Left stop at the ends of a row, Down and Up move by a row
  to the nearest column of a shorter row and stop at the first and last row, and the first Right selects the first item.
- **NodePalette_Columns_VerticalMoveCrossesSectionsAndSingleColumnIgnoresHorizontalKeys**: Down crosses category
  headers; in one column Right does nothing and Down is the next item.
- **NodePalette_Columns_SearchBoxKeysStillWork**: from the search box Down selects the first item, Right leaves the
  selection alone and Enter activates.
- **NodePalette_Columns_ScrollIntoViewRevealsTheItem**: in three columns `ScrollIntoView` and `Select` bring an item of
  the last row and the third column into view without horizontal scrolling.
- **NodePalette_Columns_ScrollHorizontallyOnlyWhenOneCellDoesNotFit**: the content is as wide as the viewport while
  cells fit, and wider only when the palette is narrower than one cell.
- **NodePalette_Columns_LayOutAgainOnFilterZoomAndTypes**: a filter, a smaller zoom and a longer type list each change the
  rows and columns at the next layout.
- **NodePalette_Columns_MaxColumnsAndMinColumnSpacing**: `MaxColumns` caps the count (zero or negative: no cap) and a
  larger `MinColumnSpacing` reduces it and is kept between cells; a negative spacing counts as zero.
- **NodePalette_Columns_EmptyPaletteHasOneColumn**: a filter that matches nothing leaves no items and one column.

**Filter and groups** (`NodePaletteTests.cs`)

- **NodePalette_Filter_MatchesNameTitleKeyAndCategory**: display name, title, key and category match ignoring case,
  surrounding spaces are ignored, no match shows nothing and clearing the filter shows all.
- **NodePalette_GroupByCategory_OrdersGroupsByRegistrationWithUncategorizedLast**: headers Sources, Processing, Sinks
  and the custom uncategorized header, with the two processing types together.
- **NodePalette_GroupByCategory_OffOrNoCategories_ShowsNoHeaders**: no headers with grouping off, or when only an
  uncategorized type is visible.

**Selection and activation** (`NodePaletteTests.cs`)

- **NodePalette_DoubleClick_RaisesNodeTypeActivated**: a click selects and focuses without activating; the second press
  activates.
- **NodePalette_Keyboard_ArrowsMoveSelectionAndEnterActivates**: Enter with no selection does nothing, Down, Up, End
  and Home move (stopping at the ends) and Enter activates the selected type (one column; the multi-column keys are
  covered under Columns).
- **NodePalette_SelectedNodeType_SetByHost_HighlightsItem**: a host-set selection highlights the item and survives
  filtering it away.

**Following the editor** (`NodePaletteEditorTests.cs`)

- **NodePalette_Editor_TakesTypesAndThemeFromTheEditor**: a palette with only an editor lists the editor's diagram's
  types and uses the editor's theme, then the diagram's theme after the editor's is cleared and after the diagram's
  `Theme` is replaced.
- **NodePalette_Editor_ExplicitTypesAndThemeWin**: an explicit type list and theme override the editor's.
- **NodePalette_Editor_FollowsDiagramReplacementButNotPageSwitches**: another page of the same diagram keeps the very
  same items (so the scroll position survives), a page of another diagram with another registry replaces them, and no
  page shows none.
- **NodePalette_Editor_PicksUpChangesMadeWhileDetached**: a palette removed from the window and put back shows the
  editor's new diagram's types.

**Activation, reveal and automation** (`NodePaletteEditorTests.cs`)

- **NodePalette_Activate_WithEditor_AddsSelectedNodeAtCenterAsOneUndoEntry**: one new node of the type, selected,
  around the editor's visible center, on the grid, one undo entry, editor focused.
- **NodePalette_Activate_Repeatedly_CascadesNodes**: three activations give three distinct positions one grid cell
  apart.
- **NodePalette_Activate_HandledSuppressesTheDefault**: a handler that sets `Handled` stops the default; one that does
  not, does not.
- **NodePalette_Activate_WithoutEditorOrWithUnusableEditor_AddsNothing**: no editor, a read-only editor and an unknown
  type only raise the event; a null type throws.
- **NodePalette_DoubleClickAndEnter_WithEditor_AddNodes**: a real double-click and Enter on the selected item add nodes
  through the same default.
- **DiagramEditor_AddNodeAtCenter_AddsOrRefuses**: the editor method returns the node, and null for an unknown type, a
  read-only editor and no page; a null type throws.
- **NodePalette_ScrollIntoViewAndSelect_RevealTheItem**: at a zoom where the items overflow, `ScrollIntoView` scrolls
  to the last item, `Select` selects (also a type with the same key) and scrolls back, and a type hidden by the filter
  returns false; null arguments throw.
- **NodePaletteItem_AutomationPeer_SupportsInvokeAndScrollIntoView**: the item's peer is a list item with the
  documented id, its bring-into-view scrolls the palette, and its Invoke provider selects the type and adds the node.

**Search box** (`NodePaletteEditorTests.cs`)

- **NodePalette_ShowFilter_DefaultSearchBoxFiltersBothWays**: the box exists by default with the id
  `NodePalette_Filter` and the prompt "Search nodes"; typing filters, setting `Filter` fills the box, clearing shows
  everything.
- **NodePalette_ShowFilter_Off_HidesTheBox**: the box is hidden and the entries move up to the top.
- **NodePalette_SearchBox_KeyboardMovesThroughResultsAndEnterActivates**: End stays with the box, Down selects the first
  result and Enter adds its node.

**Drag** (`NodePaletteTests.cs`)

- **NodePalette_DragItem_PastThreshold_StartsNodeTypeDragWithTheKey**: a click and a small movement start nothing; a
  movement past the threshold starts the drag with the item's key, and the standard payload carries that key.
- **NodePalette_DragItemOntoEditor_CreatesNodeWhereGhostWas**: the whole drag flow ends with one node at the ghost
  position and no ghost left.

**Cache** (`NodePaletteTests.cs`)

- **NodePalette_Scenes_AreCachedUntilTypesOrThemeChange**: one build per type initially; filter, grouping and zoom
  changes build nothing; a theme change rebuilds every type and every item scene uses the new theme.
