## PlexusIcons Unit Design

![PlexusAvalonia Structure](PlexusAvaloniaView.svg)

Scope: Converts host SVG icons to cached Avalonia images, tolerating malformed SVG.

### Purpose

`PlexusIcons` converts the SVG icons that hosts attach to node types into images that Avalonia can draw, and keeps the
result so that each icon is parsed only once. It is public so that hosts can use the same cache for their own palettes,
and the painter draws icons from the same cache.

### Data Model

A static, thread-safe cache maps the icon's key (the identity of its SVG text) to the converted image or to null when
the conversion failed. Failures are cached too, so a malformed icon is not parsed again on every frame. The cache grows
with the number of distinct icons and is never emptied.

### Key Methods

- **`GetImage(icon)`** — returns the icon as an Avalonia image, or null when the SVG could not be converted.
  Precondition: the icon is not null. Postcondition: repeated requests for the same icon, or for another icon with the
  same key, return the same instance (or the same null).
- **Internal image accessor** — returns the concrete SVG image type for the painter, with the same caching.
- **Conversion** — loads the SVG text with the SVG library and wraps it in an image; the library reports an
  unusable document as null or by throwing.

### Error Handling

A null icon throws `ArgumentNullException`. Any exception thrown while converting the SVG (except out-of-memory) is
swallowed deliberately and cached as "no image": a broken host icon must not take the user interface down, and callers
treat null as "no icon".

### Dependencies

- Core icon source type (key and SVG text).
- **Svg.Controls.Skia.Avalonia** (OTS) — SVG loading and the SVG image type; see _SvgSkia_ in the OTS design documents.
- **Avalonia** (OTS) — the image abstraction.

### Callers

`ScenePainter` (every frame, for icon primitives) and host applications.
