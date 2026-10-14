## CanvasNet Verification

This document provides the verification evidence for the CanvasNet OTS software item. Requirements for this OTS item
are defined in the CanvasNet OTS Software Requirements document.

### Required Functionality

DemaConsulting.CanvasNet and DemaConsulting.CanvasNet.Svg provide the drawing, text, SVG rasterization and PNG
encoding used by the PlexusRendering system. The library has no vendor-supplied compliance evidence, so its required
functionality is proven by the PlexusRendering tests, which exercise each CanvasNet capability Plexus relies on and
inspect the resulting pixels or encoded images.

### Verification Approach

No dedicated OTS test project is used. The PlexusRendering unit tests render known scenes to in-memory bitmaps and
assert on pixel values, measured text widths and PNG signatures, so a passing test run proves the CanvasNet behavior
required by the requirements below. Each scenario names a test method that ReqStream matches in the TRX results.

### Test Scenarios

#### Rasterization (Plexus-OTS-CanvasNet-Rasterize)

- **ScenePainter_Paint_FilledRect_CoversAreaOnly**: a filled rectangle colors exactly its area.
- **ScenePainter_Paint_StrokedRect_OnePixelBorderIsCrisp**: a one-pixel stroke produces a crisp border.
- **ScenePainter_Paint_RoundedRect_CornerIsCutAway**: a rounded rectangle leaves its corner pixels unpainted.
- **ScenePainter_Paint_FilledPath_FillsPolygon**: a filled path fills the polygon interior.
- **ScenePainter_Paint_StrokedCubicPath_FollowsTheCurve**: a stroked cubic path passes through the expected pixels.
- **ScenePainter_Paint_DiagonalStroke_IsAntiAliased**: a diagonal stroke produces intermediate coverage values.
- **ScenePainter_Paint_TranslucentFill_BlendsWithBackdrop**: a translucent fill blends with the backdrop color.

**Expected**: each test passes, proving shapes are rasterized with anti-aliasing and alpha blending.

#### Text (Plexus-OTS-CanvasNet-Text)

- **CanvasTextMeasurer_Measure_LongerText_IsWider**: measured width grows with text length.
- **CanvasTextMeasurer_Measure_DoubleFontSize_DoublesWidth**: measured width scales with font size.
- **CanvasTextMeasurer_Measure_Bold_IsAtLeastAsWide**: bold text is at least as wide as regular text.
- **CanvasTextMeasurer_Measure_UnknownFamily_FallsBackToBundledFont**: an unknown font family falls back to the
  bundled font.
- **CanvasTextMeasurer_Measure_SameInput_IsDeterministic**: repeated measurement gives identical results.
- **ScenePainter_Paint_Text_PaintsGlyphsInsideRect**: drawn text paints glyph pixels inside its rectangle.
- **ScenePainter_Paint_TextAlignment_AnchorsToRectEdges**: text alignment anchors glyphs to the rectangle edges.
- **ScenePainter_Paint_BoldText_DrawsMoreInk**: bold text draws more ink than regular text.

**Expected**: each test passes, proving text is measured and drawn consistently with a guaranteed fallback font.

#### SVG (Plexus-OTS-CanvasNet-Svg)

- **IconCache_Get_SameKeyAndSize_ReturnsSharedSurface**: an SVG is rasterized once per key and size.
- **IconCache_Get_MalformedSvg_ReturnsNull**: malformed SVG is reported as a missing icon without throwing.
- **ScenePainter_Paint_Icon_CompositesRasterizedSvg**: a rasterized icon is composited into the output.
- **ScenePainter_Paint_MalformedIcon_IsSkipped**: a malformed icon is skipped and the rest of the scene is painted.
- **PlexusRenderer_Render_NodeIcon_PaintsRasterizedSvg**: a node icon appears in the rendered page.

**Expected**: each test passes, proving SVG icons are rasterized and failures are contained.

#### PNG (Plexus-OTS-CanvasNet-Png)

- **PlexusRenderer_RenderPng_DefaultOptions_ProducesPngSignature**: the output starts with the PNG signature.
- **PlexusRenderer_RenderPng_TransparentBackground_KeepsAlpha**: a transparent background keeps its alpha channel.
- **PlexusRenderer_SavePng_WritesPngFile**: a PNG file is written to disk.

**Expected**: each test passes, proving valid PNG images are produced.

### Requirements Coverage

- **`Plexus-OTS-CanvasNet-Rasterize`**: ScenePainter_Paint_FilledRect_CoversAreaOnly,
  ScenePainter_Paint_StrokedRect_OnePixelBorderIsCrisp, ScenePainter_Paint_RoundedRect_CornerIsCutAway,
  ScenePainter_Paint_FilledPath_FillsPolygon, ScenePainter_Paint_StrokedCubicPath_FollowsTheCurve,
  ScenePainter_Paint_DiagonalStroke_IsAntiAliased, ScenePainter_Paint_TranslucentFill_BlendsWithBackdrop
- **`Plexus-OTS-CanvasNet-Text`**: CanvasTextMeasurer_Measure_LongerText_IsWider,
  CanvasTextMeasurer_Measure_DoubleFontSize_DoublesWidth, CanvasTextMeasurer_Measure_Bold_IsAtLeastAsWide,
  CanvasTextMeasurer_Measure_UnknownFamily_FallsBackToBundledFont, CanvasTextMeasurer_Measure_SameInput_IsDeterministic,
  ScenePainter_Paint_Text_PaintsGlyphsInsideRect, ScenePainter_Paint_TextAlignment_AnchorsToRectEdges,
  ScenePainter_Paint_BoldText_DrawsMoreInk
- **`Plexus-OTS-CanvasNet-Svg`**: IconCache_Get_SameKeyAndSize_ReturnsSharedSurface,
  IconCache_Get_MalformedSvg_ReturnsNull, ScenePainter_Paint_Icon_CompositesRasterizedSvg,
  ScenePainter_Paint_MalformedIcon_IsSkipped, PlexusRenderer_Render_NodeIcon_PaintsRasterizedSvg
- **`Plexus-OTS-CanvasNet-Png`**: PlexusRenderer_RenderPng_DefaultOptions_ProducesPngSignature,
  PlexusRenderer_RenderPng_TransparentBackground_KeepsAlpha, PlexusRenderer_SavePng_WritesPngFile
