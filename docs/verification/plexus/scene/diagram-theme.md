### DiagramTheme Unit Verification Design

#### Verification Approach

Unit tests in `DiagramThemeTests` read and copy theme values directly. No collaborators or doubles are needed because
the unit is an immutable value holder.

#### Test Environment

Standard xunit runner; no files, fonts or network.

#### Acceptance Criteria

All scenarios pass on every target framework; documented defaults compare equal to the expected values.

#### Test Scenarios

- **Classic look**
  (`DiagramTheme_Classic_HasDocumentedDefaults`): documented colors, grid size, font, wire thickness, translucent
  group fill, header weight and name alignment.
- **Customized copy**
  (`DiagramTheme_With_CustomizedCopy_LeavesClassicUnchanged`): a `with` copy carries the new values and every other
  setting, and the shared classic instance is unchanged.
- **Text styles follow settings**
  (`DiagramTheme_TextStyles_FollowFontSettings`): header, name, summary, port label, group title and annotation
  styles reflect the family, sizes and colors set in the copy.
- **Port alignment**
  (`DiagramTheme_PortAlignment_DefaultsToCenterAndCanBeChanged`): the classic theme centers ports, a `with` copy can
  switch to top alignment and the classic theme is unchanged.
- **Wire metrics**
  (`DiagramTheme_Classic_WireJunctionAndHandleDefaults`, `SceneBuilder_Build_DotsDisabledByTheme_DrawsNoDot`): junction
  dots are on with radius 3 and handles are 8 by default, and a customized copy can switch the dots off.
- **Small jog smoothing**
  (`DiagramTheme_WireSmoothSmallJogs_DefaultsToTrueAndCanBeSwitchedOff`,
  `SceneBuilder_Build_SmallJog_DrawsSCurveUnlessDisabledByTheme`): the option is on in the classic theme and a `with`
  copy can switch it off, which draws the two ordinary corners.
- **Crossing jumps**
  (`DiagramTheme_WireCrossingJumps_DefaultToOnWithRadiusFourAndCanBeChanged`,
  `SceneBuilder_Build_JumpsOffOrTouchingNets_DrawNoHop`): jumps are on with radius 4 in the classic theme, a `with`
  copy can switch them off (the crossing wire is then drawn straight) or resize them.
- **Groups are obstacles**
  (`DiagramTheme_GroupsAreObstacles_DefaultsToTrue`, `DiagramPage_Connect_GroupsAreObstaclesOff_RoutesThroughGroupFrame`,
  `DiagramPage_Connect_CollapsedGroupBetweenPorts_GroupsAreObstaclesOff_CrossesBox`): the option is on in the classic
  and in a new theme; a `with` copy that switches it off lets wires run through an expanded frame and a collapsed box.
- **Dark preset** (`Plexus-Scene-DiagramTheme-Dark`, `DefaultNodeWidthTests.cs`:
  `DiagramTheme_Dark_IsADistinctPresetWithClassicMetrics`,
  `DiagramTheme_Dark_TextsWiresAndFramesAreReadableOnTheirBackgrounds`, `DiagramTheme_Dark_AssignedToDiagram_DrivesTheScene`):
  the preset is a shared instance with a dark canvas and the classic metrics; the instance name, summary, port label, title,
  annotation and group title texts reach a 4.5:1 contrast ratio on what they are drawn on, and wires, selection and
  frame borders 3:1, with grid dots visible but quieter than wires; a diagram with the theme draws the dark node fill and
  name color.

Boundary values: copy versus original, default versus customized.
