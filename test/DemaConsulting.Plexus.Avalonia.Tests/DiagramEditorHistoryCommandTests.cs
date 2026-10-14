using Avalonia;
using Avalonia.Input;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the public <see cref="DiagramEditor.Delete"/>, <see cref="DiagramEditor.Undo"/> and <see cref="DiagramEditor.Redo"/> commands.
/// </summary>
public class DiagramEditorHistoryCommandTests
{
    /// <summary>
    ///     Validates that Delete removes the given elements without touching the selection, as one undo entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_GivenElements_RemovesThemAndLeavesSelectionAlone() => UiTest.Run(host =>
    {
        // Arrange: a connected pair plus a third node that stays selected
        var source = host.AddSource(100, 100);
        var sink = host.AddSink(400, 100);
        var keep = host.AddSink(400, 300);
        source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select(keep);
        var undo = host.Diagram.History.UndoCount;

        // Act: delete the source (and so its wire) without selecting it
        host.Editor.Delete([source]);

        // Assert
        Assert.Multiple(
            () => Assert.DoesNotContain(source, host.Page.Nodes),
            () => Assert.Empty(host.Page.Wires),
            () => Assert.Equal([keep], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // And undo restores both
        host.Diagram.History.Undo();
        Assert.Multiple(
            () => Assert.Contains(source, host.Page.Nodes),
            () => Assert.Single(host.Page.Wires));
    });

    /// <summary>
    ///     Validates that deleting a selected element through Delete also drops it from the selection.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_SelectedElement_DropsItFromSelection() => UiTest.Run(host =>
    {
        // Arrange
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);

        // Act
        host.Editor.Delete([node]);

        // Assert
        Assert.Empty(host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that Delete follows the collapsed-group rule: members go with a collapsed group, an expanded group leaves its members.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_Groups_FollowsCollapsedGroupRule() => UiTest.Run(host =>
    {
        // Arrange: one expanded and one collapsed group
        var a = host.AddSource(100, 100);
        var b = host.AddSource(100, 300);
        var expanded = host.Page.CreateGroup([a], "Open");
        var collapsed = host.Page.CreateGroup([b], "Closed");
        collapsed.IsCollapsed = true;

        // Act
        host.Editor.Delete([expanded, collapsed]);

        // Assert: the expanded group's member survives, the collapsed group's member is gone with it
        Assert.Multiple(
            () => Assert.Contains(a, host.Page.Nodes),
            () => Assert.DoesNotContain(b, host.Page.Nodes),
            () => Assert.Empty(host.Page.Groups));
    });

    /// <summary>
    ///     Validates that Delete ignores stale references, elements of other pages and duplicates, and records nothing for them.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_StaleOrForeignElements_AreIgnored() => UiTest.Run(host =>
    {
        // Arrange: a node removed earlier, and a node on another page
        var stale = host.AddSource(100, 100);
        host.Page.Remove(stale);
        var other = host.Diagram.Pages.Add("Other").AddNode("test.Source", 0, 0);
        var live = host.AddSink(300, 100);
        var undo = host.Diagram.History.UndoCount;

        // Act
        host.Editor.Delete([stale, other, live, live]);

        // Assert: only the live node was deleted, as one entry
        Assert.Multiple(
            () => Assert.DoesNotContain(live, host.Page.Nodes),
            () => Assert.Contains(other, other.Page!.Nodes),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Nothing at all to delete is a no-op
        var after = host.Diagram.History.UndoCount;
        host.Editor.Delete([stale]);
        Assert.Equal(after, host.Diagram.History.UndoCount);
    });

    /// <summary>
    ///     Validates that Delete does nothing in a read-only editor and rejects null.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Delete_ReadOnlyOrNull_DoesNothingOrThrows() => UiTest.Run(host =>
    {
        // Arrange
        var node = host.AddSource(100, 100);
        host.Editor.IsReadOnly = true;

        // Act
        host.Editor.Delete([node]);

        // Assert
        Assert.Multiple(
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.Throws<ArgumentNullException>(() => host.Editor.Delete(null!)));
    });

    /// <summary>
    ///     Validates that Undo and Redo return whether they did something and move through the history.
    /// </summary>
    [Fact]
    public Task DiagramEditor_UndoAndRedo_ReturnWhetherTheyActed() => UiTest.Run(host =>
    {
        // Arrange: start from an empty history (the host recorded its page creation)
        host.Diagram.History.Clear();
        var none = host.Editor.Undo();
        var node = host.AddSource(100, 100);

        // Act
        var undone = host.Editor.Undo();
        var goneAfterUndo = !host.Page.Nodes.Contains(node);
        var redone = host.Editor.Redo();
        var noMoreRedo = host.Editor.Redo();

        // Assert
        Assert.Multiple(
            () => Assert.False(none),
            () => Assert.True(undone),
            () => Assert.True(goneAfterUndo),
            () => Assert.True(redone),
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.False(noMoreRedo));
    });

    /// <summary>
    ///     Validates that Undo during a gesture only cancels the gesture and leaves the previous history entry alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Undo_DuringDrag_OnlyCancelsGesture() => UiTest.Run(host =>
    {
        // Arrange: a node that is being dragged
        var node = host.AddSource(150, 150);
        var origin = node.Position;
        var start = host.CenterOf(node);
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X + 90, start.Y + 60));

