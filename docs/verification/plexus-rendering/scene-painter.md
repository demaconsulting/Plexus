## ScenePainter Unit Verification Design (PlexusRendering)

Scope: Paints scene primitives (rectangles, paths, text, icons, grid) onto a CanvasNet surface.

### Verification Approach

Unit tests in `ScenePainterTests.cs` create a white surface and a painter, paint hand-built primitives, and sample
pixels; all scenarios of this unit, including the tinted-icon scenarios, are in this one file. Real `Surface`,
`IconCache` and font objects are used (they are the behavior under test); no test doubles are needed because the
painter has no other collaborators. Text and icons are checked by counting changed pixels in regions, which is
independent of exact glyph shapes. Borders are checked at scale one and at scale two, because the border width in output
pixels is the stroke thickness times the scale.

### Test Environment

No setup beyond the standard test runner.

### Acceptance Criteria

A test passes when the sampled pixels have the expected colors, regions that must stay untouched equal the background,
and the argument checks raise the documented exception.

### Test Scenarios

- **ScenePainter_Paint_FilledRect_CoversAreaOnly**: a filled rectangle colors its area and nothing outside it.
- **ScenePainter_Paint_StrokedRect_OnePixelBorderIsCrisp**: a one-unit stroke at scale one occupies exactly one pixel
  column at full coverage, and the neighbors stay white.
- **ScenePainter_Paint_StrokedRectAtScaleTwo_BorderIsTwoCrispPixels**: a one-unit stroke at scale two occupies exactly
  two pixel columns and two pixel rows at full coverage (19 and 20 for the edge at device 20), the neighbors and the
  interior stay white; this also shows that the stroke width in output pixels is the thickness times the scale.
- **ScenePainter_Paint_HalfUnitBorderAtScaleTwo_IsOneCrispPixel**: a half-unit stroke at scale two occupies exactly one
  pixel column and one pixel row at full coverage.
- **ScenePainter_Paint_RoundedRect_CornerIsCutAway**: the corner pixel of a rounded rectangle stays unpainted.
- **ScenePainter_Paint_FilledPath_FillsPolygon**: a closed triangle is filled inside and not in the opposite corner.
- **ScenePainter_Paint_OpenPathWithFill_IsImplicitlyClosed**: an unclosed filled path is filled as if closed.
- **ScenePainter_Paint_StrokedCubicPath_FollowsTheCurve**: the stroke passes through the curve apex, not the chord.
- **ScenePainter_Paint_DiagonalStroke_IsAntiAliased**: a diagonal line has partially covered pixels.
- **ScenePainter_Paint_TranslucentFill_BlendsWithBackdrop**: a half-transparent fill gives a mid tone and stays opaque.
- **ScenePainter_Paint_Sequence_PaintsBackToFront**: of two overlapping rectangles the later one covers the earlier.
- **ScenePainter_Paint_PrimitivesBeyondSurface_AreClipped**: a rectangle, an icon and a grid reaching beyond the
  surface paint their visible parts without failing.
- **ScenePainter_Paint_Text_PaintsGlyphsInsideRect**: text paints ink inside its rectangle and none below it.
- **ScenePainter_Paint_TextAlignment_AnchorsToRectEdges**: left, center and right text sit at the matching part of the
  rectangle.
- **ScenePainter_Paint_BoldText_DrawsMoreInk**: bold text darkens more pixels than regular text.
- **ScenePainter_Paint_Icon_CompositesRasterizedSvg**: an icon is opaque where its SVG paints and transparent
  elsewhere (the background shows through the transparent half).
- **ScenePainter_Paint_TintedIcon_RecolorsEveryPaintedPixel**: a two-colored icon painted with a tint is one flat color
  wherever it paints and stays transparent elsewhere, while the copy without a tint keeps both colors.
- **ScenePainter_Paint_TintAlphaAndOpacity_BlendWithTheBackground**: a half-alpha tint and a half-opaque icon without
  a tint each blend to the expected mid tone over a white background.
- **ScenePainter_Paint_MalformedIcon_IsSkipped**: a malformed icon leaves the surface unchanged.
- **ScenePainter_Paint_Grid_DrawsDotsAtSpacing**: dots appear on multiples of the spacing only.
- **ScenePainter_Paint_LargeGrid_DrawsDotsAtHighCoordinates**: a grid of about seventy thousand dots on a 4000 by 4000
  surface still paints a dot at (1500, 1500).
- **ScenePainter_Paint_Scale_MultipliesGeometry**: a 10-unit square at (5, 5) painted at scale three covers pixels 15
  to 44 (positions and sizes are multiplied; the stroke width is covered by the scale-two border scenarios above).
- **ScenePainter_Paint_Origin_OffsetsGeometry**: a painter with a non-zero origin shifts the geometry accordingly.
- **ScenePainter_Paint_NullArguments_Throw**: a null surface, primitive or sequence throws.

The branch that rejects an unknown primitive kind is not exercised: the core library's primitive base class cannot be
derived outside the core assembly, so no unknown kind can be constructed.
