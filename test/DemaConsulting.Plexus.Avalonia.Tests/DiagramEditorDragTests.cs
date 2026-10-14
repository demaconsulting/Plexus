using Avalonia;
using Avalonia.Input;
using DemaConsulting.Plexus.Scene;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the drag gestures: move, duplicate, connect, wire segments, resize and groups.
/// </summary>
public class DiagramEditorDragTests
{
    /// <summary>
    ///     Validates that dragging a node moves it, snaps it to the grid, and records exactly one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_Node_SnapsToGridWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a node on the grid
        var node = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;

        // Act: drag the body by an amount that is not a multiple of the grid size
        host.Drag(host.CenterOf(node), new Point(host.CenterOf(node).X + 37, host.CenterOf(node).Y + 22));

        // Assert: the node snapped to the nearest grid point and the whole drag is one history step
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(180, 165), node.Position),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: a single undo puts it back
        host.Diagram.History.Undo();
        Assert.Equal(new PlexusPoint(150, 150), node.Position);
    });

    /// <summary>
    ///     Validates that dragging without snapping moves by the exact pointer delta.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_SnapDisabled_MovesByExactDelta() => UiTest.Run(host =>
    {
        // Arrange: a node and an editor with snapping off
        var node = host.AddSource(150, 150);
        host.Editor.SnapToGrid = false;
        var start = host.CenterOf(node);

        // Act: drag by an odd amount
        host.Drag(start, new Point(start.X + 37, start.Y + 22));

        // Assert: the node moved by exactly that much
        Assert.Equal(new PlexusPoint(187, 172), node.Position);
    });

    /// <summary>
    ///     Validates that dragging one of several selected nodes moves them all by the same amount.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_SelectedNodes_MoveTogether() => UiTest.Run(host =>
    {
        // Arrange: two selected nodes
        var a = host.AddSource(150, 150);
        var b = host.AddSink(450, 300);
        host.Editor.Select([a, b]);
        var start = host.CenterOf(a);

        // Act: drag one of them
        host.Drag(start, new Point(start.X + 30, start.Y + 45));

        // Assert: both moved by the snapped delta
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(180, 195), a.Position),
            () => Assert.Equal(new PlexusPoint(480, 345), b.Position));
    });

    /// <summary>
    ///     Validates that wires follow the nodes live while dragging, before the button is released.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_ConnectedNode_RewiresLive() => UiTest.Run(host =>
    {
        // Arrange: a connected pair
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 60);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        var start = host.CenterOf(sink);

        // Act: drag the sink downwards but stop before releasing
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X, start.Y + 150));

        // Assert: mid-drag, the wire already ends at the sink's new port position
        var end = wire.GetPoints()[^1];
        Assert.Equal(sink.Port("value").Anchor, end);
        host.EndDrag(new Point(start.X, start.Y + 150));
    });

    /// <summary>
    ///     Validates that Escape during a drag puts everything back and records nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_EscapeCancels_RestoresPositionAndHistory() => UiTest.Run(host =>
    {
        // Arrange: a node, mid-drag
        var node = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(node);
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X + 90, start.Y + 90));
        Assert.NotEqual(new PlexusPoint(150, 150), node.Position);

        // Act: press Escape, then release the button
        host.PressFocused(Key.Escape);
        host.EndDrag(new Point(start.X + 90, start.Y + 90));

        // Assert: the node is back and no history entry remains
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that a click that barely moves does not edit the diagram.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_WithoutDrag_RecordsNoUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a node
        var node = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;

        // Act: click it
        host.Click(host.CenterOf(node));

        // Assert: no history entry and no movement
        Assert.Multiple(
            () => Assert.Equal(undo, host.Diagram.History.UndoCount),
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position));
    });

    /// <summary>
    ///     Validates that Ctrl-drag duplicates the node and drags the copy, as one undo step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlDrag_Node_DuplicatesAndMovesCopy() => UiTest.Run(host =>
    {
        // Arrange: a named node
        var original = host.AddSource(150, 150, "Src");
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(original);

        // Act: Ctrl-drag it
        host.Drag(start, new Point(start.X + 150, start.Y), RawInputModifiers.Control);

        // Assert: the original stayed, a uniquely named copy moved, the copy is selected, one history step
        var copy = Assert.Single(host.Page.Nodes, n => n != original);
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), original.Position),
            () => Assert.Equal(new PlexusPoint(300, 150), copy.Position),
            () => Assert.NotEqual(original.Name, copy.Name),
            () => Assert.Equal([copy], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: one undo removes the copy entirely
        host.Diagram.History.Undo();
        Assert.Single(host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that dragging from an output port to an input port connects them.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PortDrag_OutputToInput_ConnectsWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a source and a sink
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 200);
        var undo = host.Diagram.History.UndoCount;

        // Act: drag from the source's output port onto the sink's input port
        host.Drag(host.PortOf(source), host.PortOf(sink));

        // Assert: one wire joins them, it is selected, and there is one new history entry
        var wire = Assert.Single(host.Page.Wires);
        Assert.Multiple(
            () => Assert.Same(source.Port("value"), wire.Source),
            () => Assert.Same(sink.Port("value"), wire.Target),
            () => Assert.Equal([wire], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that the connect gesture also works in the reverse direction (input to output).
    /// </summary>
    [Fact]
    public Task DiagramEditor_PortDrag_InputToOutput_ConnectsInCorrectOrder() => UiTest.Run(host =>
    {
        // Arrange: a source and a sink
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 200);

        // Act: drag from the input port back to the output port
        host.Drag(host.PortOf(sink), host.PortOf(source));

        // Assert: the wire still runs output to input
        var wire = Assert.Single(host.Page.Wires);
        Assert.Multiple(
            () => Assert.Same(source.Port("value"), wire.Source),
            () => Assert.Same(sink.Port("value"), wire.Target));
    });

    /// <summary>
    ///     Validates that a connection the rules reject is not made.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PortDrag_IncompatibleTypes_IsRejected() => UiTest.Run(host =>
    {
        // Arrange: a rule that demands equal port types, an f32 source and an i32 sink
        host.Diagram.ConnectionRules = ConnectionRules.SameType;
        var source = host.AddSource(60, 60);
        var sink = host.Page.AddNode("test.IntSink", 400, 200);
        var undo = host.Diagram.History.UndoCount;

        // Act: try to connect them by dragging
        host.Drag(host.PortOf(source), host.PortOf(sink));

        // Assert: nothing was connected or recorded
        Assert.Multiple(
            () => Assert.Empty(host.Page.Wires),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount),
            () => Assert.False(host.Page.CanConnect(source.Port("value"), sink.Port("value")).IsAllowed));
    });

    /// <summary>
    ///     Validates that dragging a second wire onto an input that already has one is rejected by the default rules.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PortDrag_SecondWireIntoOccupiedInput_IsRejected() => UiTest.Run(host =>
    {
        // Arrange: a sink fed by one source, and a second source
        var first = host.AddSource(60, 60);
        var second = host.AddSource(60, 300);
        var sink = host.AddSink(400, 200);
        host.Page.Connect(first.Port("value"), sink.Port("value"));
        var undo = host.Diagram.History.UndoCount;

        // Act: drag the second source onto the occupied input
        host.Drag(host.PortOf(second), host.PortOf(sink));

        // Assert: still the single original wire and no new history entry
        Assert.Multiple(
            () => Assert.Single(host.Page.Wires),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount),
            () => Assert.Contains("does not accept more than one", host.Page.CanConnect(second.Port("value"), sink.Port("value")).Reason, StringComparison.Ordinal));
    });

    /// <summary>
    ///     Validates that releasing a port drag over empty canvas connects nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PortDrag_ReleasedOnCanvas_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange: a source node
        var source = host.AddSource(60, 60);

        // Act: drag from its port to empty space
        host.Drag(host.PortOf(source), new Point(500, 400));

        // Assert: no wire, and the node did not move
        Assert.Multiple(
            () => Assert.Empty(host.Page.Wires),
            () => Assert.Equal(new PlexusPoint(60, 60), source.Position));
    });

    /// <summary>
    ///     Validates that dragging a wire segment perpendicular to itself moves it and stays orthogonal, in one undo step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_WireSegmentDrag_MovesSegmentWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a wire with a vertical middle segment
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(450, 300);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Flush();
        var before = wire.GetPoints();
        var index = FindMovableSegment(before);
        Assert.True(index > 0, "the test needs a wire with a movable interior segment");
        var undo = host.Diagram.History.UndoCount;
        var a = before[index];
        var b = before[index + 1];
        var vertical = Math.Abs(a.X - b.X) < 1e-6;
        var mid = host.Editor.DiagramToScreen(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));

        // Act: drag the segment 45 units sideways (or up/down for a horizontal one)
        var target = vertical ? new Point(mid.X + 45, mid.Y) : new Point(mid.X, mid.Y + 45);
        host.Drag(mid, target);

        // Assert: both ends of that segment moved by the same snapped amount, the rest stayed, one history step
        var after = wire.GetPoints();
        var moved = vertical ? after[index].X - a.X : after[index].Y - a.Y;
        Assert.Multiple(
            () => Assert.Equal(before.Count, after.Count),
            () => Assert.True(Math.Abs(moved) >= 30, $"moved {moved}"),
            () => Assert.Equal(vertical ? after[index].X : after[index].Y, vertical ? after[index + 1].X : after[index + 1].Y),
            () => Assert.Equal(before[0], after[0]),
            () => Assert.Equal(before[^1], after[^1]),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: undo restores the original route
        host.Diagram.History.Undo();
        Assert.Equal(before, wire.GetPoints());
    });

    /// <summary>
    ///     Validates that dragging an annotation's corner handle resizes it, snapped, as one undo step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ResizeHandleDrag_Annotation_ResizesWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a selected annotation (the handle only exists while selected)
        var note = host.Page.AddAnnotation("note", new PlexusRect(150, 150, 150, 60));
        host.Editor.Select(note);
        host.Flush();
        var undo = host.Diagram.History.UndoCount;
        var handle = new Point(note.Bounds.Right - 3, note.Bounds.Bottom - 3);

        // Act: drag the handle by (+40, +30)
        host.Drag(handle, new Point(handle.X + 40, handle.Y + 30));

        // Assert: the top-left stayed, the size grew to the grid-snapped corner, one history step
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), note.Bounds.Position),
            () => Assert.True(note.Bounds.Width > 170, $"width {note.Bounds.Width}"),
            () => Assert.True(note.Bounds.Height > 80, $"height {note.Bounds.Height}"),
            () => Assert.Equal(0, (note.Bounds.Right % 15) + (note.Bounds.Bottom % 15)),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that clicking a group's collapse toggle collapses it (undoably) and clicking again expands it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_GroupCollapseToggle_CollapsesAndExpands() => UiTest.Run(host =>
    {
        // Arrange: a group of two nodes
        var a = host.AddSource(100, 100);
        var b = host.AddSink(300, 100);
        var group = host.Page.CreateGroup([a, b], "Pair");
        host.Flush();
        var toggle = FindScreenPoint(host, HitKind.GroupCollapseToggle, group);
        var undo = host.Diagram.History.UndoCount;

        // Act: click the toggle
        host.Click(toggle);

        // Assert: collapsed, one history step
        Assert.Multiple(
            () => Assert.True(group.IsCollapsed),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: the collapsed box has its own toggle, which expands again
        host.Click(FindScreenPoint(host, HitKind.GroupCollapseToggle, group));
        Assert.False(group.IsCollapsed);
    });

    /// <summary>
    ///     Validates that dragging a group's title bar moves the group together with its members.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragMove_GroupHeader_MovesMembers() => UiTest.Run(host =>
    {
        // Arrange: a group of two nodes
        var a = host.AddSource(150, 150);
        var b = host.AddSink(330, 150);
        var group = host.Page.CreateGroup([a, b], "Pair");
        host.Flush();
        var header = FindScreenPoint(host, HitKind.GroupHeader, group);
        var undo = host.Diagram.History.UndoCount;

        // Act: drag the title bar
        host.Drag(header, new Point(header.X + 45, header.Y + 60));

        // Assert: both members moved by the same amount in one history step
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(195, 210), a.Position),
            () => Assert.Equal(new PlexusPoint(375, 210), b.Position),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Finds the first interior segment of a polyline that can be dragged.
    /// </summary>
    /// <param name="points">The wire's points (anchor, bends, anchor).</param>
    /// <returns>The segment index, or -1 when there is none.</returns>
    private static int FindMovableSegment(IReadOnlyList<PlexusPoint> points)
    {
        for (var i = 1; i <= points.Count - 3; i++)
        {
            var horizontal = Math.Abs(points[i].Y - points[i + 1].Y) < 1e-6;
            var vertical = Math.Abs(points[i].X - points[i + 1].X) < 1e-6;
            if (horizontal != vertical)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Scans the screen for a point where hit testing reports a kind of hit on an element.
    /// </summary>
    /// <param name="host">The editor host.</param>
    /// <param name="kind">The kind of hit to find.</param>
    /// <param name="element">The element it must belong to.</param>
    /// <returns>A screen point with that hit.</returns>
    private static Point FindScreenPoint(EditorHost host, HitKind kind, DiagramElement element)
    {
        for (var y = 0; y < 600; y += 2)
        {
            for (var x = 0; x < 800; x += 2)
            {
                var hit = host.Editor.HitTest(new Point(x, y));
                if (hit.Kind == kind && ReferenceEquals(hit.Element, element))
                {
                    return new Point(x, y);
                }
            }
        }

        throw new InvalidOperationException($"No {kind} hit found for the element.");
    }
}
