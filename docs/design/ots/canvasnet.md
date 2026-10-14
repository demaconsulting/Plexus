## CanvasNet

### Purpose

DemaConsulting.CanvasNet (with DemaConsulting.CanvasNet.Svg) is the 2D drawing library used by the PlexusRendering
system. It was chosen because it provides a pure managed, display-independent canvas with fonts, SVG rasterization
and PNG encoding, which lets Plexus render diagrams headlessly (for example in servers, build pipelines and agent
tooling) without referencing any UI framework.

### Features Used

- Canvas drawing of filled and stroked rectangles, rounded rectangles and paths, with anti-aliasing and alpha
  blending
- Font loading, text measurement and text drawing, including a bundled fallback font
- SVG rasterization to a bitmap at a requested size (CanvasNet.Svg)
- PNG encoding of the rendered bitmap, including transparent backgrounds

### Integration Pattern

CanvasNet is consumed as a NuGet package reference of `DemaConsulting.Plexus.Rendering` only; the core package and
the Avalonia package do not reference it. The `ScenePainter` unit translates scene primitives into canvas drawing
calls, `CanvasTextMeasurer` wraps the CanvasNet font APIs behind the core text-measurement interface, `IconCache`
rasterizes and caches SVG icons, and `PlexusRenderer` encodes the final surface as PNG. CanvasNet objects are created
per render call and are not shared across threads; there is no global initialization or disposal step beyond
disposing the per-render surfaces. Because the library is in a pre-release state, the dependency is pinned to an
exact package version in the project file.

### Known Limitations

- CanvasNet.Svg supports a subset of SVG that overlaps, but does not equal, the subset of the editor's SVG library
  (see _SvgSkia_); host icons should stay within the common subset so the two painters look alike.
- Text is drawn with an installed font of the theme's family when one exists and a bundled fallback otherwise, so
  rendered images can differ slightly between machines.
