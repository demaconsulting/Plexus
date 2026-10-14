## PlexusRenderer Unit Verification Design

Scope: Public entry point that renders a page to a PNG image or file according to render options.

### Verification Approach

Unit tests in `PlexusRendererTests.cs` render real pages built with a small node type registry (`RenderTestModel`)
and inspect the decoded PNG or the returned surface pixel by pixel. Nothing is mocked: the core scene builder, the
other units of the system and CanvasNet are real collaborators, because the unit's behavior is exactly the
orchestration of them. Pixel positions are derived from the default theme and node geometry.

### Test Environment

No setup beyond the standard test runner. The file test uses a uniquely named file in the temporary folder and deletes it
afterwards.

### Acceptance Criteria

A test passes when the image has the expected size, sampled pixels have the expected theme or option colors at the
computed positions, and invalid input raises the documented exception type.

### Test Scenarios

- **PlexusRenderer_RenderPng_DefaultOptions_ProducesPngSignature**: the output starts with the PNG signature.
- **PlexusRenderer_RenderPng_TransparentBackground_KeepsAlpha**: a transparent background decodes with zero alpha.
- **PlexusRenderer_SavePng_WritesPngFile**: a PNG file with a signature and a scaled image is written to disk.
- **PlexusRenderer_Render_Scale2_DoublesSizeAndPositions**: the returned surface is twice the size and content sits at
  twice the coordinates.
- **PlexusRenderer_Render_Background_UsesOptionOrTheme**: the requested background is used, else the theme's.
- **PlexusRenderer_RenderPng_EmptyPage_ProducesPaddingSizedImage**: an empty page renders as two paddings square.
- **PlexusRenderer_Render_NoBounds_FitsContentPlusPadding**: the image fits the content, and extra padding adds its
  width on both sides.
- **PlexusRenderer_RenderPng_BoundsAndScale_DetermineImageSize**: explicit bounds fix the size and scale multiplies it.
- **PlexusRenderer_Render_ShowGrid_DrawsDotsOnGridPoints**: dots appear on grid multiples when enabled and not when
  switched off.
- **PlexusRenderer_Render_Node_PaintsBoxWithNodeFill**: a node box has the node color, and the background shows outside.
- **PlexusRenderer_Render_NodeTextTitleAndName_PaintsGlyphPixels**: the title paints in the header and the name above
  the box.
- **PlexusRenderer_Render_NodeIcon_PaintsRasterizedSvg**: the node's SVG icon shows at the center of the icon box.
- **PlexusRenderer_Render_Wire_StrokesBetweenPorts**: a wire is a two-pixel line in the wire color between aligned
  ports.
- **PlexusRenderer_Render_StyledWire_UsesStylerColorAndThickness**: a wire colored by port type and styled six units
  thick shows the port type color at its center and two pixels either side, while the default wire is black.
- **PlexusRenderer_Render_DashedWire_AlternatesInkAndGaps**: pixels along a wire with a 10/10 dash pattern are about
  half ink and half background.
- **PlexusRenderer_Render_StylerDependsOnHostState_ChangesAfterRefresh**: a styler reading a host variable gives a
  different wire color on the next render after the host changed it and refreshed the diagram.
- **PlexusRenderer_Render_StyledNode_ShowsFillBorderAndTintedIcon**: a styled node shows the style's fill, border and a
  yellow icon where the unstyled node has the theme fill and the authored red icon.
- **PlexusRenderer_Render_GlowAndOpacity_ShowHaloAndFade**: a glow tints pixels just outside the box and fades with
  distance to the plain canvas, and half opacity blends a black box to half of the canvas color.
- **PlexusRenderer_Render_AnnotationAndGroups_PaintsAnnotationFill**: a page with an annotation, an expanded and a
  collapsed group renders, and the annotation fill is visible.
- **PlexusRenderer_Render_ThemeOption_OverridesDiagramTheme**: the options' theme colors replace the diagram's.
- **PlexusRenderer_Render_SelectedNode_DrawsSelectionOutline**: a selected node has the selection outline that an
  unselected one lacks.
- **PlexusRenderer_Render_InvalidInput_Throws**: null page, zero or NaN scale, negative padding, empty bounds, an
  oversized image and a blank path each throw the documented exception.
