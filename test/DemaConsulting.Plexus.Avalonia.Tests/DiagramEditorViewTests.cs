using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Metadata;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the editor's styled properties, view transform and read-only mode.
/// </summary>
public class DiagramEditorViewTests
{
    /// <summary>
    ///     Validates that a new editor works with zero configuration and has the documented defaults.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Defaults_NewEditor_HasDocumentedValues() => UiTest.Run(() =>
    {
        // Arrange / Act: create an editor without configuring anything
        var editor = new DiagramEditor();

        // Assert: every property starts at its documented default
        Assert.Multiple(
            () => Assert.Null(editor.Page),
            () => Assert.Null(editor.DiagramTheme),
            () => Assert.Equal(1.0, editor.Zoom),
            () => Assert.Equal(new Point(0, 0), editor.Offset),
            () => Assert.True(editor.ShowGrid),
            () => Assert.True(editor.SnapToGrid),
            () => Assert.False(editor.IsReadOnly),
            () => Assert.Empty(editor.Selection));
    });

    /// <summary>
    ///     Validates that zoom is clamped to the supported range.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Zoom_OutOfRange_IsClamped() => UiTest.Run(() =>
    {
        // Arrange: an editor at the default zoom
        var editor = new DiagramEditor();

        // Act / Assert: values beyond either end are pulled back into range
        editor.Zoom = 100;
        Assert.Equal(DiagramEditor.MaxZoom, editor.Zoom);
        editor.Zoom = 0.001;
        Assert.Equal(DiagramEditor.MinZoom, editor.Zoom);
        editor.Zoom = double.NaN;
        Assert.Equal(1.0, editor.Zoom);
    });

    /// <summary>
    ///     Validates that the editor has a default automation id so UI automation can find it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AutomationId_Default_IsDiagramEditor() => UiTest.Run(() =>
    {
        // Arrange / Act: a fresh editor
        var editor = new DiagramEditor();

        // Assert: the default id is present
        Assert.Equal("DiagramEditor", global::Avalonia.Automation.AutomationProperties.GetAutomationId(editor));
    });

    /// <summary>
    ///     Validates that screen and diagram coordinates convert back and forth under zoom and offset.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ScreenToDiagram_WithZoomAndOffset_RoundTrips() => UiTest.Run(() =>
    {
        // Arrange: a zoomed and scrolled view
        var editor = new DiagramEditor { Zoom = 2, Offset = new Point(100, 50) };

        // Act: convert a screen point to the diagram and back
        var diagram = editor.ScreenToDiagram(new Point(40, 30));
        var screen = editor.DiagramToScreen(diagram);

        // Assert: (screen / zoom) + offset, and the round trip is exact
        Assert.Equal(new Point(120, 65), diagram);
        Assert.Equal(new Point(40, 30), screen);
    });

    /// <summary>
    ///     Validates that the assembly maps the documented XAML namespace URI to the control's CLR namespace.
    /// </summary>
    [Fact]
    public void DiagramEditor_Assembly_DeclaresPlexusXmlNamespace()
    {
        // Arrange / Act: read the XML namespace declarations of the control's assembly
        var definitions = typeof(DiagramEditor).Assembly.GetCustomAttributes<XmlnsDefinitionAttribute>();

        // Assert: the documented URI maps to the namespace that holds DiagramEditor
        Assert.Contains(
            definitions,
            d => d.XmlNamespace == "https://github.com/demaconsulting/plexus" && d.ClrNamespace == typeof(DiagramEditor).Namespace);
    }

    /// <summary>
    ///     Validates that zooming with Ctrl and the wheel keeps the diagram point under the cursor fixed.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlWheel_ZoomsAroundCursor() => UiTest.Run(host =>
    {
        // Arrange: a window with a view over the origin, and a cursor position
        var cursor = new Point(300, 200);
        host.Window.MouseMove(cursor);
        var before = host.Editor.ScreenToDiagram(cursor);

        // Act: zoom in two notches with Ctrl held
        host.Window.MouseWheel(cursor, new Vector(0, 2), RawInputModifiers.Control);
        host.Flush();

        // Assert: zoom grew and the point under the cursor did not move
        var after = host.Editor.ScreenToDiagram(cursor);
        Assert.Multiple(
            () => Assert.True(host.Editor.Zoom > 1.2),
            () => Assert.Equal(before.X, after.X, 6),
            () => Assert.Equal(before.Y, after.Y, 6));
    });

    /// <summary>
    ///     Validates that the plain wheel scrolls the view.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Wheel_WithoutModifiers_Scrolls() => UiTest.Run(host =>
    {
        // Arrange: a cursor over the editor
        var cursor = new Point(300, 200);

        // Act: scroll one notch down
        host.Window.MouseWheel(cursor, new Vector(0, -1), RawInputModifiers.None);
        host.Flush();

        // Assert: the content moved up, so the offset grew; zoom is unchanged
        Assert.Multiple(
            () => Assert.True(host.Editor.Offset.Y > 0),
            () => Assert.Equal(1.0, host.Editor.Zoom));
    });

    /// <summary>
    ///     Validates that dragging with the middle mouse button pans the view.
    /// </summary>
    [Fact]
    public Task DiagramEditor_MiddleDrag_PansView() => UiTest.Run(host =>
    {
        // Arrange: a window with an empty page; Act: press the middle button, move, release
        host.Window.MouseDown(new Point(100, 100), MouseButton.Middle);
        host.Window.MouseMove(new Point(160, 130), RawInputModifiers.MiddleMouseButton);
        host.Window.MouseUp(new Point(160, 130), MouseButton.Middle);
        host.Flush();

        // Assert: the content followed the pointer, so the offset moved the other way
        Assert.Equal(new Point(-60, -30), host.Editor.Offset);
    });

    /// <summary>
    ///     Validates that a read-only editor selects but does not move anything.
    /// </summary>
    [Fact]
    public Task DiagramEditor_IsReadOnly_Drag_DoesNotMoveNodes() => UiTest.Run(host =>
    {
        // Arrange: a node in a read-only editor
        var node = host.Page.AddNode("test.Source", 150, 150);
        host.Editor.IsReadOnly = true;
        host.Flush();
        var start = node.Bounds;
        var undoCount = host.Diagram.History.UndoCount;

        // Act: drag the node body
        host.Drag(host.CenterOf(node), new Point(400, 300));

        // Assert: the node was selected but stayed where it was
        Assert.Multiple(
            () => Assert.Equal(start, node.Bounds),
            () => Assert.Contains(node, host.Editor.Selection),
            () => Assert.Equal(undoCount, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that zoom to fit shows the whole diagram.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomToFit_FarApartNodes_BringsBothIntoView() => UiTest.Run(host =>
    {
        // Arrange: two nodes far apart
        var a = host.Page.AddNode("test.Source", 0, 0);
        var b = host.Page.AddNode("test.Sink", 3000, 2000);
        host.Flush();

        // Act: fit the diagram
        host.Editor.ZoomToFit();

        // Assert: both node boxes are inside the control
        var area = new Rect(host.Editor.Bounds.Size);
        Assert.Multiple(
            () => Assert.True(host.Editor.Zoom < 1),
            () => Assert.True(area.Contains(host.Editor.GetElementScreenBounds(a))),
            () => Assert.True(area.Contains(host.Editor.GetElementScreenBounds(b))));
    });

    /// <summary>
    ///     Validates that the control reports bounds and port positions for automation.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GetPortScreenPosition_ReturnsAnchorInScreenSpace() => UiTest.Run(host =>
    {
        // Arrange: a node in a zoomed view
        var node = host.Page.AddNode("test.Source", 150, 150);
        host.Editor.Zoom = 2;
        host.Flush();

        // Act: ask for the output port position and the node bounds
        var port = host.Editor.GetPortScreenPosition(node.Port("value"));
        var bounds = host.Editor.GetElementScreenBounds(node);

        // Assert: the port's wire attachment lies beyond the node's right edge, in zoomed screen units
        Assert.Multiple(
            () => Assert.Equal(node.Port("value").Anchor.X * 2, port.X, 6),
            () => Assert.True(port.X > node.Bounds.Right * 2),
            () => Assert.True(bounds.Width >= 2 * node.Bounds.Width));
    });
}
