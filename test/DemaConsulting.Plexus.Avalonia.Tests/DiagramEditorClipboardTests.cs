using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of copy, cut, paste, duplicate and delete commands.
/// </summary>
public class DiagramEditorClipboardTests
{
    /// <summary>
    ///     Validates that copy then paste round-trips a connected pair, with unique names and one undo step.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CopyPaste_ConnectedPair_RoundTrips() => UiTest.RunAsync(async host =>
    {
        // Arrange: a connected pair, both selected, and a private clipboard
        var clipboard = new FakeClipboard();
        host.Editor.Clipboard = clipboard;
        var source = host.AddSource(100, 100, "Src");
        var sink = host.AddSink(400, 100, "Snk");
        source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select([source, sink]);
        var undo = host.Diagram.History.UndoCount;

        // Act: copy and paste
        await host.Editor.CopyAsync();
        var result = await host.Editor.PasteAsync();

        // Assert: the clipboard carries Plexus JSON; the page now holds the copies and a second wire
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.True(result.Success, result.Error),
            () => Assert.Contains(PlexusClipboard.FormatId, clipboard.Text),
            () => Assert.Equal(4, host.Page.Nodes.Count),
            () => Assert.Equal(2, host.Page.Wires.Count),
            () => Assert.Equal(4, host.Page.Nodes.Select(n => n.Name).Distinct().Count()),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Equal(3, host.Editor.Selection.Count),
            () => Assert.DoesNotContain(source, host.Editor.Selection));

