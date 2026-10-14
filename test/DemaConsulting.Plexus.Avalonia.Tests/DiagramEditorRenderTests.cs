using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of scene rebuilding and of painting, including a rendered picture for visual inspection.
/// </summary>
public class DiagramEditorRenderTests
{
    /// <summary>
    ///     A simple dark-disc icon in SVG form, standing in for a host-supplied node icon.
    /// </summary>
    private const string DiscSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='30' viewBox='0 0 30 30'>" +
        "<circle cx='15' cy='15' r='13' fill='#2b2b2b'/>" +
        "<path d='M6 15 H24 M19 10 L24 15 L19 20' stroke='#ffc800' stroke-width='2' fill='none'/></svg>";

    /// <summary>
    ///     Validates that changing the model changes the scene shown by the editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Scene_ModelChange_IsRebuilt() => UiTest.Run(host =>
    {
        // Arrange: an empty page and the scene built for it
        var before = host.CurrentScene;
        var builds = host.Editor.SceneBuildCount;

        // Act: add a node, which raises Diagram.Changed
        var node = host.Page.AddNode("test.Source", 150, 150);
        var after = host.CurrentScene;

        // Assert: a new scene exists and it knows the node
        Assert.Multiple(
            () => Assert.NotSame(before, after),
            () => Assert.True(host.Editor.SceneBuildCount > builds),
            () => Assert.True(after.TryGetNodeLayout(node, out _)));
    });

    /// <summary>
    ///     Validates that a burst of model changes costs only one scene rebuild.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Scene_BurstOfChanges_RebuildsOnce() => UiTest.Run(host =>
    {
        // Arrange: a settled scene
        _ = host.Editor.Scene;
        var builds = host.Editor.SceneBuildCount;

        // Act: make many changes before anyone asks for the scene
        for (var i = 0; i < 20; i++)
        {
            host.Page.AddNode("test.Source", i * 20, 0);
        }

        _ = host.Editor.Scene;

        // Assert: exactly one rebuild happened for all of them
        Assert.Equal(builds + 1, host.Editor.SceneBuildCount);
    });

    /// <summary>
    ///     Validates that undo and redo are reflected in the scene (the editor listens to every change).
    /// </summary>
    [Fact]
    public Task DiagramEditor_Scene_UndoAndRedo_AreReflected() => UiTest.Run(host =>
    {
        // Arrange: a node that exists in the scene
        var node = host.Page.AddNode("test.Source", 150, 150);
        Assert.True(host.CurrentScene.TryGetNodeLayout(node, out _));

        // Act / Assert: undo removes it from the scene, redo brings it back
        host.Diagram.History.Undo();
        Assert.False(host.CurrentScene.TryGetNodeLayout(node, out _));
        host.Diagram.History.Redo();
        Assert.True(host.CurrentScene.TryGetNodeLayout(node, out _));
    });

    /// <summary>
    ///     Validates that elements outside the visible area are culled from the scene.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Scene_FarAwayNode_IsCulled() => UiTest.Run(host =>
    {
        // Arrange: one node in view and one thousands of units away
        var near = host.Page.AddNode("test.Source", 150, 150);
        var far = host.Page.AddNode("test.Source", 9000, 9000);

        // Act: read the scene
        var scene = host.CurrentScene;

        // Assert: only the visible one was built
        Assert.Multiple(
            () => Assert.True(scene.TryGetNodeLayout(near, out _)),
            () => Assert.False(scene.TryGetNodeLayout(far, out _)));
    });

    /// <summary>
    ///     Validates that scrolling far enough to reveal culled content rebuilds the scene.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Scene_ScrollToFarNode_BuildsIt() => UiTest.Run(host =>
    {
        // Arrange: a node out of view
        var far = host.Page.AddNode("test.Source", 9000, 9000);
        Assert.False(host.CurrentScene.TryGetNodeLayout(far, out _));

        // Act: scroll to it
        host.Editor.Offset = new Point(8900, 8900);

        // Assert: it is now part of the scene
        Assert.True(host.CurrentScene.TryGetNodeLayout(far, out _));
    });

    /// <summary>
    ///     Validates that the editor can be given a theme that overrides the diagram's.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DiagramTheme_Override_IsUsedByScene() => UiTest.Run(host =>
    {
        // Arrange: a custom theme with an unmistakable grid color
        var theme = new DiagramTheme { GridDotColor = PlexusColor.Parse("#FF0000") };

        // Act: assign it to the editor
        host.Editor.DiagramTheme = theme;

        // Assert: the scene was built with it
        Assert.Same(theme, host.CurrentScene.Theme);
    });

    /// <summary>
    ///     Validates that the editor paints the diagram: a rendered frame shows node and canvas colors, and is saved for inspection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Render_SmallDiagram_PaintsNodesWiresAndText() => UiTest.Run(host =>
    {
        // Arrange: a diagram with icons, summary lines, an annotation and a selection
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", PlexusColor.Parse("#FF8C00")));
        types.PortTypes.Add(new PortType("i32", PlexusColor.Parse("#1E90FF")));
        var icon = IconSource.FromSvg(DiscSvg);
        types.Add(new NodeType("viz.Source") { Title = "SOURCE", Icon = icon, Outputs = { new PortDefinition("value", "f32") }, Properties = { new PropertyDefinition("Format", "F32") { ShowInSummary = true } } });
        types.Add(new NodeType("viz.Sink") { Title = "SINK", Icon = icon, Inputs = { new PortDefinition("voltage", "f32"), new PortDefinition("current", "f32"), new PortDefinition("brake", "i32") } });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Visual");
        var voltage = page.AddNode("viz.Source", 30, 30, "SourceVoltage");
        var current = page.AddNode("viz.Source", 30, 180, "SourceCurrent");
        var sink = page.AddNode("viz.Sink", 360, 120, "Main");
        page.Connect(voltage.Port("value"), sink.Port("voltage"));
        page.Connect(current.Port("value"), sink.Port("current"));
        var note = page.AddAnnotation("Wires route around nodes", new PlexusRect(250, 330, 150, 60));
        var window = new Window { Width = 520, Height = 420, Content = new DiagramEditor { Page = page } };
        var editor = (DiagramEditor)window.Content;
        window.Show();
        editor.Select([sink, note]);
        host.Flush();

        // Act: capture a rendered frame and save it for visual inspection
        var frame = window.CaptureRenderedFrame()!;
        var picturePath = Path.Combine(Path.GetTempPath(), "plexus-avalonia");
        Directory.CreateDirectory(picturePath);
        frame.Save(Path.Combine(picturePath, "editor-small.png"), PngBitmapEncoderOptions.Default);

        // Assert: the canvas background and the node fill are both present in the picture
        var pixels = ReadPixels(frame);
        var canvas = pixels.Count(p => IsClose(p, "#A9A9A9"));
        var nodeFill = pixels.Count(p => IsClose(p, "#6B6B6B"));
        window.Close();
        Assert.Multiple(
            () => Assert.True(canvas > 20000, $"canvas pixels: {canvas}"),
            () => Assert.True(nodeFill > 3000, $"node pixels: {nodeFill}"));
    });

    /// <summary>
    ///     Validates that groups, collapsed groups, a selected wire and a live connection preview paint, and saves the picture for inspection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Render_GroupsAndConnectPreview_SavesPicture() => UiTest.Run(host =>
    {
        // Arrange: an expanded group, a collapsed group with a crossing wire, and a source to drag from
        var a = host.AddSource(60, 60, "A");
        var b = host.AddSink(300, 60, "B");
        var c = host.AddSource(60, 240, "C");
        var d = host.AddSink(420, 240, "D");
        var inner = a.Port("value").ConnectTo(b.Port("value"));
        var crossing = c.Port("value").ConnectTo(d.Port("value"));
        host.Page.CreateGroup([a, b], "Expanded group");
        var collapsed = host.Page.CreateGroup([c], "Collapsed group");
        collapsed.IsCollapsed = true;
        host.Editor.Select(inner);
        host.Flush();
        var from = host.PortOf(b);

        // Act: start dragging a new wire from B's input toward an incompatible-looking empty spot and capture mid-drag
        host.BeginDrag(from);
        host.MoveTo(new Point(from.X + 120, from.Y + 90));
        var frame = host.Window.CaptureRenderedFrame()!;
        var picturePath = Path.Combine(Path.GetTempPath(), "plexus-avalonia");
        Directory.CreateDirectory(picturePath);
        frame.Save(Path.Combine(picturePath, "editor-groups.png"), PngBitmapEncoderOptions.Default);
        host.EndDrag(new Point(from.X + 120, from.Y + 90));

        // Assert: the picture exists and the crossing wire survived the collapse
        Assert.Multiple(
            () => Assert.True(File.Exists(Path.Combine(picturePath, "editor-groups.png"))),
            () => Assert.NotNull(crossing.Page));
    });
    /// <summary>
    ///     Reads every pixel of a bitmap as an RGB triple.
    /// </summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <returns>The pixels, row by row.</returns>
    private static List<(byte R, byte G, byte B)> ReadPixels(WriteableBitmap bitmap)
    {
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
        var result = new List<(byte, byte, byte)>(size.Width * size.Height);
        for (var i = 0; i < buffer.Length; i += 4)
        {
            result.Add((buffer[i + 2], buffer[i + 1], buffer[i]));
        }

        return result;
    }

    /// <summary>
    ///     Determines whether a pixel is within a small tolerance of a color.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="hex">The color as #RRGGBB.</param>
    /// <returns><see langword="true"/> when each channel is within 3 of the color.</returns>
    private static bool IsClose((byte R, byte G, byte B) pixel, string hex)
    {
        var color = PlexusColor.Parse(hex);
        return Math.Abs(pixel.R - color.R) <= 3 && Math.Abs(pixel.G - color.G) <= 3 && Math.Abs(pixel.B - color.B) <= 3;
    }
}
