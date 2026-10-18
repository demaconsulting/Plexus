using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using DemaConsulting.Plexus.Rendering;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     The pixel proof of node styling: a styled node paints the same in the editor as in the headless PNG renderer.
/// </summary>
/// <remarks>
///     Each case renders a styled node (fill, border, halo and tinted icon) in a headless editor window and with
///     <see cref="PlexusRenderer"/> at the same zoom, and compares three regions of the node: the flat fill, the icon
///     and the halo. Text and the one-pixel anti-aliasing differences along edges are not compared, because the two
///     painters use different fonts and strokes. The icon allows 5% of its pixels to differ: the PNG painter
///     rasterizes the SVG at whole pixels while the editor draws the vector at its exact position, so the
///     anti-aliased edges of the artwork differ slightly. The frames are saved to <c>plexus-node-style</c> in the
///     temporary folder for visual inspection.
/// </remarks>
public class NodeStylePixelTests
{
    /// <summary>
    ///     The largest per-channel difference two pixels may have and still count as the same.
    /// </summary>
    private const int Tolerance = 12;

    /// <summary>
    ///     An icon with a thick red ring on a transparent background and a blue bar through it.
    /// </summary>
    private const string RingSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 40 40'>" +
        "<circle cx='20' cy='20' r='15' fill='none' stroke='#ff0000' stroke-width='6'/>" +
        "<rect x='4' y='17' width='32' height='6' fill='#0000ff'/></svg>";

