using Avalonia;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.Interaction;
using DemaConsulting.Plexus.Scene;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Headless tests of wire editing in <see cref="DiagramEditor"/>: bend handles, port-stub drags, double-click jogs and endpoint reconnection, driven with real pointer input.
/// </summary>
public class DiagramEditorWireEditingTests
{
    /// <summary>
    ///     Tolerance for coordinate comparisons.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Validates that a selected wire shows bend handles that hit testing reports with their bend index.
    /// </summary>
    [Fact]
    public Task DiagramEditor_HitTest_SelectedWire_ReportsBendAndEndHandles() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);

        // Act
        var bend = host.Editor.HitTest(Screen(host, wire.Route[0]));
        var source = host.Editor.HitTest(Screen(host, wire.Source.Anchor));
        var target = host.Editor.HitTest(Screen(host, wire.Target.Anchor));

        // Assert
        Assert.Multiple(
            () => Assert.Equal((HitKind.WireBend, 0), (bend.Kind, bend.BendIndex)),
            () => Assert.Equal(HitKind.WireSourceHandle, source.Kind),
            () => Assert.Equal(HitKind.WireTargetHandle, target.Kind));
    });

    /// <summary>
    ///     Validates that dragging a bend handle moves the bend, keeps the wire orthogonal and records one undo entry that undoes completely.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragBendHandle_MovesBendOrthogonallyAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var before = wire.Route.ToArray();
        var bend = wire.Route[1];
        var undo = host.Diagram.History.UndoCount;
        var expected = new PlexusPoint(host.Editor.Snap(bend.X + 45), host.Editor.Snap(bend.Y - 30));

        // Act
        host.Drag(Screen(host, bend), new Point(bend.X + 45, bend.Y - 30));

        // Assert: the bend is where it was dragged to (on the grid), the wire is orthogonal, one step recorded
        Assert.Multiple(
            () => Assert.Contains(expected, wire.Route),
            () => Assert.True(IsOrthogonal(wire.GetPoints())),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Equal([wire], host.Editor.Selection));
        host.Diagram.History.Undo();
        Assert.Equal(before, wire.Route);
    });

    /// <summary>
    ///     Validates that Escape during a bend drag restores the wire and records nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragBendHandle_EscapeCancels() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var before = wire.Route.ToArray();
        var undo = host.Diagram.History.UndoCount;
        var bend = Screen(host, wire.Route[1]);

        // Act
        host.BeginDrag(bend);
        host.MoveTo(new Point(bend.X + 60, bend.Y + 45));
        Assert.NotEqual(before, wire.Route);
        host.Press(Key.Escape);
        host.EndDrag(new Point(bend.X + 60, bend.Y + 45));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(before, wire.Route),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that a read-only editor does not reshape wires.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragBendHandle_ReadOnly_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var before = wire.Route.ToArray();
        host.Editor.IsReadOnly = true;
        var bend = Screen(host, wire.Route[1]);

        // Act
        host.Drag(bend, new Point(bend.X + 60, bend.Y + 45));

        // Assert
        Assert.Equal(before, wire.Route);
    });

    /// <summary>
    ///     Validates that dragging a port stub inserts the bends needed to move it, keeps the ports put and records one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragPortStub_InsertsJogAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: grab the first segment (the stub leaving the source) halfway along
        var (wire, _, _) = ConnectAndSelect(host);
        var points = wire.GetPoints();
        var before = wire.Route.ToArray();
        var middle = new PlexusPoint((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
        var undo = host.Diagram.History.UndoCount;
        Assert.Equal((HitKind.Wire, 0), (host.Editor.HitTest(Screen(host, middle)).Kind, host.Editor.HitTest(Screen(host, middle)).SegmentIndex));

        // Act: pull it down by three grid steps
        host.Drag(Screen(host, middle), new Point(middle.X, middle.Y + 45));

        // Assert: more bends, ports unmoved, still orthogonal, the segment really moved, one history entry
        var after = wire.GetPoints();
        Assert.Multiple(
            () => Assert.True(wire.Route.Count > before.Length),
            () => Assert.Equal(points[0], after[0]),
            () => Assert.Equal(points[^1], after[^1]),
            () => Assert.True(IsOrthogonal(after)),
            () => Assert.Contains(wire.Route, p => Math.Abs(p.Y - (points[0].Y + 45)) < Epsilon),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
        host.Diagram.History.Undo();
        Assert.Equal(before, wire.Route);
    });

    /// <summary>
    ///     Validates that double-clicking a wire segment inserts a jog (extra bends) as one undo entry and selects the wire.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DoubleClickSegment_InsertsJogAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: an unselected wire; double-click the middle of its long vertical segment
        var (wire, _, _) = ConnectAndSelect(host);
        host.Editor.ClearSelection();
        var points = wire.GetPoints();
        var before = wire.Route.ToArray();
        var middle = new PlexusPoint((points[1].X + points[2].X) / 2, (points[1].Y + points[2].Y) / 2);
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.DoubleClick(Screen(host, middle));

        // Assert: four more bends, selected, one history entry that undoes completely
        Assert.Multiple(
            () => Assert.Equal(before.Length + 4, wire.Route.Count),
            () => Assert.True(IsOrthogonal(wire.GetPoints())),
            () => Assert.Equal([wire], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
        host.Diagram.History.Undo();
        Assert.Equal(before, wire.Route);
    });

    /// <summary>
    ///     Validates that the inserted jog can be dragged like any segment and flattened again by dragging it back.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragJogSegment_ReshapesAndFlattens() => UiTest.Run(host =>
    {
        // Arrange: a jog on the long vertical segment
        var (wire, _, _) = ConnectAndSelect(host);
        var points = wire.GetPoints();
        var before = wire.Route.ToArray();
        Assert.True(wire.AddJog(1, new PlexusPoint((points[1].X + points[2].X) / 2, (points[1].Y + points[2].Y) / 2)));
        host.Flush();
        var jog = wire.GetPoints();

        // Act: grab the jog's middle piece (index 3, a horizontal segment between two new bends or its vertical equivalent) and put it back
        var grab = new PlexusPoint((jog[3].X + jog[4].X) / 2, (jog[3].Y + jog[4].Y) / 2);
        var offsetX = jog[3].X - jog[2].X;
        host.Drag(Screen(host, grab), new Point(grab.X - offsetX, grab.Y));

        // Assert: the bump is gone, merged away by the auto-clean
        Assert.Equal(before, wire.Route);
    });

    /// <summary>
    ///     Validates that dragging the target handle onto another compatible input reconnects the wire, keeping its id, in one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragTargetHandle_OverValidPort_ReconnectsWire() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var other = host.AddSink(450, 450);
        host.Flush();
        var id = wire.Id;
        var oldTarget = wire.Target;
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.Drag(Screen(host, wire.Target.Anchor), host.PortOf(other));

        // Assert
        Assert.Multiple(
            () => Assert.Same(other.Port("value"), wire.Target),
            () => Assert.Equal(id, wire.Id),
            () => Assert.Same(wire, Assert.Single(host.Page.Wires)),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
        host.Diagram.History.Undo();
        Assert.Same(oldTarget, wire.Target);
        host.Diagram.History.Redo();
        Assert.Same(other.Port("value"), wire.Target);
    });

    /// <summary>
    ///     Validates that dragging the source handle onto another output moves the source end.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragSourceHandle_OverValidPort_ReconnectsWire() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var other = host.AddSource(60, 300);
        host.Flush();

        // Act
        host.Drag(Screen(host, wire.Source.Anchor), host.PortOf(other));

        // Assert
        Assert.Multiple(
            () => Assert.Same(other.Port("value"), wire.Source),
            () => Assert.Single(host.Page.Wires));
    });

    /// <summary>
    ///     Validates that dropping an end on a port the connection rules reject, or on a port of the wrong direction, leaves the wire alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragEndHandle_OverInvalidPort_ChangesNothing() => UiTest.Run(host =>
    {
        // Arrange: only matching port types may connect, and there are an integer input and a second output to drop on
        host.Diagram.ConnectionRules = ConnectionRules.SameType;
        var (wire, source, sink) = ConnectAndSelect(host);
        var integer = host.Page.AddNode("test.IntSink", 450, 450);
        var output = host.AddSource(60, 300);
        host.Flush();
        var undo = host.Diagram.History.UndoCount;
        var before = wire.Route.ToArray();

        // Act: the target end onto the integer input (type mismatch), then onto an output (wrong direction)
        host.Drag(Screen(host, wire.Target.Anchor), host.PortOf(integer));
        host.Drag(Screen(host, wire.Target.Anchor), host.PortOf(output));

        // Assert
        Assert.Multiple(
            () => Assert.Same(sink.Port("value"), wire.Target),
            () => Assert.Same(source.Port("value"), wire.Source),
            () => Assert.Equal(before, wire.Route),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that dropping an end on empty canvas cancels the reconnection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DragEndHandle_DroppedOnCanvas_Cancels() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, sink) = ConnectAndSelect(host);
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.Drag(Screen(host, wire.Target.Anchor), new Point(700, 500));

        // Assert
        Assert.Multiple(
            () => Assert.Same(sink.Port("value"), wire.Target),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates the gesture cursors: bends show a move cursor and end handles a cross.
    /// </summary>
    [Fact]
    public Task WireGestures_Cursor_BendMoveAndEndCross() => UiTest.Run(host =>
    {
        // Arrange
        var (wire, _, _) = ConnectAndSelect(host);
        var start = new Point(100, 100);

        // Act
        var bend = WireBendGesture.TryCreate(host.Editor, start, wire, 0);
        var end = new WireEndGesture(host.Editor, start, wire, dragSource: false);
        var missing = WireBendGesture.TryCreate(host.Editor, start, wire, 99);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(StandardCursorType.SizeAll, bend?.Cursor),
            () => Assert.Equal(StandardCursorType.Cross, end.Cursor),
            () => Assert.Null(missing));
    });

    /// <summary>
    ///     Connects a source and a sink far enough apart that the wire has several bends, and selects the wire.
    /// </summary>
    /// <param name="host">The editor host.</param>
    /// <returns>The wire, its source node and its sink node.</returns>
    private static (Wire Wire, Node Source, Node Sink) ConnectAndSelect(EditorHost host)
    {
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(450, 300);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select(wire);
        host.Flush();
        Assert.True(wire.Route.Count >= 2, "The test needs a wire with at least two bends.");
        return (wire, source, sink);
    }

    /// <summary>
    ///     Converts a diagram point to the editor's screen coordinates.
    /// </summary>
    /// <param name="host">The editor host.</param>
    /// <param name="point">The diagram point.</param>
    /// <returns>The screen point.</returns>
    private static Point Screen(EditorHost host, PlexusPoint point) => host.Editor.DiagramToScreen(new Point(point.X, point.Y));

    /// <summary>
    ///     Determines whether every segment of a polyline is horizontal or vertical.
    /// </summary>
    /// <param name="points">The polyline.</param>
    /// <returns><see langword="true"/> when all segments are axis-aligned.</returns>
    private static bool IsOrthogonal(IReadOnlyList<PlexusPoint> points)
    {
        for (var i = 0; i + 1 < points.Count; i++)
        {
            if (Math.Abs(points[i].X - points[i + 1].X) > Epsilon && Math.Abs(points[i].Y - points[i + 1].Y) > Epsilon)
            {
                return false;
            }
        }

        return true;
    }
}
