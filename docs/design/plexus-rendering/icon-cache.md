## IconCache Unit Design

![PlexusRendering Structure](PlexusRenderingView.svg)

Scope: Cache of rasterized SVG icons keyed by icon and size.

### Purpose

`IconCache` rasterizes node icons from SVG once per pixel size and shares the resulting bitmap with every later paint
of the same icon at the same size. Rasterizing SVG is the most expensive step of painting a node and diagrams repeat
the same few icons many times, so caching dominates icon performance.

### Data Model

`IconCache` is an instance class (internal) with one shared default instance, `IconCache.Shared`, which `ScenePainter`
uses; tests create private instances with a small capacity. Each instance holds a thread-safe dictionary that maps
(icon key, pixel width, pixel height) to a CanvasNet surface, or to null when the icon failed to rasterize. Invariants:
cached surfaces are never modified after creation (so they can be shared between renders and threads); a failure is
cached as null so a broken icon is parsed only once; the number of entries is bounded by the instance's `Capacity`
(512 for the shared instance, `IconCache.DefaultCapacity`; at least 1 for any instance), so hosts that animate zoom
cannot accumulate one bitmap per zoom step. The `Count` and `Capacity` properties expose the number of entries and the
bound for verification.

The eviction rule is _clear when full_: a request for a key that is not cached, made while the cache holds `Capacity`
entries, first discards every cached rasterization and then adds the new one. After any single-threaded request the
cache therefore holds at most `Capacity` entries, and an icon requested after a discard is rasterized again (a new
surface). Failed rasterizations count as entries. Under concurrent requests the bound may be exceeded transiently by
at most the number of racing threads, because the check and the insertion are not one atomic step.

### Key Methods

- **`Get(icon, width, height)`** — returns the shared straight-alpha surface with a transparent background, or null
  when the SVG cannot be rasterized.
  - Preconditions: width and height are at least 1 pixel.
  - Algorithm: when the key is not cached and the cache holds `Capacity` entries, the cache is cleared; the entry is
    then looked up by key and size and, when absent, rasterized from the icon's SVG text at exactly that size.
  - Post-conditions: concurrent requests for the same key and size receive the same stored surface (a duplicate
    rasterization by a losing thread is discarded).
- **Rasterization (private)** — loads the SVG text with CanvasNet's SVG codec; invalid data or invalid arguments
  from malformed or unsupported SVG are caught and reported as null.

### Error Handling

- A capacity of less than one throws `ArgumentOutOfRangeException` when an instance is created.
- Malformed or unsupported SVG is detected through the codec's invalid-data and argument exceptions and handled locally
  by caching and returning null, because a bad icon must not prevent the rest of a diagram from rendering. The caller
  (`ScenePainter`) treats null as "draw nothing".

### Dependencies

- Core `IconSource` (icon key and SVG text).
- CanvasNet (OTS) SVG codec and surface; see _CanvasNet_ in the OTS design documents.

### Callers

`ScenePainter` is the only caller, through `IconCache.Shared`.
