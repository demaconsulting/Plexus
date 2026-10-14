## ScenePainter Unit Verification Design (PlexusAvalonia)

Scope: Paints scene primitives onto an Avalonia drawing context.

### Verification Approach

The painter is verified by painting real scenes, built by the core scene builder from small pages, onto a transparent
offscreen Avalonia bitmap with Skia drawing, and reading the pixels back. Pixel checks use colors of the default theme
and sample positions derived from the scene (for example the midpoint of a wire), with tolerances where antialiasing
applies, so no stored images are compared. The core, Avalonia and the text measurer are real; only the page is
small. Cache behavior is verified through object identity. The conversions are verified as pure functions. The tests
are in `ScenePainterTests.cs`; the painter inside a running editor is also covered by the DiagramEditor rendering
tests.

### Test Environment

The standard test runner plus the Avalonia headless platform with Skia drawing (a real surface is needed to rasterize),
started by the shared fixtures. Bitmaps are 400 by 300 pixels at 96 dpi, and a fresh painter is used for each paint so
that no cache leaks between tests.

### Acceptance Criteria

A test passes when the painted pixels, or the cached objects, are as specified: expected colors present in sufficient
numbers, dots exactly on multiples of the spacing, nothing painted where nothing should be, and no exception for
malformed icons.

### Test Scenarios

- **ScenePainter_GetBrush_SameColor_ReturnsCachedBrush**: the same color gives the same brush, another color another
  brush.
- **ScenePainter_GetPen_SameColorAndThickness_ReturnsCachedPen**: the same color and thickness give the same pen, a
  different thickness another one, with flat caps (matching the headless painter) and round joins.
- **ScenePainter_Paint_NodesWiresAndText_DrawsThemeColors**: a connected pair paints the node fill, text in the name
  color and a stroke on the wire's midpoint.
- **ScenePainter_Paint_Grid_DrawsDotsOnSpacingAndThinsWhenZoomedOut**: at zoom 1 there is a dot at the first grid
  point and none between dots; at zoom 0.1 only every eighth line remains (boundary: spacing doubling).
- **ScenePainter_Paint_Icon_DrawsIconColors**: a node with a valid SVG icon paints the icon's color.
- **StyledNode_PaintsTheSameInTheEditorAndThePng** (`NodeStylePixelTests.cs`): the tinted and faded icon, the styled
  fill and the halo painted by this painter in a headless editor match the PNG painter's pixels of the same node at two
  zooms; the full style variant checks the tint (silhouette through an opacity mask), the faded variant checks the
  uniform opacity mask.
- **ScenePainter_Paint_BrokenIcon_LeavesIconOutWithoutThrowing**: a malformed icon leaves the icon out, still paints the
  node, and raises nothing (error path).
- **ScenePainter_Paint_VisibleAreaElsewhere_PaintsNothing**: with a visible area far from the content nothing is painted
  (boundary: no intersection).
- **PlexusAvaloniaExtensions_PointSizeRectAndColor_PreserveValues**: points, sizes, rectangles and colors convert to
  Avalonia and back without losing a component, including a translucent color and a negative coordinate.
