## SvgSkia

### Purpose

Svg.Controls.Skia.Avalonia (the Avalonia successor of Avalonia.Svg.Skia, which provides Svg.Skia) converts SVG text
into images that Avalonia can draw. It is used by the PlexusAvalonia system so that node icons supplied by hosts as
SVG are drawn as native vector graphics rather than pre-rasterized bitmaps.

### Features Used

- Loading an SVG document from text into an image object that Avalonia can draw
- Failure reporting for malformed SVG, which the `PlexusIcons` unit converts into a missing icon

### Integration Pattern

The library is consumed as a NuGet package reference of `DemaConsulting.Plexus.Avalonia`. The `PlexusIcons` unit
loads each icon's SVG text once, caches the resulting image by icon key, and returns it to the painter; a failure to
load is cached as a missing image so that a broken icon never throws into the painting code. The library has no
global configuration or disposal requirements beyond the Avalonia application lifetime. Icons should stay within the
SVG subset that both this library and CanvasNet support so that the two painters look alike.
