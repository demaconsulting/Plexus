### SceneBuilder Unit Verification Design

#### Verification Approach

Unit tests in `SceneBuilderTests` and `SceneGroupTests` build scenes of small real pages (`SceneTestModel`: standard test
node types plus an icon type and custom visuals) and inspect the primitives, which are plain immutable objects, by element
and kind (`SceneTestModel.For`). Test doubles are the fixed-width text measurer (10 units per character) and two custom
visuals (a replacing and a decorating one), which are the unit's own extension points. The page model, routers and layout
code are the real implementations.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework with the primitives, colors and geometry expected from the theme.

#### Test Scenarios

Nodes:

- **Standard node** (`SceneBuilder_Build_StandardNode_EmitsBoxTitleNameSummaryAndPorts`), **port color**
  (`SceneBuilder_Build_PortTriangle_UsesPortTypeColor`), **icon** (`SceneBuilder_Build_NodeWithIcon_EmitsIconPrimitive`)
  and **long title** (`SceneBuilder_Build_LongTitle_WrapsIntoLinesInsideTheBox`): the node look and its text fitting.

Wires and annotations:

- **Straight wire** (`SceneBuilder_Build_StraightWire_IsStrokedPathBetweenAnchors`), **arrowhead**
  (`SceneBuilder_Build_Wire_EndsInFilledArrowheadAtTheTarget`), **arrowheads off**
  (`SceneBuilder_Build_WireArrowheadsDisabled_DrawsOnlyTheLine`), **rounded bends**
  (`SceneBuilder_Build_WireWithBends_RoundsCornersWithCubics`) and **radius shrinking**
  (`WirePaths_Rounded_ShrinksRadiusOnShortSegmentsAndSkipsStraightRuns`).
- **Annotation fill and wrapping** (`SceneBuilder_Build_Annotation_UsesFillAndWrapsText`) and **lines dropped when too
  small** (`SceneBuilder_Build_Annotation_TooSmallForText_DropsLines`).

Wire styling:

- **Default** (`Diagram_WireStyler_Default_IsNullAndWiresUseTheme`), **color and thickness**
  (`SceneBuilder_Build_StylerColorAndThickness_AreApplied`), **fallback of unset members and null styles**
  (`SceneBuilder_Build_NullMembersAndNullStyle_FallBackToTheme`) and **arrowhead on or off**
  (`SceneBuilder_Build_StylerArrowhead_OverridesTheme`): the style reaches the stroke and arrowhead and the theme fills
  the gaps.
- **Failing styler** (`SceneBuilder_Build_StylerThrows_FallsBackToTheme`), **dynamic property**
  (`SceneBuilder_Build_PropertyChangeOnConnectedNode_RestylesWire`), **collapsed group display route**
  (`SceneBuilder_Build_CollapsedGroupProxyWire_UsesStyler`) and **hit reach**
  (`SceneBuilder_Build_StyledThickness_WidensHitTarget`).
- **Dashes** (`SceneBuilder_Build_StylerDash_SplitsWireIntoFigures`, `WireStyle_Dash_OddLength_RepeatsPattern`,
  `WirePaths_Dashed_AroundCorner_KeepsDashContinuous`, `WirePaths_Dashed_RoundedCorner_CoversExpectedLength`,
  `WirePaths_Dashed_TooFinePattern_LeavesPathSolid`): dash figures have the pattern's lengths and spacing, an odd pattern
  repeats, dashes follow corners and curves, and an excessively fine pattern stays solid.
- **Style validation and helpers** (`WireStyle_Validation_RejectsNonPositiveValues`, `WireStyle_Dash_IsCopied`,
  `WireStylers_ByPortType_UsesSourcePortTypeColor`, `WireStylers_ByTargetNodeProperty_ReadsTargetNode`,
  `WireStylers_Combine_LaterStylersOverrideEarlierNonNullMembers`, `WireStylers_NullArguments_Throw`): invalid values are
  rejected, the dash list is copied, and the ready-made stylers resolve and combine as documented.
