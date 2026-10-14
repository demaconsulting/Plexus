using Avalonia;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.Interaction;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Unit tests of the pointer gesture state machines. Each gesture is created and driven directly, the way the editor drives it.
/// </summary>
public class GesturesTests
{
    /// <summary>
    ///     Validates that a press that has not traveled past the drag threshold edits nothing.
    /// </summary>
    [Fact]
    public Task MoveGesture_Move_BelowThreshold_EditsNothing() => UiTest.Run(host =>
    {
        // Arrange: a node and a move gesture started on it
        var node = host.AddSource(150, 150);
        var start = host.CenterOf(node);
        var undo = host.Diagram.History.UndoCount;
        var gesture = new MoveGesture(host.Editor, start, node, false, []);

        // Act: wobble by a pixel
        gesture.Move(new Point(start.X + 1, start.Y + 1), KeyModifiers.None);

        // Assert: nothing moved, nothing is recorded and no drag cursor is requested
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount),
            () => Assert.Null(gesture.Cursor));
    });

    /// <summary>
    ///     Validates that a real drag snaps the pressed node's corner to the grid and commits as one undo entry.
    /// </summary>
    [Fact]
    public Task MoveGesture_Complete_AfterDrag_SnapsAndRecordsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a node on the grid and a gesture started on it
        var node = host.AddSource(150, 150);
        host.Editor.Select(node);
        var start = host.CenterOf(node);
        var undo = host.Diagram.History.UndoCount;
        var gesture = new MoveGesture(host.Editor, start, node, true, [node]);
        var end = new Point(start.X + 37, start.Y + 22);

        // Act: drag in two steps, then release
        gesture.Move(new Point(start.X + 10, start.Y + 5), KeyModifiers.None);
        gesture.Move(end, KeyModifiers.None);
        var cursor = gesture.Cursor;
        gesture.Complete(end, KeyModifiers.None);

        // Assert: the corner snapped to the grid, the cursor showed a move, and the whole drag is one history step
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(180, 165), node.Position),
            () => Assert.Equal(StandardCursorType.SizeAll, cursor),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that holding Ctrl when the drag starts duplicates the selection and drags the copies.
    /// </summary>
    [Fact]
    public Task MoveGesture_Move_ControlHeld_DuplicatesAndMovesCopy() => UiTest.Run(host =>
    {
        // Arrange: a selected node and a gesture on it
        var original = host.AddSource(150, 150);
        host.Editor.Select(original);
        var start = host.CenterOf(original);
        var gesture = new MoveGesture(host.Editor, start, original, true, [original]);
        var end = new Point(start.X + 150, start.Y);

        // Act: drag with Ctrl held, then release
        gesture.Move(end, KeyModifiers.Control);
        gesture.Complete(end, KeyModifiers.Control);

        // Assert: the original stayed, the selected copy moved
        var copy = Assert.Single(host.Page.Nodes, n => n != original);
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), original.Position),
            () => Assert.Equal(new PlexusPoint(300, 150), copy.Position),
            () => Assert.Equal([copy], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that cancelling a drag restores positions, the selection and the history.
    /// </summary>
    [Fact]
    public Task MoveGesture_Cancel_AfterDrag_RestoresPositionSelectionAndHistory() => UiTest.Run(host =>
    {
        // Arrange: a selected node being Ctrl-dragged
        var node = host.AddSource(150, 150);
        host.Editor.Select(node);
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(node);
        var gesture = new MoveGesture(host.Editor, start, node, true, [node]);
        gesture.Move(new Point(start.X + 90, start.Y + 90), KeyModifiers.Control);
        Assert.Equal(2, host.Page.Nodes.Count);

        // Act: cancel
        gesture.Cancel();

        // Assert: the copy is gone, the original is selected again and nothing was recorded
        Assert.Multiple(
            () => Assert.Single(host.Page.Nodes),
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that releasing a press on one selected node among several narrows the selection to it, and Shift removes it.
    /// </summary>
    [Fact]
    public Task MoveGesture_Complete_ClickOnSelectedNode_FinishesDeferredSelectionChange() => UiTest.Run(host =>
    {
        // Arrange: two selected nodes
        var a = host.AddSource(150, 150);
        var b = host.AddSink(450, 300);
        host.Editor.Select([a, b]);
        var start = host.CenterOf(a);

        // Act: a plain click on A narrows the selection
        new MoveGesture(host.Editor, start, a, true, [a, b]).Complete(start, KeyModifiers.None);
        var narrowed = host.Editor.Selection.ToList();

        // Act: a Shift click on A then removes it from a two-element selection
        host.Editor.Select([a, b]);
        new MoveGesture(host.Editor, start, a, true, [a, b]).Complete(start, KeyModifiers.Shift);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([a], narrowed),
            () => Assert.Equal([b], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that releasing over a compatible port connects the two ports and selects the wire.
    /// </summary>
    [Fact]
    public Task ConnectGesture_Complete_OverCompatiblePort_ConnectsAndSelectsWire() => UiTest.Run(host =>
    {
        // Arrange: a source, a sink and a gesture started on the source's port
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 200);
        var gesture = new ConnectGesture(host.Editor, host.PortOf(source), source.Port("value"));
        var target = host.PortOf(sink);

        // Act: move over the sink's port, then release
        gesture.Move(target, KeyModifiers.None);
        gesture.Complete(target, KeyModifiers.None);

        // Assert: one wire runs from the source to the sink and is selected; the cursor was a cross
        var wire = Assert.Single(host.Page.Wires);
        Assert.Multiple(
            () => Assert.Same(source.Port("value"), wire.Source),
            () => Assert.Same(sink.Port("value"), wire.Target),
            () => Assert.Equal([wire], host.Editor.Selection),
            () => Assert.Equal(StandardCursorType.Cross, gesture.Cursor));
    });

    /// <summary>
    ///     Validates that a connection the rules reject is not made on release.
    /// </summary>
    [Fact]
    public Task ConnectGesture_Complete_OverRejectedPort_ConnectsNothing() => UiTest.Run(host =>
    {
        // Arrange: a rule that demands equal port types, an f32 source and an i32 sink
        host.Diagram.ConnectionRules = ConnectionRules.SameType;
        var source = host.AddSource(60, 60);
        var sink = host.Page.AddNode("test.IntSink", 400, 200);
        var undo = host.Diagram.History.UndoCount;
        var gesture = new ConnectGesture(host.Editor, host.PortOf(source), source.Port("value"));
        var target = host.PortOf(sink);

        // Act: move over the rejecting port and release
        gesture.Move(target, KeyModifiers.None);
        gesture.Complete(target, KeyModifiers.None);

        // Assert: no wire and no history entry
        Assert.Multiple(
            () => Assert.Empty(host.Page.Wires),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that a second wire into an input that does not allow fan-in shows a rejection with the reason and connects nothing, while an input that allows it accepts the wire.
    /// </summary>
    [Fact]
    public Task ConnectGesture_SecondWireIntoInput_RejectedUnlessInputAllowsFanIn() => UiTest.Run(host =>
    {
        // Arrange: one wire already into a plain sink, and a second source, plus a merger that has a wire as well
        var first = host.AddSource(60, 60);
        var second = host.AddSource(60, 300);
        var sink = host.AddSink(400, 60);
        var merger = host.Page.AddNode("test.Merger", 400, 300);
        host.Page.Connect(first.Port("value"), sink.Port("value"));
        host.Page.Connect(first.Port("value"), merger.Port("value"));
        var undo = host.Diagram.History.UndoCount;

        // Act: hover and release over the occupied plain sink, then over the occupied merger
        var toSink = new ConnectGesture(host.Editor, host.PortOf(second), second.Port("value"));
        toSink.Move(host.PortOf(sink), KeyModifiers.None);
        var rejected = toSink.Verdict;
        toSink.Complete(host.PortOf(sink), KeyModifiers.None);
        var afterReject = host.Page.Wires.Count;
        var toMerger = new ConnectGesture(host.Editor, host.PortOf(second), second.Port("value"));
        toMerger.Move(host.PortOf(merger), KeyModifiers.None);
        var accepted = toMerger.Verdict;
        toMerger.Complete(host.PortOf(merger), KeyModifiers.None);

        // Assert: the plain sink refuses with its reason and no history entry; the merger takes the wire
        Assert.Multiple(
            () => Assert.False(rejected.IsAllowed),
            () => Assert.Contains("does not accept more than one", rejected.Reason, StringComparison.Ordinal),
            () => Assert.Equal(2, afterReject),
            () => Assert.True(accepted.IsAllowed),
            () => Assert.Equal(3, host.Page.Wires.Count),
            () => Assert.Equal(2, merger.Port("value").Wires.Count),
            () => Assert.Single(sink.Port("value").Wires),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that releasing over the port the drag started from or over empty canvas connects nothing.
    /// </summary>
    [Fact]
    public Task ConnectGesture_Complete_OverOwnPortOrCanvas_ConnectsNothing() => UiTest.Run(host =>
    {
        // Arrange: a lone source node
        var source = host.AddSource(60, 60);
        var port = host.PortOf(source);

        // Act: release on the start port, then on the canvas
        var onOwnPort = new ConnectGesture(host.Editor, port, source.Port("value"));
        onOwnPort.Move(port, KeyModifiers.None);
        onOwnPort.Complete(port, KeyModifiers.None);
        var onCanvas = new ConnectGesture(host.Editor, port, source.Port("value"));
        onCanvas.Move(new Point(500, 400), KeyModifiers.None);
        onCanvas.Complete(new Point(500, 400), KeyModifiers.None);

        // Assert
        Assert.Empty(host.Page.Wires);
    });

    /// <summary>
    ///     Validates that a cancelled connect drag forgets its candidate so a later release connects nothing.
    /// </summary>
    [Fact]
    public Task ConnectGesture_Cancel_AfterHoveringTarget_ConnectsNothing() => UiTest.Run(host =>
    {
        // Arrange: a source and a sink, with the drag hovering over the sink
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 200);
        var gesture = new ConnectGesture(host.Editor, host.PortOf(source), source.Port("value"));
        gesture.Move(host.PortOf(sink), KeyModifiers.None);

        // Act: cancel, then release over empty canvas
        gesture.Cancel();
        gesture.Complete(new Point(600, 500), KeyModifiers.None);

        // Assert
        Assert.Empty(host.Page.Wires);
    });

    /// <summary>
    ///     Validates that the resize drag moves the bottom-right corner to the snapped pointer and commits one undo entry.
    /// </summary>
    [Fact]
    public Task ResizeGesture_Complete_AfterDrag_ResizesWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: an annotation and a gesture on its corner handle
        var note = host.Page.AddAnnotation("note", new PlexusRect(150, 150, 150, 60));
        var undo = host.Diagram.History.UndoCount;
        var handle = new Point(297, 207);
        var gesture = new ResizeGesture(host.Editor, handle, note);
        var end = new Point(handle.X + 40, handle.Y + 30);

        // Act: drag and release
        gesture.Move(end, KeyModifiers.None);
        gesture.Complete(end, KeyModifiers.None);

        // Assert: the top-left stayed, the corner snapped to the grid, one history step, resize cursor
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), note.Bounds.Position),
            () => Assert.Equal(new PlexusPoint(330, 240), new PlexusPoint(note.Bounds.Right, note.Bounds.Bottom)),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Equal(StandardCursorType.BottomRightCorner, gesture.Cursor));
    });

    /// <summary>
    ///     Validates that the resize drag never shrinks an annotation below the minimum size.
    /// </summary>
    [Fact]
    public Task ResizeGesture_Move_PointerPastTopLeft_ClampsToMinimumSize() => UiTest.Run(host =>
    {
        // Arrange: an annotation and a gesture on its corner handle
        var note = host.Page.AddAnnotation("note", new PlexusRect(150, 150, 150, 60));
        var handle = new Point(297, 207);
        var gesture = new ResizeGesture(host.Editor, handle, note);

        // Act: drag the corner far past the top-left corner
        gesture.Move(new Point(100, 100), KeyModifiers.None);
        gesture.Complete(new Point(100, 100), KeyModifiers.None);

        // Assert: the box is exactly the minimum size and still anchored at its top-left corner
        Assert.Equal(new PlexusRect(150, 150, 30, 20), note.Bounds);
    });

    /// <summary>
    ///     Validates that cancelling a resize restores the original bounds and records nothing.
    /// </summary>
    [Fact]
    public Task ResizeGesture_Cancel_AfterDrag_RestoresBoundsAndHistory() => UiTest.Run(host =>
    {
        // Arrange: a resize in progress
        var note = host.Page.AddAnnotation("note", new PlexusRect(150, 150, 150, 60));
        var undo = host.Diagram.History.UndoCount;
        var handle = new Point(297, 207);
        var gesture = new ResizeGesture(host.Editor, handle, note);
        gesture.Move(new Point(handle.X + 60, handle.Y + 60), KeyModifiers.None);
        Assert.NotEqual(new PlexusRect(150, 150, 150, 60), note.Bounds);

        // Act: cancel
        gesture.Cancel();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new PlexusRect(150, 150, 150, 60), note.Bounds),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that the port stubs at both ends of a wire can be dragged (they get a jog), while a segment index outside the wire cannot.
    /// </summary>
    [Fact]
    public Task WireSegmentGesture_TryCreate_PortStubOrOutOfRangeSegment_StubsDraggableOutOfRangeNull() => UiTest.Run(host =>
    {
        // Arrange: a routed wire
        var (wire, _) = ConnectWithInteriorSegment(host);
        var count = wire.GetPoints().Count;
        var start = new Point(100, 100);

        // Act
        var first = WireSegmentGesture.TryCreate(host.Editor, start, wire, 0);
        var last = WireSegmentGesture.TryCreate(host.Editor, start, wire, count - 2);
        var outside = WireSegmentGesture.TryCreate(host.Editor, start, wire, count + 5);
        var negative = WireSegmentGesture.TryCreate(host.Editor, start, wire, -1);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(first),
            () => Assert.NotNull(last),
            () => Assert.Null(outside),
            () => Assert.Null(negative));
    });

    /// <summary>
    ///     Validates that an interior segment moves perpendicular to itself, stays orthogonal and commits one undo entry.
    /// </summary>
    [Fact]
    public Task WireSegmentGesture_Complete_AfterDrag_MovesSegmentWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a wire with a movable interior segment, and a gesture started on its middle
        var (wire, index) = ConnectWithInteriorSegment(host);
        var before = wire.GetPoints();
        var a = before[index];
        var b = before[index + 1];
        var vertical = Math.Abs(a.X - b.X) < 1e-6;
        var mid = host.Editor.DiagramToScreen(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        var undo = host.Diagram.History.UndoCount;
        var gesture = WireSegmentGesture.TryCreate(host.Editor, mid, wire, index);
        Assert.NotNull(gesture);
        var end = vertical ? new Point(mid.X + 45, mid.Y) : new Point(mid.X, mid.Y + 45);

        // Act: drag the segment sideways and release
        gesture.Move(end, KeyModifiers.None);
        gesture.Complete(end, KeyModifiers.None);

        // Assert: both ends of the segment moved together, the port stubs stayed, one history step
        var after = wire.GetPoints();
        var moved = vertical ? after[index].X - a.X : after[index].Y - a.Y;
        Assert.Multiple(
            () => Assert.True(Math.Abs(moved) >= 30, $"moved {moved}"),
            () => Assert.Equal(vertical ? after[index].X : after[index].Y, vertical ? after[index + 1].X : after[index + 1].Y),
            () => Assert.Equal(before[0], after[0]),
            () => Assert.Equal(before[^1], after[^1]),
            () => Assert.Equal(vertical ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth, gesture.Cursor),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that cancelling a segment drag restores the original route and records nothing.
    /// </summary>
    [Fact]
    public Task WireSegmentGesture_Cancel_AfterDrag_RestoresRoute() => UiTest.Run(host =>
    {
        // Arrange: a segment drag in progress
        var (wire, index) = ConnectWithInteriorSegment(host);
        var before = wire.GetPoints();
        var a = before[index];
        var b = before[index + 1];
        var mid = host.Editor.DiagramToScreen(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        var undo = host.Diagram.History.UndoCount;
        var gesture = WireSegmentGesture.TryCreate(host.Editor, mid, wire, index);
        Assert.NotNull(gesture);
        gesture.Move(new Point(mid.X + 45, mid.Y + 45), KeyModifiers.None);

        // Act: cancel
        gesture.Cancel();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(before, wire.GetPoints()),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that panning keeps the diagram point that was under the pointer at the press under the pointer, at any zoom.
    /// </summary>
    [Fact]
    public Task PanGesture_Move_ZoomedView_KeepsPressedDiagramPointUnderPointer() => UiTest.Run(host =>
    {
        // Arrange: a zoomed and scrolled view and a pan started at a point
        host.Editor.Zoom = 2;
        host.Editor.Offset = new Point(100, 50);
        var start = new Point(300, 200);
        var anchor = host.Editor.ScreenToDiagram(start);
        var undo = host.Diagram.History.UndoCount;
        var gesture = new PanGesture(host.Editor, start);
        var end = new Point(350, 260);

        // Act: drag
        gesture.Move(end, KeyModifiers.None);

        // Assert: the same diagram point is now under the new pointer position; the model is untouched
        var under = host.Editor.ScreenToDiagram(end);
        Assert.Multiple(
            () => Assert.Equal(anchor.X, under.X, 6),
            () => Assert.Equal(anchor.Y, under.Y, 6),
            () => Assert.Equal(StandardCursorType.SizeAll, gesture.Cursor),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that a band selects the nodes it touches, updates live as it shrinks, and ignores wobbles below the threshold.
    /// </summary>
    [Fact]
    public Task RubberBandGesture_Move_Band_SelectsTouchedNodesLive() => UiTest.Run(host =>
    {
        // Arrange: two nodes far apart and a band started on empty canvas
        var a = host.AddSource(150, 150);
        var b = host.AddSink(450, 300);
        var gesture = new RubberBandGesture(host.Editor, new Point(100, 100), false);

        // Act / Assert: a one-pixel wobble selects nothing
        gesture.Move(new Point(101, 101), KeyModifiers.None);
        Assert.Empty(host.Editor.Selection);

        // Act / Assert: growing the band over A selects only A
        gesture.Move(host.CenterOf(a), KeyModifiers.None);
        Assert.Equal([a], host.Editor.Selection);

        // Act / Assert: growing over B selects both, shrinking back deselects B again, completing keeps the selection
        gesture.Move(host.CenterOf(b), KeyModifiers.None);
        var both = host.Editor.Selection.ToList();
        gesture.Move(host.CenterOf(a), KeyModifiers.None);
        gesture.Complete(host.CenterOf(a), KeyModifiers.None);
        Assert.Multiple(
            () => Assert.Equal(2, both.Count),
            () => Assert.Equal([a], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that an additive band keeps the previous selection.
    /// </summary>
    [Fact]
    public Task RubberBandGesture_Move_Additive_KeepsPreviousSelection() => UiTest.Run(host =>
    {
        // Arrange: B already selected, band started additively
        var a = host.AddSource(150, 150);
        var b = host.AddSink(450, 300);
        host.Editor.Select(b);
        var gesture = new RubberBandGesture(host.Editor, new Point(100, 100), true);

        // Act: sweep over A
        gesture.Move(host.CenterOf(a), KeyModifiers.Shift);

        // Assert: both are selected
        Assert.Equal(2, host.Editor.Selection.Count);
    });

    /// <summary>
    ///     Validates that an expanded group is selected only when the band swallows it whole, while a node inside is selected when touched.
    /// </summary>
    [Fact]
    public Task RubberBandGesture_Move_ExpandedGroup_SelectedOnlyWhenEnclosed() => UiTest.Run(host =>
    {
        // Arrange: a group of two nodes
        var a = host.AddSource(150, 150);
        var b = host.AddSink(330, 150);
        var group = host.Page.CreateGroup([a, b], "Pair");
        var partial = new RubberBandGesture(host.Editor, new Point(100, 100), false);
        var whole = new RubberBandGesture(host.Editor, new Point(5, 5), false);

        // Act: touch only A, then enclose everything
        partial.Move(host.CenterOf(a), KeyModifiers.None);
        var touched = host.Editor.Selection.ToList();
        whole.Move(new Point(790, 590), KeyModifiers.None);
        var enclosed = host.Editor.Selection.ToList();

        // Assert
        Assert.Multiple(
            () => Assert.Contains(a, touched),
            () => Assert.DoesNotContain(group, touched),
            () => Assert.Contains(group, enclosed),
            () => Assert.Contains(a, enclosed),
            () => Assert.Contains(b, enclosed));
    });

    /// <summary>
    ///     Validates that a band never records history and that cancelling it leaves the live selection alone.
    /// </summary>
    [Fact]
    public Task RubberBandGesture_Cancel_AfterMove_RecordsNoHistory() => UiTest.Run(host =>
    {
        // Arrange: a band over a node
        var a = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;
        var gesture = new RubberBandGesture(host.Editor, new Point(100, 100), false);
        gesture.Move(host.CenterOf(a), KeyModifiers.None);

        // Act
        gesture.Cancel();

        // Assert
        Assert.Equal(undo, host.Diagram.History.UndoCount);
    });

    /// <summary>
    ///     Connects a source and a sink far enough apart that the wire has a movable interior segment.
    /// </summary>
    /// <param name="host">The editor host.</param>
    /// <returns>The wire and the index of its first interior movable segment.</returns>
    private static (Wire Wire, int Index) ConnectWithInteriorSegment(EditorHost host)
    {
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(450, 300);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Flush();
        var points = wire.GetPoints();
        for (var i = 1; i <= points.Count - 3; i++)
        {
            var horizontal = Math.Abs(points[i].Y - points[i + 1].Y) < 1e-6;
            var vertical = Math.Abs(points[i].X - points[i + 1].X) < 1e-6;
            if (horizontal != vertical)
            {
                return (wire, i);
            }
        }

        throw new InvalidOperationException("The test needs a wire with a movable interior segment.");
    }
}