    /// <summary>
    ///     Validates that a styled node paints the same fill, tinted icon and halo in the editor as in the PNG.
    /// </summary>
    /// <param name="zoom">The zoom of the editor and the scale of the PNG.</param>
    /// <param name="faded">Whether to test a half-opaque node with its icon as authored, instead of the full style with halo and tinted icon.</param>
    [Theory]
    [InlineData(1.0, false)]
    [InlineData(2.0, false)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    public Task StyledNode_PaintsTheSameInTheEditorAndThePng(double zoom, bool faded) => UiTest.Run(() =>
    {
        // Arrange: a type with an icon and no title or ports (so the compared regions hold no text), styled with every painted member
        using var host = new PaletteHost(null, zoom);
        host.Types.Add(new NodeType("test.Parity") { Title = string.Empty, Icon = IconSource.FromSvg(RingSvg) });
        var node = host.Page.AddNode("test.Parity", 100, 100, "N");
        var style = faded
            ? new NodeStyle { Fill = PlexusColor.Parse("#204060"), Opacity = 0.5 }
            : new NodeStyle
            {
                Fill = PlexusColor.Parse("#204060"),
                Border = PlexusColor.Parse("#00FF00"),
                BorderThickness = 2,
                Glow = PlexusColor.Parse("#FFAA00"),
                IconTint = PlexusColor.Parse("#FFFF00"),
            };
        host.Diagram.NodeStyler = _ => style;
        host.Editor.ClearSelection();
        host.Flush();
        var scene = host.Editor.Scene!;
        var iconRect = scene.Primitives.OfType<IconPrimitive>().Single().Rect;
        var box = node.Bounds;

        // Act: capture the editor, and render the same area with the PNG painter
        var frame = host.Window.CaptureRenderedFrame()!;
        Directory.CreateDirectory(PicturePath);
        frame.Save(Path.Join(PicturePath, $"editor-z{zoom:0.##}{(faded ? "-faded" : string.Empty)}.png"), PngBitmapEncoderOptions.Default);
        var picture = PictureReader.Read(frame);
        var area = box.Inflate(24);
        using var png = PlexusRenderer.Render(host.Page, new RenderOptions { Bounds = area, Scale = zoom, ShowGrid = false });
        File.WriteAllBytes(Path.Join(PicturePath, $"png-z{zoom:0.##}{(faded ? "-faded" : string.Empty)}.png"), PlexusRenderer.RenderPng(host.Page, new RenderOptions { Bounds = area, Scale = zoom, ShowGrid = false }));
        var origin = host.ToWindow(host.Editor.DiagramToScreen(new Point(area.Left, area.Top)));

        // Assert
        var (fillTotal, fillBad) = Compare(picture, png, origin, area, zoom, p => Inside(box.Inflate(-4), p) && !Inside(iconRect.Inflate(3), p));
        var (iconTotal, iconBad) = Compare(picture, png, origin, area, zoom, p => Inside(iconRect, p));
        var (haloTotal, haloBad) = Compare(picture, png, origin, area, zoom, p => p.Y >= box.Top && Inside(box.Inflate(11), p) && !Inside(box.Inflate(3), p));
        Assert.Multiple(
            () => Assert.True(fillTotal > 500, $"fill samples {fillTotal}"),
            () => Assert.True(iconTotal > 400, $"icon samples {iconTotal}"),
            () => Assert.True(haloTotal > 500, $"halo samples {haloTotal}"),
            () => Assert.True(fillBad == 0, $"{fillBad} of {fillTotal} fill pixels differ"),
            () => Assert.True(iconBad <= iconTotal * 0.05, $"{iconBad} of {iconTotal} icon pixels differ"),
            () => Assert.True(haloBad <= haloTotal * 0.02, $"{haloBad} of {haloTotal} halo pixels differ"));
    });

    /// <summary>
    ///     Validates that the editor rebuilds its picture when the styler is assigned or the appearance is refreshed, and that palette prototypes keep the default look.
    /// </summary>
    [Fact]
    public Task Editor_NodeStyler_AssignAndRefresh_RepaintsAndPaletteStaysDefault() => UiTest.Run(() =>
    {
        // Arrange: a palette that follows the editor, and a node on the canvas
        using var host = new PaletteHost(null, 1, followEditor: true);
        var node = host.Page.AddNode("test.Sink", 100, 100, "S");
        host.Flush();
        var hostFill = PlexusColor.Parse("#336699");

        // Act / Assert: the node on the canvas follows the styler and refreshes; the prototype of the same type never does
        Assert.Equal(DiagramTheme.Classic.NodeFill, BoxFill(host.Editor.Scene!, node));
        host.Diagram.NodeStyler = _ => new NodeStyle { Fill = hostFill };
        host.Flush();
        Assert.Equal(hostFill, BoxFill(host.Editor.Scene!, node));
        var prototypeBox = host.ItemFor("test.Sink").Prototype.Scene.Primitives.OfType<RectPrimitive>().Single(r => r.Fill is not null);
        Assert.Equal(DiagramTheme.Classic.NodeFill, prototypeBox.Fill);

        var level = PlexusColor.Parse("#AA0000");
        host.Diagram.NodeStyler = _ => new NodeStyle { Fill = level };
        host.Flush();
        level = PlexusColor.Parse("#00AA00");
        host.Diagram.RefreshAppearance();
        host.Flush();
        Assert.Equal(PlexusColor.Parse("#00AA00"), BoxFill(host.Editor.Scene!, node));
    });

    /// <summary>
    ///     Gets the folder where the tests save their pictures for inspection.
    /// </summary>
    private static string PicturePath => Path.Join(Path.GetTempPath(), "plexus-node-style");

    /// <summary>
    ///     Gets the fill of a node's box in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The box fill.</returns>
    private static PlexusColor? BoxFill(DiagramScene scene, Node node) =>
        scene.Primitives.OfType<RectPrimitive>().Single(r => ReferenceEquals(r.Element, node) && r.Fill is not null).Fill;

    /// <summary>
    ///     Determines whether a diagram point is inside a rectangle.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <returns>True when the point is inside.</returns>
    private static bool Inside(PlexusRect rect, Geometry.Point point) =>
        point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;

    /// <summary>
    ///     Compares the pixels of the PNG rendering with the editor capture over the diagram points a predicate selects.
    /// </summary>
    /// <param name="picture">The editor capture.</param>
    /// <param name="png">The PNG rendering of <paramref name="area"/>.</param>
    /// <param name="origin">The window point of the area's top-left corner.</param>
    /// <param name="area">The rendered diagram area.</param>
    /// <param name="zoom">The pixels per diagram unit.</param>
    /// <param name="select">Chooses the diagram points to compare.</param>
    /// <returns>How many pixels were compared and how many differ by more than the tolerance.</returns>
    private static (int Total, int Differing) Compare(
        Picture picture,
        DemaConsulting.CanvasNet.Canvas.Surface png,
        Point origin,
        PlexusRect area,
        double zoom,
        Func<Geometry.Point, bool> select)
    {
        var (total, differing) = (0, 0);
        var left = (int)Math.Round(origin.X);
        var top = (int)Math.Round(origin.Y);
        for (var y = 0; y < png.Height; y++)
        {
            for (var x = 0; x < png.Width; x++)
            {
                // The pixel's center in diagram units decides whether it belongs to the region
                var point = new Geometry.Point(area.Left + ((x + 0.5) / zoom), area.Top + ((y + 0.5) / zoom));
                if (!select(point))
                {
                    continue;
                }

                var expected = png[x, y];
                var actual = picture.At(left + x, top + y);
                var difference = Math.Max(Math.Abs(expected.R - actual.R), Math.Max(Math.Abs(expected.G - actual.G), Math.Abs(expected.B - actual.B)));
                total++;
                differing += difference > Tolerance ? 1 : 0;
            }
        }

        return (total, differing);
    }
}
