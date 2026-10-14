using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests that the demo uses the library's simple path: direct page binding, icon images, drag-and-drop
///     node creation, inline annotation editing, editor undo, deletion by element and scene-published group geometry.
/// </summary>
public class DemoUsabilityTests
{
    /// <summary>
    ///     Validates that the page tabs are bound straight to the diagram's page collection, also after the diagram is replaced.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PageTabs_BindDirectlyToTheDiagramPagesAndFollowReplacement() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "showcase"));
        var tabs = host.Get<TabStrip>("Pages_Tabs");
        var boundToFirst = ReferenceEquals(tabs.ItemsSource, host.Session.Diagram.Pages);
        var firstCount = tabs.ItemCount;

        // Act: replace the diagram, then add a page through the model
        host.Session.New();
        host.Flush();
        var boundToSecond = ReferenceEquals(tabs.ItemsSource, host.Session.Diagram.Pages);
        host.Session.Diagram.Pages.Add("Extra");
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.True(boundToFirst),
            () => Assert.Equal(7, firstCount),
            () => Assert.True(boundToSecond),
            () => Assert.Equal(2, tabs.ItemCount),
            () => Assert.Equal(host.Session.Diagram.Pages[0], tabs.SelectedItem));
    });

    /// <summary>
    ///     Validates that a new document is clean and has no undo history without any explicit MarkClean.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task DemoSession_New_IsCleanWithEmptyHistory() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());

        // Act
        host.Session.New();

        // Assert
        Assert.Multiple(
            () => Assert.False(host.Session.IsDirty),
            () => Assert.False(host.Session.Diagram.History.CanUndo),
            () => Assert.Single(host.Session.Diagram.Pages));
    });

    /// <summary>
    ///     Validates that the palette's items are drawn from the node types themselves, so the icons the canvas shows are the palette's.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_ItemsAreTheNodeTypes() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());
        var mixer = host.Session.Diagram.Types.Get(DemoNodeTypes.Mixer);

        // Act
        var visible = host.Window.Palette.VisibleTypes;

        // Assert: the palette lists the same types (by key and display name) as the diagram's registry
        Assert.Equal(host.Session.Diagram.Types.Select(t => t.Key), visible.Select(t => t.Key));
        Assert.Equal(mixer.DisplayName, visible.Single(t => t.Key == DemoNodeTypes.Mixer).DisplayName);
    });

    /// <summary>
    ///     Validates that a node type dragged from the palette and dropped on the canvas is created at the drop point.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_DropOnCanvas_AddsNodeAtTheDropPoint() => GuiTest.Run(() =>
    {
        // Arrange: a blank page and a drop in the middle of the editor
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        host.Session.New();
        host.Flush();
        var at = host.CenterOf(host.Editor);
        var dropped = host.Editor.ScreenToDiagram(host.Window.TranslatePoint(at, host.Editor) ?? default);
        using var transfer = DiagramEditor.CreateNodeTypeDataTransfer(DemoNodeTypes.Scope);

        // Act
        host.Window.DragDrop(at, RawDragEventType.DragOver, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Window.DragDrop(at, RawDragEventType.Drop, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        host.Flush();

        // Assert: one scope, selected, centered near the drop point (snapped to the grid), one undo entry
        var node = Assert.Single(host.Session.RequirePage().Nodes);
        var grid = host.Session.Diagram.Theme.GridSize;
        Assert.Multiple(
            () => Assert.Equal(DemoNodeTypes.Scope, node.Type.Key),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.InRange(node.Bounds.Center.X, dropped.X - grid, dropped.X + grid),
            () => Assert.InRange(node.Bounds.Center.Y, dropped.Y - grid, dropped.Y + grid),
            () => Assert.True(host.Session.IsDirty),
            () => Assert.Equal("Add node", host.Session.Diagram.History.UndoDescription));
    });
    /// <summary>
    ///     Validates that the annotation context menu starts the editor's inline text edit instead of opening a dialog.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Annotation_EditTextStartsInlineEdit() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var annotation = host.Session.RequirePage().Annotations.Single();
        var menu = host.Window.BuildContextMenu(annotation, default);

        // Act
        host.Choose("Context_Annotation_EditText", menu);
        host.Get<TextBox>(DiagramEditor.AnnotationEditorAutomationId).Text = "Edited in place";
        var committed = host.Editor.CommitEdit();

        // Assert
        Assert.Multiple(
            () => Assert.True(committed),
            () => Assert.Equal("Edited in place", annotation.Text),
            () => Assert.False(host.Editor.IsEditing),
            () => Assert.Equal("Edit annotation text", host.Session.Diagram.History.UndoDescription));
    });

    /// <summary>
    ///     Validates that the Undo menu goes through the editor, which only abandons an inline rename (no history undo) while one is open.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_UndoMenu_DuringInlineEdit_OnlyAbandonsTheEdit() => GuiTest.Run(() =>
    {
        // Arrange: one recorded change, then an inline rename in progress
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var node = host.Session.RequirePage().Nodes[0];
        var original = node.Position;
        node.MoveTo(original.X + 30, original.Y);
        var moved = node.Position;
        host.Editor.BeginRename(node);
        host.Flush();

        // Act
        host.Choose("Menu_Edit_Undo");
        var afterFirst = node.Position;
        host.Choose("Menu_Edit_Undo");

        // Assert: the first Undo only closed the rename editor, the second reverted the move
        Assert.Multiple(
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.Equal(moved, afterFirst),
            () => Assert.Equal(original, node.Position));
    });

    /// <summary>
    ///     Validates that the automation delete removes the named elements without changing which other elements are selected.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Delete_DoesNotTouchTheSelection() => GuiTest.RunAsync(async host =>
    {
        // Arrange: B is selected, A is deleted by name
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"A"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":200,"y":0,"name":"B"}""");
        await host.OkAsync("""{"cmd":"select","names":["B"]}""");

        // Act
        var result = await host.OkAsync("""{"cmd":"delete","names":["A"]}""");

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, result["deleted"]!.GetValue<int>()),
            () => Assert.Equal(["B"], host.Editor.Selection.OfType<Node>().Select(n => n.Name)),
            () => Assert.Equal(["B"], host.Session.RequirePage().Nodes.Select(n => n.Name)));
    });

    /// <summary>
    ///     Validates that the automation undo command only cancels an open inline rename, like the editor's own Undo, and the next one undoes.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_Undo_RoutesThroughTheEditor() => GuiTest.RunAsync(async host =>
    {
        // Arrange: an added node and a rename in progress
        await host.OkAsync("""{"cmd":"add-node","type":"demo.audio.Filter","x":0,"y":0,"name":"A"}""");
        host.Editor.BeginRename(host.Session.RequirePage().Nodes.Single());

        // Act
        var first = await host.OkAsync("""{"cmd":"undo"}""");
        var nodesAfterFirst = host.Session.RequirePage().Nodes.Count;
        var second = await host.OkAsync("""{"cmd":"undo"}""");

        // Assert
        Assert.Multiple(
            () => Assert.True(first["performed"]!.GetValue<bool>()),
            () => Assert.False(host.Editor.IsRenaming),
            () => Assert.Equal(1, nodesAfterFirst),
            () => Assert.True(second["performed"]!.GetValue<bool>()),
            () => Assert.Empty(host.Session.RequirePage().Nodes));
    });

    /// <summary>
    ///     Validates that a collapsed group's reported bounds are the box the scene draws (which grows to fit proxy ports), and that its toggle is where the editor hit-tests it.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationGeometry_CollapsedGroup_ReportsTheSceneFrame() => GuiTest.RunAsync(async host =>
    {
        // Arrange: a constant wired into a collapsed group
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Constant","x":0,"y":30,"name":"Src"}""");
        await host.OkAsync("""{"cmd":"add-node","type":"demo.math.Scope","x":300,"y":30,"name":"Plot"}""");
        await host.OkAsync("""{"cmd":"connect","from":"Src.value","to":"Plot.signal"}""");
        var group = await host.OkAsync("""{"cmd":"group","names":["Plot"],"title":"Display"}""");
        await host.OkAsync($$"""{"cmd":"collapse","group":"{{group["id"]}}","collapsed":true}""");
        await host.OkAsync("""{"cmd":"view","zoom":1,"offsetX":0,"offsetY":0}""");
        var element = (Group)host.Session.Diagram.FindElement(group["id"]!.GetValue<string>())!;

        // Act
        var geometry = await host.OkAsync($$"""{"cmd":"geometry","element":"{{group["id"]}}"}""");

        // Assert: the reported bounds equal the scene frame
        Assert.True(host.Editor.Scene!.TryGetGroupLayout(element, out var layout));
        var bounds = geometry["bounds"]!["diagram"]!;
        Assert.Multiple(
            () => Assert.Equal(layout.Frame.X, bounds["x"]!.GetValue<double>(), 6),
            () => Assert.Equal(layout.Frame.Y, bounds["y"]!.GetValue<double>(), 6),
            () => Assert.Equal(layout.Frame.Height, bounds["height"]!.GetValue<double>(), 6),
            () => Assert.True(geometry["collapsed"]!.GetValue<bool>()),
            () => Assert.NotNull(geometry["toggle"]));
    });

    /// <summary>
    ///     Validates that the basic registry is the full registry minus the non-math types, with port types kept.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_CreateBasicRegistry_KeepsOnlyBasicTypesAndAllPortTypes()
    {
        // Arrange / Act
        var basic = DemoNodeTypes.CreateBasicRegistry();
        var full = DemoNodeTypes.CreateRegistry();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(DemoNodeTypes.BasicTypeKeys.Order(), basic.Select(t => t.Key).Order()),
            () => Assert.True(full.Count > basic.Count),
            () => Assert.Equal(full.PortTypes.Select(p => p.Key).Order(), basic.PortTypes.Select(p => p.Key).Order()),
            () => Assert.NotSame(full, basic));
    }
}
