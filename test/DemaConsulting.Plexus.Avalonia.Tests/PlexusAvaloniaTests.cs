using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using DemaConsulting.Plexus.Scene;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     System tests of the Avalonia editor: complete user scenarios driven through pointer and keyboard input in a headless window.
/// </summary>
public class PlexusAvaloniaTests
{
    /// <summary>
    ///     A small valid SVG icon.
    /// </summary>
    private const string DiscSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='30' viewBox='0 0 30 30'>" +
        "<circle cx='15' cy='15' r='13' fill='#2b2b2b'/></svg>";

    /// <summary>
    ///     Validates that the editor shows a connected page and its picture follows later model changes.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Display_ConnectedPage_ShowsSceneAndFollowsModelChanges() => UiTest.Run(host =>
    {
        // Arrange: a connected pair
        var source = host.AddSource(60, 60, "Src");
        var sink = host.AddSink(400, 180, "Snk");
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        var before = host.CurrentScene;
        Assert.True(before.TryGetWirePoints(wire, out var routeBefore));

        // Act: the model changes, without any input to the editor
        host.Page.Move([sink], 0, 120);
        var after = host.CurrentScene;
        var extra = host.AddSource(60, 400, "Extra");

        // Assert: the picture was rebuilt for the new model
        Assert.Multiple(
            () => Assert.NotSame(before, after),
            () => Assert.True(after.TryGetWirePoints(wire, out var routeAfter) && routeAfter[^1] != routeBefore[^1]),
            () => Assert.True(host.CurrentScene.TryGetNodeLayout(extra, out _)));
    });

    /// <summary>
    ///     Validates that clicking, rubber-banding and select-all each select the expected elements.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Selection_ClickBandAndSelectAll_SelectElements() => UiTest.Run(host =>
    {
        // Arrange: two nodes and a wire between them
        var a = host.AddSource(150, 150);
        var b = host.AddSink(450, 300);
        var wire = a.Port("value").ConnectTo(b.Port("value"));
        var undo = host.Diagram.History.UndoCount;

        // Act: click one node, Shift-click the other, click the empty canvas
        host.Click(host.CenterOf(a));
        var first = host.Editor.Selection.ToList();
        host.Click(host.CenterOf(b), RawInputModifiers.Shift);
        var both = host.Editor.Selection.ToList();
        host.Click(new Point(700, 500));
        var cleared = host.Editor.Selection.ToList();

        // Act: rubber-band over the first node only, then select everything
        host.Drag(new Point(100, 100), host.CenterOf(a));
        var banded = host.Editor.Selection.ToList();
        host.Press(Key.A, RawInputModifiers.Control);
        var all = host.Editor.Selection.ToList();

        // Assert: selection is view state, so no history was recorded
        Assert.Multiple(
            () => Assert.Equal([a], first),
            () => Assert.Equal(2, both.Count),
            () => Assert.Empty(cleared),
            () => Assert.Equal([a], banded),
            () => Assert.Equal(3, all.Count),
            () => Assert.Contains(wire, all),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that dragging a node moves it snapped to the grid as exactly one undo entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_DragMove_SnapsAndRecordsOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a node on the grid
        var node = host.AddSource(150, 150);
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(node);

        // Act: drag by an amount that is not a multiple of the grid
        host.Drag(start, new Point(start.X + 37, start.Y + 22));
        var moved = node.Position;
        host.Press(Key.Z, RawInputModifiers.Control);

        // Assert: snapped, one entry, and one undo puts the node back
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(180, 165), moved),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount),
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position));
    });

    /// <summary>
    ///     Validates that Ctrl-dragging duplicates the node under the pointer and moves the copy.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_CtrlDrag_DuplicatesSelection() => UiTest.Run(host =>
    {
        // Arrange
        var original = host.AddSource(150, 150, "Src");
        var start = host.CenterOf(original);

        // Act
        host.Drag(start, new Point(start.X + 150, start.Y), RawInputModifiers.Control);

        // Assert: the original stayed, a uniquely named copy moved and is selected
        var copy = Assert.Single(host.Page.Nodes, n => n != original);
        Assert.Multiple(
            () => Assert.Equal(new PlexusPoint(150, 150), original.Position),
            () => Assert.Equal(new PlexusPoint(300, 150), copy.Position),
            () => Assert.NotEqual(original.Name, copy.Name),
            () => Assert.Equal([copy], host.Editor.Selection));
    });

    /// <summary>
    ///     Validates that dragging from a port connects compatible ports and refuses incompatible ones.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_PortDrag_CompatibleAndIncompatiblePorts_ConnectsOnlyWhenAllowed() => UiTest.Run(host =>
    {
        // Arrange: a rule that demands equal port types, with a matching and a mismatching sink
        host.Diagram.ConnectionRules = ConnectionRules.SameType;
        var source = host.AddSource(60, 60);
        var good = host.AddSink(400, 60);
        var bad = host.Page.AddNode("test.IntSink", 400, 300);

        // Act: try the mismatching sink first, then the matching one (dragging from the input back to the output)
        host.Drag(host.PortOf(source), host.PortOf(bad));
        var afterBad = host.Page.Wires.Count;
        host.Drag(host.PortOf(good), host.PortOf(source));

        // Assert: only the allowed connection exists, ordered output to input
        var wire = Assert.Single(host.Page.Wires);
        Assert.Multiple(
            () => Assert.Equal(0, afterBad),
            () => Assert.Same(source.Port("value"), wire.Source),
            () => Assert.Same(good.Port("value"), wire.Target));
    });

    /// <summary>
    ///     Validates that an interior segment of a wire can be dragged sideways as one undo entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_WireSegmentDrag_MovesInteriorSegmentWithOneUndoEntry() => UiTest.Run(host =>
    {
        // Arrange: a wire with a movable interior segment
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(450, 300);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Flush();
        var before = wire.GetPoints();
        var index = Enumerable.Range(1, Math.Max(0, before.Count - 3))
            .First(i => (Math.Abs(before[i].Y - before[i + 1].Y) < 1e-6) != (Math.Abs(before[i].X - before[i + 1].X) < 1e-6));
        var a = before[index];
        var b = before[index + 1];
        var vertical = Math.Abs(a.X - b.X) < 1e-6;
        var mid = host.Editor.DiagramToScreen(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2));
        var undo = host.Diagram.History.UndoCount;

        // Act: drag the segment sideways
        host.Drag(mid, vertical ? new Point(mid.X + 45, mid.Y) : new Point(mid.X, mid.Y + 45));

        // Assert: the segment moved, the ports did not, one undo restores the route
        var after = wire.GetPoints();
        var moved = vertical ? after[index].X - a.X : after[index].Y - a.Y;
        var entries = host.Diagram.History.UndoCount - undo;
        host.Press(Key.Z, RawInputModifiers.Control);
        Assert.Multiple(
            () => Assert.True(Math.Abs(moved) >= 30, $"moved {moved}"),
            () => Assert.Equal(before[0], after[0]),
            () => Assert.Equal(before[^1], after[^1]),
            () => Assert.Equal(1, entries),
            () => Assert.Equal(before, wire.GetPoints()));
    });

    /// <summary>
    ///     Validates that a selected wire can be reshaped through its bend handle, a port stub, a double-click jog and an end handle, each as one undo entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_WireEditing_BendStubJogAndEndpoint_AreSingleUndoEntries() => UiTest.Run(host =>
    {
        // Arrange: a selected wire with several bends and a second sink to move the wire to
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(450, 300);
        var other = host.AddSink(450, 450);
        var wire = source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select(wire);
        host.Flush();
        var original = wire.Route.ToArray();
        var undo = host.Diagram.History.UndoCount;
        Point Screen(PlexusPoint p) => host.Editor.DiagramToScreen(new Point(p.X, p.Y));

        // Act: drag a bend, then pull the first stub, then double-click a segment, then move the target end
        var bend = wire.Route[1];
        host.Drag(Screen(bend), new Point(bend.X + 30, bend.Y - 30));
        var afterBend = host.Diagram.History.UndoCount - undo;
        var stubPoints = wire.GetPoints();
        var stub = new PlexusPoint((stubPoints[0].X + stubPoints[1].X) / 2, (stubPoints[0].Y + stubPoints[1].Y) / 2);
        host.Drag(Screen(stub), new Point(stub.X, stub.Y + 30));
        var afterStub = host.Diagram.History.UndoCount - undo;
        var points = wire.GetPoints();
        var segment = new PlexusPoint((points[^3].X + points[^2].X) / 2, (points[^3].Y + points[^2].Y) / 2);
        host.DoubleClick(Screen(segment));
        var afterJog = host.Diagram.History.UndoCount - undo;
        host.Editor.Select(wire);
        host.Flush();
        host.Drag(Screen(wire.Target.Anchor), host.PortOf(other));
        var afterReconnect = host.Diagram.History.UndoCount - undo;

        // Assert: four edits, four entries, the wire kept its identity, and four Ctrl+Z presses restore the original wire
        var reconnectedTo = wire.Target;
        for (var i = 0; i < 4; i++)
        {
            host.Press(Key.Z, RawInputModifiers.Control);
        }

        Assert.Multiple(
            () => Assert.Equal([1, 2, 3, 4], new[] { afterBend, afterStub, afterJog, afterReconnect }),
            () => Assert.Same(other.Port("value"), reconnectedTo),
            () => Assert.Same(sink.Port("value"), wire.Target),
            () => Assert.Equal(original, wire.Route),
            () => Assert.Same(wire, Assert.Single(host.Page.Wires)));
    });

    /// <summary>
    ///     Validates that an annotation can be resized by its handle and its text edited in place, each as one undo entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Annotation_ResizeAndInlineEdit_AreUndoable() => UiTest.Run(host =>
    {
        // Arrange: a selected annotation
        var note = host.Page.AddAnnotation("old text", new PlexusRect(150, 150, 150, 60));
        host.Editor.Select(note);
        host.Flush();
        var undo = host.Diagram.History.UndoCount;
        var handle = new Point(note.Bounds.Right - 3, note.Bounds.Bottom - 3);

        // Act: resize with the handle
        host.Drag(handle, new Point(handle.X + 45, handle.Y + 30));
        var resized = note.Bounds;
        var afterResize = host.Diagram.History.UndoCount;

        // Act: double-click inside the annotation, type, and commit with Ctrl+Enter
        host.DoubleClick(new Point(180, 170));
        var editing = host.Editor.IsEditingAnnotation;
        host.Editor.AnnotationEditor!.Text = "new text";
        host.PressFocused(Key.Enter, RawInputModifiers.Control);

        // Assert: both edits are single entries, and undo walks back through them
        Assert.Multiple(
            () => Assert.True(resized.Width > 150),
            () => Assert.Equal(undo + 1, afterResize),
            () => Assert.True(editing),
            () => Assert.Equal("new text", note.Text),
            () => Assert.Equal(undo + 2, host.Diagram.History.UndoCount));
        host.Diagram.History.Undo();
        Assert.Equal("old text", note.Text);
    });

    /// <summary>
    ///     Validates that clicking a group's toggle collapses it undoably and clicking again expands it.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_GroupToggle_CollapsesAndExpandsUndoably() => UiTest.Run(host =>
    {
        // Arrange: a group of two nodes
        var a = host.AddSource(100, 100);
        var b = host.AddSink(300, 100);
        var group = host.Page.CreateGroup([a, b], "Pair");
        host.Flush();
        var undo = host.Diagram.History.UndoCount;

        // Act: collapse, undo, collapse again, expand
        host.Click(host.FindHit(HitKind.GroupCollapseToggle, group));
        var collapsed = group.IsCollapsed;
        var entries = host.Diagram.History.UndoCount - undo;
        host.Press(Key.Z, RawInputModifiers.Control);
        var afterUndo = group.IsCollapsed;
        host.Click(host.FindHit(HitKind.GroupCollapseToggle, group));
        host.Click(host.FindHit(HitKind.GroupCollapseToggle, group));

        // Assert
        Assert.Multiple(
            () => Assert.True(collapsed),
            () => Assert.Equal(1, entries),
            () => Assert.False(afterUndo),
            () => Assert.False(group.IsCollapsed));
    });

    /// <summary>
    ///     Validates that wheel zoom, wheel scroll, middle-button pan and zoom-to-fit change the view without touching the model.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_PanAndZoom_ChangeViewWithoutEditingModel() => UiTest.Run(host =>
    {
        // Arrange: two far apart nodes
        host.AddSource(60, 60);
        host.AddSink(2000, 1500);
        var undo = host.Diagram.History.UndoCount;
        var cursor = new Point(300, 200);
        host.Window.MouseMove(cursor);
        var anchor = host.Editor.ScreenToDiagram(cursor);

        // Act: zoom around the cursor, scroll, pan with the middle button, then fit
        host.Window.MouseWheel(cursor, new Vector(0, 2), RawInputModifiers.Control);
        host.Flush();
        var zoomed = host.Editor.Zoom;
        var underCursor = host.Editor.ScreenToDiagram(cursor);
        var beforeScroll = host.Editor.Offset;
        host.Window.MouseWheel(cursor, new Vector(0, -1), RawInputModifiers.None);
        host.Flush();
        var scrolled = host.Editor.Offset != beforeScroll;
        var beforePan = host.Editor.Offset;
        host.Window.MouseDown(new Point(100, 100), MouseButton.Middle);
        host.Window.MouseMove(new Point(160, 130), RawInputModifiers.MiddleMouseButton);
        host.Window.MouseUp(new Point(160, 130), MouseButton.Middle);
        host.Flush();
        var panned = host.Editor.Offset != beforePan;
        host.Editor.ZoomToFit();

        // Assert
        Assert.Multiple(
            () => Assert.True(zoomed > 1.2),
            () => Assert.Equal(anchor.X, underCursor.X, 6),
            () => Assert.Equal(anchor.Y, underCursor.Y, 6),
            () => Assert.True(scrolled),
            () => Assert.True(panned),
            () => Assert.True(host.Editor.Zoom < 1),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates the delete, undo, redo, select-all, duplicate and rename shortcuts.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_KeyboardShortcuts_DeleteUndoRedoSelectAllAndDuplicate() => UiTest.Run(host =>
    {
        // Arrange: a connected pair, the source selected
        var source = host.AddSource(100, 100, "Src");
        var sink = host.AddSink(400, 100, "Snk");
        source.Port("value").ConnectTo(sink.Port("value"));
        host.Editor.Select(source);

        // Act: Delete removes the node and its wire, Ctrl+Z brings both back, Ctrl+Y and Ctrl+Shift+Z both redo the delete
        host.Press(Key.Delete);
        var deleted = (host.Page.Nodes.Count, host.Page.Wires.Count);
        host.Press(Key.Z, RawInputModifiers.Control);
        var restored = (host.Page.Nodes.Count, host.Page.Wires.Count);
        host.Press(Key.Y, RawInputModifiers.Control);
        var redone = (host.Page.Nodes.Count, host.Page.Wires.Count);
        host.Press(Key.Z, RawInputModifiers.Control);
        host.Press(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        var redoneByShift = (host.Page.Nodes.Count, host.Page.Wires.Count);
        host.Press(Key.Z, RawInputModifiers.Control);

        // Act / Assert: select all, duplicate, then F2 on the single selected copy
        host.Press(Key.A, RawInputModifiers.Control);
        host.Press(Key.D, RawInputModifiers.Control);
        var duplicated = host.Page.Nodes.Count;
        host.Editor.Select(host.Page.Nodes[0]);
        host.Press(Key.F2);

        // Assert
        Assert.Multiple(
            () => Assert.Equal((1, 0), deleted),
            () => Assert.Equal((2, 1), restored),
            () => Assert.Equal((1, 0), redone),
            () => Assert.Equal((1, 0), redoneByShift),
            () => Assert.Equal(4, duplicated),
            () => Assert.True(host.Editor.IsRenaming));
    });

    /// <summary>
    ///     Validates that Undo during a drag only abandons the drag, and the next Undo then reverts the previous entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Undo_DuringDrag_CancelsOnlyTheGesture() => UiTest.Run(host =>
    {
        // Arrange: a node (one history entry) that is being dragged
        var node = host.AddSource(150, 150);
        var start = host.CenterOf(node);
        var far = new Point(start.X + 90, start.Y + 60);
        host.BeginDrag(start);
        host.MoveTo(far);

        // Act: Ctrl+Z mid-drag, then release, then Ctrl+Z again
        host.Press(Key.Z, RawInputModifiers.Control);
        host.EndDrag(far);
        var nodeAfterFirst = host.Page.Nodes.Contains(node);
        var positionAfterFirst = node.Position;
        host.Press(Key.Z, RawInputModifiers.Control);

        // Assert: the first Undo only cancelled the drag; the second removed the node
        Assert.Multiple(
            () => Assert.True(nodeAfterFirst),
            () => Assert.Equal(new PlexusPoint(150, 150), positionAfterFirst),
            () => Assert.Empty(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates the inline rename pipeline: a taken name is rejected with a reason, a host veto is shown, and a valid name commits undoably.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Rename_DuplicateThenValidName_RejectsThenCommits() => UiTest.Run(host =>
    {
        // Arrange: two nodes and a host rule that forbids names starting with an underscore
        host.Diagram.NodeRenaming += (_, e) =>
        {
            if (e.ProposedName.StartsWith('_'))
            {
                e.Cancel = true;
                e.Reason = "No underscores.";
            }
        };
        var node = host.AddSource(150, 150, "First");
        host.AddSource(450, 150, "Second");

        // Act: double-click the name, try a taken name, then a vetoed name, then a good one
        host.DoubleClick(host.NameOf(node));
        var started = host.Editor.IsRenaming;
        host.Editor.RenameEditor!.Text = "Second";
        host.PressFocused(Key.Enter);
        var duplicateReason = host.Editor.RenameError;
        var stillOpenAfterDuplicate = host.Editor.IsRenaming;
        host.Editor.RenameEditor.Text = "_hidden";
        host.PressFocused(Key.Enter);
        var hostReason = host.Editor.RenameError;
        host.Editor.RenameEditor.Text = "Fresh";
        host.PressFocused(Key.Enter);

        // Assert
        Assert.Multiple(
            () => Assert.True(started),
            () => Assert.False(string.IsNullOrWhiteSpace(duplicateReason)),
            () => Assert.True(stillOpenAfterDuplicate),
            () => Assert.Equal("No underscores.", hostReason),
            () => Assert.Equal("Fresh", node.Name),
            () => Assert.False(host.Editor.IsRenaming));
        host.Diagram.History.Undo();
        Assert.Equal("First", node.Name);
    });

    /// <summary>
    ///     Validates that the operating-system clipboard receives the Plexus format and a plain-text copy, and that pasting restores the content.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Clipboard_SystemClipboard_CarriesPlexusFormatAndTextFallback() => UiTest.RunAsync(async host =>
    {
        // Arrange: a node, copied to the real clipboard of the window (no host clipboard assigned)
        var node = host.AddSource(100, 100, "Sys");
        host.Editor.Select(node);

        // Act: copy, then paste back
        await host.Editor.CopyAsync();
        var result = await host.Editor.PasteAsync();

        // Assert: both clipboard representations hold the same Plexus JSON and the paste added a second node
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
    ///     Validates that pasting unusable content and a platform clipboard failure are reported to the host as events instead of exceptions.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Clipboard_Failures_RaiseHostEvents() => UiTest.RunAsync(async host =>
    {
        // Arrange: handlers, a clipboard with unrelated text, and a selected node
        PasteFailedEventArgs? pasteFailure = null;
        ClipboardFailedEventArgs? clipboardFailure = null;
        host.Editor.PasteFailed += (_, e) => pasteFailure = e;
        host.Editor.ClipboardFailed += (_, e) => clipboardFailure = e;
        host.Editor.Clipboard = new FakeClipboard { Text = "just some words" };
        host.Editor.Select(host.AddSource(100, 100));

        // Act: paste the words, then swap in a clipboard that fails and press Ctrl+C
        var result = await host.Editor.PasteAsync();
        host.Editor.Clipboard = new LockedClipboard();
        host.Press(Key.C, RawInputModifiers.Control);
        for (var i = 0; i < 100 && clipboardFailure is null; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        // Assert: both failures arrived as events and the diagram is unchanged
        Assert.NotNull(result);
        Assert.Multiple(
            () => Assert.False(result.Success),
            () => Assert.Same(result, pasteFailure?.Result),
            () => Assert.NotNull(clipboardFailure),
            () => Assert.Single(host.Page.Nodes));
    });

    /// <summary>
    ///     Validates that dropping a node type payload creates a selected node at the drop point as one undo entry.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_DropNodeType_OntoEditor_CreatesNode() => UiTest.Run(host =>
    {
        // Arrange: exact placement so the centering can be checked
        host.Editor.SnapToGrid = false;
        var undo = host.Diagram.History.UndoCount;
        var at = new Point(400, 300);

        // Act: drop a node type
        using var transfer = DiagramEditor.CreateNodeTypeDataTransfer("test.Source");
        host.Window.DragDrop(at, RawDragEventType.DragEnter, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Window.DragDrop(at, RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Flush();

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal("test.Source", node.Type.Key),
            () => Assert.Equal(400, node.Bounds.Center.X, 1),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.Equal(undo + 1, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates the palette end to end: it lists the registry's types, an item looks exactly like the node dropped on the canvas, and double-click activates a type.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_NodePalette_ItemsLookLikeCanvasNodes_AndActivate() => UiTest.Run(() =>
    {
        // Act: the pixel proof for a node with icon, ports and summary, and one with a custom visual
        NodePalettePixelTests.AssertItemMatchesDroppedNode("test.Mixer", 1.0, false);
        NodePalettePixelTests.AssertItemMatchesDroppedNode("test.Badge", 1.5, true);

        // Act: a palette over the registry, activated by double-click
        using var host = new PaletteHost();
        var activated = new List<string>();
        host.Palette.NodeTypeActivated += (_, e) => activated.Add(e.NodeType.Key);
        var center = host.CenterOf(host.ItemFor("test.Sink"));
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(host.Types.Count, host.Palette.Items.Count),
            () => Assert.Equal(["test.Sink"], activated));
    });

    /// <summary>
    ///     Validates that the host is told about node double-clicks and context requests, with the element and position involved.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_HostEvents_DoubleClickAndContextRequest_AreRaised() => UiTest.Run(host =>
    {
        // Arrange: a node and handlers
        var node = host.AddSource(150, 150);
        var doubleClicked = new List<Node>();
        var contexts = new List<DiagramContextRequestedEventArgs>();
        host.Editor.NodeDoubleClicked += (_, e) => doubleClicked.Add(e.Node);
        host.Editor.DiagramContextRequested += (_, e) => contexts.Add(e);
        host.Editor.Offset = new Point(100, 50);
        host.Flush();

        // Act
        host.DoubleClick(host.CenterOf(node));
        host.RightClick(host.CenterOf(node));
        host.RightClick(new Point(600, 450));

        // Assert: the canvas request carries the diagram position of the click
        Assert.Multiple(
            () => Assert.Equal([node], doubleClicked),
            () => Assert.Equal(2, contexts.Count),
            () => Assert.Same(node, contexts[0].Element),
            () => Assert.Null(contexts[1].Element),
            () => Assert.Equal(new Point(700, 500), contexts[1].Position));
    });

    /// <summary>
    ///     Validates that a read-only editor still selects but neither moves nor deletes anything.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ReadOnly_DragAndDelete_LeaveDiagramUnchanged() => UiTest.Run(host =>
    {
        // Arrange: a node in a read-only editor
        var node = host.AddSource(150, 150);
        host.Editor.IsReadOnly = true;
        var undo = host.Diagram.History.UndoCount;
        var start = host.CenterOf(node);

        // Act: drag it, then try to delete it
        host.Drag(start, new Point(start.X + 90, start.Y + 60));
        host.Press(Key.Delete);

        // Assert: selected, but unchanged
        Assert.Multiple(
            () => Assert.Contains(node, host.Editor.Selection),
            () => Assert.Equal(new PlexusPoint(150, 150), node.Position),
            () => Assert.Contains(node, host.Page.Nodes),
            () => Assert.Equal(undo, host.Diagram.History.UndoCount));
    });

    /// <summary>
    ///     Validates that valid and malformed SVG icons both leave the editor displaying the diagram.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Icons_ValidAndMalformedSvg_DisplayWithoutFailure() => UiTest.Run(host =>
    {
        // Arrange: one node type with a good icon and one with a broken icon
        var good = IconSource.FromSvg(DiscSvg);
        var broken = IconSource.FromSvg("this is <not> svg");
        host.Diagram.Types.Add(new NodeType("test.Good") { Title = "GOOD", Icon = good });
        host.Diagram.Types.Add(new NodeType("test.Broken") { Title = "BROKEN", Icon = broken });
        host.Page.AddNode("test.Good", 60, 60);
        host.Page.AddNode("test.Broken", 300, 60);

        // Act: display both and paint a frame
        host.Flush();
        var frame = host.Window.CaptureRenderedFrame();

        // Assert: the good icon converts, the broken one is simply missing, and the frame was painted
        Assert.Multiple(
            () => Assert.NotNull(PlexusIcons.GetImage(good)),
            () => Assert.Null(PlexusIcons.GetImage(broken)),
            () => Assert.Equal(2, host.CurrentScene.Primitives.OfType<IconPrimitive>().Count()),
            () => Assert.NotNull(frame));
    });

    /// <summary>
    ///     Validates that the editor lays nodes out with Avalonia's text metrics: a longer name makes a wider name label.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Layout_LongerNodeName_WidensNameLabel() => UiTest.Run(host =>
    {
        // Arrange: two nodes whose names differ in length
        var shortNode = host.AddSource(60, 60, "A");
        var longNode = host.AddSource(60, 300, "AVeryLongNodeNameThatNeedsMoreRoom");

        // Act
        var scene = host.CurrentScene;
        var hasShort = scene.TryGetNodeLayout(shortNode, out var shortLayout);
        var hasLong = scene.TryGetNodeLayout(longNode, out var longLayout);

        // Assert
        Assert.Multiple(
            () => Assert.True(hasShort),
            () => Assert.True(hasLong),
            () => Assert.True(longLayout!.Name.Bounds.Width > shortLayout!.Name.Bounds.Width));
    });

    /// <summary>
    ///     Validates that the editor works in a headless window: it lays out to the window size and renders a frame.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_Headless_HostedEditor_LaysOutAndRendersFrame() => UiTest.Run(host =>
    {
        // Arrange: a node in the headless window
        host.AddSource(150, 150);
        host.Flush();

        // Act
        var frame = host.Window.CaptureRenderedFrame();

        // Assert: the editor fills the window and a frame of that size exists
        Assert.NotNull(frame);
        Assert.Multiple(
            () => Assert.Equal(800, host.Editor.Bounds.Width),
            () => Assert.Equal(600, host.Editor.Bounds.Height),
            () => Assert.Equal(800, frame.PixelSize.Width),
            () => Assert.Equal(600, frame.PixelSize.Height));
    });

    /// <summary>
    ///     Validates that the core editing session (add, connect by dragging, render, undo) works on the platform running the tests.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_EditingSession_ConnectRenderAndUndo_WorksOnThisPlatform() => UiTest.Run(host =>
    {
        // Arrange: a source and a sink
        var source = host.AddSource(60, 60);
        var sink = host.AddSink(400, 200);

        // Act: connect by dragging, render, undo the connection
        host.Drag(host.PortOf(source), host.PortOf(sink));
        var connected = host.Page.Wires.Count;
        var frame = host.Window.CaptureRenderedFrame();
        host.Press(Key.Z, RawInputModifiers.Control);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, connected),
            () => Assert.NotNull(frame),
            () => Assert.Empty(host.Page.Wires));
    });

    /// <summary>
    ///     Validates that hovering shows node, port and wire tooltips, that hosts can override them, and that they can be turned off.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ToolTips_HoverElements_ShowDescriptionsAndHostOverrides() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var bus = host.Page.AddNode("test.Bus", 300, 100, "B");
        host.Editor.ToolTipRequested += (_, e) =>
        {
            if (e.Port is { } port)
            {
                e.Content = $"{e.Content} on {port.Node.Name}";
            }
        };
        host.Flush();

        // Act: hover the node, then one of its ports, then turn tooltips off
        host.Window.MouseMove(host.CenterOf(bus));
        host.Flush();
        var nodeTip = ToolTip.GetTip(host.Editor);
        host.Window.MouseMove(host.FindHit(HitKind.Port, bus));
        host.Flush();
        var portTip = ToolTip.GetTip(host.Editor) as string;
        host.Editor.ShowToolTips = false;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Merges channels.", nodeTip),
            () => Assert.EndsWith(" on B", portTip, StringComparison.Ordinal),
            () => Assert.Null(ToolTip.GetTip(host.Editor)));
    });

    /// <summary>
    ///     Validates that the default tooltips describe a node, a port (label, type and description) and a wire (source to target).
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ToolTips_DefaultContent_NodePortAndWire() => UiTest.Run(() =>
    {
        // Arrange: a described bus fed by a source
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var source = host.AddSource(60, 300, "S");
        var bus = host.Page.AddNode("test.Bus", 400, 60, "B");
        var wire = host.Page.Connect(source.Port("value"), bus.Input("in0"));
        host.Flush();

        // Act: hover the node body, a port and the wire
        var tips = new List<object?>();
        foreach (var at in new[] { host.CenterOf(bus), host.FindHit(HitKind.Port, bus), host.FindHit(HitKind.Wire, wire) })
        {
            host.Window.MouseMove(at);
            host.Flush();
            tips.Add(ToolTip.GetTip(host.Editor));
        }

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Merges channels.", tips[0]),
            () => Assert.Equal(host.Editor.GetToolTipContent(bus, host.Editor.HitTest(host.FindHit(HitKind.Port, bus)).Port), tips[1]),
            () => Assert.Matches(@"^in\d \(f32\)\nChannel \d$", tips[1] as string),
            () => Assert.Equal("S.value → B.in0", tips[2]));
    });

    /// <summary>
    ///     Validates that a host that cancels a tooltip, or turns tooltips off, makes hovering show nothing.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ToolTips_HostCancelOrDisable_ShowNoTip() => UiTest.Run(() =>
    {
        // Arrange: a handler that cancels node tooltips only
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var bus = host.Page.AddNode("test.Bus", 300, 100, "B");
        var requests = 0;
        host.Editor.ToolTipRequested += (_, e) =>
        {
            requests++;
            e.Cancel = e.Port is null;
        };
        host.Flush();

        // Act: hover the node (canceled) and a port (shown), then turn tooltips off and hover both again
        host.Window.MouseMove(host.CenterOf(bus));
        host.Flush();
        var canceled = ToolTip.GetTip(host.Editor);
        host.Window.MouseMove(host.FindHit(HitKind.Port, bus));
        host.Flush();
        var shown = ToolTip.GetTip(host.Editor);
        host.Editor.ShowToolTips = false;
        var requestsWhenOff = requests;
        host.Window.MouseMove(host.CenterOf(bus));
        host.Window.MouseMove(host.FindHit(HitKind.Port, bus));
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Null(canceled),
            () => Assert.NotNull(shown),
            () => Assert.Null(ToolTip.GetTip(host.Editor)),
            () => Assert.Equal(requestsWhenOff, requests));
    });

    /// <summary>
    ///     Validates that pressing a button and moving the pointer out of the editor hide the tooltip.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ToolTips_PressAndPointerExit_HideTip() => UiTest.Run(() =>
    {
        // Arrange: a bus with a shown tooltip
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var bus = host.Page.AddNode("test.Bus", 300, 100, "B");
        host.Flush();
        host.Window.MouseMove(host.CenterOf(bus));
        host.Flush();
        var shown = ToolTip.GetTip(host.Editor);

        // Act: press and drag the node, then hover it again, then leave the editor
        host.Drag(host.CenterOf(bus), host.CenterOf(bus) + new Point(40, 0));
        var afterPress = ToolTip.GetTip(host.Editor);
        host.Window.MouseMove(new Point(700, 550));
        host.Flush();
        host.Window.MouseMove(host.CenterOf(bus));
        host.Flush();
        var shownAgain = ToolTip.GetTip(host.Editor);
        host.Window.MouseMove(new Point(-50, -50));
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(shown),
            () => Assert.Null(afterPress),
            () => Assert.NotNull(shownAgain),
            () => Assert.Null(ToolTip.GetTip(host.Editor)));
    });

    /// <summary>
    ///     Validates that a host can navigate to an element on another page and fit a set of elements.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_ElementNavigation_FindAndFit_ShowElements() => UiTest.Run(host =>
    {
        // Arrange: a "find node" on another page, and the selection to fit on the first
        var second = host.Diagram.Pages.Add("Second");
        second.AddNode("test.Sink", 3000, 2500, "Target");
        var a = host.AddSource(0, 0);
        var b = host.AddSink(2000, 1500);
        var view = new Rect(host.Editor.Bounds.Size);

        // Act: find by name and show it, then go back and fit the two nodes
        var found = host.Diagram.FindNode("Target")!;
        host.Editor.BringIntoView(found);
        var foundVisible = view.Contains(host.Editor.GetElementScreenBounds(found));
        var shownPage = host.Editor.Page;
        host.Editor.ZoomToFit([a, b]);

        // Assert
        Assert.Multiple(
            () => Assert.Same(second, shownPage),
            () => Assert.True(foundVisible),
            () => Assert.Same(host.Page, host.Editor.Page),
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(a))),
            () => Assert.True(view.Contains(host.Editor.GetElementScreenBounds(b))));
    });

    /// <summary>
    ///     Validates that the editor reflects ports a port provider recomputes, connects to them and undoes the change in one step.
    /// </summary>
    [Fact]
    public Task PlexusAvalonia_DynamicPorts_PropertyChange_EditorConnectsAndUndoes() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new EditorHost(configureTypes: EditorHost.AddBus);
        var source = host.AddSource(60, 300);
        var bus = host.Page.AddNode("test.Bus", 400, 60);
        bus.Properties["Channels"] = 3;
        host.Flush();

        // Act: connect to the new port, then lower the channel count, then undo
        host.Drag(host.PortOf(source), host.Editor.GetPortScreenPosition(bus.Input("in2")));
        var connected = host.Page.Wires.Count;
        bus.Properties["Channels"] = 2;
        host.Flush();
        var afterRemoval = host.Page.Wires.Count;
        host.Editor.Undo();
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, connected),
            () => Assert.Equal(0, afterRemoval),
            () => Assert.Same(bus.Input("in2"), Assert.Single(host.Page.Wires).Target),
            () => Assert.True(host.CurrentScene.TryGetNodeLayout(bus, out var layout) && layout.Ports.Count == 4));
    });
    /// <summary>
    ///     A clipboard whose every operation fails like a locked desktop session.
    /// </summary>
    private sealed class LockedClipboard : IDiagramClipboard
    {
        /// <inheritdoc />
        public Task SetAsync(string json) => Task.FromException(new System.Runtime.InteropServices.COMException("locked", unchecked((int)0x800401D0)));

        /// <inheritdoc />
        public Task<string?> GetAsync() => Task.FromException<string?>(new System.Runtime.InteropServices.COMException("locked", unchecked((int)0x800401D0)));
    }
}
