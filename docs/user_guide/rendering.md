# Rendering to PNG

`DemaConsulting.Plexus.Rendering` paints one page to a PNG image without any UI framework and without a display. It
builds the same scene the interactive editor draws and measures text with real fonts, so the picture matches what the
editor shows: the same theme, wire and node stylers, custom node visuals, icons, junction dots and crossing hops.

## Rendering a Page

```csharp
using DemaConsulting.Plexus.Rendering;

PlexusRenderer.SavePng(page, "diagram.png");           // the whole page at 1:1, with a grid
byte[] png = PlexusRenderer.RenderPng(page);           // or keep the bytes (a web response, a test)
```

Without options the image fits the page content plus a margin. `SavePng` replaces an existing file and does not
create folders. Exporting every page of a diagram is a loop:

```csharp
Directory.CreateDirectory("out");
foreach (var p in diagram.Pages)
{
    PlexusRenderer.SavePng(p, Path.Combine("out", $"{p.Name}.png"), new RenderOptions { Scale = 2, ShowGrid = false });
}
```

## Options

Every option has a default; set only what you need.

```csharp
using DemaConsulting.Plexus.Scene;

byte[] picture = PlexusRenderer.RenderPng(page, new RenderOptions
{
    Scale = 2,                                // pixels per diagram unit (a 2x image for high-DPI screens)
    Padding = 20,                             // margin around the content, in diagram units
    Background = Color.White,                 // default: the theme's canvas color; Color.Transparent for none
    ShowGrid = false,
    Selection = [a, sum],                     // draw these elements with the selection outline
    Theme = DiagramTheme.Classic with { WireColor = Color.Black },    // override the diagram's theme for this image
});
```

`RenderOptions.Bounds` renders a fixed area of the page (in diagram units) instead of fitting the content, which is how
to produce a series of identical frames. An image may be at most 8192 pixels on either side: lower `Scale` or the
bounds, otherwise an `ArgumentException` says so. `PlexusRenderer.Render()` returns the CanvasNet surface for callers
that want to post-process the pixels or encode another format.

## Fonts and Determinism

Rendering is deterministic. Text is measured and drawn with the theme's font family (`DiagramTheme.FontFamily`, default
Segoe UI) when it is installed and with a bundled font otherwise, so rendering also works on a bare CI machine or in a
container, only with slightly different text metrics. Set the font family in the theme when pictures must look the same
everywhere. The package depends on CanvasNet, which NuGet restores; nothing else needs to be installed.

## Rendering from the Command Line

The demo contains a small command-line renderer, `plexus-demo-render`, that loads a diagram file (or builds a built-in
sample) and writes one PNG per page. It is a complete example of a host-side export tool:

```bash
dotnet run --project demo/DemaConsulting.Plexus.Demo.Cli -- --sample wiring --out out --scale 2
dotnet run --project demo/DemaConsulting.Plexus.Demo.Cli -- diagram.json --out out --page Main --no-grid
```

![Command-line rendering of the wiring sample](images/render-cli.png)

Because the renderer is headless and deterministic, the same call is the basis of visual regression tests of a node
type catalog: render a sample page and compare it with a stored image (see *Testing and Automating*).
