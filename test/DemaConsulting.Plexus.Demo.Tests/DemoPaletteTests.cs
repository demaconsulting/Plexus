using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the demo's use of the library's <see cref="NodePalette"/> and its live wire styling in the window.
/// </summary>
public class DemoPaletteTests
{
    /// <summary>
    ///     The categories in the order the demo registers them.
    /// </summary>
    private static readonly string[] Categories =
    [
        DemoNodeTypes.MathCategory, DemoNodeTypes.PlumbingCategory, DemoNodeTypes.LogicCategory, DemoNodeTypes.AudioCategory, DemoNodeTypes.WorkflowCategory,
    ];

    /// <summary>
    ///     Validates that the palette lists every node type, grouped under one header per category in registration order.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_ListsEveryTypeGroupedByCategory() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());
        var registry = host.Session.Diagram.Types;
        var visible = host.Window.Palette.VisibleTypes;
        var headers = host.Window.Palette.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        // Assert: all 30 types, contiguous by category in the registry's order, each category announced by a header
        Assert.Equal(registry.Select(t => t.Key), visible.Select(t => t.Key));
        Assert.Equal(30, visible.Count);
        Assert.Equal(Categories, visible.Select(t => t.Category!).Distinct());
        Assert.All(Categories, category => Assert.Contains(category, headers));
        Assert.True(host.Window.Palette.GroupByCategory);
    });

    /// <summary>
    ///     Validates the library's id scheme for palette entries, which the automation scripts rely on.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_ItemsHaveTheNodePaletteIds() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());

        // Assert
        Assert.Equal("NodePalette_demo_audio_Mixer", AutomationIds.PaletteItem(DemoNodeTypes.Mixer));
        Assert.NotNull(host.Find("NodePalette_demo_audio_Mixer"));
        Assert.NotNull(host.Find("NodePalette_demo_workflow_End"));
        Assert.Same(host.Window.Palette, host.Get<NodePalette>("NodePalette"));
    });

    /// <summary>
    ///     Validates that typing in the filter box narrows the palette by name, key or category, and clearing it restores everything.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_FilterBox_NarrowsTheList() => GuiTest.Run(() =>
    {
        // Arrange: the search box is the palette's own, not one the window adds
        using var host = new MainWindowHost(MainWindowHost.Options());
        var filter = host.Get<TextBox>(AutomationIds.PaletteFilter);
        var palette = host.Window.Palette;

        // Act
        filter.Text = "pump";
        host.Flush();
        var byName = palette.VisibleTypes.Select(t => t.Key).ToList();
        filter.Text = "LOGIC";
        host.Flush();
        var byCategory = palette.VisibleTypes.Select(t => t.Key).ToList();
        filter.Text = string.Empty;
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal("NodePalette_Filter", AutomationIds.PaletteFilter),
            () => Assert.True(palette.ShowFilter),
            () => Assert.Contains(filter, palette.GetVisualDescendants()),
            () => Assert.Equal([DemoNodeTypes.Pump], byName),
            () => Assert.Equal(6, byCategory.Count),
            () => Assert.All(byCategory, key => Assert.StartsWith("demo.logic.", key, StringComparison.Ordinal)),
            () => Assert.Equal(30, palette.VisibleTypes.Count),
            () => Assert.Equal(string.Empty, palette.Filter));
    });

    /// <summary>
    ///     Validates that the window gives the palette nothing but the editor: the types and theme come from the editor's document.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_OnlyFollowsTheEditor() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());
        var palette = host.Window.Palette;

        // Assert
        Assert.Multiple(
            () => Assert.Same(host.Editor, palette.Editor),
            () => Assert.Null(palette.Types),
            () => Assert.Null(palette.DiagramTheme),
            () => Assert.Equal(host.Session.Diagram.Types.Select(t => t.Key), palette.VisibleTypes.Select(t => t.Key)));
    });

    /// <summary>
    ///     Validates that activating a palette entry adds that node type near the middle of the visible canvas and selects it, with no window code involved.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_Activation_AddsTheNodeAtTheVisibleCenter() => GuiTest.Run(() =>
    {
        // Arrange: an empty page, and an entry that is not necessarily on screen (no filtering needed)
        using var host = new MainWindowHost(MainWindowHost.Options());
        host.Session.New();
        host.Flush();
        var type = host.Session.Diagram.Types.Get(DemoNodeTypes.Switch);
        var center = host.Editor.ScreenToDiagram(new AvaloniaPoint(host.Editor.Bounds.Width / 2, host.Editor.Bounds.Height / 2));

        // Act
        host.Window.Palette.Activate(type);

        // Assert: one switch, selected, around the middle of the canvas, as one undo entry
        var node = Assert.Single(host.Session.RequirePage().Nodes);
        var grid = host.Session.Diagram.Theme.GridSize;
        Assert.Multiple(
            () => Assert.Equal(DemoNodeTypes.Switch, node.Type.Key),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.InRange(node.Bounds.Center.X, center.X - (2 * grid), center.X + (2 * grid)),
            () => Assert.InRange(node.Bounds.Center.Y, center.Y - (2 * grid), center.Y + (2 * grid)),
            () => Assert.Equal("Add node", host.Session.Diagram.History.UndoDescription));
    });

    /// <summary>
    ///     Validates that a real double-click on an entry that was scrolled into view adds the node.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_DoubleClick_OnScrolledIntoViewEntry_AddsTheNode() => GuiTest.Run(() =>
    {
        // Arrange: the last entry is below the fold; scroll to it instead of filtering the palette down
        using var host = new MainWindowHost(MainWindowHost.Options());
        host.Session.New();
        host.Flush();
        var type = host.Session.Diagram.Types.Get(DemoNodeTypes.Pump);
        Assert.True(host.Window.Palette.ScrollIntoView(type));
        host.Flush();
        var item = host.Get<Control>(AutomationIds.PaletteItem(DemoNodeTypes.Pump));

        // Act
        host.DoubleClick(host.CenterOf(item));

        // Assert
        var node = Assert.Single(host.Session.RequirePage().Nodes);
        Assert.Equal(DemoNodeTypes.Pump, node.Type.Key);
    });

    /// <summary>
    ///     Validates that a palette created for one document keeps listing the full set after New and Open replace the diagram.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_SurvivesDiagramReplacement() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());

        // Act
        host.Session.New();
        host.Session.OpenSample("logic");
        host.Flush();

        // Assert
        Assert.Equal(30, host.Window.Palette.VisibleTypes.Count);
        Assert.NotNull(host.Find("NodePalette_demo_logic_Switch"));
    });

    /// <summary>
    ///     Validates that double-clicking a switch toggles it (recoloring its wires) instead of opening the properties dialog.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_SwitchDoubleClick_TogglesAndRecolorsTheWires() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "logic"));
        var switchB = host.Session.RequirePage().Nodes.Single(n => n.Name == "SwitchB");
        var wire = switchB.Output("out").Wires[0];
        var off = host.Session.Diagram.WireStyler!(wire)!.Color;
        var point = host.Editor.TranslatePoint(host.Editor.DiagramToScreen(new AvaloniaPoint(switchB.Bounds.Center.X, switchB.Bounds.Center.Y)), host.Window) ?? default;

        // Act
        host.DoubleClick(point);
        var on = host.Session.Diagram.WireStyler!(wire)!.Color;

        // Assert: toggled, no dialog, one undo entry, and the wire is now the bright "true" color
        Assert.Multiple(
            () => Assert.True(switchB.Properties.Get<bool>("On")),
            () => Assert.Empty(host.Window.OwnedWindows.OfType<PropertyGridDialog>()),
            () => Assert.Equal("SwitchB switched on.", host.Session.Message),
            () => Assert.Equal(PlexusColor.Parse("#3A3A3A"), off),
            () => Assert.Equal(PlexusColor.Parse("#00E83C"), on));
    });

    /// <summary>
    ///     Validates that only switches get the Toggle entry in their context menu, that it works, and that Properties stays available.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Switch_OffersToggle() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "logic"));
        var nodes = host.Session.RequirePage().Nodes;
        var switchB = nodes.Single(n => n.Name == "SwitchB");
        var gate = nodes.Single(n => n.Name == "Carry");
        var menu = host.Window.BuildContextMenu(switchB, default);
        var gateMenu = host.Window.BuildContextMenu(gate, default);

        // Act
        host.Choose("Context_Node_Toggle", menu);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(host.Find("Context_Node_Properties", menu)),
            () => Assert.True(switchB.Properties.Get<bool>("On")),
            () => Assert.Null(host.Find("Context_Node_Toggle", gateMenu)),
            () => Assert.NotNull(host.Find("Context_Node_Properties", gateMenu)));
    });

    /// <summary>
    ///     Validates that every document the window creates or opens carries the demo's wire styler.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task DemoSession_Documents_AllCarryTheWireStyler() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());
        using var temp = new TempDirectory();
        var path = Path.Join(temp.Create(), "logic.json");
        DemoDocument.Save(DemoSamples.Logic(), path);

        // Act
        var initial = host.Session.Diagram.WireStyler;
        host.Session.New();
        var blank = host.Session.Diagram.WireStyler;
        host.Session.Open(path);
        var opened = host.Session.Diagram.WireStyler;

        // Assert
        Assert.NotNull(initial);
        Assert.NotNull(blank);
        Assert.NotNull(opened);
    });

    /// <summary>
    ///     Validates that the toggle-switch automation command flips the switch and rejects other nodes.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_ToggleSwitch_FlipsTheSwitchAndRejectsOtherNodes() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        host.Session.OpenSample("logic");
        var switchB = host.Session.RequirePage().Nodes.Single(n => n.Name == "SwitchB");

        // Act
        var first = await host.OkAsync("""{"cmd":"toggle-switch","node":"SwitchB"}""");
        var second = await host.OkAsync("""{"cmd":"toggle-switch","node":"SwitchB"}""");
        var refused = await host.ErrorAsync("""{"cmd":"toggle-switch","node":"Carry"}""");

        // Assert
        Assert.Multiple(
            () => Assert.True(first["on"]!.GetValue<bool>()),
            () => Assert.False(second["on"]!.GetValue<bool>()),
            () => Assert.False(switchB.Properties.Get<bool>("On")),
            () => Assert.Contains("not a switch", refused, StringComparison.Ordinal));
    });

    /// <summary>
    ///     Validates that a splitter sits between the palette and the canvas, with its AutomationId and sensible minimum widths, and that the palette starts one column wide.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PaletteSplitter_ExistsWithMinimumWidths() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());
        var splitter = host.Get<GridSplitter>(AutomationIds.PaletteSplitter);
        var columns = Assert.IsType<Grid>(splitter.Parent);

        // Assert: palette column, splitter column, editor column, then the splitter and column of the properties panel; the splitter is a column splitter
        Assert.Multiple(
            () => Assert.Equal("Main_PaletteSplitter", AutomationIds.PaletteSplitter),
            () => Assert.Same(host.Window.PaletteSplitter, splitter),
            () => Assert.Equal(GridResizeDirection.Columns, splitter.ResizeDirection),
            () => Assert.Equal(5, columns.ColumnDefinitions.Count),
            () => Assert.Same(host.Window.PaletteColumn, columns.ColumnDefinitions[0]),
            () => Assert.Equal(1, Grid.GetColumn(splitter)),
            () => Assert.Equal(MainWindow.MinPaletteWidth, columns.ColumnDefinitions[0].MinWidth),
            () => Assert.Equal(MainWindow.MinEditorWidth, columns.ColumnDefinitions[2].MinWidth),
            () => Assert.Equal(MainWindow.PaletteWidth, MainWindow.MinPaletteWidth),
            () => Assert.Equal(300, MainWindow.MinEditorWidth),
            () => Assert.Equal(MainWindow.PaletteWidth, host.Window.PaletteColumn.Width.Value),
            () => Assert.Equal(1, PaletteColumnCount(host)));
    });

    /// <summary>
    ///     Validates that widening the palette column, as dragging the splitter does, gives the palette two and then three columns, and narrowing it again one.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PaletteSplitter_WideningThePaletteShowsMoreColumns() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());
        var counts = new List<int>();

        // Act: the widths of the visual check, then back to the default
        foreach (var width in new[] { 420.0, 650.0, MainWindow.PaletteWidth })
        {
            host.Window.PaletteColumn.Width = new GridLength(width);
            host.Flush();
            counts.Add(PaletteColumnCount(host));
        }

        // Assert
        Assert.Equal([2, 3, 1], counts);
    });

    /// <summary>
    ///     Validates dragging the splitter with the mouse: the palette follows the pointer, stops at its minimum, and leaves the canvas its minimum width.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PaletteSplitter_DraggingResizesThePaletteWithinLimits() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());
        var splitter = host.Window.PaletteSplitter;
        void Drag(double dx)
        {
            var from = host.CenterOf(splitter);
            host.Window.MouseMove(from);
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(new AvaloniaPoint(from.X + (dx / 2), from.Y));
            host.Window.MouseMove(new AvaloniaPoint(from.X + dx, from.Y));
            host.Window.MouseUp(new AvaloniaPoint(from.X + dx, from.Y), MouseButton.Left);
            host.Flush();
        }

        // Act
        Drag(230);
        var wide = (host.Window.PaletteColumn.ActualWidth, Columns: PaletteColumnCount(host));
        Drag(-2000);
        var narrow = host.Window.PaletteColumn.ActualWidth;
        Drag(5000);
        var widest = host.Window.PaletteColumn.ActualWidth;
        var editorWidth = host.Editor.Bounds.Width;

        // Assert: 205 + 230 gives two columns; the palette never gets below its minimum nor leaves the canvas under its minimum
        Assert.Multiple(
            () => Assert.InRange(wide.ActualWidth, 400, 440),
            () => Assert.Equal(2, wide.Columns),
            () => Assert.Equal(MainWindow.MinPaletteWidth, narrow, 1),
            () => Assert.True(widest > 800, "the palette can be dragged wide"),
            () => Assert.True(editorWidth >= MainWindow.MinEditorWidth - 1, $"the canvas keeps its minimum width ({editorWidth})"));
    });

    /// <summary>
    ///     Gets the number of columns the palette shows its items in, from the positions of two neighbors in the first category.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The number of columns the items of the first category are laid out in (counting the distinct left edges of its first six entries).</returns>
    private static int PaletteColumnCount(MainWindowHost host)
    {
        var palette = host.Window.Palette;
        var types = new[] { DemoNodeTypes.Constant, DemoNodeTypes.Add, DemoNodeTypes.Multiply, DemoNodeTypes.Sine, DemoNodeTypes.Clamp, DemoNodeTypes.Scope };
        return types
            .Select(type => host.Get<Control>(AutomationIds.PaletteItem(type)).TranslatePoint(default, palette)!.Value.X)
            .Select(x => Math.Round(x))
            .Distinct()
            .Count();
    }
}
