using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using DemaConsulting.Plexus.Scene;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Subsystem tests of the editor: the control, the gestures, the clipboard bridge and the node type drag helper working together.
/// </summary>
public class EditorTests
{
    /// <summary>
    ///     Validates that scene, hit testing and painting follow model changes, the theme, and zoom and scroll consistently.
    /// </summary>
    [Fact]
    public Task Editor_SceneSync_ModelThemeAndViewChanges_KeepSceneAndHitTestingConsistent() => UiTest.Run(host =>
    {
        // Arrange: an empty page and the scene built for it
        var before = host.CurrentScene;

        // Act: a model change, then a theme override, then zoom and scroll
        var node = host.AddSource(150, 150, "Sync");
        var afterModelChange = host.CurrentScene;
        var theme = DiagramTheme.Classic with { NodeFill = PlexusColorOf("#336699") };
        host.Editor.DiagramTheme = theme;
        host.Editor.Zoom = 2;
        host.Editor.Offset = new Point(100, 80);
        host.Flush();
        var frame = host.Window.CaptureRenderedFrame();

        // Assert: the scene was rebuilt, uses the theme, hit testing agrees with the zoomed view, and a frame was painted
        var hit = host.Editor.HitTest(host.CenterOf(node));
        Assert.Multiple(
            () => Assert.NotSame(before, afterModelChange),
            () => Assert.True(afterModelChange.TryGetNodeLayout(node, out _)),
            () => Assert.Same(theme, host.CurrentScene.Theme),
            () => Assert.Same(node, hit.Node),
            () => Assert.NotNull(frame));
    });

    /// <summary>
    ///     Validates that select, move, connect, resize, band-select and pan gestures all edit through the same editor, each drag as one undo step.
    /// </summary>
    [Fact]
    public Task Editor_PointerEditing_SelectMoveConnectResizeBandAndPan_EditModelWithOneUndoStepEach() => UiTest.Run(host =>
    {
        // Arrange: a source, a sink and an annotation
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(405, 195);
        var note = host.Page.AddAnnotation("note", new PlexusRect(150, 330, 150, 60));
        var undo = host.Diagram.History.UndoCount;

        // Act / Assert: a click selects without recording history
        host.Click(host.CenterOf(source));
        Assert.Multiple(
            () => Assert.Equal([source], host.Editor.Selection),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));

        // Act / Assert: a drag (started elsewhere on the body, so it is not a double-click) moves the node as one step
        var start = new Point(host.CenterOf(source).X + 12, host.CenterOf(source).Y);
        host.Drag(start, new Point(start.X + 30, start.Y));
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(90, 60), source.Position),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: a port drag connects as one step
        host.Drag(host.PortOf(source), host.PortOf(sink));
        Assert.Multiple(
            () => Assert.Single(host.Page.Wires),
            () => Assert.Equal(undo + 2, host.Diagram.History.UndoCount));

        // Act / Assert: the resize handle resizes as one step
        host.Editor.Select(note);
        host.Flush();
        var handle = new Point(note.Bounds.Right - 3, note.Bounds.Bottom - 3);
        host.Drag(handle, new Point(handle.X + 45, handle.Y + 30));
        Assert.Multiple(
            () => Assert.True(note.Bounds.Width > 150),
            () => Assert.Equal(undo + 3, host.Diagram.History.UndoCount));

        // Act / Assert: a rubber band over the canvas selects nodes and records nothing
        host.Drag(new Point(5, 5), new Point(700, 280));
        Assert.Multiple(
            () => Assert.Contains(source, host.Editor.Selection),
            () => Assert.Contains(sink, host.Editor.Selection),
            () => Assert.Equal(undo + 3, host.Diagram.History.UndoCount));

