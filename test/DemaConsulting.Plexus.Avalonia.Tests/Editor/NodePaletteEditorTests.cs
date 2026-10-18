using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     Tests of the node palette's zero-code mode: following an editor, default activation, scrolling and selecting by type,
///     the UI Automation patterns of its items, and the built-in search box.
/// </summary>
public class NodePaletteEditorTests
{
    /// <summary>
    ///     Gets the scroll viewer that holds the palette's entries (not the one inside the search box).
    /// </summary>
    /// <param name="host">The host.</param>
    /// <returns>The palette's own scroll viewer.</returns>
    private static ScrollViewer ScrollerOf(PaletteHost host) => host.Palette.Items[0].FindAncestorOfType<ScrollViewer>()!;

    /// <summary>
    ///     Validates that a palette given only an editor lists the types of the editor's diagram and draws with its theme.
    /// </summary>
    [Fact]
    public Task NodePalette_Editor_TakesTypesAndThemeFromTheEditor() => UiTest.Run(() =>
    {
        // Arrange / Act: the palette has neither Types nor DiagramTheme
        using var host = new PaletteHost(followEditor: true);
        var viaEditorTheme = new DiagramTheme { NodeFill = PlexusColor.Parse("#336699") };
        host.Editor.DiagramTheme = viaEditorTheme;
        host.Flush();
        var themeOfEditor = host.Palette.Items.Select(i => i.Prototype.Scene.Theme).Distinct().ToList();
        host.Editor.DiagramTheme = null;
        var viaDiagramTheme = new DiagramTheme { NodeFill = PlexusColor.Parse("#993366") };
        host.Diagram.Theme = viaDiagramTheme;
        host.Flush();
        var themeOfDiagram = host.Palette.Items.Select(i => i.Prototype.Scene.Theme).Distinct().ToList();

        // Assert
        Assert.Multiple(
            () => Assert.Null(host.Palette.Types),
            () => Assert.Null(host.Palette.DiagramTheme),
            () => Assert.Equal(host.Types.Select(t => t.Key).Order(), host.Palette.VisibleTypes.Select(t => t.Key).Order()),
            () => Assert.Same(viaEditorTheme, Assert.Single(themeOfEditor)),
            () => Assert.Same(viaDiagramTheme, Assert.Single(themeOfDiagram)));
    });

    /// <summary>
    ///     Validates that an explicit Types or DiagramTheme wins over what the editor supplies.
    /// </summary>
    [Fact]
    public Task NodePalette_Editor_ExplicitTypesAndThemeWin() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        var own = new DiagramTheme { NodeFill = PlexusColor.Parse("#112233") };
        var subset = host.Types.Where(t => t.Key is "test.Sink" or "test.Gain").ToList();

