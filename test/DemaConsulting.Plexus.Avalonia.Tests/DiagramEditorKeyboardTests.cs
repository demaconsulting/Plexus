using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the keyboard shortcuts.
/// </summary>
public class DiagramEditorKeyboardTests
{
    /// <summary>
    ///     Validates that Delete removes the selected node and its wires as one undoable step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_SelectedNode_RemovesNodeAndWires() => UiTest.Run(host =>
    {
        // Arrange: a connected pair with the source selected
        var source = host.AddSource(100, 100);
        var sink = host.AddSink(400, 100);
        source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select(source);
        var undo = host.Diagram.History.UndoCount;

        // Act: press Delete
        host.Press(Key.Delete);

        // Assert: the source and its wire are gone, the sink stays, one history step, empty selection
        Assert.Multiple(
            () => Assert.DoesNotContain(source, host.Page.Nodes),
            () => Assert.Contains(sink, host.Page.Nodes),
            () => Assert.Empty(host.Page.Wires),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Empty(host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that Ctrl+Z undoes and Ctrl+Y redoes.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlZAndCtrlY_UndoAndRedo() => UiTest.Run(host =>
    {
        // Arrange: a node that exists because of a recorded change
        var node = host.AddSource(100, 100);

        // Act / Assert: Ctrl+Z removes it
        host.Press(Key.Z, RawInputModifiers.Control);
        Assert.DoesNotContain(node, host.Page.Nodes);

        // Act / Assert: Ctrl+Y brings it back
        host.Press(Key.Y, RawInputModifiers.Control);
        Assert.Contains(node, host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that Ctrl+Shift+Z is an alternative redo.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlShiftZ_Redoes() => UiTest.Run(host =>
    {
        // Arrange: an undone addition
        var node = host.AddSource(100, 100);
        host.Diagram.History.Undo();

        // Act: Ctrl+Shift+Z
        host.Press(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift);

        // Assert: redone
        Assert.Contains(node, host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that Ctrl+A selects every element.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlA_SelectsAllElements() => UiTest.Run(host =>
    {
        // Arrange: two nodes and a wire
        var source = host.AddSource(100, 100);
        var sink = host.AddSink(400, 100);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));

        // Act: Ctrl+A
        host.Press(Key.A, RawInputModifiers.Control);

        // Assert: all three are selected
        Assert.Multiple(
            () => Assert.Equal(3, host.Editor.Selection.Count),
            () => Assert.Contains(wire, host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that Ctrl+D duplicates the selection, offset by a grid cell, and selects the copy.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlD_DuplicatesSelection() => UiTest.Run(host =>
    {
        // Arrange: a selected node
        var node = host.AddSource(150, 150);
        host.Editor.Select(node);
        var undo = host.Diagram.History.UndoCount;

        // Act: Ctrl+D
        host.Press(Key.D, RawInputModifiers.Control);

        // Assert: one copy at +grid, selected, one history step
        var copy = Assert.Single(host.Page.Nodes, n => n != node);
        Assert.Multiple(
            () => Assert.Equal(new Geometry.Point(165, 165), copy.Position),
            () => Assert.Equal([copy], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that F2 starts renaming the single selected node.
    /// </summary>
    [Fact]
    public Task DiagramEditor_F2_SelectedNode_StartsRename() => UiTest.Run(host =>
    {
        // Arrange: a selected node
        var node = host.AddSource(150, 150);
        host.Editor.Select(node);

        // Act: F2
        host.Press(Key.F2);

        // Assert: the inline editor is open with the node's name
        Assert.Multiple(
            () => Assert.True(host.Editor.IsRenaming),
            () => Assert.Equal(node.Name, host.Editor.RenameEditor!.Text));
    });

    /// <summary>
    ///     Validates that editing shortcuts do nothing in a read-only editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_ReadOnly_DoesNothing() => UiTest.Run(host =>
    {
        // Arrange: a selected node in a read-only editor
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        host.Editor.IsReadOnly = true;

        // Act: Delete and Ctrl+Z
        host.Press(Key.Delete);
        host.Press(Key.Z, RawInputModifiers.Control);

        // Assert: the node is still there
        Assert.Contains(node, host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that Space+drag pans the view.
    /// </summary>
    [Fact]
    public Task DiagramEditor_SpaceDrag_PansView() => UiTest.Run(host =>
    {
        // Arrange: a node under the pointer, and Space held down
        var node = host.AddSource(150, 150);
        host.Editor.Focus();
        host.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);

        // Act: drag from the node while Space is down
        var start = host.CenterOf(node);
        host.Drag(start, new Point(start.X + 40, start.Y + 20));
        host.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);

        // Assert: the view scrolled, the node did not move
        Assert.Multiple(
            () => Assert.Equal(new Point(-40, -20), host.Editor.Offset),
            () => Assert.Equal(new Geometry.Point(150, 150), node.Position));
    });
}
