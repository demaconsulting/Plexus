using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;
using PlexusSize = DemaConsulting.Plexus.Geometry.Size;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the painter that turns scene primitives into Avalonia drawing calls, and of the value conversions it uses.
/// </summary>
public class ScenePainterTests
{
    /// <summary>
    ///     A simple dark-disc icon in SVG form, standing in for a host-supplied node icon.
    /// </summary>
    private const string DiscSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='30' viewBox='0 0 30 30'>" +
        "<circle cx='15' cy='15' r='13' fill='#2b2b2b'/></svg>";

    /// <summary>
    ///     The width of the bitmaps the tests paint into.
    /// </summary>
    private const int BitmapWidth = 400;

    /// <summary>
    ///     The height of the bitmaps the tests paint into.
    /// </summary>
    private const int BitmapHeight = 300;

    /// <summary>
    ///     Validates that asking for the same color twice returns the same cached brush.
    /// </summary>
    [Fact]
    public Task ScenePainter_GetBrush_SameColor_ReturnsCachedBrush() => UiTest.Run(() =>
    {
        // Arrange
        var painter = new ScenePainter();
        var red = PlexusColor.Parse("#FF0000");

        // Act
        var first = painter.GetBrush(red);
        var second = painter.GetBrush(red);
        var other = painter.GetBrush(PlexusColor.Parse("#00FF00"));

        // Assert
        Assert.Multiple(
            () => Assert.Same(first, second),
            () => Assert.NotSame(first, other),
            () => Assert.Equal(Color.FromRgb(255, 0, 0), first.Color));
    });

    /// <summary>
    ///     Validates that pens are cached per color and thickness and have flat caps (matching the headless
    ///     painter, so dashes keep their lengths) and round joins.
    /// </summary>
    [Fact]
    public Task ScenePainter_GetPen_SameColorAndThickness_ReturnsCachedPen() => UiTest.Run(() =>
    {
        // Arrange
        var painter = new ScenePainter();
        var blue = PlexusColor.Parse("#0000FF");

        // Act
        var first = painter.GetPen(blue, 2);
        var second = painter.GetPen(blue, 2);
        var thicker = painter.GetPen(blue, 4);

        // Assert
        Assert.Multiple(
            () => Assert.Same(first, second),
            () => Assert.NotSame(first, thicker),
            () => Assert.Equal(4, thicker.Thickness),
            () => Assert.Equal(PenLineCap.Flat, first.LineCap),
            () => Assert.Equal(PenLineJoin.Round, first.LineJoin));
    });

    /// <summary>
    ///     Validates that nodes, wires and text of a scene reach the drawing surface in their theme colors.
    /// </summary>
    [Fact]
    public Task ScenePainter_Paint_NodesWiresAndText_DrawsThemeColors() => UiTest.Run(host =>
    {
        // Arrange: a connected pair and the scene built for it
        var source = host.AddSource(30, 30, "Alpha");
        var sink = host.AddSink(240, 150, "Beta");
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        var scene = SceneBuilder.Build(host.Page, new SceneOptions { TextMeasurer = new AvaloniaTextMeasurer() });
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        var middle = points[points.Count / 2];

        // Act: paint it
        var pixels = Paint(scene, new PlexusRect(0, 0, BitmapWidth, BitmapHeight), 1);

        // Assert: node fill, wire stroke and name text are all present
        var fill = PlexusColor.Parse("#6B6B6B");
        var nodePixels = pixels.Count(p => p.A == 255 && Near(p, fill));
        var textPixels = pixels.Count(p => p.A > 0 && p.G > p.R + 40 && p.G > p.B + 40);
        var wirePixel = pixels.At((int)middle.X, (int)middle.Y);
        Assert.Multiple(
            () => Assert.True(nodePixels > 2000, $"node pixels: {nodePixels}"),
            () => Assert.True(textPixels > 10, $"name text pixels: {textPixels}"),
            () => Assert.True(wirePixel.A > 0, "the wire passes through its own midpoint"));
    });

