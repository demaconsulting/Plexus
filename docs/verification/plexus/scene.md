## Scene Subsystem Verification Design

### Verification Approach

The Scene subsystem is verified by integration tests in `SceneTests` that build real scenes from real pages and
diagrams (the shared `SceneTestModel` fixture) and observe them only through the public surface: primitives, hit
tests, queries, layouts and published group geometry. The units are not mocked; the only test doubles are a
fixed-width `ITextMeasurer` and two small custom node visuals, which are themselves public extension points of the
subsystem. No painter participates, which is the point of the subsystem. Unit-level behavior is verified in the unit
verification designs (DiagramTheme, NodeLayout, TextMeasurement, SceneBuilder, DiagramScene).

### Test Environment

The standard xunit runner of `DemaConsulting.Plexus.Tests` on net8.0, net9.0 and net10.0 on Windows, Linux and macOS.
No fonts, files or network are used.

### Acceptance Criteria

All scenarios pass on every target framework. A scene built from a page contains only primitives of the closed set;
hit testing agrees with the drawn geometry; building and querying never change the model dump or undo count.

### Test Scenarios

- **Closed primitive set, grid first**
  (`Scene_BuildPage_ProducesPrimitivesOfTheClosedKindsWithGridFirst`): a page with icon nodes, a wire and an
  annotation yields only rectangles, paths, text, icons and the grid, with the grid first. This is built with the
  default measurer and no fonts.
- **Drawing order**
  (`Scene_DrawingOrder_GridGroupFramesWiresThenElements`): grid, group frame, wire, nodes in z-order and annotation
  appear in that order.
- **Hit testing agrees with drawing**
  (`Scene_HitTest_AgreesWithDrawnGeometry`): the centers of drawn node and annotation boxes, the middle of the wire
  and a port's hit area report the node, annotation, wire and port.
- **Selection feedback and handle**
  (`Scene_SelectedAnnotation_ShowsResizeHandleThatIsHittable`): the resize handle is drawn and hittable only while
  the annotation is selected.
- **Anchors independent of the measurer**
  (`Scene_PortAnchors_AreIdenticalForAnyTextMeasurer`): wire points are identical for the approximate and a
  fixed-width measurer, and layout anchors equal model anchors.
- **Theme drives drawing and hit reach**
  (`Scene_Theme_WireThickness_DrivesDrawingAndHitTesting`): a thick red wire theme is drawn thick and red, and is hit
  from a probe point that misses with the thin default.
- **Diagram theme or override**
  (`Scene_Theme_DiagramThemeIsUsedUnlessTheOptionOverridesIt`): the diagram's theme is used unless the options name
  another, observed through the scene theme and grid spacing.
- **Wire styling**
  (`Scene_WireStyling_StyleFollowsConnectionsAndFallsBackToTheme`): a styler that styles only one wire with only a
  color changes that wire's color alone; the other wire and every unset member keep the theme.
- **Node styling**
  (`Scene_NodeStyling_StyleFollowsNodesAndFallsBackToTheme`): a styler that styles only one node with only a fill
  changes that node's fill alone; the other node and every unset member keep the theme, and the selection outline stays
  on top.
- **Culling**
  (`Scene_VisibleBounds_OmitOutsideElementsEverywhere`): a far node has no layout, no primitives, no hit target and
  no query result with visible bounds set.
- **Groups**
  (`Scene_Groups_ExpandedAndCollapsed_AreDrawnPublishedAndHitConsistently`): the published toggle is hit as the
  toggle when expanded; when collapsed, members vanish, the box is hit as the collapsed group and the wire ends at
  the published proxy port anchor.
- **Read-only**
  (`Scene_BuildAndQuery_DoNotModifyTheModel`): the model dump and undo count are unchanged by build, hit test and
  query with selection, hover and a collapsed group.
- **Pluggable measurer and visual**
  (`Scene_CustomMeasurerAndVisual_ArePluggable`): a custom visual replaces the template of its node only, the
  measurer sizes the name, and hit testing still uses the layout.
- **Region query**
  (`Scene_Query_ReturnsTheDrawnPrimitivesIntersectingTheRegionInOrder`): the query result equals the intersecting
  primitives in drawing order and excludes a far node.
- **Lookups**
  (`Scene_Lookups_ExposeLayoutsWirePointsAndContentBounds`): layouts and wire points of displayed elements are
  available, a node of another page is not, and the content bounds contain the layouts.
- **Large page**
  (`Scene_LargePage_BuildAndHitTest_EveryNodeIsHit`): a page of one thousand nodes with wires is built and the center
  of every node hits that node.
- **Null arguments**
  (`Scene_NullArguments_AreRejected`): the scene builder, layout, measurer and lookups reject nulls.

Boundary conditions covered: empty selection, far elements, collapsed and expanded groups, a thousand nodes. Error
paths: null arguments.
