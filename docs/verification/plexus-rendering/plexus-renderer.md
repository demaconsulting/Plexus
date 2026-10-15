## PlexusRenderer Unit Verification Design

Scope: Public entry point that renders a page to a PNG image or file according to render options.

### Verification Approach

Unit tests in `PlexusRendererTests.cs` render real pages built with a small node type registry (`RenderTestModel`)
and inspect the decoded PNG or the returned surface pixel by pixel. Nothing is mocked: the core scene builder, the
other units of the system and CanvasNet are real collaborators, because the unit's behavior is exactly the
orchestration of them. Pixel positions are derived from the default theme and node geometry. The scenarios of the
styled wire and styled node requirements are in `WireStyleRenderingTests.cs` and `NodeStyleRenderingTests.cs`, which
use the same fixtures and the same technique; the evidence of this unit is therefore the three files
`PlexusRendererTests.cs`, `WireStyleRenderingTests.cs` and `NodeStyleRenderingTests.cs` (the scenarios below name the
test of each requirement).

### Test Environment

No setup beyond the standard test runner. The file tests use uniquely named files or folders in the temporary folder and
delete them afterwards (the missing-folder test creates nothing).

### Acceptance Criteria

A test passes when the image has the expected size, sampled pixels have the expected theme or option colors at the
computed positions, and invalid input raises the documented exception type. A failed save raises the file-system
exception of the platform unchanged and leaves no file or folder behind.

### Test Scenarios

- **PlexusRenderer_RenderPng_DefaultOptions_ProducesPngSignature**: the output starts with the PNG signature.
- **PlexusRenderer_RenderPng_TransparentBackground_KeepsAlpha**: a background requested as transparent decodes with zero
  alpha.
- **PlexusRenderer_RenderPng_TransparentOptionTheme_KeepsAlpha**: a theme in the options whose canvas background is
  transparent, with no background color requested, also decodes with zero alpha although the diagram's own theme is
  opaque.
- **PlexusRenderer_SavePng_WritesPngFile**: a PNG file with a signature and a scaled image is written to disk.
- **PlexusRenderer_SavePng_MissingFolder_ThrowsDirectoryNotFound**: saving into a folder that does not exist throws
  `DirectoryNotFoundException` and creates neither the folder nor the file.
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
- **PlexusRenderer_Render_AnnotationAndGroups_PaintsAnnotationFill**: with only an annotation in view, its fill is
  visible near its bottom-right corner (a page with an annotation, an expanded and a collapsed group also renders).
- **PlexusRenderer_Render_ExpandedGroup_DrawsFrameAndTitleBar**: with a theme of opaque, distinctive group colors, the
  border column of the group frame below the title bar has the border color, the right end of the title bar has the
  title-bar color, the frame equals the group's bounds, and the member node is still drawn with the node fill.
- **PlexusRenderer_Render_CollapsedGroup_DrawsBoxAndHidesMembers**: while the group is expanded the member's pixel has
  the node fill; after the group is collapsed that pixel shows the background, the collapsed box body has the node fill,
  and the group border color is absent at the box edge.
- **PlexusRenderer_Render_ThemeOption_OverridesDiagramTheme**: the options' theme colors replace the diagram's.
- **PlexusRenderer_Render_SelectedNode_DrawsSelectionOutline**: a selected node has the selection outline that an
  unselected one lacks.
- **PlexusRenderer_RenderOptions_SelectionChangedAfterInit_KeepsSnapshot**: options created from a list keep the node
  after the caller empties the list and adds another element; the node is drawn with its outline, the other element
  is not, and a null selection throws `ArgumentNullException`.
- **PlexusRenderer_Render_InvalidInput_Throws**: null page, zero or NaN scale, negative padding, empty bounds, an
  oversized image and a blank path each throw the documented exception.
- **PlexusRenderer_Render_NonFiniteOrNegativeOptions_Throw**: an infinite or negative scale and a NaN or infinite
  padding each throw `ArgumentException`.
- **PlexusRenderer_RenderPng_DynamicPortNode_DrawsPortsFromProvider**: the pixel where a node's third computed input
  triangle is drawn has the port color only after the property change that adds that input.
