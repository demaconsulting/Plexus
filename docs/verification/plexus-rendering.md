# PlexusRendering System Verification Design

Scope: Headless renderer that paints a Plexus scene for one page to a PNG image using CanvasNet.

## Verification Approach

System verification is end to end: real diagrams are built with the Plexus core library, rendered through the public
`PlexusRenderer` API, and the output is checked by decoding PNG bytes or by sampling pixels of the returned surface.
There are no mocks; the core library, CanvasNet and the unit collaborators are all real. The tests are xUnit tests in
`PlexusRenderingTests.cs` in the PlexusRendering test project, sharing the fixtures in `RenderTestModel.cs` (a small
node type registry and PNG/pixel helpers). Unit-level behavior is verified separately (see the unit verification
designs) and CanvasNet is verified as an OTS item through the unit tests (see _CanvasNet Verification_).

## Test Environment

- .NET 10 test runner, run on Windows, Linux and macOS in the build pipeline; the platform requirements use source
  filters so each platform's own results are the evidence.
- No display, UI framework or network is needed. Tests that write files use a uniquely named file in the operating
  system's temporary folder and delete it afterwards.
- Text assertions tolerate the difference between machines with and without the theme font installed (the bundled
  fallback font is used otherwise), so widths are compared within a small tolerance rather than exactly.

## Acceptance Criteria

A system test passes when the rendered output has the documented size and content: PNG output starts with the PNG
signature and decodes to an image of the expected dimensions, sampled pixels have the theme or option colors expected at
the computed positions, and invalid input raises the documented exception. All system tests must pass on every
supported platform for the platform requirements to be satisfied.

## Test Scenarios

- **PlexusRendering_RenderPng_DiagramPage_ProducesDecodablePng**: a page with two connected nodes renders to bytes with
  a PNG signature that decode to an image covering both nodes. Also the platform-evidence scenario for every platform
  requirement.
- **PlexusRendering_SavePng_DiagramPage_WritesDecodableFile**: saving a page writes a file whose bytes equal the
  rendered PNG and decode to an image.
- **PlexusRendering_Render_StyledNodes_ShowTheStyleOnlyWhereTheStylerDecides**: of two icon nodes only the styled one
  shows the style's fill and tinted icon, the other keeps the theme fill and the icon as authored, and a changed tint
  shows after the host refreshes the appearance.
- **PlexusRendering_Render_FullPage_ShowsEveryElementKind**: a page with an icon node, a titled node, a wire, an
  annotation and a group shows the icon, title text, name text, wire, annotation fill and group at the expected pixels.
- **PlexusRendering_Render_DynamicPortNode_ShowsCurrentPorts**: a node whose inputs come from a port provider shows the
  port color at the position of its third input triangle only while the node has three ports (after the property change
  that adds the input, not before it and not after it is shrunk again), and a static node's declared port is drawn in
  every render.
- **PlexusRendering_Render_MalformedIcon_StillRendersTheRestOfThePage**: a node with a malformed SVG icon does not
  prevent its own box and the neighboring node from being painted.
- **PlexusRendering_Render_NodeTitle_MatchesMeasuredTextWidth**: the horizontal extent of the painted title equals
  the width reported by the text measurer, within a tolerance for glyph side bearings, and is centered in the node.
- **PlexusRendering_Render_FramingOptions_SizeTheImage**: padding grows the content-fitted image on each side, explicit
  bounds fix the size, and scale multiplies it.
- **PlexusRendering_Render_BackgroundAndGridOptions_ChangeThePixels**: the default background is the theme's, a
  requested background replaces it, and grid dots appear only when the grid is enabled.
- **PlexusRendering_Render_ThemeAndSelectionOptions_RestyleTheImage**: a theme option replaces the diagram's colors,
  and a selected node has the selection outline that an unselected render lacks.
- **PlexusRendering_Render_PixelSurface_IsOwnedByTheCaller**: a rendered surface has the requested size, and changing
  and disposing it does not affect a later render.
- **PlexusRendering_Render_InvalidInput_IsRejected**: a null page, an unusable scale, padding or bounds, an oversized
  image and an empty path each raise an argument exception.
- **PlexusRendering_Render_ConcurrentCalls_ProduceIdenticalImages**: sixteen parallel renders of one page, using text
  and an icon, all produce exactly the bytes of a sequential render.
- **PlexusRendering_Dependencies_AssemblyReferences_ContainNoUiFramework**: the rendering assembly references the core
  library and no UI framework assembly.
