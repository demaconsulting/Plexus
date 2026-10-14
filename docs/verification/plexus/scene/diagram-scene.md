### DiagramScene Unit Verification Design

#### Verification Approach

Unit tests in `SceneHitTestTests`, `SpatialIndexTests` and `SceneGroupLayoutTests` build real scenes from small pages
and call `HitTest`, `Query` and the lookup methods, comparing results with the expected elements, kinds, ports,
segment indexes and rectangles. The spatial index is tested directly with integer items and also through scenes
against a brute-force intersection test, so the optimization is proven equivalent to the obvious algorithm. No doubles
are used.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework. Query results equal brute-force results exactly, including order.

#### Test Scenarios

Hit testing:

- **Empty canvas**
  (`Scene_HitTest_EmptyCanvas_ReturnsCanvas`).
- **Priority within and between elements**: body (`Scene_HitTest_NodeBody_ReturnsNode`), name
  (`Scene_HitTest_NodeName_ReturnsNodeName`), port (`Scene_HitTest_PortAnchor_ReturnsPort`), node over wire
  (`Scene_HitTest_NodeOverWire_ReturnsNode`) and topmost of overlapping nodes
  (`Scene_HitTest_OverlappingNodes_ReturnsTopmost`).
- **Wires**: segment indexes (`Scene_HitTest_WireSegments_ReturnWireWithSegmentIndex`), tolerance near and far
  (`Scene_HitTest_WireTolerance_NearHitsFarMisses`) and hidden wires (`Scene_HitTest_HiddenWire_IsNotHit`).
- **Annotations**: box and handle only when selected
  (`Scene_HitTest_Annotation_ReturnsAnnotationAndHandleWhenSelected`).
- **Groups**: border, header, toggle and transparent interior
  (`Scene_HitTest_ExpandedGroup_ReturnsBorderHeaderToggleAndTransparentInterior`), node inside a group
  (`Scene_HitTest_NodeInsideGroup_ReturnsNode`) and collapsed group and toggle
  (`Scene_HitTest_CollapsedGroup_ReturnsCollapsedGroupAndToggle`).
- **Rendered geometry**
  (`Scene_HitTest_UsesRenderedGeometry`): the drawn box and icon are hit as the node.

Queries and index:

- **Brute-force equivalence and order**
  (`Scene_Query_MatchesBruteForceAndKeepsDrawingOrder`) and **small region**
  (`Scene_Query_SmallRegion_ReturnsOnlyNearbyPrimitives`).
- **Index**: insertion order (`SpatialIndex_Query_ReturnsIntersectingItemsInInsertionOrder`), item spanning cells
  returned once (`SpatialIndex_Query_ItemSpanningCells_IsReturnedOnce`), large items
  (`SpatialIndex_Query_LargeItem_IsFoundWhenIntersecting`), huge region
  (`SpatialIndex_Query_HugeRegion_ReturnsAllIntersecting`), negative coordinates
  (`SpatialIndex_Query_NegativeCoordinates_AreFound`) and a non-positive cell size
  (`SpatialIndex_Constructor_NonPositiveCellSize_Throws`).
- **Large scenes**
  (`Scene_HitTest_ThousandNodes_ManyQueriesAreCorrect`): five thousand hit tests at the centers of a thousand nodes
  each report that node.

Layouts and lookups:

- **Group layouts**: expanded geometry (`DiagramScene_TryGetGroupLayout_ExpandedGroup_MatchesDrawnGeometry`),
  rectangles used by hit testing
  (`DiagramScene_TryGetGroupLayout_ExpandedGroup_RectanglesAreWhatHitTestUses`), collapsed box and proxies
  (`DiagramScene_TryGetGroupLayout_CollapsedGroup_PublishesBoxToggleAndProxyPorts`) and unavailable groups
  (`DiagramScene_TryGetGroupLayout_HiddenGroupOrOtherPage_ReturnsFalse`).
- **Bounds and wire points**
  (`Scene_Bounds_CoverNodeLayouts`, `Scene_TryGetWirePoints_VisibleWire_ReturnsModelPoints`).
- **Wire handles** (tests in `WireVisualsTests.cs`): a selected wire reports bend handles with their index and the end
  handles with their port (`DiagramScene_HitTest_SelectedWireHandles_ReturnBendAndEndKinds`), an unselected wire only
  segments (`DiagramScene_HitTest_UnselectedWire_ReturnsSegmentNotHandle`), and a selected wire's segment wins over a
  node it crosses while an unselected one does not
  (`DiagramScene_HitTest_SelectedWireOverNode_WireBeatsNodeButOnlyWhenSelected`).

Boundary values: points on thin targets at and beyond the tolerance, overlapping nodes, items spanning many cells,
negative coordinates, whole-scene regions. Error paths: non-positive cell size.
