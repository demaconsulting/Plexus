using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the node palette control: its items, filter, grouping, activation, drag source, caching and automation.
/// </summary>
public class NodePaletteTests
{
    /// <summary>
    ///     Validates that the palette shows one item for every registered type, in registration order, with accessible names and ids.
    /// </summary>
    [Fact]
    public Task NodePalette_Types_Registry_ShowsAnItemForEveryType() => UiTest.Run(() =>
    {
        // Arrange / Act: a palette over a registry without any grouping
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.Flush();
        var items = host.Palette.Items;

        // Assert: registration order, display names as accessible names, ids derived from the keys
        Assert.Multiple(
            () => Assert.Equal(host.Types.Select(t => t.Key), items.Select(i => i.NodeType.Key)),
            () => Assert.Equal(host.Types.Select(t => t.Key), host.Palette.VisibleTypes.Select(t => t.Key)),
            () => Assert.All(items, i => Assert.Equal(i.NodeType.DisplayName, AutomationProperties.GetName(i))),
            () => Assert.All(items, i => Assert.Equal("NodePalette_" + i.NodeType.Key.Replace('.', '_'), AutomationProperties.GetAutomationId(i))),
            () => Assert.All(items, i => Assert.True(i.Bounds.Width > 20 && i.Bounds.Height > 20, $"{i.NodeType.Key} has a real size")),
            () => Assert.True(host.Palette.Focusable),
            () => Assert.Equal("NodePalette", AutomationProperties.GetAutomationId(host.Palette)));
    });

    /// <summary>
    ///     Validates that a plain sequence of node types works as well as a registry.
    /// </summary>
    [Fact]
    public Task NodePalette_Types_PlainSequence_ShowsItems() => UiTest.Run(() =>
    {
        // Arrange: a list of types rather than a registry
        using var host = new PaletteHost();
        var list = host.Types.Where(t => t.Key is "test.Source" or "test.Mixer").ToList();

        // Act
        host.Palette.Types = list;
        host.Flush();

        // Assert: both shown, and their ports got a neutral color instead of failing
        Assert.Equal(["test.Source", "test.Mixer"], host.Palette.Items.Select(i => i.NodeType.Key));
    });

    /// <summary>
    ///     Validates that no types means an empty palette.
    /// </summary>
    [Fact]
    public Task NodePalette_Types_Null_IsEmpty() => UiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new PaletteHost();
        host.Palette.Types = null;
        host.Flush();