- **Per-page styler** (`WireStylers_PerPage_BuildsOncePerPageAndVersion`, `WireStylers_PerPage_FollowsPropertyEditsUndoAndRedo`,
  `WireStylers_PerPage_KeepsPagesApartAndCombines`,
  `WireStylers_PerPage_FailingFactoryFallsBackAndRetries_NullStylerStylesNothing`,
  `WireStylers_PerPage_ArgumentChecksAndDetachedWire`, in `DiagramVersionTests.cs`): two wires and two builds cost one
  factory call, a refresh, an edit, undo or redo builds a new styler, pages are kept apart and compose with other
  stylers, a failing factory draws the theme look and is retried, a null styler is remembered for the version, and a
  null factory or a wire that is not on a page is handled.

Node styling (`NodeStyleTests.cs`):

- **Default and theme fallback** (`Diagram_NodeStyler_Default_IsNullAndNodesUseTheme`,
  `SceneBuilder_Build_NodeStyle_NullMembersAndNullStyle_FallBackToTheme`): no styler, a null style and unset members all
  give the theme look.
- **Style members** (`SceneBuilder_Build_NodeStyle_FillBorderThicknessAndHeaderText_AreApplied`,
  `SceneBuilder_Build_NodeStyle_IconTintAndOpacity_ReachTheIcon`,
  `SceneBuilder_Build_NodeStyle_Opacity_FadesEverythingTheNodeDraws`,
  `SceneBuilder_Build_NodeStyle_Glow_AddsFadingRingsBelowTheBox`): fill, border, thickness and title color reach the box
  and title; tint and opacity reach the icon; opacity halves the alpha of box, texts and port triangles but not the
  selection outline; a glow adds six fading rings below the box that extend beyond it and the scene bounds.
- **Failure, dynamics and selection** (`SceneBuilder_Build_NodeStylerThrows_FallsBackToTheme`,
  `SceneBuilder_Build_PropertyChangeOnNode_RestylesNode`, `SceneBuilder_Build_StyledSelectedNode_DrawsSelectionOutlineOnTop`):
  a throwing styler draws the theme look, a property edit and its undo restyle the node on the next build, and the
  selection outline is the last rectangle of a styled node.
- **Custom visuals** (`SceneBuilder_Build_CustomVisual_ReadsTheResolvedStyleFromTheContext`,
  `NodeVisualContext_Style_WithoutStyler_IsNoneAndFadedIsIdentity`): a visual reads the faded fill, the themed fallbacks
  and the style itself from the context, and the context holds `NodeStyle.None` without a styler.
- **Validation and helpers** (`NodeStyle_ValidationAndWithOverrides_BehaveAsDocumented`,
  `IconPrimitive_TintAndOpacity_DefaultAndValidate`, `NodeStylers_ByPropertyAndWhen_MapNodes`,
  `NodeStylers_Combine_LaterStylersOverrideEarlierNonNullMembers`, `NodeStylers_NullArguments_Throw`): invalid values
  are rejected, overrides win where set, and the ready-made stylers map, match and combine as documented.
- **Per-page styler** (`NodeStylers_PerPage_BuildsOncePerPageAndVersion`,
  `NodeStylers_PerPage_FollowsPropertyEditsUndoAndRedo`,
  `NodeStylers_PerPage_KeepsPagesApartAndHandlesFailuresAndDetachedNodes`): two nodes and two builds cost one factory
  call, a refresh, an edit, undo or redo builds a new styler, pages are kept apart, a failing factory is retried, a
  null styler is remembered for the version, and a detached node is not styled.

Grid, order, highlights, culling:

- **Empty page** (`SceneBuilder_Build_EmptyPage_ContainsOnlyGrid`) and **grid switch**
  (`SceneBuilder_Build_ShowGrid_ControlsGridPrimitive`).
- **Wires under nodes** (`SceneBuilder_Build_DrawingOrder_WiresUnderNodesInZOrder`) and **nested frames outermost first**
  (`SceneBuilder_Build_NestedExpandedGroups_DrawOuterFrameFirst`).
- **Selected node** (`SceneBuilder_Build_SelectedNode_AddsSelectionOutline`), **selected wire**
  (`SceneBuilder_Build_SelectedWire_AddsWiderUnderlay`), **hover** (`SceneBuilder_Build_Hover_UsesHoverColorUnlessSelected`),
  **selected annotation handle** (`SceneBuilder_Build_SelectedAnnotation_ShowsResizeHandle`) and **selected group**
  (`SceneBuilder_Build_SelectedGroup_AddsSelectionOutline`).