        // Act / Assert: a middle-button drag pans and records nothing
        host.Window.MouseDown(new Point(100, 500), MouseButton.Middle);
        host.Window.MouseMove(new Point(130, 520), RawInputModifiers.MiddleMouseButton);
        host.Window.MouseUp(new Point(130, 520), MouseButton.Middle);
        host.Flush();
        Assert.Multiple(
            () => Assert.Equal(new Point(-30, -20), host.Editor.Offset),
            () => Assert.Equal(undo + 3, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that Escape, Undo, Redo and a page change each abandon a drag in flight without leaving history behind.
    /// </summary>
    [Fact]
    public Task Editor_GestureInFlight_EscapeUndoRedoAndPageChange_CancelWithoutHistory() => UiTest.Run(host =>
    {
        // Arrange: a node, and a way to start a drag on it
        var node = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;
        var center = host.CenterOf(node);
        var far = new Point(center.X + 90, center.Y + 60);
        var results = new List<(string Name, bool Restored)>();
        var presses = 0;

        void StartDrag()
        {
            // Each press lands a little elsewhere on the body so that none counts as a double-click
            var start = new Point(center.X - 20 + (presses++ * 10), center.Y);
            host.BeginDrag(start);
            host.MoveTo(far);
            Assert.NotEqual(new PlexusPoint(150, 150), node.Position);
        }

        void Record(string name)
        {
            host.EndDrag(far);
            results.Add((name, node.Position == new PlexusPoint(150, 150) && host.Diagram.History.UndoCount == undo));
        }

        // Act: abandon four drags in four different ways
        StartDrag();
        host.PressFocused(Key.Escape);
        Record("escape");
        StartDrag();
        var undone = host.Editor.Undo();
        Record("undo");
        StartDrag();
        var redone = host.Editor.Redo();
        Record("redo");
        StartDrag();
        host.Editor.Page = null;
        host.Editor.Page = host.Page;
        Record("page change");

        // Assert: every drag was rolled back to the original position, with the history untouched, and Undo/Redo consumed their request
        Assert.Multiple(
            () => Assert.All(results, r => Assert.True(r.Restored, r.Name)),
            () => Assert.True(undone),
            () => Assert.True(redone),
            () => Assert.False(host.Diagram.History.IsInTransaction));
    });

    /// <summary>
    ///     Validates that one inline editor is open at a time and that rename and annotation edits commit or cancel through the same commands.
    /// </summary>
    [Fact]
    public Task Editor_InlineEditing_RenameAndAnnotation_CommitOrCancel() => UiTest.Run(host =>
    {
        // Arrange: two nodes and an annotation
        var node = host.AddSource(150, 150, "First");
        host.AddSource(450, 150, "Second");
        var note = host.Page.AddAnnotation("old", new PlexusRect(150, 330, 150, 60));

        // Act / Assert: a rename to a taken name is rejected with a reason and stays open
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor!.Text = "Second";
        var rejected = host.Editor.CommitEdit();
        Assert.Multiple(
            () => Assert.False(rejected),
            () => Assert.True(host.Editor.IsRenaming),
            () => Assert.False(string.IsNullOrWhiteSpace(host.Editor.RenameError)));

        // Act / Assert: opening an annotation edit replaces the rename; Ctrl+Enter commits it as one undo entry
        var undo = host.Diagram.History.UndoCount;
        host.Editor.BeginEditAnnotation(note);
        host.Editor.AnnotationEditor!.Text = "new text";
        host.PressFocused(Key.Enter, RawInputModifiers.Control);
        Assert.Multiple(
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.False(host.Editor.IsEditingAnnotation),
            () => Assert.Equal("new text", note.Text),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));

        // Act / Assert: a valid rename commits when the canvas is clicked
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor.Text = "Renamed";
        host.Click(new Point(700, 500));
        Assert.Multiple(
            () => Assert.Equal("Renamed", node.Name),
            () => Assert.False(host.Editor.IsEditing));

        // Act / Assert: Escape cancels an edit without changing anything
        host.Editor.BeginRename(node);
        host.Editor.RenameEditor.Text = "Discarded";
        host.PressFocused(Key.Escape);
        Assert.Multiple(
            () => Assert.Equal("Renamed", node.Name),
            () => Assert.False(host.Editor.IsEditing));
    });

    /// <summary>
    ///     Validates that copy, cut and paste travel through the host clipboard and that failures reach the host as events.
    /// </summary>
    [Fact]
    public Task Editor_Clipboard_CopyCutPasteAndFailures_GoThroughBridgeAndRaiseEvents() => UiTest.RunAsync(async host =>
    {
        // Arrange: a connected pair and a private clipboard
        var clipboard = new FakeClipboard();
        host.Editor.Clipboard = clipboard;
        var source = host.AddSource(100, 100, "Src");
        var sink = host.AddSink(400, 100, "Snk");
        source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select([source, sink]);

        // Act: copy, then paste, then cut and paste again
        await host.Editor.CopyAsync();
        var copied = clipboard.Text;
        var pasted = await host.Editor.PasteAsync(new PlexusPoint(500, 300));
        var afterPaste = host.Page.Nodes.Count;
        host.Editor.Select([source, sink]);
        await host.Editor.CutAsync();
        var afterCut = host.Page.Nodes.Count;

        // Act: a paste of plain words and a shortcut copy on a failing clipboard
        PasteFailedEventArgs? pasteFailure = null;
        ClipboardFailedEventArgs? clipboardFailure = null;
        host.Editor.PasteFailed += (_, e) => pasteFailure = e;
        host.Editor.ClipboardFailed += (_, e) => clipboardFailure = e;
        clipboard.Text = "just some words";
        await host.Editor.PasteAsync();
        host.Editor.Clipboard = new ThrowingClipboard();
        host.Editor.Select(host.Page.Nodes[0]);
        host.Press(Key.C, RawInputModifiers.Control);
        for (var i = 0; i < 100 && clipboardFailure is null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        // Assert
        Assert.NotNull(pasted);
        Assert.Multiple(
            () => Assert.Contains(PlexusClipboard.FormatId, copied),
            () => Assert.True(pasted.Success, pasted.Error),
            () => Assert.Equal(4, afterPaste),
            () => Assert.Equal(2, afterCut),
            () => Assert.NotNull(pasteFailure),
            () => Assert.NotNull(clipboardFailure));
    });

    /// <summary>
    ///     Validates that a node type payload dropped on the editor creates a node, a drag source can be attached to a control, and foreign payloads are ignored.
    /// </summary>
    [Fact]
    public Task Editor_NodeDrop_PayloadDroppedOnEditor_CreatesSelectedNodeAndIgnoresUnknownTypes() => UiTest.Run(host =>
    {
        // Arrange: a palette entry made a drag source, and no snapping so the drop point is exact
        using var handle = DiagramEditor.EnableNodeTypeDrag(new Border(), "test.Source");
        host.Editor.SnapToGrid = false;
        var undo = host.Diagram.History.UndoCount;

        // Act: drop a known type, then an unknown one
        Drop(host, "test.Source", new Point(400, 300));
        var created = host.Page.Nodes.ToList();
        Drop(host, "no.such.Type", new Point(200, 200));

        // Assert: one node centered at the drop point, selected, one undo step; the unknown type created nothing
        var node = Assert.Single(created);
        Assert.Multiple(
            () => Assert.Equal(400, node.Bounds.Center.X, 1),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount),
            () => Assert.Single(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates that a palette over the registry lists every type, that dragging an item over the editor shows a ghost, and that the drop creates the node where the ghost was.
    /// </summary>
    [Fact]
    public Task Editor_NodePalette_DragItemOverEditor_ShowsGhostAndDropsNodeWhereGhostWas() => UiTest.Run(() =>
    {
        // Arrange: a palette beside an editor; the starter delivers the events a platform drag would
        using var host = new PaletteHost();
        var target = new Point(300, 250);
        var ghostWhileOver = default(PlexusPoint?);
        var ghostType = default(NodeType?);
        host.Palette.DragStarter = (_, key) =>
        {
            host.DragEvent(RawDragEventType.DragEnter, key, target);
            host.DragEvent(RawDragEventType.DragOver, key, target);
            ghostWhileOver = host.Editor.GhostPosition;
            ghostType = host.Editor.GhostNodeType;
            host.DragEvent(RawDragEventType.Drop, key, target);
            return Task.CompletedTask;
        };
        var center = host.CenterOf(host.ItemFor("test.Gain"));

        // Act: press the item and drag it past the threshold
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseMove(new Point(center.X + 40, center.Y + 10), RawInputModifiers.LeftMouseButton);
        host.Window.MouseUp(new Point(center.X + 40, center.Y + 10), MouseButton.Left);
        host.Flush();

        // Assert: every type was listed, the ghost showed the dragged type, and the node landed on the ghost
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal(host.Types.Count, host.Palette.Items.Count),
            () => Assert.Same(host.Types.Get("test.Gain"), ghostType),
            () => Assert.Equal(ghostWhileOver, node.Bounds.Position),
            () => Assert.Null(host.Editor.GhostNodeType),
            () => Assert.Equal([node], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that double-click and context events reach the host and that a read-only editor still selects but never edits.
    /// </summary>
    [Fact]
    public Task Editor_HostIntegration_EventsAndReadOnly_AreRaisedAndRespected() => UiTest.Run(host =>
    {
        // Arrange: a node and handlers
        var node = host.AddSource(150, 150);
        var doubleClicked = new List<Node>();
        var contexts = new List<DiagramContextRequestedEventArgs>();
        host.Editor.NodeDoubleClicked += (_, e) => doubleClicked.Add(e.Node);
        host.Editor.DiagramContextRequested += (_, e) => contexts.Add(e);

        // Act: double-click the body, right-click the node, right-click the canvas
        host.DoubleClick(host.CenterOf(node));
        host.RightClick(host.CenterOf(node));
        host.RightClick(new Point(600, 450));

        // Act: switch to read-only and try to edit
        host.Editor.IsReadOnly = true;
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(node);
        host.Drag(start, new Point(start.X + 60, start.Y + 60));
        host.Press(Key.Delete);

        // Assert
        Assert.Multiple(
            () => Assert.Equal([node], doubleClicked),
            () => Assert.Equal(2, contexts.Count),
            () => Assert.Same(node, contexts[0].Element),
            () => Assert.Null(contexts[1].Element),
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position),
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that keyboard shortcuts run the editor commands, and that an open inline editor owns the keyboard instead.
    /// </summary>
    [Fact]
    public Task Editor_Keyboard_Shortcuts_RunCommandsUnlessInlineEditorIsOpen() => UiTest.Run(host =>
    {
        // Arrange: two nodes
        var a = host.AddSource(150, 150, "A");
        var b = host.AddSink(450, 150, "B");

        // Act: select all, duplicate, delete the copies, undo the delete, redo it
        host.Press(Key.A, RawInputModifiers.Control);
        var selected = host.Editor.Selection.Count;
        host.Press(Key.D, RawInputModifiers.Control);
        var duplicated = host.Page.Nodes.Count;
        host.Press(Key.Delete);
        var afterDelete = host.Page.Nodes.Count;
        host.Press(Key.Z, RawInputModifiers.Control);
        var afterUndo = host.Page.Nodes.Count;
        host.Press(Key.Y, RawInputModifiers.Control);
        var afterRedo = host.Page.Nodes.Count;

        // Act: F2 on a single selected node opens the rename box, which then owns Delete
        host.Editor.Select(a);
        host.Press(Key.F2);
        var renaming = host.Editor.IsRenaming;
        host.PressFocused(Key.Delete);
        var stillThere = host.Page.Nodes.Contains(a);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, selected),
            () => Assert.Equal(4, duplicated),
            () => Assert.Equal(2, afterDelete),
            () => Assert.Equal(4, afterUndo),
            () => Assert.Equal(2, afterRedo),
            () => Assert.True(renaming),
            () => Assert.True(stillThere),
            () => Assert.Contains(b, host.Page.Nodes));
    });

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
    ///     Parses a color given as #RRGGBB.
    /// </summary>
    /// <param name="hex">The color text.</param>
    /// <returns>The color.</returns>
    private static Geometry.Color PlexusColorOf(string hex) => Geometry.Color.Parse(hex);

    /// <summary>
    ///     Validates that hovering nodes, ports and wires shows tooltips the host can change, and that gestures hide them.
    /// </summary>
    [Fact]
    public Task Editor_ToolTips_HoverNodePortAndWire_ShowDefaultOrHostContent() => UiTest.Run(() =>
    {
        // Arrange: a described bus fed by a source, and a host handler that marks node tooltips
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var source = host.AddSource(60, 300, "S");
        var bus = host.Page.AddNode("test.Bus", 400, 60, "B");
        var wire = host.Page.Connect(source.Port("value"), bus.Input("in0"));
        host.Editor.ToolTipRequested += (_, e) => e.Content = e.Element is Node && e.Port is null ? $"[{e.Content}]" : e.Content;
        host.Flush();

        // Act: hover the node body, a port, the wire, then start a drag
        var tips = new List<object?>();
        foreach (var at in new[] { host.CenterOf(bus), host.FindHit(HitKind.Port, bus), host.FindHit(HitKind.Wire, wire) })
        {
            host.Window.MouseMove(at);
            host.Flush();
            tips.Add(ToolTip.GetTip(host.Editor));
        }

        host.BeginDrag(host.CenterOf(bus));
        host.MoveTo(host.CenterOf(bus) + new Point(30, 30));
        var duringDrag = ToolTip.GetTip(host.Editor);
        host.EndDrag(host.CenterOf(bus));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("[Merges channels.]", tips[0]),
            () => Assert.Contains("Channel", tips[1] as string, StringComparison.Ordinal),
            () => Assert.Equal("S.value → B.in0", tips[2]),
            () => Assert.Null(duringDrag));
    });

    /// <summary>
    ///     Validates that elements can be brought into view and fitted across pages and collapsed groups.
    /// </summary>
    [Fact]
    public Task Editor_ElementNavigation_BringIntoViewAndFitElements_ShowElementsAcrossPages() => UiTest.Run(host =>
    {
        // Arrange: a node on a second page, and a node hidden in a collapsed group on the first
        var second = host.Diagram.Pages.Add("Second");
        var remote = second.AddNode("test.Source", 2500, 1800);
        var hidden = host.AddSource(4000, 3000);
        var group = host.Page.CreateGroup([hidden], "G");
        group.IsCollapsed = true;
        var view = new Rect(host.Editor.Bounds.Size);

        // Act / Assert: the remote node switches the page
        host.Editor.BringIntoView(remote);
        Assert.Same(second, host.Editor.Page);
        Assert.True(view.Contains(host.Editor.GetElementScreenBounds(remote)));

        // Act / Assert: the hidden node shows its collapsed group on the first page
        host.Editor.BringIntoView(hidden);
        Assert.Same(host.Page, host.Editor.Page);
        Assert.True(view.Contains(host.Editor.GetElementScreenBounds(group)));

        // Act / Assert: fitting two far-apart elements zooms out to show both
        var near = host.AddSink(0, 0);
        host.Editor.ZoomToFit([near, group]);
        Assert.True(host.Editor.Zoom < 1);
        Assert.True(view.Contains(host.Editor.GetElementScreenBounds(near)));
        Assert.True(view.Contains(host.Editor.GetElementScreenBounds(group)));
    });

    /// <summary>
    ///     Validates that the editor shows, hit tests and connects the current ports of a node whose ports a property change recomputed.
    /// </summary>
    [Fact]
    public Task Editor_DynamicPorts_PropertyChange_SceneHitTestAndConnectFollow() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var source = host.AddSource(60, 300);
        var bus = host.Page.AddNode("test.Bus", 400, 60);

        // Act: grow to four inputs, connect the last one by dragging
        bus.Properties["Channels"] = 4;
        host.Flush();
        var layoutPorts = host.CurrentScene.TryGetNodeLayout(bus, out var layout) ? layout.Ports.Count : -1;
        host.Drag(host.PortOf(source), host.Editor.GetPortScreenPosition(bus.Input("in3")));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(5, layoutPorts),
            () => Assert.Same(bus.Input("in3"), Assert.Single(host.Page.Wires).Target),
            () => Assert.Same(bus.Input("in3"), host.Editor.HitTest(host.Editor.GetPortScreenPosition(bus.Input("in3"))).Port));
    });
    /// <summary>
    ///     A clipboard whose every operation fails like a locked desktop session.
    /// </summary>
    private sealed class ThrowingClipboard : IDiagramClipboard
    {
        /// <inheritdoc />
        public Task SetAsync(string json) => Task.FromException(new System.Runtime.InteropServices.COMException("locked", unchecked((int)0x800401D0)));

        /// <inheritdoc />
        public Task<string?> GetAsync() => Task.FromException<string?>(new System.Runtime.InteropServices.COMException("locked", unchecked((int)0x800401D0)));
    }
}
