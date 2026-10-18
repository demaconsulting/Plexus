using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Rendering.Tests;

/// <summary>
///     System-level tests that exercise the PlexusRendering package end to end: a real diagram built with
///     the core library is rendered through the public API and the resulting image is inspected.
/// </summary>
public class PlexusRenderingTests
{
    /// <summary>
    ///     The theme background as a pixel.
    /// </summary>
    private static readonly Rgba32 ThemeBackground = RenderTestModel.Pixel(DiagramTheme.Classic.CanvasBackground);

    /// <summary>
    ///     The standard node fill as a pixel.
    /// </summary>
    private static readonly Rgba32 NodeFill = RenderTestModel.Pixel(DiagramTheme.Classic.NodeFill);

    /// <summary>
    ///     The PNG file signature.
    /// </summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Builds a page with a connected source and sink node, which is the smallest realistic diagram.
    /// </summary>
    /// <returns>The page.</returns>
    private static DiagramPage CreateConnectedPage()
    {
        var (_, page) = RenderTestModel.CreatePage();
        var source = page.AddNode("t.Source", 30, 30, "A");
        var sink = page.AddNode("t.Sink", 300, 30, "B");
        page.Connect(source.Port("value"), sink.Port("in"));
        return page;
    }

    /// <summary>
    ///     Proves that a page of a diagram renders to PNG bytes that decode to an image of the page content.
    /// </summary>
    [Fact]
    public void PlexusRendering_RenderPng_DiagramPage_ProducesDecodablePng()
    {
        // Arrange: a diagram with two connected nodes
        var page = CreateConnectedPage();

        // Act
        var png = PlexusRenderer.RenderPng(page);
        using var image = RenderTestModel.Decode(png);

        // Assert: a PNG signature and an image that covers both nodes plus padding
        Assert.Equal(PngSignature, png.Take(8).ToArray());
        Assert.True(image.Width > 390);
        Assert.True(image.Height > 120);
    }

    /// <summary>
    ///     Proves that a page can be written to a PNG file that decodes to the same image as the PNG bytes.
    /// </summary>
    [Fact]
    public void PlexusRendering_SavePng_DiagramPage_WritesDecodableFile()
    {
        // Arrange
        var page = CreateConnectedPage();
        var path = Path.Join(Path.GetTempPath(), $"plexus-system-{Guid.NewGuid():N}.png");

        try
        {
            // Act
            PlexusRenderer.SavePng(page, path);

            // Assert: the file holds exactly the bytes RenderPng produces and decodes
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(PlexusRenderer.RenderPng(page), bytes);
            using var image = RenderTestModel.Decode(bytes);
            Assert.True(image.Width > 390);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves that nodes (with titles, names and icons), wires, annotations and groups all appear in the image.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_FullPage_ShowsEveryElementKind()
    {
        // Arrange: an icon node, a titled node, a wired source and sink, an annotation and a grouped node
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Icon", 30, 30, "Icon");
        page.AddNode("t.Titled", 300, 30, "Name");
        var source = page.AddNode("t.Source", 30, 200, "A");
        var sink = page.AddNode("t.Sink", 330, 200, "B");
        page.Connect(source.Port("value"), sink.Port("in"));
        page.AddAnnotation("note", new Rect(450, 300, 100, 50));
        var grouped = page.AddNode("t.Plain", 60, 320, "G");
        page.CreateGroup([grouped], "Group");

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 600, 450), ShowGrid = false });