- **Culling of elements and grid** (`SceneBuilder_Build_VisibleBounds_CullsElementsOutsideAndSizesGrid`) and **of wires**
  (`SceneBuilder_Build_VisibleBounds_CullsWiresOutside`).

Options and extension:

- **Theme override** (`SceneBuilder_Build_ThemeOverride_ChangesColors`) and **measurer option**
  (`SceneBuilder_Build_TextMeasurerOption_IsUsedForLayout`).
- **Custom visual replaces** (`SceneBuilder_Build_CustomVisual_ReplacesStandardTemplate`) and **decorating visual**
  (`SceneBuilder_Build_DecoratingVisual_KeepsStandardPrimitivesAndAddsOwn`).

Groups:

- **Expanded group** (`SceneBuilder_Build_ExpandedGroup_DrawsFrameTitleBarAndToggleBehindMembers`) and **expand again**
  (`SceneBuilder_Build_ExpandAgain_RestoresMembers`).
- **Collapsed box** (`SceneBuilder_Build_CollapsedGroup_HidesMembersAndDrawsSummaryBox`), **growth**
  (`SceneBuilder_Build_CollapsedGroup_BoxGrowsToFitProxyPorts`), **nest** (`SceneBuilder_Build_NestedCollapsedGroups_OutermostWins`)
  and **hidden inner group** (`SceneBuilder_Build_ExpandedGroupInsideCollapsedGroup_IsHidden`).
- **Proxy ports** (`SceneBuilder_Build_CollapsedGroup_CrossingWiresAttachToProxyPorts`), **hidden inside wire**
  (`SceneBuilder_Build_CollapsedGroup_WireInsideIsHidden`) and **shared inner port** (`SceneBuilder_Build_CollapsedGroup_SharedInnerPortHasOneProxy`).
- **Display routes and group frames** (`Plexus-Scene-SceneBuilder-GroupObstacles`): a collapsed group's wire avoids an
  unrelated expanded group's frame (`SceneBuilder_Build_CollapsedGroupWire_RoutesAroundUnrelatedGroupFrame`), may cross
  the frame of a group holding its far end (`SceneBuilder_Build_CollapsedGroupWire_TargetInsideGroup_FrameIsNotAnObstacle`)
  and ignores frames when the theme option is off (`SceneBuilder_Build_CollapsedGroupWire_GroupsAreObstaclesOff_CrossesFrame`).
- **Display routes and nets** (`Plexus-Scene-SceneBuilder-DisplayRouteNets`): with a recording router, the request of a
  collapsed group's wire carries its own net and the three segments of the other wire's stored route, none of them in
  its net (`SceneBuilder_Build_CollapsedGroupWire_DisplayRequestCarriesOtherNetsSegments`); two wires out of one collapsed
  output share a net and the second sees the first's display segments, a third wire of another net sees both
  (`SceneBuilder_Build_CollapsedGroupWires_ShareNetBySourcePort`); the display route is the same on every build and the
  stored route is untouched (`SceneBuilder_Build_CollapsedGroupWire_DisplayRouteIsDeterministic`).

Read-only and errors:

- **Model untouched** (`SceneBuilder_Build_WithCollapsedGroupAndHighlights_DoesNotModifyModelOrHistory`): dump, undo count
  and stored route unchanged, while the scene shows a different display route.
- **Null page** (`SceneBuilder_Build_NullPage_ThrowsArgumentNullException`).

Wire polish (tests in `WireVisualsTests.cs`):

- **Junction dots**: a branching net gets one dot of the theme radius on both wires
  (`SceneBuilder_Build_BranchingNet_DrawsJunctionDotOnBothWires`) in the wire style's color
  (`SceneBuilder_Build_StyledBranchingNet_ColorsJunctionDotLikeTheWire`); different nets and a disabled theme setting
  draw none (`SceneBuilder_Build_DifferentNets_DrawNoJunctionDot`, `SceneBuilder_Build_DotsDisabledByTheme_DrawsNoDot`);
  detection itself is checked for a bend on a passing trunk and for identical wires
  (`WireJunctions_Find_BendOnPassingTrunk_IsAJunction`, `WireJunctions_Find_IdenticalWires_HaveNoJunction`).
