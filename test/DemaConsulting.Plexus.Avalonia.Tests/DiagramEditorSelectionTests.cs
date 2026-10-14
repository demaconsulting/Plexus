using Avalonia;
using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of click and rubber-band selection.
/// </summary>
public class DiagramEditorSelectionTests
{
    /// <summary>
    ///     Validates that clicking a node selects it and raises SelectionChanged once.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_OnNode_SelectsIt() => UiTest.Run(host =>
    {
        // Arrange: two nodes and a counter for the selection event
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var changes = 0;
        host.Editor.SelectionChanged += (_, _) => changes++;

        // Act: click the first node
        host.Click(host.CenterOf(a));

        // Assert: exactly that node is selected and the event fired once
        Assert.Multiple(
            () => Assert.Equal([a], host.Editor.Selection),
            () => Assert.DoesNotContain(b, host.Editor.Selection),
            () => Assert.Equal(1, changes));
    });

    /// <summary>
    ///     Validates that clicking another node replaces the selection, and clicking the canvas clears it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_OtherNodeThenCanvas_ReplacesThenClears() => UiTest.Run(host =>
    {
        // Arrange: two nodes with the first selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        host.Click(host.CenterOf(a));

        // Act / Assert: clicking the other node replaces the selection
        host.Click(host.CenterOf(b));
        Assert.Equal([b], host.Editor.Selection);

        // Act / Assert: clicking empty canvas clears it
        host.Click(new Point(300, 450));
        Assert.Empty(host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that Shift-click adds to the selection and Ctrl-click on a selected node removes it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ShiftAndCtrlClick_AddAndToggle() => UiTest.Run(host =>
    {
        // Arrange: two nodes with the first selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        host.Click(host.CenterOf(a));

        // Act / Assert: Shift-click adds the second
        host.Click(host.CenterOf(b), RawInputModifiers.Shift);
        Assert.Equal(2, host.Editor.Selection.Count);

        // Act / Assert: Ctrl-click on a selected node toggles it off
        host.Click(host.CenterOf(a), RawInputModifiers.Control);
        Assert.Equal([b], host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that clicking a node that is already one of several selected narrows the selection to it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_OnSelectedNodeAmongSeveral_NarrowsSelection() => UiTest.Run(host =>
    {
        // Arrange: two nodes, both selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        host.Editor.Select([a, b]);

        // Act: plain click on one of them, without dragging
        host.Click(host.CenterOf(a));

        // Assert: only that one remains
        Assert.Equal([a], host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that dragging a rectangle over the canvas selects the nodes it touches.
    /// </summary>
    [Fact]
    public Task DiagramEditor_RubberBand_OverNodes_SelectsTouchedNodes() => UiTest.Run(host =>
    {
        // Arrange: two nodes inside the band area and one far away
        var a = host.AddSource(100, 100);
        var b = host.AddSink(250, 100);
        var far = host.AddSink(600, 400);

        // Act: drag a band from empty canvas that covers the first two nodes only
        host.Drag(new Point(60, 60), new Point(380, 230));

        // Assert: exactly the two covered nodes are selected
        Assert.Multiple(
            () => Assert.Contains(a, host.Editor.Selection),
            () => Assert.Contains(b, host.Editor.Selection),
            () => Assert.DoesNotContain(far, host.Editor.Selection),
            () => Assert.Equal(2, host.Editor.Selection.Count));
    });

    /// <summary>
    ///     Validates that a Shift rubber band keeps the existing selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_RubberBand_WithShift_AddsToSelection() => UiTest.Run(host =>
    {
        // Arrange: one selected node far from the band
        var kept = host.AddSink(600, 400);
        var caught = host.AddSource(100, 100);
        host.Editor.Select(kept);

        // Act: shift-drag a band around the other node
        host.Drag(new Point(60, 60), new Point(260, 260), RawInputModifiers.Shift);

        // Assert: both are selected
        Assert.Equal(2, host.Editor.Selection.Count);
        Assert.Contains(kept, host.Editor.Selection);
        Assert.Contains(caught, host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that a rubber band never edits the diagram.
    /// </summary>
    [Fact]
    public Task DiagramEditor_RubberBand_DoesNotCreateUndoEntries() => UiTest.Run(host =>
    {
        // Arrange: a node and the current history depth
        host.AddSource(100, 100);
        var undo = host.Diagram.History.UndoCount;

        // Act: drag a band over it
        host.Drag(new Point(60, 60), new Point(260, 260));

        // Assert: history is unchanged
        Assert.Equal(undo, host.Diagram.History.UndoCount);
    });

    /// <summary>
    ///     Validates that clicking a wire selects the wire.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Click_OnWire_SelectsWire() => UiTest.Run(host =>
    {
        // Arrange: two nodes joined by a wire; pick a point on the middle of the wire's polyline
        var source = host.AddSource(100, 100);
        var sink = host.AddSink(400, 250);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Flush();
        var points = wire.GetPoints();
        var segment = points.Count / 2;
        var onWire = host.Editor.DiagramToScreen(new Point(
            (points[segment - 1].X + points[segment].X) / 2,
            (points[segment - 1].Y + points[segment].Y) / 2));

        // Act: click on it
        host.Click(onWire);

        // Assert: the wire is the selection
        Assert.Equal([wire], host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that selected elements that are removed from the page leave the selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Selection_ElementRemovedByUndo_IsDropped() => UiTest.Run(host =>
    {
        // Arrange: a freshly added, selected node
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);

        // Act: undo the add
        host.Diagram.History.Undo();

        // Assert: nothing is selected any more
        Assert.Empty(host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that Select ignores elements of other pages.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Select_ElementOnOtherPage_IsIgnored() => UiTest.Run(host =>
    {
        // Arrange: a node on a second page
        var other = host.Diagram.Pages.Add("Other").AddNode("test.Source", 10, 10);

        // Act: try to select it in the editor showing the first page
        host.Editor.Select(other);

        // Assert: the selection stays empty
        Assert.Empty(host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that changing the page clears the selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Page_Changed_ClearsSelection() => UiTest.Run(host =>
    {
        // Arrange: a selected node and a second page
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        var other = host.Diagram.Pages.Add("Other");

        // Act: show the other page
        host.Editor.Page = other;

        // Assert: selection is empty
        Assert.Empty(host.Editor.Selection);
    });
}