        // Assert: the icon, the title text, the name text, the wire, the annotation and the group are all visible
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[75, 90]);
        Assert.True(RenderTestModel.CountDifferent(image, 305, 33, 80, 24, NodeFill) > 20);
        Assert.True(RenderTestModel.CountDifferent(image, 300, 5, 90, 22, ThemeBackground) > 20);
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.WireColor), image[200, 244]);
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.AnnotationFill), image[540, 345]);
        Assert.NotEqual(ThemeBackground, image[45, 360]);
    }

    /// <summary>
    ///     Proves that the image shows each node with its current ports, including ports a port provider computes from the node's properties.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_DynamicPortNode_ShowsCurrentPorts()
    {
        // Arrange: a bus node whose inputs follow its Channels property, a static sink beside it, and where the third bus input is drawn at three channels
        var (diagram, page) = RenderTestModel.CreatePage();
        var bus = page.AddNode("t.Bus", 60, 30, "B");
        var sink = page.AddNode("t.Sink", 300, 30, "S");
        var portColor = RenderTestModel.Pixel(Color.Parse("#FF8C00"));
        var edge = NodeGeometry.PortEdge(new Rect(60, 30, 90, NodeGeometry.RequiredHeight(3, diagram.Theme)), PortDirection.Input, 2, 3, diagram.Theme);
        var (x, y) = ((int)(edge.X - 3), (int)edge.Y);
        var options = new RenderOptions { Bounds = new Rect(0, 0, 450, 250), ShowGrid = false };
        using var oneChannel = PlexusRenderer.Render(page, options);

        // Act: grow to three channels, render, shrink to two channels, render
        bus.Properties["Channels"] = 3;
        using var threeChannels = PlexusRenderer.Render(page, options);
        bus.Properties["Channels"] = 2;
        using var twoChannels = PlexusRenderer.Render(page, options);

        // Assert: the third port triangle exists exactly while the node has three ports; the static node's declared port is always drawn
        var sinkEdge = NodeGeometry.PortEdge(sink.Bounds, PortDirection.Input, 0, 1, diagram.Theme);
        var sinkPixel = ((int)(sinkEdge.X - 3), (int)sinkEdge.Y);
        Assert.Multiple(
            () => Assert.NotEqual(portColor, oneChannel[x, y]),
            () => Assert.Equal(portColor, threeChannels[x, y]),
            () => Assert.NotEqual(portColor, twoChannels[x, y]),
            () => Assert.Equal(portColor, oneChannel[sinkPixel.Item1, sinkPixel.Item2]),
            () => Assert.Equal(portColor, twoChannels[sinkPixel.Item1, sinkPixel.Item2]));
    }

    /// <summary>
    ///     Proves that the render area is controlled by bounds, padding and scale.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_FramingOptions_SizeTheImage()
    {
        // Arrange
        var page = CreateConnectedPage();

        // Act
        using var fitted = PlexusRenderer.Render(page, new RenderOptions { Padding = 0 });
        using var padded = PlexusRenderer.Render(page, new RenderOptions { Padding = 20 });
        using var bounded = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 200, 100) });
        using var scaled = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 200, 100), Scale = 2 });

        // Assert: padding grows the fitted image on every side, bounds are used as is and scale multiplies the pixels
        Assert.InRange(padded.Width - fitted.Width, 39, 41);
        Assert.InRange(padded.Height - fitted.Height, 39, 41);
        Assert.Equal((200, 100), (bounded.Width, bounded.Height));
        Assert.Equal((400, 200), (scaled.Width, scaled.Height));
    }

    /// <summary>
    ///     Proves that the background color and the grid option change the image.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_BackgroundAndGridOptions_ChangeThePixels()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        var area = new Rect(0, 0, 60, 60);

        // Act
        using var plain = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false });
        using var custom = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false, Background = Color.Parse("#336699") });
        using var gridded = PlexusRenderer.Render(page, new RenderOptions { Bounds = area });

        // Assert: theme background by default, the requested color when given, and grid dots only when enabled
        Assert.Equal(ThemeBackground, plain[15, 15]);
        Assert.Equal(new Rgba32(0x33, 0x66, 0x99, 255), custom[15, 15]);
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.GridDotColor), gridded[15, 15]);
    }

    /// <summary>
    ///     Proves that a theme option restyles the image and that selected elements are highlighted.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_ThemeAndSelectionOptions_RestyleTheImage()
    {
        // Arrange
        var (_, page) = RenderTestModel.CreatePage();
        var node = page.AddNode("t.Plain", 30, 30, "N");
        var area = new Rect(0, 0, 200, 200);
        var theme = new DiagramTheme { CanvasBackground = Color.Parse("#102030"), NodeFill = Color.Parse("#405060") };

        // Act
        using var themed = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false, Theme = theme });
        using var selected = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false, Selection = [node] });
        using var unselected = PlexusRenderer.Render(page, new RenderOptions { Bounds = area, ShowGrid = false });

        // Assert: the theme colors replace the diagram's, and only the selected render has the outline
        Assert.Equal(new Rgba32(0x10, 0x20, 0x30, 255), themed[5, 5]);
        Assert.Equal(new Rgba32(0x40, 0x50, 0x60, 255), themed[40, 100]);
        Assert.Equal(RenderTestModel.Pixel(DiagramTheme.Classic.SelectionColor), selected[28, 70]);
        Assert.Equal(ThemeBackground, unselected[28, 70]);
    }

    /// <summary>
    ///     Proves that the painted title text occupies the width the text measurer reported during layout.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_NodeTitle_MatchesMeasuredTextWidth()
    {
        // Arrange: the widest sensible check is a title of wide glyphs, measured with the renderer's own measurer
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Titled", 30, 30, "N");
        var measured = new CanvasTextMeasurer().Measure("MMMM", DiagramTheme.Classic.HeaderTextStyle).Width;

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 200, 200), ShowGrid = false });

        // Assert: find the horizontal extent of the white title ink inside the header band
        var (first, last) = (int.MaxValue, int.MinValue);
        for (var x = 31; x < 119; x++)
        {
            if (RenderTestModel.CountDifferent(image, x, 33, 1, 24, NodeFill) > 0)
            {
                first = Math.Min(first, x);
                last = Math.Max(last, x);
            }
        }

        var inkWidth = last - first + 1;
        Assert.InRange(inkWidth, measured - 6, measured + 1);
        Assert.InRange(first + (inkWidth / 2.0), 74, 76);
    }

    /// <summary>
    ///     Proves that a node whose icon cannot be rasterized does not stop the rest of the page from rendering.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_MalformedIcon_StillRendersTheRestOfThePage()
    {
        // Arrange: a node with a malformed SVG icon beside a normal node
        var types = RenderTestModel.CreateTypes();
        types.Add(new NodeType("t.Broken") { Title = string.Empty, Icon = IconSource.FromSvg($"this is not svg {Guid.NewGuid()}") });
        var page = new Diagram(types).Pages.Add("Main");
        page.AddNode("t.Broken", 30, 30, "Broken");
        page.AddNode("t.Plain", 150, 30, "Plain");

        // Act
        using var image = PlexusRenderer.Render(page, new RenderOptions { Bounds = new Rect(0, 0, 300, 200), ShowGrid = false });

        // Assert: both node bodies are painted and the missing icon leaves the body fill untouched
        Assert.Equal(NodeFill, image[75, 90]);
        Assert.Equal(NodeFill, image[190, 100]);
    }

    /// <summary>
    ///     Proves that unusable inputs are rejected with argument exceptions instead of producing an image.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_InvalidInput_IsRejected()
    {
        // Arrange
        var page = CreateConnectedPage();

        // Act / Assert: null page, unusable scale, padding and bounds, an oversized image and an empty path
        Assert.Throws<ArgumentNullException>(() => PlexusRenderer.RenderPng(null!));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.RenderPng(page, new RenderOptions { Scale = -1 }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.RenderPng(page, new RenderOptions { Padding = double.PositiveInfinity }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.RenderPng(page, new RenderOptions { Bounds = new Rect(0, 0, 10, 0) }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.RenderPng(page, new RenderOptions { Bounds = new Rect(0, 0, 1_000_000, 10) }));
        Assert.Throws<ArgumentException>(() => PlexusRenderer.SavePng(page, string.Empty));
    }

    /// <summary>
    ///     Proves that a rendered pixel surface belongs to the caller and is independent of later renders.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_PixelSurface_IsOwnedByTheCaller()
    {
        // Arrange
        var page = CreateConnectedPage();
        var options = new RenderOptions { Bounds = new Rect(0, 0, 100, 100), ShowGrid = false };

        // Act: render, then scribble over the first surface and dispose it before rendering again
        var first = PlexusRenderer.Render(page, options);
        first.Clear(new Rgba32(1, 2, 3, 255));
        first.Dispose();
        using var second = PlexusRenderer.Render(page, options);

        // Assert: the second render is unaffected by what the caller did to the first
        Assert.Equal((100, 100), (second.Width, second.Height));
        Assert.Equal(ThemeBackground, second[2, 2]);
    }

    /// <summary>
    ///     Proves that concurrent renders of the same unmodified page all produce identical images.
    /// </summary>
    [Fact]
    public void PlexusRendering_Render_ConcurrentCalls_ProduceIdenticalImages()
    {
        // Arrange: a page with text and an icon, so fonts and the icon cache are shared between the calls
        var (_, page) = RenderTestModel.CreatePage();
        page.AddNode("t.Icon", 30, 30, "Icon");
        page.AddNode("t.Titled", 200, 30, "Titled");
        var expected = PlexusRenderer.RenderPng(page);
        var results = new byte[16][];

        // Act
        Parallel.For(0, results.Length, index => results[index] = PlexusRenderer.RenderPng(page));

        // Assert
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    /// <summary>
    ///     Proves that the rendering assembly is headless: it references no UI framework assembly.
    /// </summary>
    [Fact]
    public void PlexusRendering_Dependencies_AssemblyReferences_ContainNoUiFramework()
    {
        // Arrange
        var references = typeof(PlexusRenderer).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToList();

        // Act: names of well-known UI frameworks
        var uiFrameworks = references.Where(name =>
            name.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("System.Windows", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.Maui", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SkiaSharp", StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert: the renderer depends on the core library and CanvasNet, never on a UI framework
        Assert.Empty(uiFrameworks);
        Assert.Contains("DemaConsulting.Plexus", references);
    }
}