        // Act
        host.Palette.Types = subset;
        host.Palette.DiagramTheme = own;
        host.Editor.DiagramTheme = new DiagramTheme { NodeFill = PlexusColor.Parse("#FFFFFF") };
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["test.Sink", "test.Gain"], host.Palette.VisibleTypes.Select(t => t.Key)),
            () => Assert.All(host.Palette.Items, i => Assert.Same(own, i.Prototype.Scene.Theme)));
    });

    /// <summary>
    ///     Validates that the palette follows a different diagram (New, Open) but keeps its items when only the page changes.
    /// </summary>
    [Fact]
    public Task NodePalette_Editor_FollowsDiagramReplacementButNotPageSwitches() => UiTest.Run(() =>
    {
        // Arrange: a second page of the same diagram, and a second diagram with a different registry
        using var host = new PaletteHost(followEditor: true);
        var secondPage = host.Diagram.Pages.Add("Second");
        var items = host.Palette.Items.ToList();
        var otherTypes = new NodeTypeRegistry { new NodeType("other.Thing") { Title = "THING" } };
        var otherPage = new Diagram(otherTypes).Pages.Add("Other");

        // Act
        host.Editor.Page = secondPage;
        host.Flush();
        var afterPageSwitch = host.Palette.Items.ToList();
        host.Editor.Page = otherPage;
        host.Flush();
        var afterReplacement = host.Palette.VisibleTypes.Select(t => t.Key).ToList();
        host.Editor.Page = null;
        host.Flush();
        var withoutPage = host.Palette.Items.Count;

        // Assert
        Assert.Multiple(
            () => Assert.True(items.SequenceEqual(afterPageSwitch), "a page switch keeps the palette's items (and so its scroll position)"),
            () => Assert.Equal(["other.Thing"], afterReplacement),
            () => Assert.Equal(0, withoutPage));
    });

    /// <summary>
    ///     Validates that a palette that was out of the visual tree picks up what the editor did meanwhile when it comes back.
    /// </summary>
    [Fact]
    public Task NodePalette_Editor_PicksUpChangesMadeWhileDetached() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        var grid = (Grid)host.Window.Content!;
        grid.Children.Remove(host.Palette);
        host.Flush();
        var otherTypes = new NodeTypeRegistry { new NodeType("other.Thing") { Title = "THING" } };
        var otherPage = new Diagram(otherTypes).Pages.Add("Other");

        // Act: the editor moves on while the palette is away, then the palette returns
        host.Editor.Page = otherPage;
        host.Flush();
        grid.Children.Add(host.Palette);
        host.Flush();

        // Assert
        Assert.Equal(["other.Thing"], host.Palette.VisibleTypes.Select(t => t.Key));
    });

    /// <summary>
    ///     Validates that activating a type with no handler adds the node at the editor's center, selected, as one undo entry.
    /// </summary>
    [Fact]
    public Task NodePalette_Activate_WithEditor_AddsSelectedNodeAtCenterAsOneUndoEntry() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        var type = host.Types.Get("test.Mixer");
        var center = host.Editor.ScreenToDiagram(new Point(host.Editor.Bounds.Width / 2, host.Editor.Bounds.Height / 2));
        var undoBefore = host.Diagram.History.UndoCount;
        var grid = host.Diagram.Theme.GridSize;

        // Act
        host.Palette.Activate(type);
        host.Flush();

        // Assert
        var node = Assert.Single(host.Page.Nodes);
        Assert.Multiple(
            () => Assert.Equal("test.Mixer", node.Type.Key),
            () => Assert.Equal([node], host.Editor.Selection),
            () => Assert.InRange(node.Bounds.Center.X, center.X - grid, center.X + grid),
            () => Assert.InRange(node.Bounds.Center.Y, center.Y - grid, center.Y + grid),
            () => Assert.Equal(node.Bounds.X, Math.Round(node.Bounds.X / grid) * grid),
            () => Assert.Equal(undoBefore + 1, host.Diagram.History.UndoCount),
            () => Assert.True(host.Editor.IsFocused));
    });

    /// <summary>
    ///     Validates that repeated activations cascade by a grid cell instead of stacking nodes exactly on top of each other.
    /// </summary>
    [Fact]
    public Task NodePalette_Activate_Repeatedly_CascadesNodes() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        var type = host.Types.Get("test.Sink");

        // Act
        host.Palette.Activate(type);
        host.Palette.Activate(type);
        host.Palette.Activate(type);

        // Assert
        var positions = host.Page.Nodes.Select(n => n.Position).ToList();
        Assert.Multiple(
            () => Assert.Equal(3, positions.Count),
            () => Assert.Equal(3, positions.Distinct().Count()),
            () => Assert.Equal(positions[0].X + host.Diagram.Theme.GridSize, positions[1].X));
    });

    /// <summary>
    ///     Validates that a handler that marks the event handled suppresses the default action, and one that does not leaves it.
    /// </summary>
    [Fact]
    public Task NodePalette_Activate_HandledSuppressesTheDefault() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        var type = host.Types.Get("test.Sink");
        var seen = new List<string>();
        var mark = true;
        host.Palette.NodeTypeActivated += (_, e) =>
        {
            seen.Add(e.NodeType.Key);
            e.Handled = mark;
        };

        // Act
        host.Palette.Activate(type);
        var handledCount = host.Page.Nodes.Count;
        mark = false;
        host.Palette.Activate(type);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, handledCount),
            () => Assert.Single(host.Page.Nodes),
            () => Assert.Equal(["test.Sink", "test.Sink"], seen));
    });

    /// <summary>
    ///     Validates that activation without an editor only raises the event, and that unusable targets add nothing.
    /// </summary>
    [Fact]
    public Task NodePalette_Activate_WithoutEditorOrWithUnusableEditor_AddsNothing() => UiTest.Run(() =>
    {
        // Arrange: a palette with no editor, then one whose editor is read-only
        using var host = new PaletteHost();
        var raised = new List<string>();
        host.Palette.NodeTypeActivated += (_, e) => raised.Add(e.NodeType.Key);
        var type = host.Types.Get("test.Sink");

        // Act
        host.Palette.Activate(type);
        host.Palette.Editor = host.Editor;
        host.Editor.IsReadOnly = true;
        host.Palette.Activate(type);
        host.Editor.IsReadOnly = false;
        host.Palette.Activate(new NodeType("unknown.Type"));

        // Assert
        Assert.Multiple(
            () => Assert.Empty(host.Page.Nodes),
            () => Assert.Equal(["test.Sink", "test.Sink", "unknown.Type"], raised),
            () => Assert.Throws<ArgumentNullException>(() => host.Palette.Activate(null!)));
    });

    /// <summary>
    ///     Validates that a double-click and Enter on items use the same default and add the node.
    /// </summary>
    [Fact]
    public Task NodePalette_DoubleClickAndEnter_WithEditor_AddNodes() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        host.Palette.GroupByCategory = false;
        host.Flush();
        var center = host.CenterOf(host.ItemFor("test.Gain"));

        // Act: a double-click
        host.Window.MouseMove(center);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Window.MouseDown(center, MouseButton.Left);
        host.Window.MouseUp(center, MouseButton.Left);
        host.Flush();
        var afterDoubleClick = host.Page.Nodes.Select(n => n.Type.Key).ToList();

        // Act: Enter on the selected item (the editor took focus, so give it back to the palette)
        host.Palette.Focus();
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        host.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(["test.Gain"], afterDoubleClick),
            () => Assert.Equal(["test.Gain", "test.Gain"], host.Page.Nodes.Select(n => n.Type.Key)));
    });

    /// <summary>
    ///     Validates the editor's AddNodeAtCenter on its own: result, refusals and argument check.
    /// </summary>
    [Fact]
    public Task DiagramEditor_AddNodeAtCenter_AddsOrRefuses() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var type = host.Types.Get("test.Sink");
        var foreign = new NodeType("unknown.Type");

        // Act
        var added = host.Editor.AddNodeAtCenter(type);
        var unknown = host.Editor.AddNodeAtCenter(foreign);
        host.Editor.IsReadOnly = true;
        var readOnly = host.Editor.AddNodeAtCenter(type);
        host.Editor.IsReadOnly = false;
        host.Editor.Page = null;
        var noPage = host.Editor.AddNodeAtCenter(type);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(added),
            () => Assert.Same(added, Assert.Single(host.Page.Nodes)),
            () => Assert.Null(unknown),
            () => Assert.Null(readOnly),
            () => Assert.Null(noPage),
            () => Assert.Throws<ArgumentNullException>(() => host.Editor.AddNodeAtCenter(null!)));
    });

    /// <summary>
    ///     Validates that ScrollIntoView and Select reveal an item that is outside the viewport and report whether the type is shown.
    /// </summary>
    [Fact]
    public Task NodePalette_ScrollIntoViewAndSelect_RevealTheItem() => UiTest.Run(() =>
    {
        // Arrange: a zoom large enough that the five items do not fit
        using var host = new PaletteHost(zoom: 4);
        var scroller = ScrollerOf(host);
        var last = host.Types.Get("test.Gain");
        var overflows = scroller.Extent.Height > scroller.Viewport.Height;
        var offsetBefore = scroller.Offset.Y;

        // Act
        var scrolled = host.Palette.ScrollIntoView(last);
        host.Flush();
        var offsetAfter = scroller.Offset.Y;
        var selected = host.Palette.Select(host.Types.Get("test.Source"));
        host.Flush();
        var selectedType = host.Palette.SelectedNodeType;
        var offsetAtFirst = scroller.Offset.Y;
        var byKeyOnly = host.Palette.Select(new NodeType("test.Gain"));
        host.Flush();
        host.Palette.Filter = "zzz";
        host.Flush();
        var hidden = host.Palette.ScrollIntoView(last);
        var hiddenSelect = host.Palette.Select(last);

        // Assert
        Assert.Multiple(
            () => Assert.True(overflows, "the content overflows the viewport"),
            () => Assert.Equal(0, offsetBefore),
            () => Assert.True(scrolled),
            () => Assert.True(offsetAfter > 0, $"scrolled down to the last item ({offsetAfter})"),
            () => Assert.True(selected),
            () => Assert.Same(host.Types.Get("test.Source"), selectedType),
            () => Assert.True(offsetAtFirst < offsetAfter, $"scrolled back up to the first item ({offsetAtFirst} < {offsetAfter})"),
            () => Assert.True(byKeyOnly),
            () => Assert.Same(last, host.Palette.SelectedNodeType),
            () => Assert.False(hidden),
            () => Assert.False(hiddenSelect),
            () => Assert.Throws<ArgumentNullException>(() => host.Palette.ScrollIntoView(null!)),
            () => Assert.Throws<ArgumentNullException>(() => host.Palette.Select(null!)));
    });

    /// <summary>
    ///     Validates that a palette item exposes the Invoke pattern (select and activate) and that its peer can scroll it into view.
    /// </summary>
    [Fact]
    public Task NodePaletteItem_AutomationPeer_SupportsInvokeAndScrollIntoView() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(zoom: 4, followEditor: true);
        var scroller = ScrollerOf(host);
        var item = host.ItemFor("test.Gain");
        var peer = ControlAutomationPeer.CreatePeerForElement(item);
        var provider = peer.GetProvider<IInvokeProvider>();

        // Act
        peer.BringIntoView();
        host.Flush();
        var offset = scroller.Offset.Y;
        provider?.Invoke();
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType()),
            () => Assert.Equal("NodePalette_test_Gain", peer.GetAutomationId()),
            () => Assert.NotNull(provider),
            () => Assert.True(offset > 0, "the peer's bring-into-view scrolled the palette"),
            () => Assert.Same(host.Types.Get("test.Gain"), host.Palette.SelectedNodeType),
            () => Assert.Equal(["test.Gain"], host.Page.Nodes.Select(n => n.Type.Key)));
    });

    /// <summary>
    ///     Validates the built-in search box: present by default with the documented id and prompt, editing it filters and setting Filter fills it.
    /// </summary>
    [Fact]
    public Task NodePalette_ShowFilter_DefaultSearchBoxFiltersBothWays() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var box = host.Palette.GetVisualDescendants().OfType<TextBox>().Single();

        // Act
        box.Text = "mixer";
        host.Flush();
        var typed = host.Palette.VisibleTypes.Select(t => t.Key).ToList();
        var filterAfterTyping = host.Palette.Filter;
        host.Palette.Filter = "sink";
        host.Flush();
        var fromCode = host.Palette.VisibleTypes.Select(t => t.Key).ToList();
        var textAfterCode = box.Text;
        host.Palette.Filter = null;
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.True(host.Palette.ShowFilter),
            () => Assert.True(box.IsVisible),
            () => Assert.Equal("NodePalette_Filter", AutomationProperties.GetAutomationId(box)),
            () => Assert.Equal(NodePalette.FilterAutomationId, AutomationProperties.GetAutomationId(box)),
            () => Assert.Equal("Search nodes", box.PlaceholderText),
            () => Assert.Equal(["test.Mixer"], typed),
            () => Assert.Equal("mixer", filterAfterTyping),
            () => Assert.Equal(["test.Sink"], fromCode),
            () => Assert.Equal("sink", textAfterCode),
            () => Assert.True(string.IsNullOrEmpty(box.Text)),
            () => Assert.Equal(5, host.Palette.VisibleTypes.Count));
    });

    /// <summary>
    ///     Validates that ShowFilter off hides the box and that the box takes layout room above the entries only while shown.
    /// </summary>
    [Fact]
    public Task NodePalette_ShowFilter_Off_HidesTheBox() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost();
        var box = host.Palette.GetVisualDescendants().OfType<TextBox>().Single();
        var scroller = ScrollerOf(host);
        var shownTop = scroller.TranslatePoint(default, host.Palette)!.Value.Y;

        // Act
        host.Palette.ShowFilter = false;
        host.Flush();
        var hiddenTop = scroller.TranslatePoint(default, host.Palette)!.Value.Y;

        // Assert
        Assert.Multiple(
            () => Assert.False(box.IsVisible),
            () => Assert.True(shownTop >= box.Bounds.Height, "the entries start below the box"),
            () => Assert.Equal(0, hiddenTop));
    });

    /// <summary>
    ///     Validates the keyboard from the search box: Down moves through the results and Enter activates, while editing keys stay with the box.
    /// </summary>
    [Fact]
    public Task NodePalette_SearchBox_KeyboardMovesThroughResultsAndEnterActivates() => UiTest.Run(() =>
    {
        // Arrange
        using var host = new PaletteHost(followEditor: true);
        host.Palette.GroupByCategory = false;
        var box = host.Palette.GetVisualDescendants().OfType<TextBox>().Single();
        box.Text = "s";
        host.Flush();
        box.Focus();
        host.Flush();
        void Press(Key key)
        {
            host.Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
            host.Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
            host.Flush();
        }

        // Act: End belongs to the text box, Down moves into the results, Enter activates the selected one
        Press(Key.End);
        var afterEnd = host.Palette.SelectedNodeType;
        Press(Key.Down);
        var afterDown = host.Palette.SelectedNodeType?.Key;
        Press(Key.Enter);

        // Assert
        Assert.Multiple(
            () => Assert.Null(afterEnd),
            () => Assert.Equal(host.Palette.VisibleTypes[0].Key, afterDown),
            () => Assert.Equal([afterDown], host.Page.Nodes.Select(n => n.Type.Key)));
    });
}