    /// <summary>
    ///     Validates that the grid draws dots on multiples of its spacing and thins itself by powers of two when zoomed far out.
    /// </summary>
    [Fact]
    public Task ScenePainter_Paint_Grid_DrawsDotsOnSpacingAndThinsWhenZoomedOut() => UiTest.Run(host =>
    {
        // Arrange: an empty page with its grid on
        var area = new PlexusRect(0, 0, 200, 200);
        var scene = SceneBuilder.Build(host.Page, new SceneOptions { TextMeasurer = new AvaloniaTextMeasurer(), ShowGrid = true, Bounds = area });

        // Act: paint at actual size and at a zoom where the spacing must be thinned to every eighth line
        var actual = Paint(scene, area, 1);
        var zoomedOut = Paint(scene, area, 0.1);

        // Assert: at zoom 1 there is a dot at (15,15) and none between dots; zoomed out only multiples of 120 remain
        Assert.Multiple(
            () => Assert.True(actual.At(15, 15).A > 0, "dot at 15,15"),
            () => Assert.Equal(0, actual.At(7, 7).A),
            () => Assert.Equal(0, zoomedOut.At(30, 30).A),
            () => Assert.True(zoomedOut.At(120, 120).A > 0, "dot at 120,120"));
    });

    /// <summary>
    ///     Validates that an SVG icon is drawn inside its rectangle.
    /// </summary>
    [Fact]
    public Task ScenePainter_Paint_Icon_DrawsIconColors() => UiTest.Run(() =>
    {
        // Arrange: a node type with an icon
        var scene = BuildIconScene(IconSource.FromSvg(DiscSvg));

        // Act
        var pixels = Paint(scene, new PlexusRect(0, 0, BitmapWidth, BitmapHeight), 1);

        // Assert: the dark disc of the icon is on the surface
        var disc = pixels.Count(p => p.A == 255 && Near(p, PlexusColor.Parse("#2b2b2b")));
        Assert.True(disc > 100, $"icon pixels: {disc}");
    });

    /// <summary>
    ///     Validates that a malformed icon is simply left out and painting still completes.
    /// </summary>
    [Fact]
    public Task ScenePainter_Paint_BrokenIcon_LeavesIconOutWithoutThrowing() => UiTest.Run(() =>
    {
        // Arrange: a node type whose icon is not SVG
        var scene = BuildIconScene(IconSource.FromSvg("this is <not> svg"));

        // Act
        var pixels = Paint(scene, new PlexusRect(0, 0, BitmapWidth, BitmapHeight), 1);

        // Assert: the node itself was painted, but no icon pixels exist
        var disc = pixels.Count(p => p.A == 255 && Near(p, PlexusColor.Parse("#2b2b2b")));
        var nodePixels = pixels.Count(p => p.A == 255 && Near(p, PlexusColor.Parse("#6B6B6B")));
        Assert.Multiple(
            () => Assert.True(nodePixels > 500, $"node pixels: {nodePixels}"),
            () => Assert.Equal(0, disc));
    });

    /// <summary>
    ///     Validates that primitives outside the visible area are not painted at all.
    /// </summary>
    [Fact]
    public Task ScenePainter_Paint_VisibleAreaElsewhere_PaintsNothing() => UiTest.Run(host =>
    {
        // Arrange: a node near the origin
        host.AddSource(30, 30);
        var scene = SceneBuilder.Build(host.Page, new SceneOptions { TextMeasurer = new AvaloniaTextMeasurer() });

        // Act: say that only a far-away area is visible
        var pixels = Paint(scene, new PlexusRect(5000, 5000, 100, 100), 1);

        // Assert
        Assert.DoesNotContain(pixels, p => p.A > 0);
    });

