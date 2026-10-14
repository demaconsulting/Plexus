namespace DemaConsulting.Plexus.Rendering;

/// <summary>
///     Headless rendering of a Plexus page to PNG with CanvasNet, with no UI framework and no display needed.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="PlexusRenderer"/> is a stateless static class. It builds the same painter-agnostic scene the
///         interactive editor uses (<c>SceneBuilder</c>), measures text with
///         <see cref="CanvasTextMeasurer"/> so the layout matches the pixels, and paints it with CanvasNet. The image
///         covers the page content plus padding unless <see cref="RenderOptions.Bounds"/> asks for a fixed area, and may be at most
///         8192 pixels on either side (reduce <see cref="RenderOptions.Scale"/> or the bounds, otherwise an <see cref="ArgumentException"/> says so).
///         <see cref="RenderOptions"/> chooses <see cref="RenderOptions.Scale"/>, <see cref="RenderOptions.Padding"/>,
///         <see cref="RenderOptions.Background"/>, <see cref="RenderOptions.ShowGrid"/>, a <see cref="RenderOptions.Theme"/>
///         override and a <see cref="RenderOptions.Selection"/> to highlight; every value has a default.
///     </para>
///     <para>
///         The package depends on CanvasNet (the <c>DemaConsulting.CanvasNet</c> and <c>DemaConsulting.CanvasNet.Svg</c> NuGet packages), which NuGet restores
///         automatically; nothing else is installed. Fonts: the theme's font family (default Segoe UI) is used when it is installed, otherwise the
///         bundled Liberation Sans, so rendering works on a bare CI machine or container, only with slightly different text metrics.
///         Use <see cref="PlexusRenderer.Render"/> when you want the pixels as a CanvasNet surface to post-process or
///         encode in another format. Appearance comes from the diagram (its theme, stylers and node visuals), so
///         there is nothing to configure here for a custom look; the default look is described on <c>DiagramTheme</c>.
///     </para>
/// </remarks>
/// <example>
///     Render a page to a PNG file (<c>types</c> is the registry of the <c>DemaConsulting.Plexus</c> namespace example).
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Rendering;
///
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     page.AddNode("demo.Source", 0, 0, name: "A");
///     page.AddNode("demo.Sink", 180, 0, name: "B");
///     page.Connect("A.out", "B.in");
///
///     Directory.CreateDirectory("out");                                                      // SavePng does not create folders
///     PlexusRenderer.SavePng(page, "out/diagram.png");                                       // whole page at 1:1 with a grid
///     PlexusRenderer.SavePng(page, "out/diagram@2x.png", new RenderOptions { Scale = 2, ShowGrid = false });
///     byte[] png = PlexusRenderer.RenderPng(page);                                           // or keep the bytes in memory
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
