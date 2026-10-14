using Avalonia;
using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of inline rename and of the events the editor raises to its host.
/// </summary>
public class DiagramEditorRenameAndEventTests
{
    /// <summary>
    ///     Validates that Enter commits a valid inline rename through the rename pipeline.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Rename_EnterWithValidName_CommitsAndCloses() => UiTest.Run(host =>
    {
        // Arrange: a node being renamed
        var node = host.AddSource(150, 150, "Old");
        host.Editor.BeginRename(node);

        // Act: type a new name and press Enter
        host.Editor.RenameEditor!.Text = "Fresh";
        host.PressFocused(Key.Enter);

        // Assert: renamed, editor closed
        Assert.Multiple(
            () => Assert.Equal("Fresh", node.Name),
            () => Assert.False(host.Editor.IsRenaming));
    });

    /// <summary>
    ///     Validates that a rejected name keeps the editor open and explains why.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Rename_EnterWithDuplicateName_StaysOpenWithReason() => UiTest.Run(host =>
    {
        // Arrange: two nodes, renaming the first to the second's name
        var node = host.AddSource(150, 150, "First");
        host.AddSource(350, 150, "Second");
        host.Editor.BeginRename(node);

        // Act: type the taken name and press Enter
        host.Editor.RenameEditor!.Text = "Second";
        host.PressFocused(Key.Enter);

        // Assert: still editing, name unchanged, reason available
        Assert.Multiple(
            () => Assert.True(host.Editor.IsRenaming),
            () => Assert.Equal("First", node.Name),
            () => Assert.False(string.IsNullOrWhiteSpace(host.Editor.RenameError)));
    });

    /// <summary>
    ///     Validates that a host handler can reject a rename through Diagram.NodeRenaming and the editor shows its reason.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Rename_HostHandlerCancels_ShowsHostReason() => UiTest.Run(host =>
    {
        // Arrange: a host rule forbidding names that start with an underscore
        host.Diagram.NodeRenaming += (_, e) =>
        {
            if (e.ProposedName.StartsWith('_'))
            {
                e.Cancel = true;
                e.Reason = "Names may not start with an underscore.";
            }
        };
        var node = host.AddSource(150, 150, "Plain");
        host.Editor.BeginRename(node);

        // Act: try to commit a forbidden name
        host.Editor.RenameEditor!.Text = "_hidden";
        host.PressFocused(Key.Enter);

        // Assert: the host's reason is shown and the node is unchanged
        Assert.Multiple(
            () => Assert.Equal("Names may not start with an underscore.", host.Editor.RenameError),
            () => Assert.Equal("Plain", node.Name));
    });

    /// <summary>
    ///     Validates that Escape cancels the rename and leaves the name alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Rename_Escape_CancelsWithoutChange() => UiTest.Run(host =>
    {
        // Arrange: a node being renamed with changed text
        var node = host.AddSource(150, 150, "Keep");
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "Changed";

        // Act: Escape
        host.PressFocused(Key.Escape);

        // Assert: closed, unchanged
        Assert.Multiple(
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.Equal("Keep", node.Name));
    });

    /// <summary>
    ///     Validates that a rename is one undoable step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Rename_Committed_CanBeUndone() => UiTest.Run(host =>
    {
        // Arrange: a committed rename
        var node = host.AddSource(150, 150, "Before");
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "After";
        host.Editor.CommitRename();

        // Act: undo
        host.Diagram.History.Undo();

        // Assert: the old name is back
        Assert.Equal("Before", node.Name);
    });

    /// <summary>
    ///     Validates that double-clicking a node's name starts a rename and does not raise NodeDoubleClicked.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DoubleClick_NodeName_StartsRenameOnly() => UiTest.Run(host =>
    {
        // Arrange: a node and a handler for the double-click event
        var node = host.AddSource(150, 150, "Namey");
        var raised = new List<Node>();
        host.Editor.NodeDoubleClicked += (_, e) => raised.Add(e.Node);

        // Act: double-click the name label above the box
        host.DoubleClick(host.NameOf(node));

        // Assert: renaming, no event
        Assert.Multiple(
            () => Assert.True(host.Editor.IsRenaming),
            () => Assert.Empty(raised));
    });

    /// <summary>
    ///     Validates that double-clicking a node's body raises NodeDoubleClicked and does not rename.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DoubleClick_NodeBody_RaisesNodeDoubleClicked() => UiTest.Run(host =>
    {
        // Arrange: a node and a handler
        var node = host.AddSource(150, 150);
        var raised = new List<Node>();
        host.Editor.NodeDoubleClicked += (_, e) => raised.Add(e.Node);

        // Act: double-click the body
        host.DoubleClick(host.CenterOf(node));

        // Assert: one event for that node, no rename
        Assert.Multiple(
            () => Assert.Equal([node], raised),
            () => Assert.False(host.Editor.IsRenaming));
    });

    /// <summary>
    ///     Validates that right-clicking a node raises DiagramContextRequested with that node, and selects it.
    /// </summary>
    [Fact]
    public Task DiagramEditor_RightClick_OnNode_RaisesContextRequestedWithNode() => UiTest.Run(host =>
    {
        // Arrange: a node and a handler
        var node = host.AddSource(150, 150);
        DiagramContextRequestedEventArgs? args = null;
        host.Editor.DiagramContextRequested += (_, e) => args = e;

        // Act: right-click the node
        host.RightClick(host.CenterOf(node));

        // Assert: the event names the node and the node became the selection
        Assert.Multiple(
            () => Assert.NotNull(args),
            () => Assert.Same(node, args?.Element),
            () => Assert.Equal([node], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that right-clicking the empty canvas raises the event with no element and the diagram position.
    /// </summary>
    [Fact]
    public Task DiagramEditor_RightClick_OnCanvas_RaisesContextRequestedWithPosition() => UiTest.Run(host =>
    {
        // Arrange: a scrolled view and a handler
        host.Editor.Offset = new Point(100, 50);
        host.Flush();
        DiagramContextRequestedEventArgs? args = null;
        host.Editor.DiagramContextRequested += (_, e) => args = e;

        // Act: right-click empty canvas at screen (200, 300)
        host.RightClick(new Point(200, 300));

        // Assert: no element, and the position is in diagram coordinates
        Assert.Multiple(
            () => Assert.NotNull(args),
            () => Assert.Null(args?.Element),
            () => Assert.Equal(new Point(300, 350), args?.Position),
            () => Assert.Equal(new Point(200, 300), args?.ScreenPosition));
    });

    /// <summary>
    ///     Validates that HitTest reports node, port and canvas hits through the public API.
    /// </summary>
    [Fact]
    public Task DiagramEditor_HitTest_ReportsWhatIsUnderThePoint() => UiTest.Run(host =>
    {
        // Arrange: a node
        var node = host.AddSource(150, 150);

        // Act: test the body, the port and an empty spot
        var body = host.Editor.HitTest(host.CenterOf(node));
        var port = host.Editor.HitTest(host.PortOf(node));
        var canvas = host.Editor.HitTest(new Point(700, 500));

        // Assert: each is classified correctly
        Assert.Multiple(
            () => Assert.Equal(Scene.HitKind.Node, body.Kind),
            () => Assert.Equal(Scene.HitKind.Port, port.Kind),
            () => Assert.Same(node.Port("value"), port.Port),
            () => Assert.Equal(Scene.HitKind.Canvas, canvas.Kind));
    });
}