    /// <summary>
    ///     Validates that the conversions between Plexus and Avalonia values keep every component.
    /// </summary>
    [Fact]
    public void PlexusAvaloniaExtensions_PointSizeRectAndColor_PreserveValues()
    {
        // Arrange
        var point = new PlexusPoint(1.5, -2.5);
        var size = new PlexusSize(7.5, 0);
        var rect = new PlexusRect(10, 20, 30, 40);
        var color = PlexusColor.Parse("#80FF8800");

        // Act
        var avaloniaPoint = point.ToAvalonia();
        var avaloniaSize = size.ToAvalonia();
        var avaloniaRect = rect.ToAvalonia();
        var avaloniaColor = color.ToAvalonia();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new Point(1.5, -2.5), avaloniaPoint),
            () => Assert.Equal(new PlexusPoint(1.5, -2.5), avaloniaPoint.ToPlexus()),
            () => Assert.Equal(new Size(7.5, 0), avaloniaSize),
            () => Assert.Equal(size, avaloniaSize.ToPlexus()),
            () => Assert.Equal(new Rect(10, 20, 30, 40), avaloniaRect),
            () => Assert.Equal(rect, avaloniaRect.ToPlexus()),
            () => Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x88, 0x00), avaloniaColor),
            () => Assert.Equal(color, avaloniaColor.ToPlexus()));
    }

    /// <summary>
    ///     Builds a scene with one node whose type carries an icon.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The scene.</returns>
    private static DiagramScene BuildIconScene(IconSource icon)
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("viz.Iconic") { Title = "ICONIC", Icon = icon });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Icons");
        page.AddNode("viz.Iconic", 60, 60, "Icon1");
        return SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new AvaloniaTextMeasurer() });
    }

    /// <summary>
    ///     Paints a scene with a fresh painter onto a transparent bitmap and reads the result.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="visible">The visible area in diagram units.</param>
    /// <param name="zoom">The zoom passed to the painter.</param>
    /// <returns>The pixels.</returns>
    private static PixelBuffer Paint(DiagramScene scene, PlexusRect visible, double zoom)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(BitmapWidth, BitmapHeight), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            new ScenePainter().Paint(context, scene, visible, zoom);
        }

        // Saving and reloading through PNG gives a predictable pixel layout regardless of the platform format
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var decoded = new Bitmap(stream);
        var size = decoded.PixelSize;
        var buffer = new byte[size.Width * size.Height * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            decoded.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), buffer.Length, size.Width * 4);
        }
        finally
        {
            handle.Free();
        }

        // Skia bitmaps are BGRA
        var result = new Pixel[size.Width * size.Height];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new Pixel(buffer[(i * 4) + 2], buffer[(i * 4) + 1], buffer[i * 4], buffer[(i * 4) + 3]);
        }

        return new PixelBuffer(result, size.Width);
    }

    /// <summary>
    ///     Determines whether a pixel is within a small tolerance of a color.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when each channel is within 3 of the color.</returns>
    private static bool Near(Pixel pixel, PlexusColor color) =>
        Math.Abs(pixel.R - color.R) <= 3 && Math.Abs(pixel.G - color.G) <= 3 && Math.Abs(pixel.B - color.B) <= 3;

    /// <summary>
    ///     One pixel of a painted bitmap.
    /// </summary>
    /// <param name="R">The red channel.</param>
    /// <param name="G">The green channel.</param>
    /// <param name="B">The blue channel.</param>
    /// <param name="A">The alpha channel.</param>
    private readonly record struct Pixel(byte R, byte G, byte B, byte A);

    /// <summary>
    ///     The pixels of a painted bitmap, row by row.
    /// </summary>
    /// <param name="Pixels">The pixels.</param>
    /// <param name="Width">The bitmap width.</param>
    private sealed record PixelBuffer(Pixel[] Pixels, int Width) : IEnumerable<Pixel>
    {
        /// <summary>
        ///     Gets the pixel at a position.
        /// </summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The pixel.</returns>
        public Pixel At(int x, int y) => Pixels[(y * Width) + x];

        /// <inheritdoc />
        public IEnumerator<Pixel> GetEnumerator() => ((IEnumerable<Pixel>)Pixels).GetEnumerator();

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