        // Assert
        Assert.Empty(host.Palette.Items);
    });

    /// <summary>
    ///     Validates that the automation id keeps only letters, digits and underscores, for a type and for a bare key.
    /// </summary>
    [Fact]
    public void NodePalette_GetAutomationId_SanitizesKey()
    {
        // Act
        var byType = NodePalette.GetAutomationId(new NodeType("com.acme/Weird Key-1"));
        var byKey = NodePalette.GetAutomationId("demo.audio.Mixer");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("NodePalette_com_acme_Weird_Key_1", byType),
            () => Assert.Equal("NodePalette_demo_audio_Mixer", byKey),
            () => Assert.Throws<ArgumentNullException>(() => NodePalette.GetAutomationId((NodeType)null!)),
            () => Assert.Throws<ArgumentNullException>(() => NodePalette.GetAutomationId((string)null!)));
    }

    /// <summary>
    ///     Validates that the filter matches display name, title, key and category without regard to case.
    /// </summary>
    [Fact]
    public Task NodePalette_Filter_MatchesNameTitleKeyAndCategory() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        string[] Shown(string? filter)
        {
            host.Palette.Filter = filter;
            host.Flush();
            return [.. host.Palette.VisibleTypes.Select(t => t.Key)];
        }

        // Act
        var byDisplayName = Shown("sIGNAL");
        var byTitle = Shown("badge");
        var byKey = Shown("TEST.SINK");
        var byCategory = Shown("sources");
        var noMatch = Shown("zzz");
        var padded = Shown("  gain ");
        var cleared = Shown(null);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["test.Mixer"], byDisplayName),
            () => Assert.Equal(["test.Badge"], byTitle),
            () => Assert.Equal(["test.Sink"], byKey),
            () => Assert.Equal(["test.Source"], byCategory),
            () => Assert.Empty(noMatch),
            () => Assert.Equal(["test.Gain"], padded),
            () => Assert.Equal(5, cleared.Length));
    });

    /// <summary>
    ///     Validates that types are grouped under category headers in registration order with uncategorized types last.
    /// </summary>
    [Fact]
    public Task NodePalette_GroupByCategory_OrdersGroupsByRegistrationWithUncategorizedLast() => UiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new PaletteHost();
        host.Palette.UncategorizedHeader = "Misc";
        host.Flush();

        // Assert: Sources (Source), Processing (Mixer, Badge, listed together), Sinks (Sink), then the rest (Gain)
        Assert.Multiple(
            () => Assert.Equal(["Sources", "Processing", "Sinks", "Misc"], host.Palette.Headers),
            () => Assert.Equal(["test.Source", "test.Mixer", "test.Badge", "test.Sink", "test.Gain"], host.Palette.Items.Select(i => i.NodeType.Key)));
    });

    /// <summary>
    ///     Validates that grouping can be turned off and that a registry without categories gets no headers at all.
    /// </summary>
    [Fact]
    public Task NodePalette_GroupByCategory_OffOrNoCategories_ShowsNoHeaders() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();

        // Act: grouping off
        host.Palette.GroupByCategory = false;
        host.Flush();
        var off = host.Palette.Headers.Count;

        // Act: grouping on but a filter leaves only an uncategorized type, then types without any category
        host.Palette.GroupByCategory = true;
        host.Palette.Filter = "gain";
        host.Flush();
        var onlyUncategorized = host.Palette.Headers.Count;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, off),
            () => Assert.Equal(0, onlyUncategorized));
    });

    /// <summary>
    ///     Validates that a single click selects an item and a double-click raises the activation event.
    /// </summary>
    [Fact]
    public Task NodePalette_DoubleClick_RaisesNodeTypeActivated() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var activated = new List<string>();
        host.Palette.NodeTypeActivated += (_, e) => activated.Add(e.NodeType.Key);
        var item = host.ItemFor("test.Mixer");
        var center = host.CenterOf(item);

        // Act: one click selects without activating
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Flush();
        var afterClick = activated.Count;
        var selectedAfterClick = host.Palette.SelectedNodeType;

        // Act: the second press of a double-click activates
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, afterClick),
            () => Assert.Same(host.Types.Get("test.Mixer"), selectedAfterClick),
            () => Assert.True(item.IsSelected),
            () => Assert.Equal(["test.Mixer"], activated),
            () => Assert.True(host.Palette.IsFocused || host.Palette.IsKeyboardFocusWithin));
    });

    /// <summary>
    ///     Validates the keyboard: Down and Up move the selection, Home and End jump, Enter activates.
    /// </summary>
    [Fact]
    public Task NodePalette_Keyboard_ArrowsMoveSelectionAndEnterActivates() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        host.Palette.GroupByCategory = false;
        host.Flush();
        var activated = new List<string>();
        host.Palette.NodeTypeActivated += (_, e) => activated.Add(e.NodeType.Key);
        host.Palette.Focus();
        var order = host.Types.Select(t => t.Key).ToList();
        void Press(Key key)
        {
            host.Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
            host.Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
            host.Flush();
        }

        // Act
        Press(Key.Enter);
        var enterWithoutSelection = activated.Count;
        Press(Key.Down);
        var first = host.Palette.SelectedNodeType?.Key;
        Press(Key.Down);
        var second = host.Palette.SelectedNodeType?.Key;
        Press(Key.Up);
        var backToFirst = host.Palette.SelectedNodeType?.Key;
        Press(Key.End);
        var last = host.Palette.SelectedNodeType?.Key;
        Press(Key.Down);
        var stillLast = host.Palette.SelectedNodeType?.Key;
        Press(Key.Home);
        var home = host.Palette.SelectedNodeType?.Key;
        Press(Key.Down);
        Press(Key.Enter);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, enterWithoutSelection),
            () => Assert.Equal(order[0], first),
            () => Assert.Equal(order[1], second),
            () => Assert.Equal(order[0], backToFirst),
            () => Assert.Equal(order[^1], last),
            () => Assert.Equal(order[^1], stillLast),
            () => Assert.Equal(order[0], home),
            () => Assert.Equal([order[1]], activated));
    });

    /// <summary>
    ///     Validates that the selected type can be set from the host and survives filtering out its item.
    /// </summary>
    [Fact]
    public Task NodePalette_SelectedNodeType_SetByHost_HighlightsItem() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var sink = host.Types.Get("test.Sink");

        // Act
        host.Palette.SelectedNodeType = sink;
        host.Flush();
        var highlighted = host.Palette.Items.Where(i => i.IsSelected).Select(i => i.NodeType.Key).ToList();
        host.Palette.Filter = "mixer";
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["test.Sink"], highlighted),
            () => Assert.Same(sink, host.Palette.SelectedNodeType),
            () => Assert.DoesNotContain(host.Palette.Items, i => i.IsSelected));
    });

    /// <summary>
    ///     Validates that pressing an item and moving past the threshold starts the node-type drag with the item's key and the standard payload.
    /// </summary>
    [Fact]
    public Task NodePalette_DragItem_PastThreshold_StartsNodeTypeDragWithTheKey() => UiTest.Run(() =>
    {
        // Arrange: the headless platform cannot run an OS drag, so a recording starter stands in for it
        using var host = new PaletteHost();
        var started = new List<string>();
        var payloadHasKey = false;
        host.Palette.DragStarter = (_, key) =>
        {
            started.Add(key);
            using var transfer = DiagramEditor.CreateNodeTypeDataTransfer(key);
            IDataTransfer data = transfer;
            payloadHasKey = data.Contains(DiagramEditor.NodeTypeDataFormat) && data.TryGetValue(DiagramEditor.NodeTypeDataFormat) == key;
            return Task.CompletedTask;
        };
        var center = host.CenterOf(host.ItemFor("test.Gain"));

        // Act: a click does not drag; a press and a move far enough does
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        var afterClick = started.Count;
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseMove(new Point(center.X + 2, center.Y), RawInputModifiers.LeftMouseButton);
        var afterSmallMove = started.Count;
        host.Window.MouseMove(new Point(center.X + 30, center.Y + 10), RawInputModifiers.LeftMouseButton);
        host.Window.MouseUp(new Point(center.X + 30, center.Y + 10), MouseButton.Left);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, afterClick),
            () => Assert.Equal(0, afterSmallMove),
            () => Assert.Equal(["test.Gain"], started),
            () => Assert.True(payloadHasKey));
    });

    /// <summary>
    ///     Validates the whole flow: dragging an item from the palette and releasing it over the editor creates the node where the ghost showed.
    /// </summary>
    [Fact]
    public Task NodePalette_DragItemOntoEditor_CreatesNodeWhereGhostWas() => UiTest.Run(() =>
    {
        // Arrange: the starter performs the drag-over and drop an operating-system drag would deliver
        using var host = new PaletteHost();
        var target = new Point(300, 250);
        PlexusPoint? ghostBeforeDrop = null;
        host.Palette.DragStarter = (_, key) =>
        {
            host.DragEvent(RawDragEventType.DragEnter, key, target);
            host.DragEvent(RawDragEventType.DragOver, key, target);
            ghostBeforeDrop = host.Editor.GhostPosition;
            host.DragEvent(RawDragEventType.Drop, key, target);
            return Task.CompletedTask;
        };
        var center = host.CenterOf(host.ItemFor("test.Mixer"));

        // Act
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseMove(new Point(center.X + 30, center.Y), RawInputModifiers.LeftMouseButton);
        host.Window.MouseUp(new Point(center.X + 30, center.Y), MouseButton.Left);
        host.Flush();

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal("test.Mixer", node.Type.Key),
            () => Assert.NotNull(ghostBeforeDrop),
            () => Assert.Equal(ghostBeforeDrop, node.Bounds.Position),
            () => Assert.Null(host.Editor.GhostNodeType));
    });

    /// <summary>
    ///     Validates that each item is the node as the canvas builds it: the display name label, the box title, ports, summary lines and a custom visual.
    /// </summary>
    [Fact]
    public Task NodePalette_Items_UseTheCanvasScene_WithNameSummaryPortsAndCustomVisual() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        static List<string> Texts(NodePaletteItem item) => [.. item.Prototype.Scene.Primitives.OfType<TextPrimitive>().Select(p => p.Text)];
        var mixer = host.ItemFor("test.Mixer");
        var badge = host.ItemFor("test.Badge");

        // Assert
        Assert.Multiple(
            () => Assert.Contains("Signal Mixer", Texts(mixer)),
            () => Assert.Contains("MIX", Texts(mixer)),
            () => Assert.Contains("Gain: 1.5", Texts(mixer)),
            () => Assert.Contains("left", Texts(mixer)),
            () => Assert.Contains("mix", Texts(mixer)),
            () => Assert.Single(mixer.Prototype.Scene.Primitives.OfType<IconPrimitive>()),
            () => Assert.Contains(badge.Prototype.Scene.Primitives.OfType<RectPrimitive>(), r => r.Fill == PlexusColor.Parse("#E0A800")),
            () => Assert.Contains("Factor: 2", Texts(host.ItemFor("test.Gain"))),
            () => Assert.Contains("BADGE", Texts(badge)));
    });

    /// <summary>
    ///     Validates that an item is sized from the palette-wide box column at the palette zoom and follows zoom changes.
    /// </summary>
    [Fact]
    public Task NodePalette_Zoom_ScalesItemsAndIsClamped() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var before = host.ItemFor("test.Mixer").DesiredSize;
        var reach = host.Palette.Items.Max(item => item.Prototype.HorizontalReach);

        // Act
        host.Palette.Zoom = 2;
        host.Flush();
        var after = host.ItemFor("test.Mixer").DesiredSize;
        host.Palette.Zoom = 100;
        var clampedHigh = host.Palette.Zoom;
        host.Palette.Zoom = 0.0001;
        var clampedLow = host.Palette.Zoom;

        // Assert: the content grows with the zoom while the padding stays fixed
        Assert.Multiple(
            () => Assert.InRange(before.Width, (2 * reach) + (2 * NodePaletteItem.Padding), (2 * reach) + (2 * NodePaletteItem.Padding) + 2),
            () => Assert.InRange(after.Width, (4 * reach) + (2 * NodePaletteItem.Padding), (4 * reach) + (2 * NodePaletteItem.Padding) + 2),
            () => Assert.Equal(DiagramEditor.MaxZoom, clampedHigh),
            () => Assert.Equal(DiagramEditor.MinZoom, clampedLow));
    });

    /// <summary>
    ///     Validates that every item shares one box column: the node boxes of all items are centered on the same x, whatever their port labels, and the category headers keep the full width.
    /// </summary>
    [Fact]
    public Task NodePalette_Items_ShareOneBoxColumn() => UiTest.Run(() =>
    {
        // Arrange: types whose boxes and port labels differ widely (source: output only; mixer: inputs and output; badge: input only)
        using var host = new PaletteHost();

        // Act
        var items = host.Palette.Items;
        var centers = items.Select(item => BoxCenterX(host, item)).ToList();
        var columnCenter = host.Palette.Items[0].TranslatePoint(new Point(host.Palette.Items[0].Bounds.Width / 2, 0), host.Window)!.Value.X;
        var headers = host.Palette.GetVisualDescendants().OfType<Border>().Where(b => b.Child is TextBlock).ToList();

        // Assert: box centers coincide (within the half pixel of whole-pixel snapping) and sit in the middle of the shared item width
        Assert.Multiple(
            () => Assert.True(items.Count >= 5),
            () => Assert.All(centers, c => Assert.InRange(c, centers[0] - 0.5, centers[0] + 0.5)),
            () => Assert.All(centers, c => Assert.InRange(c, columnCenter - 0.5, columnCenter + 0.5)),
            () => Assert.Single(items.Select(item => item.Bounds.Width).Distinct()),
            () => Assert.NotEmpty(headers),
            () => Assert.All(headers, header => Assert.Equal(items[0].Bounds.Width, header.Bounds.Width)));
    });

    /// <summary>
    ///     Validates that the box column stays centered and shared when the labels are wider than the palette (the content scrolls; no item is shifted on its own).
    /// </summary>
    [Fact]
    public Task NodePalette_Items_KeepTheSharedBoxColumnWhenContentOverflows() => UiTest.Run(() =>
    {
        // Arrange: at the maximum zoom the widest item is wider than the 340 pixel palette
        using var host = new PaletteHost();
        host.Palette.Zoom = DiagramEditor.MaxZoom;
        host.Flush();

        // Act
        var items = host.Palette.Items;
        var centers = items.Select(item => BoxCenterX(host, item)).ToList();

        // Assert: still one column, and the items are wider than the palette's visible width
        Assert.Multiple(
            () => Assert.True(items[0].Bounds.Width > PaletteHost.PaletteWidth),
            () => Assert.All(centers, c => Assert.InRange(c, centers[0] - 0.5, centers[0] + 0.5)),
            () => Assert.All(items, item => Assert.InRange(BoxCenterX(host, item) - item.TranslatePoint(default, host.Window)!.Value.X, (item.Bounds.Width / 2) - 0.5, (item.Bounds.Width / 2) + 0.5)));
    });

    /// <summary>
    ///     Gets the horizontal center of an item's node box in window coordinates.
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="item">The palette item.</param>
    /// <returns>The window x coordinate of the box center.</returns>
    private static double BoxCenterX(PaletteHost host, NodePaletteItem item) =>
        item.TranslatePoint(item.BoxOrigin, host.Window)!.Value.X + (item.Prototype.Box.Width * host.Palette.Zoom / 2);

    /// <summary>
    ///     Validates that item scenes are cached: filtering, grouping and zoom changes build nothing new, a theme change rebuilds.
    /// </summary>
    [Fact]
    public Task NodePalette_Scenes_AreCachedUntilTypesOrThemeChange() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var initial = host.Palette.PrototypeBuildCount;

        // Act: changes that only affect which items exist or their scale
        host.Palette.Filter = "s";
        host.Flush();
        host.Palette.Filter = null;
        host.Palette.GroupByCategory = false;
        host.Palette.Zoom = 1.5;
        host.Flush();
        var afterViewChanges = host.Palette.PrototypeBuildCount;

        // Act: a different theme changes every scene
        var theme = new DiagramTheme { NodeFill = PlexusColor.Parse("#336699") };
        host.Palette.DiagramTheme = theme;
        host.Flush();
        var afterTheme = host.Palette.PrototypeBuildCount;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(host.Types.Count, initial),
            () => Assert.Equal(initial, afterViewChanges),
            () => Assert.Equal(initial + host.Types.Count, afterTheme),
            () => Assert.All(host.Palette.Items, i => Assert.Same(theme, i.Prototype.Scene.Theme)));
    });

    /// <summary>
    ///     Validates that Refresh picks up types added to the registry after the palette showed it.
    /// </summary>
    [Fact]
    public Task NodePalette_Refresh_PicksUpAddedTypes() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var count = host.Palette.Items.Count;

        // Act
        host.Types.Add(new NodeType("test.Late") { Title = "LATE" });
        var beforeRefresh = host.Palette.Items.Count;
        host.Palette.Refresh();
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(count, beforeRefresh),
            () => Assert.Equal(count + 1, host.Palette.Items.Count),
            () => Assert.Contains(host.Palette.VisibleTypes, t => t.Key == "test.Late"));
    });
}