- **Junction audit** (tests in `WireJunctionAuditTests.cs`, page built by the `WiringAudit` helper; the same
  configurations are drawn by the demo's `wiring` sample): each configuration has the exact expected dots
  (`SceneBuilder_Build_AuditConfiguration_DrawsDotsExactlyAtJunctions`):

  | Configuration | Expected |
  | :------------ | :------- |
  | `TOnStraightTrunk`: one wire straight on, one branching off | one dot at the branch, square corner |
  | `SplitAtCorner`: both branches turn at one point | one dot on the bend, two square corners |
  | `FanOut3`: one input above, two below | dots at the split and where the lower pair separates |
  | `FanOut4`: two above, two below | three dots |
  | `FanInTwoNets`: two nets into an input that allows fan-in | one dot where the second wire joins, square corner |
  | `CrossingDifferentNets` | no dot; the horizontal wire hops |
  | `TouchingDifferentNets`: one wire bends on another and runs along it | no dot, no hop |
  | `EndOnTrunk`: one wire ends exactly on another's vertical run | dots at the end and where it leaves |
  | `StyledFanOut`: one wire dashed, one thick and red | the dots of `FanOut3`, twice the radius (thick net) |

  The different-net configurations draw no dot (`SceneBuilder_Build_DifferentNetConfigurations_DrawNoDot`);
  automatically routed wires into an opted-in input draw one merge dot and plain single-wire inputs draw none
  (`SceneBuilder_Build_AutoRoutedFanIn_DrawsMergeDot`, `SceneBuilder_Build_PlainInputsWithoutFanOut_DrawNoDot`), and
  such wires route as one net (`DiagramPage_Connect_FanInIntoOptedInInput_RoutesAsOneNet`); styled
  nets keep their dots scaled and colored (`SceneBuilder_Build_StyledNet_KeepsDotsScaledAndColored`); a fan-out from a
  collapsed group's proxy gets dots and square corners from its display routes
  (`SceneBuilder_Build_CollapsedGroupFanOut_DotsAndSquareCornersFollowDisplayRoutes`); two builds of the audit page
  draw the same primitives (`SceneBuilder_Build_AuditPage_IsDeterministic`); detection of a wire that is a prefix of
  another (no dot), a wire ending on a trunk (dot) and a "+" of one net (dot, stable)
  (`WireJunctions_Find_CollinearOverlapWithoutDivergence_HasNoJunction`,
  `WireJunctions_Find_WireEndingOnTrunk_IsAJunctionAtTheEnd`, `WireJunctions_Find_PlusOfOneNet_IsAJunctionAndStable`);
  the demo sample (`DemoSamples_Wiring_Page_ShowsDotsHopsAndMergeDotForOptedInFanIn`).
- **Square junction corners** (`Plexus-Scene-SceneBuilder-WireJunctionCorners`): in every configuration with
  junctions the bend at a junction point has a line to the exact point and no curve, and every other bend has one curve
  (`SceneBuilder_Build_AuditConfiguration_JunctionCornersAreSquareAndOthersRounded`); the corner stays square with
  dots switched off (`SceneBuilder_Build_DotsOff_JunctionCornerStaysSquare`); `WirePaths.Rounded` draws no curve at a
  square corner and lets the next corner use the full segment
  (`WirePaths_Rounded_SharpCorner_HasNoCurveAndLetsTheNextCornerUseTheSegment`) and does not smooth a small jog whose
  corner is a junction (`WirePaths_Rounded_SharpCornerOnSmallJog_IsNotSmoothed`).
- **Crossing jumps** (`Plexus-Scene-SceneBuilder-WireCrossingJumps`, tests in `WireCrossingTests.cs`): detection reports
  an interior crossing once, to the horizontal wire (`WireCrossings_Find_InteriorCrossing_IsReportedOnTheHorizontalWire`)
  and none for the same net, for ends and bends, near a corner of the vertical wire or at junctions
  (`WireCrossings_Find_SameNet_HasNoHop`, `WireCrossings_Find_EndsAndBends_HaveNoHop`,
  `WireCrossings_Find_CrossingNearVerticalCorner_HasNoHop`, `WireCrossings_Find_JunctionAtCrossing_HasNoHop`); the radius
  grows with the thickness of either wire (`WireCrossings_Find_ThickWires_GrowTheRadius`). The path of one hop is two
  cubic quarter arcs of the radius over the crossing, also right to left
  (`WirePaths_Rounded_SingleHop_IsASemicircleOverTheCrossing`, `WirePaths_Rounded_RightToLeftWire_HopsOverTheCrossing`);
  crossings 6 apart merge into one flat-topped hop, those 40 apart stay separate, and a hop that does not fit near a
  corner curve, the start or the arrowhead is skipped (`WirePaths_Rounded_CloseCrossings_MergeIntoOneWiderHop`,
  `WirePaths_Rounded_DistantCrossings_GetSeparateHops`, `WirePaths_Rounded_HopsThatDoNotFit_AreSkipped`); vertical
  runs never hop (`WirePaths_Rounded_VerticalRun_NeverHops`); dashes follow the arc
  (`WirePaths_Dashed_Hop_DashesFollowTheArc`). In the scene the audit crossing hops by default with the apex 4 above the
  wire, the vertical wire has only its own corners, switching the theme option off or a touching pair draws no hop
  (`SceneBuilder_Build_DifferentNetCrossing_HorizontalWireHopsByDefault`, `SceneBuilder_Build_JumpsOffOrTouchingNets_DrawNoHop`),
  a thick dashed wire hops with the scaled radius and dashes on the arc
  (`SceneBuilder_Build_ThickDashedHoppingWire_ScalesAndDashesTheHop`), the polyline, stored route and hit testing stay
  straight (`SceneBuilder_Build_Hop_LeavesPolylineAndHitTestingStraight`), a collapsed group's display route is hopped
  over like a stored one (`SceneBuilder_Build_CollapsedGroupDisplayRoute_ParticipatesInCrossings`) and the result is
  deterministic (`SceneBuilder_Build_Hops_AreDeterministic`).
- **Arrowhead fit**: with the usual 15 unit stub the last corner keeps the full radius and the stroke ends inside the
  head (`WirePaths_Rounded_StubLengthFinalSegment_KeepsFullRadius`); on a 12 unit last segment the curve ends exactly at
  the arrowhead base (`WirePaths_Rounded_ShortFinalSegment_ArrowheadSitsOnStraightRun`); on a 5 unit last segment the
  head shrinks and the corner is sharp (`SceneBuilder_Build_VeryShortFinalSegment_ShrinksArrowheadToFit`).
- **Handles**: a selected wire has one square per bend and a circle at each end, an unselected one none
  (`SceneBuilder_Build_SelectedWire_DrawsHandlesAtBendsAndEnds`).
- **Small jogs** (`Plexus-Scene-SceneBuilder-WireSmallJogs`): a 7.5 step between runs is one cubic spanning 30 units
  centered on the step (`WirePaths_Rounded_ShortStep_DrawsOneSCurveSpanningFourSteps`), clamped to a 10 unit run at the
  start of the wire (`WirePaths_Rounded_ShortStepNearTheStart_ClampsTheSpanToTheRun`); a 20 step keeps two corners
  (`WirePaths_Rounded_LongStep_KeepsTwoCorners`), as do a request drawn without smoothing and a U turn
  (`WirePaths_Rounded_ShortStepWithoutSmoothingOrAsUTurn_KeepsTwoCorners`) and a step with too little run left before
  the arrowhead (`WirePaths_Rounded_ShortStepTooCloseToArrowhead_KeepsCorners`); dashes have points inside the step
  height (`WirePaths_Dashed_SCurve_DashesFollowTheCurve`); the built scene has the S by default and two corners with
  `WireSmoothSmallJogs` off, while the stored route and polyline stay square
  (`SceneBuilder_Build_SmallJog_DrawsSCurveUnlessDisabledByTheme`).

Boundary values: empty page, culled and partially visible elements, annotation too small, nested groups, shared proxy
ports. Error path: null page.