        // Act
        var cancelled = host.Editor.Undo();
        host.EndDrag(new Point(start.X + 90, start.Y + 60));

        // Assert: the drag was cancelled (no stray move entry), the node is still there and the add stays undoable
        Assert.Multiple(
            () => Assert.True(cancelled),
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.Equal(origin, node.Position),
            () => Assert.Equal(0, host.Diagram.History.RedoCount),
            () => Assert.False(host.Diagram.History.IsInTransaction),
            () => Assert.Equal("Add node", host.Diagram.History.UndoDescription));
    });

    /// <summary>
    ///     Validates that a second Undo after a cancelled gesture then reverts the previous entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Undo_AfterCancelledDrag_UndoesPreviousEntry() => UiTest.Run(host =>
    {
        // Arrange: a node with a drag in flight, cancelled by the first Undo
        var node = host.AddSource(150, 150);
        var start = host.CenterOf(node);
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X + 90, start.Y + 60));
        host.Editor.Undo();
        host.EndDrag(new Point(start.X + 90, start.Y + 60));

        // Act
        var undone = host.Editor.Undo();

        // Assert
        Assert.Multiple(
            () => Assert.True(undone),
            () => Assert.DoesNotContain(node, host.Page.Nodes),
            () => Assert.Equal(1, host.Diagram.History.RedoCount));
    });

    /// <summary>
    ///     Validates that Undo during an inline rename only abandons the rename and does not undo the previous committed change.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Undo_DuringRename_OnlyCancelsRename() => UiTest.Run(host =>
    {
        // Arrange: a renamed node whose second rename is in progress
        var node = host.AddSource(150, 150, "Old");
        node.Name = "Middle";
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "Typed";

        // Act
        var cancelled = host.Editor.Undo();

        // Assert: the typed text was discarded and the committed rename is still in force
        Assert.Multiple(
            () => Assert.True(cancelled),
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.Equal("Middle", node.Name),
            () => Assert.Equal(0, host.Diagram.History.RedoCount));
    });

    /// <summary>
    ///     Validates that Redo during an inline annotation edit only abandons the edit, and a second Redo then re-applies the entry.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Redo_DuringAnnotationEdit_OnlyCancelsEdit() => UiTest.Run(host =>
    {
        // Arrange: an undone text change, then a new edit in progress
        var note = host.Page.AddAnnotation("one", new PlexusRect(150, 150, 150, 60));
        note.Text = "two";
        host.Editor.Undo();
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "typed";

        // Act
        var cancelled = host.Editor.Redo();
        var textAfterCancel = note.Text;
        var redoCountAfterCancel = host.Diagram.History.RedoCount;
        var redone = host.Editor.Redo();

        // Assert
        Assert.Multiple(
            () => Assert.True(cancelled),
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("one", textAfterCancel),
            () => Assert.Equal(1, redoCountAfterCancel),
            () => Assert.True(redone),
            () => Assert.Equal("two", note.Text));
    });

    /// <summary>
    ///     Validates that Redo during a drag only cancels the gesture and leaves the redo stack alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Redo_DuringDrag_OnlyCancelsGesture() => UiTest.Run(host =>
    {
        // Arrange: an undone node addition, then a drag of another node in flight
        var other = host.AddSource(300, 300);
        var node = host.AddSource(150, 150);
        host.Editor.Undo();
        var start = host.CenterOf(other);
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X + 90, start.Y + 60));

        // Act
        var cancelled = host.Editor.Redo();
        host.EndDrag(new Point(start.X + 90, start.Y + 60));

        // Assert: the drag is gone, the undone node is still undone and still redoable
        Assert.Multiple(
            () => Assert.True(cancelled),
            () => Assert.DoesNotContain(node, host.Page.Nodes),
            () => Assert.Equal(1, host.Diagram.History.RedoCount),
            () => Assert.False(host.Diagram.History.IsInTransaction));
    });

    /// <summary>
    ///     Validates that Undo and Redo are refused by a read-only editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_UndoAndRedo_ReadOnlyEditor_ReturnFalse() => UiTest.Run(host =>
    {
        // Arrange
        host.AddSource(100, 100);
        host.Editor.IsReadOnly = true;

        // Act / Assert
        Assert.Multiple(
            () => Assert.False(host.Editor.Undo()),
            () => Assert.True(host.Diagram.History.CanUndo),
            () => Assert.False(host.Editor.Redo()));
    });

    /// <summary>
    ///     Validates that Ctrl+Z during a drag only cancels the drag, like the public Undo.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlZ_DuringDrag_OnlyCancelsTheDrag() => UiTest.Run(host =>
    {
        // Arrange: a node mid-drag
        var node = host.AddSource(150, 150);
        var origin = node.Position;
        var start = host.CenterOf(node);
        host.BeginDrag(start);
        host.MoveTo(new Point(start.X + 60, start.Y));

        // Act
        host.PressFocused(Key.Z, RawInputModifiers.Control);
        host.EndDrag(new Point(start.X + 60, start.Y));

        // Assert: the node stays (the add was not undone) and is back where the drag started
        Assert.Multiple(
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.Equal(origin, node.Position),
            () => Assert.Equal(0, host.Diagram.History.RedoCount));
    });
}
