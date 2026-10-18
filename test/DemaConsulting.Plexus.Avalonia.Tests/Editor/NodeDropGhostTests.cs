using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the translucent drop ghost the editor shows while a node type is dragged over it.
/// </summary>
public class NodeDropGhostTests
{
    /// <summary>
    ///     Validates that entering with a node-type payload shows the ghost of that type at the snapped drop position, and moving updates it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Ghost_DragEnterAndOver_ShowsGhostAtSnappedPosition() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var grid = host.Diagram.Theme.GridSize;
        var type = host.Types.Get("test.Mixer");
        var size = NodeGeometry.DefaultNodeSize(type, host.Diagram.Theme);

        // Act
        host.DragEvent(RawDragEventType.DragEnter, "test.Mixer", new Point(400, 300));
        var entered = host.Editor.GhostPosition;
        host.DragEvent(RawDragEventType.DragOver, "test.Mixer", new Point(500, 350));
        var moved = host.Editor.GhostPosition;

        // Assert: the ghost is the right type, on the grid and centered on the pointer to within half a cell
        Assert.Multiple(
            () => Assert.Same(type, host.Editor.GhostNodeType),
            () => Assert.NotNull(entered),
            () => Assert.NotEqual(entered, moved),
            () => Assert.Equal(0, moved!.Value.X % grid),
            () => Assert.Equal(0, moved!.Value.Y % grid),
            () => Assert.InRange(moved!.Value.X, 500 - (size.Width / 2) - (grid / 2), 500 - (size.Width / 2) + (grid / 2)),
            () => Assert.InRange(moved!.Value.Y, 350 - (size.Height / 2) - (grid / 2), 350 - (size.Height / 2) + (grid / 2)));
    });

    /// <summary>
    ///     Validates that leaving the editor removes the ghost.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Ghost_DragLeave_RemovesGhost() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        host.DragEvent(RawDragEventType.DragOver, "test.Source", new Point(300, 300));
        var shown = host.Editor.GhostNodeType;

        // Act
        host.DragEvent(RawDragEventType.DragLeave, "test.Source", new Point(300, 300));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(shown),
            () => Assert.Null(host.Editor.GhostNodeType),
            () => Assert.Null(host.Editor.GhostPosition),
            () => Assert.Empty(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates that dropping removes the ghost and creates the node exactly where the ghost was, for several zoom and scroll settings.
    /// </summary>
    [Theory]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(1.5, 40.0, 25.0)]
    [InlineData(0.5, -30.0, 70.0)]
    public Task DiagramEditor_Ghost_Drop_NodeLandsExactlyWhereGhostWas(double zoom, double offsetX, double offsetY) => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        host.Editor.Zoom = zoom;
        host.Editor.Offset = new Point(offsetX, offsetY);
        host.Flush();
        var at = new Point(333, 277);
        host.DragEvent(RawDragEventType.DragOver, "test.Badge", at);
        var ghost = host.Editor.GhostPosition;

        // Act
        host.DragEvent(RawDragEventType.Drop, "test.Badge", at);

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.NotNull(ghost),
            () => Assert.Equal(ghost, node.Bounds.Position),
            () => Assert.Null(host.Editor.GhostNodeType));
    });

    /// <summary>
    ///     Validates that a foreign payload, an unknown key, a read-only editor and a missing page show no ghost.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Ghost_NotAcceptedPayloads_ShowNoGhost() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var at = new Point(300, 300);

        // Act: unknown key
        host.DragEvent(RawDragEventType.DragOver, "test.Unknown", at);
        var unknown = host.Editor.GhostNodeType;

        // Act: a text payload that is not ours
        using (var text = new DataTransfer())
        {
            text.Add(DataTransferItem.CreateText("test.Source"));
            host.Window.DragDrop(host.ToWindow(at), RawDragEventType.DragOver, text, DragDropEffects.Copy, RawInputModifiers.None);
        }

        var foreign = host.Editor.GhostNodeType;

        // Act: read-only, then no page
        host.Editor.IsReadOnly = true;
        host.DragEvent(RawDragEventType.DragOver, "test.Source", at);
        var readOnly = host.Editor.GhostNodeType;
        host.Editor.IsReadOnly = false;
        host.Editor.Page = null;
        host.DragEvent(RawDragEventType.DragOver, "test.Source", at);
        var noPage = host.Editor.GhostNodeType;

        // Assert
        Assert.Multiple(
            () => Assert.Null(unknown),
            () => Assert.Null(foreign),
            () => Assert.Null(readOnly),
            () => Assert.Null(noPage));
    });

    /// <summary>
    ///     Validates that the ghost is painted see-through: inside the box it is neither the plain canvas nor the opaque node fill.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Ghost_IsPaintedTranslucent_AndGoneAfterLeave() => UiTest.Run(() =>
    {
        // Arrange: a node-type drag over the editor
        using var host = new PaletteHost();
        var at = new Point(400, 300);
        host.DragEvent(RawDragEventType.DragOver, "test.Gain", at);
        var box = host.Editor.GhostPosition!.Value;
        var size = NodeGeometry.DefaultNodeSize(host.Types.Get("test.Gain"), host.Diagram.Theme);

        // Act: sample the box interior while the ghost shows, then after it left
        var sample = host.ToWindow(new Point(box.X + (size.Width / 2), box.Y + size.Height - 8));
        var during = host.Window.CaptureRenderedFrame()!;
        var duringPixel = PixelAt(during, sample);
        Directory.CreateDirectory(PicturePath);
        during.Save(Path.Join(PicturePath, "ghost-over-editor.png"), PngBitmapEncoderOptions.Default);
        host.DragEvent(RawDragEventType.DragLeave, "test.Gain", at);
        var afterPixel = PixelAt(host.Window.CaptureRenderedFrame()!, sample);

        // Assert: during the drag the pixel is a blend; afterwards it is the canvas again
        var canvas = PlexusColor.Parse("#A9A9A9");
        var fill = PlexusColor.Parse("#6B6B6B");
        Assert.Multiple(
            () => Assert.False(IsNear(duringPixel, canvas), $"ghost pixel {duringPixel}"),
            () => Assert.False(IsNear(duringPixel, fill), $"ghost pixel {duringPixel}"),
            () => Assert.InRange(duringPixel.R, fill.R, canvas.R),
            () => Assert.True(IsNear(afterPixel, canvas), $"pixel after leave {afterPixel}"));
    });

    /// <summary>
    ///     Gets the folder where the tests save their pictures for inspection.
    /// </summary>
    private static string PicturePath => Path.Join(Path.GetTempPath(), "plexus-palette");

    /// <summary>
    ///     Reads one pixel of a captured frame.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="at">The window point.</param>
    /// <returns>The pixel's color.</returns>
    private static (byte R, byte G, byte B) PixelAt(WriteableBitmap frame, Point at)
    {
        var pixels = PictureReader.Read(frame);
        return pixels.At((int)at.X, (int)at.Y);
    }

    /// <summary>
    ///     Determines whether a pixel is within a small tolerance of a color.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when all channels are within 3.</returns>
    private static bool IsNear((byte R, byte G, byte B) pixel, PlexusColor color) =>
        Math.Abs(pixel.R - color.R) <= 3 && Math.Abs(pixel.G - color.G) <= 3 && Math.Abs(pixel.B - color.B) <= 3;
}
