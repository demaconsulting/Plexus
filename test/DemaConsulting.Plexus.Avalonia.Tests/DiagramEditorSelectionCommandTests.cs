using Avalonia;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the selection and view commands of the editor: group, ungroup, collapse, move to page, zoom steps, view center and annotations.
/// </summary>
public class DiagramEditorSelectionCommandTests
{
    /// <summary>
    ///     Validates that GroupSelection groups the selected nodes as one undo entry, selects the group and ignores wires.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GroupSelection_GroupsSelectionAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: two wired nodes and a wire, all selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var wire = host.Page.Connect(a.Port("value"), b.Port("value"));
        host.Editor.Select([a, b, wire]);
        var undoCount = host.Diagram.History.UndoCount;

        // Act
        var group = host.Editor.GroupSelection("Pair");

        // Assert
        Assert.NotNull(group);
        Assert.Equal("Pair", group.Title);
        Assert.Equal([a, b], group.Members);
        Assert.Equal([group], host.Editor.Selection);
        Assert.Equal(undoCount + 1, host.Diagram.History.UndoCount);
        host.Diagram.History.Undo();
        Assert.Empty(host.Page.Groups);
    });

    /// <summary>
    ///     Validates that GroupSelection does nothing when nothing can be grouped or the editor is read-only.
    /// </summary>
    [Fact]
    public Task DiagramEditor_GroupSelection_NothingToGroupOrReadOnly_ReturnsNull() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var wire = host.Page.Connect(a.Port("value"), b.Port("value"));
        var undoCount = host.Diagram.History.UndoCount;

        // Act / Assert: empty selection, a lone wire, and a read-only editor
        Assert.Null(host.Editor.GroupSelection("None"));
        host.Editor.Select(wire);
        Assert.Null(host.Editor.GroupSelection("Wire"));
        host.Editor.Select(a);
        host.Editor.IsReadOnly = true;
        Assert.Null(host.Editor.GroupSelection("ReadOnly"));
        Assert.Equal(undoCount, host.Diagram.History.UndoCount);
        Assert.Throws<ArgumentNullException>(() => host.Editor.GroupSelection(null!));
    });

    /// <summary>
    ///     Validates that UngroupSelection dissolves every selected group in one undo entry and keeps the members.
    /// </summary>
    [Fact]
    public Task DiagramEditor_UngroupSelection_DissolvesSelectedGroupsAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var c = host.AddSource(100, 300);
        var first = host.Page.CreateGroup([a], "One");
        var second = host.Page.CreateGroup([b], "Two");
        host.Editor.Select([first, second, c]);
        var undoCount = host.Diagram.History.UndoCount;

        // Act
        var count = host.Editor.UngroupSelection();

        // Assert
        Assert.Equal(2, count);
        Assert.Empty(host.Page.Groups);
        Assert.Equal(3, host.Page.Nodes.Count);
        Assert.Empty(host.Editor.Selection);
        Assert.Equal(undoCount + 1, host.Diagram.History.UndoCount);
        host.Diagram.History.Undo();
        Assert.Equal(2, host.Page.Groups.Count);
    });

    /// <summary>
    ///     Validates that UngroupSelection is a no-op without a selected group or in a read-only editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_UngroupSelection_NoGroupOrReadOnly_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var group = host.Page.CreateGroup([a], "One");
        var undoCount = host.Diagram.History.UndoCount;

        // Act / Assert: a selected member is not a selected group
        host.Editor.Select(a);
        Assert.Equal(0, host.Editor.UngroupSelection());
        host.Editor.Select(group);
        host.Editor.IsReadOnly = true;
        Assert.Equal(0, host.Editor.UngroupSelection());
        Assert.Equal(undoCount, host.Diagram.History.UndoCount);
        Assert.Single(host.Page.Groups);
    });

    /// <summary>
    ///     Validates that ToggleCollapseSelection flips the group of a selected member (and a selected group) in one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToggleCollapseSelection_MemberStandsForItsGroup() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var c = host.AddSource(100, 300);
        var d = host.AddSink(400, 300);
        var one = host.Page.CreateGroup([a, b], "One");
        var two = host.Page.CreateGroup([c, d], "Two");
        two.IsCollapsed = true;
        host.Editor.Select([a, b]);
        var undoCount = host.Diagram.History.UndoCount;

        // Act: the two selected members stand for the same group, which counts once
        var first = host.Editor.ToggleCollapseSelection();

        // Assert
        Assert.Equal(1, first);
        Assert.True(one.IsCollapsed);
        Assert.Equal(undoCount + 1, host.Diagram.History.UndoCount);

        // Act: the frame itself, and a mixed selection that flips each group on its own
        host.Editor.Select([one, two]);
        var both = host.Editor.ToggleCollapseSelection();
        Assert.Equal(2, both);
        Assert.False(one.IsCollapsed);
        Assert.False(two.IsCollapsed);
        host.Diagram.History.Undo();
        Assert.True(one.IsCollapsed);
        Assert.True(two.IsCollapsed);
    });

    /// <summary>
    ///     Validates that ToggleCollapseSelection does nothing without a group involved or when read-only.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ToggleCollapseSelection_NoGroupOrReadOnly_ReturnsZero() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var group = host.Page.CreateGroup([b], "G");
        var undoCount = host.Diagram.History.UndoCount;

        // Act / Assert
        host.Editor.Select(a);
        Assert.Equal(0, host.Editor.ToggleCollapseSelection());
        host.Editor.Select(group);
        host.Editor.IsReadOnly = true;
        Assert.Equal(0, host.Editor.ToggleCollapseSelection());
        Assert.False(group.IsCollapsed);
        Assert.Equal(undoCount, host.Diagram.History.UndoCount);
    });

    /// <summary>
    ///     Validates that MoveSelectionToPage moves the selection as one undo entry, clears the selection and keeps the shown page.
    /// </summary>
    [Fact]
    public Task DiagramEditor_MoveSelectionToPage_MovesAsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var b = host.AddSink(400, 100);
        var c = host.AddSource(100, 300);
        var target = host.Diagram.Pages.Add("Target");
        host.Editor.Select([a, b]);
        var undoCount = host.Diagram.History.UndoCount;

        // Act
        var moved = host.Editor.MoveSelectionToPage(target);

        // Assert
        Assert.True(moved);
        Assert.Equal([c], host.Page.Nodes);
        Assert.Equal(2, target.Nodes.Count);
        Assert.Empty(host.Editor.Selection);
        Assert.Same(host.Page, host.Editor.Page);
        Assert.Equal(undoCount + 1, host.Diagram.History.UndoCount);
        host.Diagram.History.Undo();
        Assert.Equal(3, host.Page.Nodes.Count);
    });

    /// <summary>
    ///     Validates the cases in which MoveSelectionToPage refuses: empty selection, the shown page, a removed page, a page of another diagram and a read-only editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_MoveSelectionToPage_NotApplicable_ReturnsFalse() => UiTest.Run(host =>
    {
        // Arrange
        var a = host.AddSource(100, 100);
        var target = host.Diagram.Pages.Add("Target");
        var removed = host.Diagram.Pages.Add("Removed");
        host.Diagram.Pages.Remove(removed);
        var foreign = new Diagram(host.Diagram.Types).Pages.Add("Foreign");
        var undoCount = host.Diagram.History.UndoCount;

        // Act / Assert
        Assert.False(host.Editor.MoveSelectionToPage(target));
        host.Editor.Select(a);
        Assert.False(host.Editor.MoveSelectionToPage(host.Page));
        Assert.False(host.Editor.MoveSelectionToPage(removed));
        Assert.False(host.Editor.MoveSelectionToPage(foreign));
        host.Editor.IsReadOnly = true;
        Assert.False(host.Editor.MoveSelectionToPage(target));
        Assert.Equal(undoCount, host.Diagram.History.UndoCount);
        Assert.Throws<ArgumentNullException>(() => host.Editor.MoveSelectionToPage(null!));
    });

    /// <summary>
    ///     Validates that ViewCenter is the diagram point under the middle of the control and follows zoom and scrolling.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ViewCenter_FollowsZoomAndOffset() => UiTest.Run(host =>
    {
        // Arrange
        var middle = new Point(host.Editor.Bounds.Width / 2, host.Editor.Bounds.Height / 2);

        // Act / Assert: at zoom 1 without scrolling the center is the middle of the control
        Assert.Equal(new PlexusPoint(middle.X, middle.Y), host.Editor.ViewCenter);
        host.Editor.Zoom = 2;
        host.Editor.Offset = new Point(100, 50);
        var expected = host.Editor.ScreenToDiagram(middle);
        Assert.Equal(new PlexusPoint(expected.X, expected.Y), host.Editor.ViewCenter);
        Assert.Equal(new PlexusPoint((middle.X / 2) + 100, (middle.Y / 2) + 50), host.Editor.ViewCenter);
    });

    /// <summary>
    ///     Validates that ZoomIn and ZoomOut step by 1.25 around the view center, which stays fixed, and respect the limits.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ZoomInAndOut_StepAroundTheCenterAndClamp() => UiTest.Run(host =>
    {
        // Arrange
        var before = host.Editor.ViewCenter;

        // Act
        host.Editor.ZoomIn();
        var inZoom = host.Editor.Zoom;
        var inCenter = host.Editor.ViewCenter;
        host.Editor.ZoomOut();
        host.Editor.ZoomOut();
        var outZoom = host.Editor.Zoom;

        // Assert
        Assert.Equal(DiagramEditor.ZoomStep, inZoom, 9);
        Assert.Equal(before.X, inCenter.X, 6);
        Assert.Equal(before.Y, inCenter.Y, 6);
        Assert.Equal(1 / DiagramEditor.ZoomStep, outZoom, 9);
        host.Editor.Zoom = DiagramEditor.MaxZoom;
        host.Editor.ZoomIn();
        Assert.Equal(DiagramEditor.MaxZoom, host.Editor.Zoom);
        host.Editor.Zoom = DiagramEditor.MinZoom;
        host.Editor.ZoomOut();
        Assert.Equal(DiagramEditor.MinZoom, host.Editor.Zoom);
    });

    /// <summary>
    ///     Validates that SnapPoint rounds to the grid only while snapping is on.
    /// </summary>
    [Fact]
    public Task DiagramEditor_SnapPoint_FollowsSnapToGrid() => UiTest.Run(host =>
    {
        // Arrange
        var grid = host.Diagram.Theme.GridSize;
        var point = new PlexusPoint((grid * 3) + 4, (grid * 5) - 4);

        // Act / Assert
        host.Editor.SnapToGrid = true;
        Assert.Equal(new PlexusPoint(grid * 3, grid * 5), host.Editor.SnapPoint(point));
        host.Editor.SnapToGrid = false;
        Assert.Equal(point, host.Editor.SnapPoint(point));
    });

    /// <summary>
    ///     Validates that AddAnnotationAtCenter adds a snapped, selected annotation centered on the view as one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AddAnnotationAtCenter_AddsSelectedAnnotationAtTheViewCenter() => UiTest.Run(host =>
    {
        // Arrange
        host.Editor.SnapToGrid = true;
        var undoCount = host.Diagram.History.UndoCount;
        var center = host.Editor.ViewCenter;

        // Act
        var note = host.Editor.AddAnnotationAtCenter("Remember");

        // Assert
        Assert.NotNull(note);
        Assert.Equal("Remember", note.Text);
        Assert.Equal([note], host.Editor.Selection);
        Assert.Equal(undoCount + 1, host.Diagram.History.UndoCount);
        var grid = host.Diagram.Theme.GridSize;
        Assert.Equal(0, note.Bounds.X % grid);
        Assert.Equal(0, note.Bounds.Y % grid);
        Assert.True(Math.Abs(note.Bounds.Center.X - center.X) <= (grid / 2) + 0.001);
        Assert.True(Math.Abs(note.Bounds.Center.Y - center.Y) <= (grid / 2) + 0.001);
        host.Diagram.History.Undo();
        Assert.Empty(host.Page.Annotations);
    });

    /// <summary>
    ///     Validates that AddAnnotationAtCenter adds nothing to a read-only editor and rejects null text.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AddAnnotationAtCenter_ReadOnly_ReturnsNull() => UiTest.Run(host =>
    {
        // Arrange
        host.Editor.IsReadOnly = true;

        // Act / Assert
        Assert.Null(host.Editor.AddAnnotationAtCenter("x"));
        Assert.Empty(host.Page.Annotations);
        Assert.Throws<ArgumentNullException>(() => host.Editor.AddAnnotationAtCenter(null!));
    });

    /// <summary>
    ///     Validates that the commands do nothing for an editor without a page.
    /// </summary>
    [Fact]
    public Task DiagramEditor_SelectionCommands_WithoutPage_DoNothing() => UiTest.Run(host =>
    {
        // Arrange
        var editor = new DiagramEditor();
        var target = host.Diagram.Pages.Add("Target");

        // Act / Assert
        Assert.Null(editor.GroupSelection("x"));
        Assert.Equal(0, editor.UngroupSelection());
        Assert.Equal(0, editor.ToggleCollapseSelection());
        Assert.False(editor.MoveSelectionToPage(target));
        Assert.Null(editor.AddAnnotationAtCenter("x"));
    });
}
