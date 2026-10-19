using Avalonia;
using Avalonia.Headless;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     The pixel-identity proof of the node palette: an item looks exactly like the same node type dropped on the canvas.
/// </summary>
/// <remarks>
///     Each case renders a window with a palette that shows one type and an editor that received a node
///     of that type by drop, at the same zoom and theme. The node's visual extent is cropped from both
///     places and the pixels compared. The frames are saved to <c>plexus-palette</c> in the temporary
///     folder for visual inspection.
/// </remarks>
public class NodePalettePixelTests
{
    /// <summary>
    ///     The largest per-channel difference two pixels may have and still count as identical.
    /// </summary>
    private const int Tolerance = 2;

    /// <summary>
    ///     Gets a light theme with different metrics, colors and corner radius, so identity is shown beyond the classic theme.
    /// </summary>
    private static DiagramTheme LightTheme { get; } = new()
    {
        CanvasBackground = PlexusColor.Parse("#F1EFE8"),
        NodeFill = PlexusColor.Parse("#3A5A8C"),
        NodeBorder = PlexusColor.Parse("#1F3558"),
        NodeCornerRadius = 6,
        NodeNameText = PlexusColor.Parse("#8A1C1C"),
        SummaryText = PlexusColor.Parse("#222222"),
        PortLabelText = PlexusColor.Parse("#222222"),
        NameFontSize = 13,
    };

    /// <summary>
    ///     Validates that the palette item and a node dropped on the editor are pixel-identical over the node's visual bounds.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <param name="zoom">The zoom of palette and editor.</param>
    /// <param name="light">Whether to use the light theme rather than the classic one.</param>
    [Theory]
    [InlineData("test.Source", 1.0, false)]
    [InlineData("test.Mixer", 1.0, false)]
    [InlineData("test.Mixer", 1.5, false)]
    [InlineData("test.Badge", 1.0, true)]
    [InlineData("test.Gain", 2.0, true)]
    [InlineData("test.Sink", 0.75, false)]
    [InlineData("test.Mixer", 1.0, true)]
    public Task NodePalette_Item_IsPixelIdenticalToTheDroppedNode(string key, double zoom, bool light) =>
        UiTest.Run(() => AssertItemMatchesDroppedNode(key, zoom, light));

    /// <summary>
    ///     Renders a palette item and the same type dropped on the editor, and asserts that their pixels are identical; must run on the UI thread.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <param name="zoom">The zoom of palette and editor.</param>
    /// <param name="light">Whether to use the light theme rather than the classic one.</param>
    internal static void AssertItemMatchesDroppedNode(string key, double zoom, bool light)
    {
        // Arrange: a palette showing only this type, at the editor's zoom and theme
        var theme = light ? LightTheme : null;
        using var host = new PaletteHost(theme, zoom);
        host.Palette.Filter = key;
        host.Flush();
        var item = host.Palette.Items.Single();
        var type = host.Types.Get(key);
        var paletteBox = item.TranslatePoint(item.BoxOrigin, host.Window)!.Value;

        // Act: drop the same type on the editor; the canvas names it automatically, so give it the palette's label
        host.DragEvent(RawDragEventType.DragEnter, key, new Point(150, 150));
        host.DragEvent(RawDragEventType.DragOver, key, new Point(150, 150));
        host.DragEvent(RawDragEventType.Drop, key, new Point(150, 150));
        var node = Assert.Single(host.Page.Nodes);
        Assert.True(host.Diagram.TryRename(node, type.DisplayName.Trim(), out _));
        host.Editor.ClearSelection();

        // Put the node's box where its on-screen fractional position equals the palette box's, then render
        var origin = host.ToWindow(default);
        var wanted = new Point(origin.X + 100 + Fraction(paletteBox.X), origin.Y + 100 + Fraction(paletteBox.Y));
        host.Editor.Offset = new Point(node.Bounds.X - ((wanted.X - origin.X) / zoom), node.Bounds.Y - ((wanted.Y - origin.Y) / zoom));
        host.Flush();
        var frame = host.Window.CaptureRenderedFrame()!;
        Directory.CreateDirectory(PicturePath);
        var name = $"palette-vs-canvas-{key.Replace("test.", string.Empty, StringComparison.Ordinal)}-z{zoom:0.##}-{(light ? "light" : "classic")}";
        frame.Save(Path.Join(PicturePath, name + ".png"), PngBitmapEncoderOptions.Default);

        // Assert: crop the node's visual extent from both places and compare
        var picture = PictureReader.Read(frame);
        var extent = item.Prototype.VisualBounds;
        var left = (int)Math.Floor(extent.Left * zoom) - 1;
        var top = (int)Math.Floor(extent.Top * zoom) - 1;
        var width = (int)Math.Ceiling(extent.Width * zoom) + 4;
        var height = (int)Math.Ceiling(extent.Height * zoom) + 4;
        var (differing, inked, worst) = Compare(
            picture,
            ((int)Math.Floor(paletteBox.X) + left, (int)Math.Floor(paletteBox.Y) + top),
            ((int)Math.Floor(wanted.X) + left, (int)Math.Floor(wanted.Y) + top),
            width,
            height,
            theme?.CanvasBackground ?? DiagramTheme.Classic.CanvasBackground);
        Assert.Multiple(
            () => Assert.True(inked > 400, $"the crop shows a node ({inked} non-canvas pixels)"),
            () => Assert.True(differing == 0, $"{differing} of {width * height} pixels differ by more than {Tolerance} (worst channel difference {worst}); see {name}.png"));
    }

    /// <summary>
    ///     Gets the folder where the tests save their pictures for inspection.
    /// </summary>
    private static string PicturePath => Path.Join(Path.GetTempPath(), "plexus-palette");

    /// <summary>
    ///     Gets the fractional part of a coordinate.
    /// </summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The part after the whole number.</returns>
    private static double Fraction(double value) => value - Math.Floor(value);

    /// <summary>
    ///     Compares two same-sized crops of a picture.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="first">The top-left of the first crop.</param>
    /// <param name="second">The top-left of the second crop.</param>
    /// <param name="width">The crop width.</param>
    /// <param name="height">The crop height.</param>
    /// <param name="canvas">The canvas color, used to count the pixels that show something.</param>
    /// <returns>How many pixels differ by more than the tolerance, how many pixels of the first crop differ from the canvas, and the largest channel difference.</returns>
    private static (int Differing, int Inked, int Worst) Compare(Picture picture, (int X, int Y) first, (int X, int Y) second, int width, int height, PlexusColor canvas)
    {
        var differing = 0;
        var inked = 0;
        var worst = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = picture.At(first.X + x, first.Y + y);
                var b = picture.At(second.X + x, second.Y + y);
                var difference = Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
                worst = Math.Max(worst, difference);
                if (difference > Tolerance)
                {
                    differing++;
                }

                if (Math.Abs(a.R - canvas.R) > Tolerance || Math.Abs(a.G - canvas.G) > Tolerance || Math.Abs(a.B - canvas.B) > Tolerance)
                {
                    inked++;
                }
            }
        }

        return (differing, inked, worst);
    }
}
