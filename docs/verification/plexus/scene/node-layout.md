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
- **Centered ports** (`PortAlignmentTests`: `NodeGeometry_PortEdge_Center_OddCountPutsMiddlePortOnTheBoxCenter`,
  `NodeGeometry_PortEdge_Center_EvenCountStraddlesTheBoxCenter`, `NodeGeometry_PortEdge_Center_FollowsTheBoxHeight`,
  `NodeGeometry_RequiredHeight_Center_IsTwiceTheHeaderClearancePlusTheSpan`,
  `NodeGeometry_DefaultNodeSize_Center_KeepsTheTopPortBelowTheHeader`,
  `NodeGeometry_DefaultNodeSize_Center_KeepsMinimumAndHonorsDeclaredSize`,
  `NodeLayout_Compute_Center_StacksAreSymmetricAboutTheBoxCenter`, `DiagramPage_Connect_AlignedNodesWithSamePortCounts_RouteStraight`):
  odd and even columns are centered on the box center at one-pitch spacing, follow a resized box, the default height
  grows by the documented rule (twice the clearance plus the span, honoring a first-port offset), a declared size wins,
  triangles, hit areas and labels share each anchor's row and agree with `Port.Anchor`, and the wire between aligned
  nodes with equal port counts has two points on one y.
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
- **Default width** (`Plexus-Scene-NodeLayout-DefaultWidth`, `DefaultNodeWidthTests.cs`:
  `NodeGeometry_DefaultNodeSize_ShortTitle_KeepsThemeDefaultWidth`,
  `NodeGeometry_DefaultNodeSize_LongTitle_GrowsWidthToFitSnappedToTheGrid`,
  `NodeGeometry_DefaultNodeSize_DeclaredSize_WinsOverTheTitle`,
  `NodeGeometry_DefaultNodeSize_WideThemeMinimumAndNoGrid_AreHonored`,
  `SceneBuilder_Build_LongTitleWithDefaultSize_DrawsTheTitleOnOneLine`): a short title keeps 90 units, THERMOMETER widens
  the node to a grid multiple at which the measured title fits the header margins and which is the smallest such
  multiple, a declared size is returned unchanged, a larger theme width or a theme without a grid is honored, and the scene
  draws the whole title as one text primitive.
- **Text wrapping**
  (`TextWrapper_Wrap_BreaksAtWordsAndSplitsLongWords`): words wrap at a width of five characters, explicit line
  breaks are kept and a long word is split by character.

Boundary values: node without icon, many ports, a word wider than the line. Error paths: null arguments.
