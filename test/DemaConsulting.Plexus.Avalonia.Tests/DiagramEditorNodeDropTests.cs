using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of drag-and-drop node creation: dropping a node type key onto the editor.
/// </summary>
public class DiagramEditorNodeDropTests
{
    /// <summary>
    ///     Drops a node type onto the editor the way another control's drag would.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="key">The node type key carried by the payload.</param>
    /// <param name="at">The drop point relative to the editor.</param>
    private static void Drop(EditorHost host, string key, Point at)
    {
        using var transfer = DiagramEditor.CreateNodeTypeDataTransfer(key);
        host.Window.DragDrop(at, RawDragEventType.DragEnter, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Window.DragDrop(at, RawDragEventType.DragOver, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Window.DragDrop(at, RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Flush();
    }

    /// <summary>
    ///     Validates that the data format identifies the node type payload and the payload carries the key.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CreateNodeTypeDataTransfer_CarriesTheTypeKey() => UiTest.Run(() =>
    {
        // Arrange / Act
        using var transfer = DiagramEditor.CreateNodeTypeDataTransfer("test.Source");
        IDataTransfer data = transfer;

        // Assert
        Assert.Multiple(
            () => Assert.True(data.Contains(DiagramEditor.NodeTypeDataFormat)),
            () => Assert.Equal("test.Source", data.TryGetValue(DiagramEditor.NodeTypeDataFormat)),
            () => Assert.Throws<ArgumentNullException>(() => DiagramEditor.CreateNodeTypeDataTransfer(null!)));
    });

    /// <summary>
    ///     Validates that dropping a node type creates a node centered at the drop point (unsnapped), selected, as one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_NodeType_CreatesCenteredSelectedNodeWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: no snapping so the centering can be checked exactly
        host.Editor.SnapToGrid = false;
        var undo = host.Diagram.History.UndoCount;

        // Act
        Drop(host, "test.Source", new Point(400, 300));

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal("test.Source", node.Type.Key),
            () => Assert.Equal(400, node.Bounds.Center.X, 1),
            () => Assert.Equal(300, node.Bounds.Center.Y, 1),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Equal("Add node", host.Diagram.History.UndoDescription));

        // And undo removes it again
        host.Diagram.History.Undo();
        Assert.Empty(host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that the drop position honors zoom and scroll.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_NodeType_UsesViewTransform() => UiTest.Run(host =>
    {
        // Arrange: a zoomed and scrolled view without snapping
        host.Editor.SnapToGrid = false;
        host.Editor.Zoom = 2;
        host.Editor.Offset = new Point(100, 50);
        host.Flush();
        var at = new Point(400, 300);
        var expected = host.Editor.ScreenToDiagram(at);

        // Act
        Drop(host, "test.Sink", at);

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal(expected.X, node.Bounds.Center.X, 1),
            () => Assert.Equal(expected.Y, node.Bounds.Center.Y, 1));
    });

    /// <summary>
    ///     Validates that the node's top-left corner is snapped to the grid when snapping is on.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_NodeType_SnapsToGrid() => UiTest.Run(host =>
    {
        // Arrange
        var grid = host.Diagram.Theme.GridSize;
        var type = host.Diagram.Types.Get("test.Source");
        var size = NodeGeometry.DefaultNodeSize(type, host.Diagram.Theme);

        // Act
        Drop(host, "test.Source", new Point(401, 303));

        // Assert: top-left on the grid and within half a cell of the unsnapped centered position
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal(0, node.Bounds.X % grid),
            () => Assert.Equal(0, node.Bounds.Y % grid),
            () => Assert.InRange(node.Bounds.X, 401 - (size.Width / 2) - (grid / 2), 401 - (size.Width / 2) + (grid / 2)),
            () => Assert.InRange(node.Bounds.Y, 303 - (size.Height / 2) - (grid / 2), 303 - (size.Height / 2) + (grid / 2)));
    });

    /// <summary>
    ///     Validates that an unknown type key, a read-only editor and a foreign payload create nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_UnknownKeyReadOnlyOrForeignPayload_CreatesNothing() => UiTest.Run(host =>
    {
        // Act: unknown key
        var undo = host.Diagram.History.UndoCount;
        Drop(host, "test.DoesNotExist", new Point(300, 300));
        var afterUnknown = host.Page.Nodes.Count;

        // Act: a text payload that is not ours
        using (var text = new DataTransfer())
        {
            text.Add(DataTransferItem.CreateText("test.Source"));
            host.Window.DragDrop(new Point(300, 300), RawDragEventType.DragOver, text, DragDropEffects.Copy, RawInputModifiers.None);
            host.Window.DragDrop(new Point(300, 300), RawDragEventType.Drop, text, DragDropEffects.Copy, RawInputModifiers.None);
        }

        host.Flush();
        var afterForeign = host.Page.Nodes.Count;

        // Act: read-only editor
        host.Editor.IsReadOnly = true;
        Drop(host, "test.Source", new Point(300, 300));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, afterUnknown),
            () => Assert.Equal(0, afterForeign),
            () => Assert.Empty(host.Page.Nodes),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that an editor without a page ignores drops.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_NoPage_CreatesNothing() => UiTest.Run(host =>
    {
        // Arrange
        host.Editor.Page = null;

        // Act
        Drop(host, "test.Source", new Point(300, 300));

        // Assert
        Assert.Empty(host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that node creation by drop works repeatedly and names nodes uniquely.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Drop_TwiceInARow_CreatesTwoDistinctNodes() => UiTest.Run(host =>
    {
        // Act
        Drop(host, "test.Source", new Point(200, 200));
        Drop(host, "test.Source", new Point(500, 200));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, host.Page.Nodes.Count),
            () => Assert.Equal(2, host.Page.Nodes.Select(n => n.Name).Distinct().Count()));
    });

    /// <summary>
    ///     Validates the argument checks of the drag helpers and that a plain click on a drag source is harmless.
    /// </summary>
    [Fact]
    public Task DiagramEditor_EnableNodeTypeDrag_ArgumentsAndPlainClick() => UiTest.Run(host =>
    {
        // Arrange: a border standing in for a palette entry
        var entry = new Border { Width = 40, Height = 40 };
        using var handle = DiagramEditor.EnableNodeTypeDrag(entry, "test.Source");

        // Act: a click that never moves does not start a drag (and must not throw)
        var click = () => host.Click(new Point(10, 10));

        // Assert
        Assert.Multiple(
            () => click(),
            () => Assert.Throws<ArgumentNullException>(() => DiagramEditor.EnableNodeTypeDrag(null!, "k")),
            () => Assert.Throws<ArgumentNullException>(() => DiagramEditor.EnableNodeTypeDrag(entry, null!)),
            () => Assert.Empty(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates that StartNodeTypeDragAsync rejects null arguments.
    /// </summary>
    [Fact]
    public Task DiagramEditor_StartNodeTypeDragAsync_NullArguments_Throw() => UiTest.RunAsync(async _ =>
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => DiagramEditor.StartNodeTypeDragAsync(null!, "test.Source"));
    });
}
