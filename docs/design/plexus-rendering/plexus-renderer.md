## PlexusRenderer Unit Design

![PlexusRendering Structure](PlexusRenderingView.svg)

Scope: Public entry point that renders a page to a PNG image or file according to render options.

### Purpose

`PlexusRenderer` is the single public entry point for rendering. It turns a page into a PNG (bytes or file) or into a
CanvasNet pixel surface, driven by an optional `RenderOptions` object. It owns the decisions about what area of the
page is drawn, at which pixel size, with which theme and background, and orchestrates the other units of the system.

### Data Model

`PlexusRenderer` is a static class without state. `RenderOptions` is an immutable (init-only) options class:

| Property     | Type                          | Default             | Invariant                       |
|--------------|-------------------------------|---------------------|---------------------------------|
| `Scale`      | `double`                      | 1                   | Finite and greater than zero    |
| `Padding`    | `double`                      | 15                  | Finite, not negative            |
| `Background` | `Color?`                      | null (theme canvas) | None                            |
| `ShowGrid`   | `bool`                        | true                | None                            |
| `Bounds`     | `Rect?`                       | null (fit content)  | Non-empty and finite if given   |
| `Theme`      | `DiagramTheme?`               | null (diagram's)    | None; changes the look only     |
| `Selection`  | `IEnumerable<DiagramElement>` | empty               | Copied at creation; not null    |

`Padding` is ignored when `Bounds` is given. `Theme` changes the look only: the geometry of ports, port triangles and
wire ends always follows the diagram's own theme, so wires stay attached to their ports.

The `Selection` accessor copies the sequence it is given into a private read-only array when the options are created
(a null value throws `ArgumentNullException`), so later changes to the caller's collection do not affect the options.
Because every member is a value or a copy, the options are immutable, and one instance can be shared between renders
and threads. The elements in the selection are the live model elements; an element removed from the page afterwards
is simply no longer drawn highlighted.

### Key Methods

- **`Render(page, options)`** — returns a new `Surface` owned by the caller.
  - Preconditions: `page` is not null; options are valid (see _Error Handling_).
  - Algorithm: validate the options; choose the theme; build the scene through the core scene builder with a
    `CanvasTextMeasurer`, the selection and the options' bounds, with the grid disabled (the grid must cover the final
    image area, which is only known afterwards); compute the area as the requested bounds, or the scene bounds
    inflated by the padding; floor the area's origin to a whole unit; compute the pixel size as the area size times
    the scale, rounded up, in double precision before converting to `int` so absurd sizes cannot overflow; create the
    surface (at least 1 x 1); clear it with the background; paint the grid primitive when enabled and the theme's grid
    size is positive; paint the scene primitives with a `ScenePainter`.
  - Post-conditions: the surface is fully opaque unless a translucent background was requested; if painting fails the
    surface is disposed before the exception propagates. Because the scene is built from the model as it is, each node
    is drawn with its current ports, including ports a port provider computed for it. Expanded groups are drawn as a
    frame with a title bar; collapsed groups as one box, and their members are not drawn.
- **`RenderPng(page, options)`** — renders a surface and encodes it as PNG, as RGB when the background is opaque and
  as RGBA otherwise. Returns the file contents.
- **`SavePng(page, path, options)`** — validates the path, renders the PNG completely (`RenderPng`), and only then
  opens the file and writes the bytes, replacing an existing file. An invalid argument or a rendering failure therefore
  never creates, truncates or replaces a file.

### Error Handling

The library translates no exception (see the exception policy of the system design).

- A null page throws `ArgumentNullException`.
- A scale that is not finite or not greater than zero, padding that is negative or not finite, bounds that are empty or
  not finite, an image whose pixel size exceeds the CanvasNet maximum surface dimension, and an empty or whitespace
  path throw `ArgumentException`. Checks happen before any surface is allocated.
- A null selection given to `RenderOptions` throws `ArgumentNullException` when the options are created.
- File-system failures of `SavePng` (a missing folder, a path that is too long, access denied, an unsupported path)
  propagate unchanged as `IOException` (including `DirectoryNotFoundException` and `PathTooLongException`),
  `UnauthorizedAccessException`, `NotSupportedException` or `SecurityException`; the PNG was rendered before the file
  was opened.
- Failures raised by CanvasNet while painting or encoding propagate to the caller unchanged, after the partly painted
  surface has been disposed.
- Icon failures never reach this unit: `IconCache` and `ScenePainter` contain them.

### Dependencies

- Core scene builder, scene options, `DiagramTheme`, `Rect`, `Point`, `Color` and `DiagramPage` from the Plexus core system.
- `CanvasTextMeasurer` and `ScenePainter` units of this system.
- CanvasNet (OTS) for the surface and the PNG codec; see _CanvasNet_ in the OTS design documents.

### Callers

Host applications and tools (including the demo and agent tooling, which are not software items) call
`PlexusRenderer` directly. No other unit of this system calls it.
