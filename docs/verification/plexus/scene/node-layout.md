### NodeLayout Unit Verification Design

#### Verification Approach

Unit tests in `NodeLayoutTests` compute layouts of nodes in a real test diagram (`SceneTestModel`), compare them with
the model's anchors, the shared `NodeGeometry` functions and the theme metrics, and exercise `TextWrapper` with a
fixed-width measurer test double (10 units per character) so wrapping results are exact.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework; rectangles equal the values derived from the theme within the stated
tolerance.

#### Test Scenarios

- **Anchors match the model**
  (`NodeLayout_Compute_PortAnchors_MatchModelAndNodeGeometry`): every port anchor equals `Port.Anchor` and the
  geometry function, for a node with four inputs and one output.
- **Geometry theme** (`Plexus-Scene-NodeLayout-GeometryTheme`, also evidence for
  `Plexus-Scene-NodeLayout-PortAnchorsMatchModel`; `NodeLayout_Compute_OtherThemeWithChangedPortMetrics_KeepsModelAnchors`):
  a layout computed with a theme whose port pitch, label gap, triangle length and hit radius differ from the classic
  diagram theme keeps every anchor at `Port.Anchor`, and the triangles and hit areas have the diagram theme's sizes
  and sit on the anchor rows.
- **Anchors are text independent**
  (`NodeLayout_Compute_PortAnchors_AreTextIndependent`): anchors are equal for different measurers.
- **Label placement**
  (`NodeLayout_Compute_PortLabels_AreBesideTheTrianglesCenteredOnTheRow`): labels are beside the triangles and
  centered on the port row.
- **Triangles and hit areas**
  (`NodeLayout_Compute_PortTrianglesAndHitAreas_SitAtTheAnchors`): triangles touch the edge and hit rectangles reach
  from just inside the edge to the anchor.
- **Title, name, summary**
  (`NodeLayout_Compute_TitleNameAndSummary_AreArrangedAroundTheBox`): header band, name above and stacked summary
  lines below.
- **Centered ports** (`Plexus-Scene-NodeLayout-CenteredPorts`; `PortAlignmentTests`:
  `NodeGeometry_PortEdge_Center_OddCountPutsMiddlePortOnTheBoxCenter`,
  `NodeGeometry_PortEdge_Center_EvenCountStraddlesTheBoxCenter`, `NodeGeometry_PortEdge_Center_FollowsTheBoxHeight`):
  odd and even columns are centered on the box center at one-pitch spacing and follow a resized box.
- **Centered port rows** (`Plexus-Scene-NodeLayout-CenteredPortRows`;
  `NodeLayout_Compute_Center_StacksAreSymmetricAboutTheBoxCenter`): triangles, hit areas and labels share each anchor's
  row and agree with `Port.Anchor`.
- **Centered port height** (`Plexus-Scene-NodeLayout-CenteredPortHeight`;
  `NodeGeometry_RequiredHeight_Center_IsTwiceTheHeaderClearancePlusTheSpan`,
  `NodeGeometry_DefaultNodeSize_Center_KeepsTheTopPortBelowTheHeader`,
  `NodeGeometry_DefaultNodeSize_Center_KeepsMinimumAndHonorsDeclaredSize`): the default height grows by the documented
  rule (twice the clearance plus the span, honoring a first-port offset), keeps the theme minimum, and a declared size
  wins.
- **Straight wires between aligned nodes** (`Plexus-Scene-NodeLayout-CenteredPortStraightWires`;
  `DiagramPage_Connect_AlignedNodesWithSamePortCounts_RouteStraight`): the wire between aligned nodes with equal port
  counts has two points on one y.
- **Top ports** (`NodeGeometry_PortEdge_Top_StartsBelowTheHeaderRegardlessOfCount`,
  `NodeGeometry_RequiredHeight_Top_IsHeaderOffsetSpanAndPadding`,
  `NodeLayout_Compute_Top_PortsStartBelowTheHeaderAndMatchTheModel`): the earlier rows and heights, including the
  first-port offset, and agreement between layout and model.
- **Proxy ports** (`SceneBuilder_Build_CollapsedGroup_ProxyPortsAreCenteredOnTheBox`,
  `SceneBuilder_Build_CollapsedGroup_Top_ProxyPortsStartBelowTheTitleBar`): four input proxies are symmetric about the
  collapsed box's center and the single output proxy lies on it; with top alignment the first row is below the title
  bar.
- **Icon**
  (`NodeLayout_Compute_Icon_IsCenteredInBodyOnlyWhenTypeHasIcon`): present and centered only for a type with an icon.
- **Overall bounds**
  (`NodeLayout_Compute_Bounds_CoverEverythingDrawn`): bounds contain box, name, summary, triangles and labels.
- **Null arguments**
  (`NodeLayout_Compute_NullArguments_ThrowArgumentNullException`): node, theme and measurer.
- **Default width** (`Plexus-Scene-NodeLayout-DefaultWidth`; `DefaultNodeWidthTests.cs`:
  `NodeGeometry_DefaultNodeSize_ShortTitle_KeepsThemeDefaultWidth`,
  `NodeGeometry_DefaultNodeSize_WideThemeMinimumAndNoGrid_AreHonored`): a short title keeps 90 units, and a larger theme
  width or a theme without a grid is honored.
- **Default width from the title** (`Plexus-Scene-NodeLayout-DefaultWidthTitle`;
  `NodeGeometry_DefaultNodeSize_LongTitle_GrowsWidthToFitSnappedToTheGrid`,
  `SceneBuilder_Build_LongTitleWithDefaultSize_DrawsTheTitleOnOneLine`): THERMOMETER widens the node to the smallest
  grid multiple at which the measured title fits the header margins, and the scene draws the whole title as one text
  primitive.
- **Declared size** (`Plexus-Scene-NodeLayout-DeclaredSize`; `NodeGeometry_DefaultNodeSize_DeclaredSize_WinsOverTheTitle`):
  a declared size is returned unchanged even for a long title.
- **Text wrapping**
  (`TextWrapper_Wrap_BreaksAtWordsAndSplitsLongWords`): words wrap at a width of five characters, explicit line
  breaks are kept and a long word is split by character.

- **Per-node ports** (`Plexus-Scene-NodeLayout-DynamicPorts`): `NodeLayout_Compute_DynamicPorts_LaysOutCurrentPorts`
  lays out the six current ports of a reconfigured node at the model's anchors.
- **Per-node extent and obstacles** (`Plexus-Scene-NodeLayout-DynamicPortExtent`,
  `Plexus-Scene-NodeLayout-DynamicPortObstacles`): `NodeGeometry_VisualBoundsAndRoutingObstacles_ProviderPorts_UseNodePorts`
  shows that long computed output labels widen the node's visual bounds and add routing strips the type-based overload
  does not have.
- **Visual extent covers port labels** (`Plexus-Scene-NodeLayout-VisualExtent`):
  `NodeGeometry_VisualBounds_PortLabelLineHeight_IsContained` squeezes a four-input node so that its port rows stick out
  of the box and checks that the extent reaches half the label line height (7.5 units in the classic theme, more than
  half the 10-unit triangle) above the first and below the last input row;
  `NodeGeometry_VisualBounds_LargePortLabelFont_ContainsLabels` uses a 24-unit port label font and checks that every
  port label rectangle of the layout lies inside the extent.

Boundary values: node without icon, many ports, port rows outside a squeezed box, a large port label font, a word wider
than the line. Error paths: null arguments.