        // Act / Assert: one undo removes everything pasted
        host.Diagram.History.Undo();
        Assert.Equal(2, host.Page.Nodes.Count);
    });

    /// <summary>
    ///     Validates that paste places the content at the pointer when the pointer is over the editor.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Paste_PointerOverEditor_PlacesContentAtPointer() => UiTest.RunAsync(async host =>
    {
        // Arrange: copied node and the pointer resting over the canvas
        host.Editor.Clipboard = new FakeClipboard();
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        await host.Editor.CopyAsync();
        host.Window.MouseMove(new Point(500, 400));
        host.Flush();

        // Act: paste
        await host.Editor.PasteAsync();

        // Assert: the pasted node's top-left is at the snapped pointer position
        var pasted = Assert.Single(host.Page.Nodes, n => n != node);
        Assert.Equal(new PlexusPoint(495, 405), pasted.Position);
    });

    /// <summary>
    ///     Validates that pasting at an explicit position ignores the pointer and uses the position as given.
    /// </summary>
    [Fact]
    public Task DiagramEditor_PasteAt_ExplicitPosition_PlacesContentThere() => UiTest.RunAsync(async host =>
    {
        // Arrange: copied node and the pointer resting somewhere else
        host.Editor.Clipboard = new FakeClipboard();
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        await host.Editor.CopyAsync();
        host.Window.MouseMove(new Point(500, 400));
        host.Flush();

        // Act: paste at a position that is not on the grid
        var result = await host.Editor.PasteAsync(new PlexusPoint(203, 117));

        // Assert: the position is used as given, and the pasted node is selected
        Assert.NotNull(result);
        Assert.True(result.Success, result.Error);
        var pasted = Assert.Single(host.Page.Nodes, n => n != node);
        Assert.Equal(new PlexusPoint(203, 117), pasted.Position);
        Assert.Contains(pasted, host.Editor.Selection);
    });

    /// <summary>
    ///     Validates that cut removes the selection and leaves it on the clipboard for a later paste.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CutThenPaste_MovesNodes() => UiTest.RunAsync(async host =>
    {
        // Arrange: a selected node
        host.Editor.Clipboard = new FakeClipboard();
        var node = host.AddSource(100, 100, "Cutme");
        host.Editor.Select(node);

        // Act: cut, then paste
        await host.Editor.CutAsync();
        var afterCut = host.Page.Nodes.Count;
        await host.Editor.PasteAsync();

        // Assert: it vanished on cut and reappeared on paste, with its name free again
        Assert.Multiple(
            () => Assert.Equal(0, afterCut),
            () => Assert.Equal("Cutme", Assert.Single(host.Page.Nodes).Name));
    });

    /// <summary>
    ///     Validates that content copied in one editor can be pasted into a separate one (as between application instances).
    /// </summary>
    [Fact]
    public Task DiagramEditor_Paste_FromOtherEditorInstance_Works() => UiTest.RunAsync(async host =>
    {
        // Arrange: a second, independent editor and diagram sharing only the clipboard
        using var other = new EditorHost();
        var clipboard = new FakeClipboard();
        host.Editor.Clipboard = clipboard;
        other.Editor.Clipboard = clipboard;
        var node = host.AddSource(100, 100, "Travels");
        host.Editor.Select(node);

        // Act: copy in the first, paste in the second
        await host.Editor.CopyAsync();
        var result = await other.Editor.PasteAsync();

        // Assert: the second diagram received the node
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.True(result.Success, result.Error),
            () => Assert.Equal("Travels", Assert.Single(other.Page.Nodes).Name));
    });

    /// <summary>
    ///     Validates that pasting something that is not Plexus data raises PasteFailed and leaves the page alone.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Paste_NotPlexusData_RaisesPasteFailed() => UiTest.RunAsync(async host =>
    {
        // Arrange: unrelated text on the clipboard and a handler
        host.Editor.Clipboard = new FakeClipboard { Text = "just some words" };
        PasteFailedEventArgs? failure = null;
        host.Editor.PasteFailed += (_, e) => failure = e;
        var undo = host.Diagram.History.UndoCount;

        // Act: paste
        var result = await host.Editor.PasteAsync();

        // Assert: no exception, the event explains, the page is untouched
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.False(result.Success),
            () => Assert.NotNull(failure),
            () => Assert.Same(result, failure?.Result),
            () => Assert.Empty(host.Page.Nodes),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that pasting a node type the target does not know reports it through PasteFailed.
    /// </summary>
    [Fact]
    public Task DiagramEditor_Paste_UnknownNodeType_ListsMissingTypes() => UiTest.RunAsync(async host =>
    {
        // Arrange: clipboard content produced by a diagram that has an extra type
        var other = new NodeTypeRegistry();
        other.PortTypes.Add(new PortType("f32", Geometry.Color.Black));
        other.Add(new NodeType("alien.Thing") { Title = "ALIEN" });
        var foreign = new Diagram(other).Pages.Add("X");
        var thing = foreign.AddNode("alien.Thing", 10, 10);
        host.Editor.Clipboard = new FakeClipboard { Text = PlexusClipboard.Copy([thing]) };
        PasteFailedEventArgs? failure = null;
        host.Editor.PasteFailed += (_, e) => failure = e;

        // Act: paste into the editor whose registry lacks the type
        await host.Editor.PasteAsync();

        // Assert: the failure names the missing type
        Assert.NotNull(failure);
        Assert.Contains("alien.Thing", failure.Result.MissingNodeTypes);
    });

    /// <summary>
    ///     Validates that Ctrl+C and Ctrl+V work from the keyboard.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlCAndCtrlV_CopyAndPasteViaKeyboard() => UiTest.RunAsync(async host =>
    {
        // Arrange: a selected node and a private clipboard
        var clipboard = new FakeClipboard();
        host.Editor.Clipboard = clipboard;
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);

        // Act: Ctrl+C, then Ctrl+V; give the fire-and-forget commands time to finish
        host.Press(Key.C, RawInputModifiers.Control);
        await WaitUntil(() => clipboard.Text is not null);
        host.Press(Key.V, RawInputModifiers.Control);
        await WaitUntil(() => host.Page.Nodes.Count == 2);

        // Assert: the node was duplicated through the clipboard
        Assert.Equal(2, host.Page.Nodes.Count);
    });

    /// <summary>
    ///     Validates that a clipboard failure during a shortcut-started copy is reported through ClipboardFailed instead of being lost.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CtrlC_ClipboardThrows_RaisesClipboardFailed() => UiTest.RunAsync(async host =>
    {
        // Arrange: a clipboard that fails like a locked desktop session does
        host.Editor.Clipboard = new FailingClipboard(new System.Runtime.InteropServices.COMException("stale text", unchecked((int)0x800401D0)));
        ClipboardFailedEventArgs? failure = null;
        host.Editor.ClipboardFailed += (_, e) => failure = e;
        host.Editor.Select(host.AddSource(100, 100));

        // Act: Ctrl+C; the fire-and-forget command reports asynchronously
        host.Press(Key.C, RawInputModifiers.Control);
        await WaitUntil(() => failure is not null);

        // Assert: the host learns the HRESULT, and the editor stays usable
        Assert.NotNull(failure);
        Assert.Multiple(
            () => Assert.Equal(unchecked((int)0x800401D0), failure.Error.HResult),
            () => Assert.Single(host.Page.Nodes));
    });

    /// <summary>
    ///     A clipboard whose every operation throws the given exception.
    /// </summary>
    /// <param name="error">The exception to throw.</param>
    private sealed class FailingClipboard(Exception error) : IDiagramClipboard
    {
        /// <inheritdoc />
        public Task SetAsync(string json) => Task.FromException(error);

        /// <inheritdoc />
        public Task<string?> GetAsync() => Task.FromException<string?>(error);
    }

    /// <summary>
    ///     Validates that the window's own clipboard carries a copy to a paste, in both the tagged format and as plain text.
    /// </summary>
    [Fact]
    public Task DiagramEditor_CopyPaste_SystemClipboard_RoundTripsWhenAvailable() => UiTest.RunAsync(async host =>
    {
        // Arrange: no private clipboard, so the window's clipboard is used
        var node = host.AddSource(100, 100, "Sys");
        host.Editor.Select(node);

        // Act: copy and paste
        await host.Editor.CopyAsync();
        var result = await host.Editor.PasteAsync();

        // Assert: the round trip worked, and the clipboard holds both the tagged format and the plain-text fallback
        var clipboard = TopLevel.GetTopLevel(host.Editor)!.Clipboard!;
        var tagged = await clipboard.TryGetValueAsync(DataFormat.CreateStringApplicationFormat(PlexusClipboard.FormatId));
        var text = await clipboard.TryGetTextAsync();
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.True(result.Success, result.Error),
            () => Assert.Equal(2, host.Page.Nodes.Count),
            () => Assert.NotNull(tagged),
            () => Assert.Equal(tagged, text));
    });

    /// <summary>
    ///     Validates that read-only editors refuse to paste, cut or delete.
    /// </summary>
    [Fact]
    public Task DiagramEditor_ReadOnly_PasteAndCut_DoNothing() => UiTest.RunAsync(async host =>
    {
        // Arrange: a selected node, content on the clipboard, and a read-only editor
        var clipboard = new FakeClipboard();
        host.Editor.Clipboard = clipboard;
        var node = host.AddSource(100, 100);
        host.Editor.Select(node);
        await host.Editor.CopyAsync();
        host.Editor.IsReadOnly = true;

        // Act: paste, cut, delete
        var pasted = await host.Editor.PasteAsync();
        await host.Editor.CutAsync();
        host.Editor.DeleteSelection();

        // Assert: nothing changed
        Assert.Null(pasted);
        Assert.Single(host.Page.Nodes);
    });

    /// <summary>
    ///     Validates that duplicating an empty selection does nothing.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DuplicateSelection_Empty_ReturnsNothing() => UiTest.Run(host =>
    {
        // Arrange: a page with a node but no selection
        host.AddSource(100, 100);

        // Act: duplicate
        var copies = host.Editor.DuplicateSelection();

        // Assert: nothing was created
        Assert.Multiple(
            () => Assert.Empty(copies),
            () => Assert.Single(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates that deleting a collapsed group removes its hidden members too.
    /// </summary>
    [Fact]
    public Task DiagramEditor_DeleteSelection_CollapsedGroup_RemovesMembers() => UiTest.Run(host =>
    {
        // Arrange: a collapsed group of two nodes, selected
        var a = host.AddSource(100, 100);
        var b = host.AddSink(300, 100);
        var group = host.Page.CreateGroup([a, b], "Pair");
        group.IsCollapsed = true;
        host.Editor.Select(group);

        // Act: delete
        host.Editor.DeleteSelection();

        // Assert: nothing is left and one undo brings everything back
        Assert.Empty(host.Page.Nodes);
        host.Diagram.History.Undo();
        Assert.Equal(2, host.Page.Nodes.Count);
    });

    /// <summary>
    ///     Waits (on the UI thread, letting queued work run) until a condition holds or a timeout passes.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when the condition holds or after about two seconds.</returns>
    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
