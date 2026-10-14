using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Shows the real <see cref="MainWindow"/> in the headless platform and offers helpers to find and drive its controls.
/// </summary>
internal sealed class MainWindowHost : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindowHost"/> class and shows the window.
    /// </summary>
    /// <param name="options">The command-line options.</param>
    public MainWindowHost(GuiOptions options)
    {
        Window = new MainWindow(options);
        Window.Editor.Clipboard = new MemoryClipboard();
        Window.Show();
        Flush();
    }

    /// <summary>
    ///     Gets the window under test.
    /// </summary>
    public MainWindow Window { get; }

    /// <summary>
    ///     Gets the window's session.
    /// </summary>
    public DemoSession Session => Window.Session;

    /// <summary>
    ///     Gets the window's editor.
    /// </summary>
    public DiagramEditor Editor => Window.Editor;

    /// <summary>
    ///     Parses a command line into options.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options.</returns>
    public static GuiOptions Options(params string[] args)
    {
        Assert.True(GuiOptions.TryParse(args, out var options, out var error), error);
        return options!;
    }

    /// <summary>
    ///     Lets pending layout, input and rendering work finish.
    /// </summary>
    public void Flush()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>
    ///     Finds a control by AutomationId anywhere under a root (menus included).
    /// </summary>
    /// <param name="id">The AutomationId.</param>
    /// <param name="root">The root to search; defaults to the main window.</param>
    /// <returns>The control, or null.</returns>
    public Control? Find(string id, Control? root = null) =>
        (root ?? Window).GetSelfAndLogicalDescendants().OfType<Control>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id)
        ?? (root ?? Window).GetSelfAndVisualDescendants().OfType<Control>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);

    /// <summary>
    ///     Finds a control that must exist.
    /// </summary>
    /// <typeparam name="T">The expected control type.</typeparam>
    /// <param name="id">The AutomationId.</param>
    /// <param name="root">The root to search; defaults to the main window.</param>
    /// <returns>The control.</returns>
    public T Get<T>(string id, Control? root = null)
        where T : Control =>
        Find(id, root) as T ?? throw new InvalidOperationException($"No {typeof(T).Name} with AutomationId '{id}'.");

    /// <summary>
    ///     Chooses a menu item as a user would (raises its click).
    /// </summary>
    /// <param name="id">The AutomationId.</param>
    /// <param name="root">The root to search; defaults to the main window.</param>
    public void Choose(string id, Control? root = null)
    {
        var item = Get<MenuItem>(id, root);
        Assert.True(item.IsEnabled, $"{id} is disabled");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Flush();
    }

    /// <summary>
    ///     Types text into the editor of a property row (in a dialog or in the properties panel) and commits it with Enter.
    /// </summary>
    /// <param name="root">The window that holds the grid.</param>
    /// <param name="path">The property path, for example <c>Name</c>.</param>
    /// <param name="text">The text.</param>
    public void TypeIntoRow(Window root, string path, string text)
    {
        var box = Get<Control>(AutomationIds.PropertiesEditor(path), root).GetSelfAndVisualDescendants().OfType<TextBox>().First();
        box.Focus();
        box.Text = text;
        root.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        root.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Flush();
    }

    /// <summary>
    ///     Gets the center of a control in window coordinates.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>The point.</returns>
    public Point CenterOf(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
        ?? throw new InvalidOperationException("The control is not in the window.");

    /// <summary>
    ///     Double-clicks the left button at a window point.
    /// </summary>
    /// <param name="at">The point.</param>
    public void DoubleClick(Point at)
    {
        Window.MouseMove(at);
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.CloseWithoutPrompt();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
///     Tests for the demo's main window: layout, AutomationIds, menus, status bar, context menu and the properties dialog.
/// </summary>
public class MainWindowTests
{
    /// <summary>
    ///     The AutomationId of every palette entry: the library's palette names them after the whole node type key.
    /// </summary>
    private static readonly string[] PaletteItemIds = [.. DemoNodeTypes.CreateRegistry().Select(type => AutomationIds.PaletteItem(type.Key))];

    /// <summary>
    ///     The AutomationIds every static interactive control must carry (documented in the demo README).
    /// </summary>
    private static readonly string[] RequiredIds =
    [
        "Main_Window", "Menu_Bar", "NodePalette", "NodePalette_Filter", "Pages_Tabs", "Page_Tab_0", "Editor", "Status_Bar", "Status_Text", "Status_Selection", "Status_Zoom", "Status_Dirty",
        "Menu_File", "Menu_File_New", "Menu_File_Open", "Menu_File_Save", "Menu_File_SaveAs", "Menu_File_OpenSample", "Menu_File_OpenSample_showcase", "Menu_File_ExportPng", "Menu_File_Exit",
        "Menu_Edit", "Menu_Edit_Undo", "Menu_Edit_Redo", "Menu_Edit_Cut", "Menu_Edit_Copy", "Menu_Edit_Paste", "Menu_Edit_Duplicate", "Menu_Edit_Delete", "Menu_Edit_SelectAll", "Menu_Edit_Rename", "Menu_Edit_Properties",
        "Menu_Insert", "Menu_Insert_Node", "Menu_Insert_Node_Mixer", "Menu_Insert_Annotation", "Menu_Insert_Group", "Menu_Insert_Ungroup", "Menu_Insert_ToggleCollapse",
        "Menu_Page", "Menu_Page_Add", "Menu_Page_Rename", "Menu_Page_Delete", "Menu_Page_MoveLeft", "Menu_Page_MoveRight", "Menu_Page_MoveSelectionTo",
        "Menu_View", "Menu_View_ZoomIn", "Menu_View_ZoomOut", "Menu_View_ZoomToFit", "Menu_View_ShowGrid", "Menu_View_SnapToGrid", "Menu_View_PropertiesPanel",
        "Properties_Panel", "Main_PaletteSplitter", "Main_PropertiesPanelSplitter", "PropertyGrid",
        .. PaletteItemIds,
        "Menu_File_OpenSample_math", "Menu_File_OpenSample_plumbing", "Menu_File_OpenSample_logic", "Menu_File_OpenSample_audio",
        "Menu_File_OpenSample_workflow", "Menu_File_OpenSample_groups", "Menu_File_OpenSample_wiring",
    ];

    /// <summary>
    ///     Validates that the default start opens the showcase sample, untitled and clean, with a tab per page.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Startup_Default_OpensShowcase() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());

        // Assert
        Assert.Equal("Plexus Demo - Untitled", host.Window.Title);
        Assert.Equal(["Math", "Plumbing", "Logic", "Audio", "Workflow", "Groups", "Wiring"], host.Session.Diagram.Pages.Select(p => p.Name));
        Assert.Same(host.Session.Diagram.Pages[0], host.Editor.Page);
        Assert.False(host.Session.IsDirty);
        Assert.Equal("Saved", host.Get<TextBlock>("Status_Dirty").Text);
    });

    /// <summary>
    ///     Validates that every documented static AutomationId exists on the window.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_AutomationIds_AllDocumentedIdsExist() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());

        // Assert
        var missing = RequiredIds.Where(id => host.Find(id) is null).ToList();
        Assert.True(missing.Count == 0, $"Missing AutomationIds: {string.Join(", ", missing)}");
        Assert.Equal(6, Enumerable.Range(0, 6).Count(i => host.Find($"Page_Tab_{i}") is not null));
    });

    /// <summary>
    ///     Validates that every AutomationId in the window is unique, so automation never picks the wrong control.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_AutomationIds_AreUnique() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());
        var ids = host.Window.GetSelfAndLogicalDescendants().OfType<Control>()
            .Select(AutomationProperties.GetAutomationId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        // Assert
        Assert.Empty(ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key));
    });

    /// <summary>
    ///     Validates the command-line start-up variants: sample, basic types without a document, and a bad file.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Startup_Variants_OpenTheRequestedDocument() => GuiTest.Run(() =>
    {
        // Arrange / Act / Assert: a named sample
        using (var sample = new MainWindowHost(MainWindowHost.Options("--sample", "math")))
        {
            Assert.Equal(["Math"], sample.Session.Diagram.Pages.Select(p => p.Name));
        }

        // Basic types cannot open the full showcase, so the window starts empty and says so
        using (var basic = new MainWindowHost(MainWindowHost.Options("--types", "basic")))
        {
            Assert.Equal(["Page 1"], basic.Session.Diagram.Pages.Select(p => p.Name));
            Assert.Contains("Basic node types", basic.Get<TextBlock>("Status_Text").Text, StringComparison.Ordinal);
            Assert.Equal(DemoNodeTypes.BasicTypeKeys.Count, basic.Session.Diagram.Types.Count);
        }

        // A file that does not exist is reported, not thrown
        using var bad = new MainWindowHost(MainWindowHost.Options("--open", "C:\\no\\such\\file.json"));
        Assert.Contains("Could not open", bad.Get<TextBlock>("Status_Text").Text, StringComparison.Ordinal);
        Assert.Equal("Plexus Demo - Untitled", bad.Window.Title);
    });

    /// <summary>
    ///     Validates that opening a file shows its name in the title and edits add the dirty marker.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Title_ShowsFileNameAndDirtyMarker() => GuiTest.Run(() =>
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = System.IO.Path.Combine(temp.Create(), "plumbing.json");
        DemoDocument.Save(DemoSamples.Plumbing(), path);

        // Act
        using var host = new MainWindowHost(MainWindowHost.Options("--open", path));
        var clean = host.Window.Title;
        host.Choose("Menu_Insert_Node_Mixer");

        // Assert
        Assert.Equal("Plexus Demo - plumbing.json", clean);
        Assert.Equal("Plexus Demo - plumbing.json*", host.Window.Title);
        Assert.Equal("Modified *", host.Get<TextBlock>("Status_Dirty").Text);
    });

    /// <summary>
    ///     Validates that the Undo and Redo items show what they will do and are enabled only when they can.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_UndoRedoMenu_ShowsDescriptionsAndEnablement() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var undo = host.Get<MenuItem>("Menu_Edit_Undo");
        var redo = host.Get<MenuItem>("Menu_Edit_Redo");
        Assert.False(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        // Act
        host.Choose("Menu_Insert_Node_Mixer");
        var afterEdit = (Enabled: undo.IsEnabled, Header: undo.Header?.ToString(), RedoEnabled: redo.IsEnabled);
        var description = host.Session.Diagram.History.UndoDescription;
        host.Choose("Menu_Edit_Undo");

        // Assert
        Assert.True(afterEdit.Enabled);
        Assert.Equal($"_Undo {description}", afterEdit.Header);
        Assert.False(afterEdit.RedoEnabled);
        Assert.False(undo.IsEnabled);
        Assert.Equal("_Undo", undo.Header);
        Assert.True(redo.IsEnabled);
        Assert.Equal($"_Redo {description}", redo.Header);
    });

    /// <summary>
    ///     Validates that the status bar follows the selection, the zoom and the last message.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_StatusBar_FollowsSelectionZoomAndMessage() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        Assert.Equal("Selected: 0", host.Get<TextBlock>("Status_Selection").Text);

        // Act
        host.Editor.SelectAll();
        host.Flush();
        var selected = host.Get<TextBlock>("Status_Selection").Text;
        host.Editor.Zoom = 2;
        var zoom = host.Get<TextBlock>("Status_Zoom").Text;
        host.Session.SetMessage("Hello status");

        // Assert
        Assert.Equal($"Selected: {host.Editor.Selection.Count}", selected);
        Assert.NotEqual("Selected: 0", selected);
        Assert.Equal("Zoom: 200%", zoom);
        Assert.Equal("Hello status", host.Get<TextBlock>("Status_Text").Text);
    });

    /// <summary>
    ///     Validates that the window icon (the repository's Icon.png, linked as a resource) loads.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task App_LoadWindowIcon_EmbeddedRepositoryIcon_Loads() => GuiTest.Run(() =>
    {
        // Act
        var icon = App.LoadWindowIcon();

        // Assert: the linked resource exists and decodes into an icon
        Assert.NotNull(icon);
    });

    /// <summary>
    ///     Validates that a very long status message is trimmed with its full text in a tooltip, and never pushes the
    ///     selection, zoom and dirty fields out of the status bar.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_StatusBar_LongMessage_TrimsAndKeepsFieldsVisible() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var message = "Paste failed: " + string.Join(", ", Enumerable.Range(0, 40).Select(i => $"demo.type.Number{i}"));

        // Act
        host.Session.SetMessage(message);
        host.Flush();
        var text = host.Get<TextBlock>("Status_Text");
        var zoom = host.Get<TextBlock>("Status_Zoom");
        var bar = host.Get<Border>("Status_Bar");
        var zoomRight = zoom.TranslatePoint(new Point(zoom.Bounds.Width, 0), bar);

        // Assert: trimmed message, full text on hover, and the zoom field still inside the bar
        Assert.NotNull(zoomRight);
        Assert.Multiple(
            () => Assert.Equal(global::Avalonia.Media.TextTrimming.CharacterEllipsis, text.TextTrimming),
            () => Assert.Equal(message, ToolTip.GetTip(text)),
            () => Assert.True(zoomRight.Value.X <= bar.Bounds.Width + 0.5, $"zoom field ends at {zoomRight.Value.X}, bar is {bar.Bounds.Width} wide"));
    });

    /// <summary>
    ///     Validates that Insert, Edit and Page menu commands change the diagram as their captions say.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Menus_InsertEditAndPageCommandsWork() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        var page = host.Session.RequirePage();

        // Act: Insert node, annotation; select all and group; collapse; ungroup
        host.Choose("Menu_Insert_Node_Scope");
        host.Choose("Menu_Insert_Annotation");
        host.Editor.SelectAll();
        host.Flush();
        var groupEnabled = host.Get<MenuItem>("Menu_Insert_Group").IsEnabled;
        var nodeAdded = page.Nodes.Count;
        var annotationAdded = page.Annotations.Count;

        // Assert: the group item prompts for a title (a modal dialog), so only its enablement is checked here
        Assert.True(groupEnabled);
        Assert.Equal(1, nodeAdded);
        Assert.Equal(1, annotationAdded);

        // Act: Edit > Delete removes the selection; Page > Add adds a page and shows it
        host.Editor.SelectAll();
        host.Choose("Menu_Edit_Delete");
        host.Choose("Menu_Page_Add");

        // Assert
        Assert.Empty(page.Nodes);
        Assert.Equal(2, host.Session.Diagram.Pages.Count);
        Assert.Equal("Page 2", host.Editor.Page!.Name);
        Assert.Equal(1, host.Get<TabStrip>("Pages_Tabs").SelectedIndex);
        Assert.False(host.Get<MenuItem>("Menu_Page_MoveRight").IsEnabled);
        Assert.True(host.Get<MenuItem>("Menu_Page_MoveLeft").IsEnabled);
    });

    /// <summary>
    ///     Validates the page tabs: selecting a tab shows that page, and page changes made elsewhere are reflected in the tabs.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PageTabs_AreBoundToThePages() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options());
        var tabs = host.Get<TabStrip>("Pages_Tabs");

        // Act: choose a tab like a user, then change the page from the model side
        tabs.SelectedIndex = 2;
        host.Flush();
        var viaTab = host.Editor.Page!.Name;
        host.Session.SelectPage(host.Session.Diagram.Pages[1]);
        host.Flush();
        var viaModel = tabs.SelectedIndex;
        host.Session.Diagram.Pages[3].Name = "Renamed";
        host.Flush();

        // Assert
        Assert.Equal("Logic", viaTab);
        Assert.Equal(1, viaModel);
        Assert.Equal("Renamed", host.Session.Diagram.Pages[3].Name);
    });

    /// <summary>
    ///     Validates that undoing "Add page" while that page is shown moves the editor and the tab strip to the remaining page by itself (the editor follows page removal).
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_UndoAddPage_ShownPage_EditorAndTabsFollowToNeighbor() => GuiTest.Run(() =>
    {
        // Arrange: a document with one page, then add a second page, which is shown
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        var tabs = host.Get<TabStrip>("Pages_Tabs");
        var first = host.Session.Diagram.Pages[0];
        host.Choose("Menu_Page_Add");
        var shownAfterAdd = host.Editor.Page;

        // Act: undo the addition, then redo it
        host.Session.Diagram.History.Undo();
        host.Flush();
        var afterUndo = host.Editor.Page;
        var tabAfterUndo = tabs.SelectedItem;
        host.Session.Diagram.History.Redo();
        host.Flush();

        // Assert: the editor followed the removal and did not jump back when the page returned
        Assert.NotSame(first, shownAfterAdd);
        Assert.Same(first, afterUndo);
        Assert.Same(first, tabAfterUndo);
        Assert.Same(first, host.Editor.Page);
        Assert.Equal(2, host.Session.Diagram.Pages.Count);
    });

    /// <summary>
    ///     Validates Move Selection To: the submenu lists the other pages and moving takes the selection there.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_MoveSelectionTo_ListsOtherPagesAndMoves() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        host.Choose("Menu_Insert_Node_Scope");
        host.Choose("Menu_Page_Add");
        host.Session.SelectPage(host.Session.Diagram.Pages[0]);
        host.Flush();
        var node = host.Session.RequirePage().Nodes.Single();
        host.Editor.Select(node);
        host.Flush();
        var parent = host.Get<MenuItem>("Menu_Page_MoveSelectionTo");

        // Act
        var enabled = parent.IsEnabled;
        host.Choose("Menu_Page_MoveSelectionTo_1");

        // Assert
        Assert.True(enabled);
        Assert.Empty(host.Session.Diagram.Pages[0].Nodes);
        Assert.Same(node, host.Session.Diagram.Pages[1].Nodes.Single());
    });

    /// <summary>
    ///     Validates that the View toggles drive the editor and stay in step with it.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ViewMenu_TogglesGridAndZoom() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var grid = host.Get<MenuItem>("Menu_View_ShowGrid");
        var snap = host.Get<MenuItem>("Menu_View_SnapToGrid");
        var zoom = host.Editor.Zoom;

        // Act
        grid.IsChecked = false;
        snap.IsChecked = false;
        host.Choose("Menu_View_ZoomIn");
        var zoomedIn = host.Editor.Zoom;
        host.Choose("Menu_View_ZoomOut");
        host.Editor.ShowGrid = true;

        // Assert
        Assert.False(host.Editor.SnapToGrid);
        Assert.True(grid.IsChecked);
        Assert.True(zoomedIn > zoom);
        Assert.Equal(zoom, host.Editor.Zoom, 6);
    });

    /// <summary>
    ///     Validates that double-clicking a palette entry adds that node type and selects it.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_DoubleClickAddsNode() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var before = host.Session.RequirePage().Nodes.Count;
        var item = host.Get<Control>(AutomationIds.PaletteItem(DemoNodeTypes.Constant));

        // Act
        host.DoubleClick(host.CenterOf(item));

        // Assert
        var nodes = host.Session.RequirePage().Nodes;
        Assert.Equal(before + 1, nodes.Count);
        Assert.Equal(DemoNodeTypes.Constant, nodes[^1].Type.Key);
        Assert.Equal([nodes[^1]], host.Editor.Selection.OfType<Node>());
    });

    /// <summary>
    ///     Validates that the palette lists every registered node type with its title.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_ListsEveryNodeType() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        var palette = host.Get<NodePalette>("NodePalette");

        // Assert
        Assert.Equal(DemoNodeTypes.BasicTypeKeys.Order(), palette.VisibleTypes.Select(t => t.Key).Order());
        Assert.NotNull(host.Find("NodePalette_demo_math_Scope"));
        Assert.Null(host.Find("NodePalette_demo_audio_Mixer"));
    });

    /// <summary>
    ///     Validates that palette entries have the node type's display name as their UI Automation name.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_ItemsAreNamedAfterTheirType() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        var item = host.Get<Control>("NodePalette_demo_math_Sine");

        // Assert
        Assert.Equal("Sine wave", global::Avalonia.Automation.AutomationProperties.GetName(item));
    });

    /// <summary>
    ///     Validates the node context menu: its entries, and that they act on the node.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Node_OffersNodeCommands() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        host.Session.AddPage();
        host.Session.SelectPage(host.Session.Diagram.Pages[0]);
        var node = host.Session.RequirePage().Nodes[0];
        host.Editor.Select(node);
        var count = host.Session.RequirePage().Nodes.Count;

        // Act
        var menu = host.Window.BuildContextMenu(node, default);
        var ids = menu.Items.OfType<Control>().Select(AutomationProperties.GetAutomationId).ToList();
        host.Choose("Context_Node_Duplicate", menu);
        var duplicated = host.Session.RequirePage().Nodes.Count;
        host.Choose("Context_Node_Delete", menu);

        // Assert
        Assert.Equal(["Context_Node_Properties", "Context_Node_Rename", "Context_Node_Duplicate", "Context_Node_Delete", "Context_Node_MoveToPage"], ids);
        Assert.Equal(count + 1, duplicated);
        Assert.Equal(count, host.Session.RequirePage().Nodes.Count);
        Assert.NotNull(host.Find("Context_Node_MoveToPage_1", menu));
    });

    /// <summary>
    ///     Validates the wire context menu: Reroute and Delete.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Wire_OffersDeleteAndReroute() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var wire = host.Session.RequirePage().Wires[0];
        host.Editor.Select(wire);
        var count = host.Session.RequirePage().Wires.Count;

        // Act
        var menu = host.Window.BuildContextMenu(wire, default);
        host.Choose("Context_Wire_Reroute", menu);
        host.Choose("Context_Wire_Delete", menu);

        // Assert
        Assert.Equal(["Context_Wire_Delete", "Context_Wire_Reroute"], menu.Items.OfType<Control>().Select(AutomationProperties.GetAutomationId));
        Assert.Equal(count - 1, host.Session.RequirePage().Wires.Count);
    });

    /// <summary>
    ///     Validates the canvas context menu: Add Node Here (per type), Paste and Add Annotation Here, all at the clicked position.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Canvas_AddsAtTheClickedPosition() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
        var page = host.Session.RequirePage();

        // Act
        var menu = host.Window.BuildContextMenu(null, new Point(152, 98));
        host.Choose("Context_Canvas_AddNode_Scope", menu);
        host.Choose("Context_Canvas_AddAnnotation", menu);

        // Assert: positions snap to the 15-unit grid
        Assert.Equal(["Context_Canvas_AddNode", "Context_Canvas_Paste", "Context_Canvas_AddAnnotation"], menu.Items.OfType<Control>().Select(AutomationProperties.GetAutomationId));
        Assert.Equal(new Geometry.Point(150, 105), page.Nodes.Single().Position);
        Assert.Equal(new Geometry.Point(150, 105), page.Annotations.Single().Bounds.Position);
        Assert.Null(host.Find("Context_Canvas_AddNode_Mixer", menu));
    });

    /// <summary>
    ///     Validates that the canvas Paste entry pastes where the user right-clicked.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_ContextMenu_Canvas_PasteGoesToTheClickedPosition() => GuiTest.RunAsync(
        async _ =>
        {
            // Arrange
            using var host = new MainWindowHost(MainWindowHost.Options("--types", "basic"));
            var source = host.Session.RequirePage().AddNode(DemoNodeTypes.Scope, 0, 0, "Original");
            host.Editor.Select(source);
            await host.Editor.CopyAsync();

            // Act
            var menu = host.Window.BuildContextMenu(null, new Point(300, 150));
            host.Choose("Context_Canvas_Paste", menu);
            await Task.Delay(50);
            host.Flush();

            // Assert
            var pasted = host.Session.RequirePage().Nodes.Single(n => n != source);
            Assert.Equal(new Geometry.Point(300, 150), pasted.Position);
        },
        NodeTypeSet.Basic);

    /// <summary>
    ///     Validates that double-clicking a node opens the library's properties dialog and OK applies everything as one undo entry.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_NodeDoubleClick_OpensLibraryDialogWhoseOkIsOneUndoEntry() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var node = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        var before = host.Session.Diagram.History.UndoCount;
        var point = host.Editor.TranslatePoint(host.Editor.DiagramToScreen(new Point(node.Bounds.Center.X, node.Bounds.Center.Y)), host.Window)!.Value;

        // Act
        host.DoubleClick(point);
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();
        var title = dialog.Title;
        host.TypeIntoRow(dialog, NodePropertySource.NamePath, "Triage");
        host.TypeIntoRow(dialog, NodePropertySource.PropertyPath("Duration"), "9");
        host.TypeIntoRow(dialog, NodePropertySource.DataPath("Notes"), "Look at it twice");
        host.Get<Button>(AutomationIds.PropertiesOk, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();

        // Assert
        Assert.Equal("Review - Properties", title);
        Assert.Equal("Triage", node.Name);
        Assert.Equal(9, node.Properties.Get<int>("Duration"));
        Assert.Equal("Look at it twice", ((TaskDetails)node.Data!).Notes);
        Assert.Equal(TaskPriority.High, ((TaskDetails)node.Data).Priority);
        Assert.Equal(before + 1, host.Session.Diagram.History.UndoCount);
        Assert.Equal("Edited Triage", host.Session.Message);
        Assert.Empty(host.Window.OwnedWindows.OfType<PropertyGridDialog>());
    });

    /// <summary>
    ///     Validates that Cancel changes nothing and that a rename that is not allowed keeps the dialog open with the reason on the name row.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesDialog_CancelChangesNothingAndABadNameIsExplained() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "plumbing"));
        var nodes = host.Session.RequirePage().Nodes;
        var tank = nodes.Single(n => n.Name == "Supply");
        var other = nodes.Single(n => n.Name == "Reservoir");
        var before = host.Session.Diagram.History.UndoCount;
        host.Editor.Select(tank);
        host.Choose("Menu_Edit_Properties");
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();

        // Act: a duplicate name and a number with its unit
        host.TypeIntoRow(dialog, NodePropertySource.NamePath, other.Name);
        host.TypeIntoRow(dialog, NodePropertySource.PropertyPath("Capacity"), "800 L");
        host.Get<Button>(AutomationIds.PropertiesOk, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();

        // Assert: still open, the reason is shown, nothing changed
        Assert.True(dialog.IsVisible);
        Assert.Contains("already used", host.Get<TextBlock>(AutomationIds.PropertiesStatus, dialog).Text, StringComparison.Ordinal);
        Assert.Equal(before, host.Session.Diagram.History.UndoCount);

        // Act: Cancel
        host.Get<Button>(AutomationIds.PropertiesCancel, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();

        // Assert
        Assert.False(dialog.IsVisible);
        Assert.Equal(before, host.Session.Diagram.History.UndoCount);
        Assert.Equal("Supply", tank.Name);
        Assert.Equal(500, tank.Properties.Get<int>("Capacity"));
    });

    /// <summary>
    ///     Validates that the host data annotations show in the dialog: categories, display names, read-only, hidden, defaults (bold),
    ///     standard values, flags, nested object, collection, password and the custom slider editor.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesDialog_ShowsTheHostDataAnnotations() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var node = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        host.Editor.Select(node);
        host.Choose("Menu_Edit_Properties");
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();
        var session = dialog.Grid.Session!;
        PropertyItem Item(string name) => session.FindItem(NodePropertySource.DataPath(name)) ?? throw new InvalidOperationException($"No data item {name}");

        // Act
        var categories = PropertyOrdering.Group(session.Source.GetItems(), PropertySortMode.Categorized).Select(g => g.Category).ToList();
        dialog.Grid.SetExpanded(NodePropertySource.DataPath("Contact"), true);
        host.Flush();

        // Assert: the model of the rows
        Assert.Equal(["General", "Checklist", "Details", "People", "Tracking"], categories);
        Assert.Equal("Summary", Item("Notes").DisplayName);
        Assert.Equal("What the task involves, in a sentence or two.", Item("Notes").Description);
        Assert.True(Item("Reference").IsReadOnly);
        Assert.Null(session.FindItem("Revision"));
        Assert.True(Item("Priority").IsChanged);
        Assert.True(Item("Team").IsChanged);
        Assert.True(Item("Team").HasStandardValues && Item("Team").StandardValuesExclusive);
        Assert.True(Item("Reviewer").HasStandardValues && !Item("Reviewer").StandardValuesExclusive);
        Assert.Equal(typeof(TaskLabels), Item("Labels").PropertyType);
        Assert.Equal("slider", Item("Progress").EditorKey);
        Assert.True(Item("AccessCode").IsPasswordText);
        Assert.True(Item("Contact").IsExpandable);
        Assert.Equal(["Name", "Email", "Phone"], Item("Contact").Children.Select(c => c.Name));
        Assert.True(Item("Checklist").IsCollection);
        Assert.Equal(3, Item("Checklist").GetCollectionValues().Count);
        Assert.Equal(["Assignee", "Duration"], session.Source.GetItems().Where(i => i.Category == "General" && i.Name != "Name").Select(i => i.Name).Order());

        // Assert: the controls
        Assert.NotNull(host.Find(AutomationIds.PropertiesRow(NodePropertySource.NamePath), dialog));
        Assert.NotNull(host.Find(AutomationIds.PropertiesRow(NodePropertySource.DataPath("Contact.Email")), dialog));
        Assert.Contains(host.Get<Control>(AutomationIds.PropertiesEditor(NodePropertySource.DataPath("Progress")), dialog).GetSelfAndVisualDescendants(), c => c is Slider);
        Assert.Contains(host.Get<Control>(AutomationIds.PropertiesEditor(NodePropertySource.DataPath("Team")), dialog).GetSelfAndVisualDescendants(), c => c is ComboBox);
        Assert.Contains(host.Get<Control>(AutomationIds.PropertiesEditor(NodePropertySource.DataPath("AccessCode")), dialog).GetSelfAndVisualDescendants(), c => c is TextBox { PasswordChar: not '\0' });
        dialog.Close(false);
    });

    /// <summary>
    ///     Validates that the collection editor's edits (an added step and an edited step) persist with OK and undo as one entry.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesDialog_CollectionEditsPersistAndUndo() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var node = host.Session.RequirePage().Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        var before = host.Session.Diagram.History.UndoCount;
        var original = (TaskDetails)node.Data!;
        host.Editor.Select(node);
        host.Choose("Menu_Edit_Properties");
        var dialog = host.Window.OwnedWindows.OfType<PropertyGridDialog>().Single();
        var session = dialog.Grid.Session!;

        // Act: add a step with a remark in the collection editor's working copy, edit the first step, press OK
        var editor = session.BeginCollectionEdit(session.FindItem(NodePropertySource.DataPath("Checklist"))!);
        editor.Add(new ChecklistItem { Text = "Sign off", Remarks = ["Needs two signatures"] });
        session.SetText(session.FindItem(NodePropertySource.DataPath("Checklist[0].Text"))!, "Read the whole request");
        var accepted = dialog.Accept();
        host.Flush();

        // Assert
        var edited = (TaskDetails)node.Data!;
        Assert.True(accepted);
        Assert.Equal(4, edited.Checklist.Count);
        Assert.Equal("Read the whole request", edited.Checklist[0].Text);
        Assert.Equal(["Needs two signatures"], edited.Checklist[3].Remarks);
        Assert.Equal(3, original.Checklist.Count);
        Assert.Equal("Read the request", original.Checklist[0].Text);
        Assert.Equal(before + 1, host.Session.Diagram.History.UndoCount);

        host.Editor.Undo();
        Assert.Same(original, node.Data);
        Assert.Equal(3, ((TaskDetails)node.Data!).Checklist.Count);
    });

    /// <summary>
    ///     Validates the docked properties panel: View &gt; Properties Panel shows it, it follows the selection, edits are live (one undo entry each) and it follows undo.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_PropertiesPanel_FollowsTheSelectionAndEditsAreLive() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "workflow"));
        var page = host.Session.RequirePage();
        var task = page.Nodes.First(n => n.Type.Key == DemoNodeTypes.Task);
        var other = page.Nodes.First(n => n.Type.Key == DemoNodeTypes.Notify);
        var panel = host.Get<Control>(AutomationIds.PropertiesPanel);
        var hiddenAtStart = !panel.IsVisible;

        // Act: show the panel and select the task
        host.Get<MenuItem>("Menu_View_PropertiesPanel").IsChecked = true;
        host.Flush();
        var shownWidth = host.Window.PropertiesPanelColumn.Width.Value;
        host.Editor.Select(task);
        host.Flush();
        var before = host.Session.Diagram.History.UndoCount;
        host.TypeIntoRow(host.Window, NodePropertySource.PropertyPath("Duration"), "6");
        host.TypeIntoRow(host.Window, NodePropertySource.DataPath("Notes"), "Edited in the panel");

        // Assert: the panel shows the task and each edit was applied at once, as its own undo entry
        Assert.True(hiddenAtStart);
        Assert.Equal(MainWindow.PropertiesPanelWidth, shownWidth);
        Assert.Same(task, host.Window.PropertiesGrid.SelectedObject);
        Assert.Equal(PropertyEditMode.Live, host.Window.PropertiesGrid.EditMode);
        Assert.Equal(6, task.Properties.Get<int>("Duration"));
        Assert.Equal("Edited in the panel", ((TaskDetails)task.Data!).Notes);
        Assert.Equal(before + 2, host.Session.Diagram.History.UndoCount);

        // Act / Assert: undo is followed by the panel
        host.Editor.Undo();
        host.Flush();
        Assert.Equal("Check the request for completeness", host.Get<Control>(AutomationIds.PropertiesEditor(NodePropertySource.DataPath("Notes"))).GetSelfAndVisualDescendants().OfType<TextBox>().First().Text);

        // Act / Assert: another node, an annotation, several elements, nothing
        host.Editor.Select(other);
        host.Flush();
        Assert.Same(other, host.Window.PropertiesGrid.SelectedObject);
        var annotation = page.Elements.OfType<Annotation>().First();
        host.Editor.Select(annotation);
        host.Flush();
        host.TypeIntoRow(host.Window, "Text", "A new note");
        Assert.Equal("A new note", annotation.Text);
        host.Editor.SelectAll();
        host.Flush();
        Assert.Null(host.Window.PropertiesGrid.SelectedObject);

        // Act / Assert: hiding the panel detaches it
        host.Editor.Select(task);
        host.Get<MenuItem>("Menu_View_PropertiesPanel").IsChecked = false;
        host.Flush();
        Assert.False(panel.IsVisible);
        Assert.Null(host.Window.PropertiesGrid.SelectedObject);
        Assert.Equal(0, host.Window.PropertiesPanelColumn.Width.Value);
    });
    /// <summary>
    ///     Validates that F2 style rename from the Edit menu starts the editor's inline rename of the selected node.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_EditRename_StartsInlineRename() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var rename = host.Get<MenuItem>("Menu_Edit_Rename");
        Assert.False(rename.IsEnabled);
        host.Editor.Select(host.Session.RequirePage().Nodes[0]);
        host.Flush();

        // Act
        host.Choose("Menu_Edit_Rename");

        // Assert
        Assert.True(host.Editor.IsRenaming);
    });

    /// <summary>
    ///     Validates that Edit &gt; Properties... is enabled only for a single selected node and then opens its dialog.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_EditProperties_OpensDialogForTheSelectedNode() => GuiTest.Run(() =>
    {
        // Arrange: nothing selected yet
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var properties = host.Get<MenuItem>("Menu_Edit_Properties");
        var disabledWithoutSelection = !properties.IsEnabled;
        var nodes = host.Session.RequirePage().Nodes;
        host.Editor.SelectAll();
        host.Flush();
        var disabledWithManySelected = !properties.IsEnabled;
        host.Editor.Select(nodes[0]);
        host.Flush();

        // Act
        host.Choose("Menu_Edit_Properties");
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.True(disabledWithoutSelection),
            () => Assert.True(disabledWithManySelected),
            () => Assert.True(properties.IsEnabled),
            () => Assert.Equal(new KeyGesture(Key.Enter, KeyModifiers.Alt), properties.InputGesture),
            () => Assert.Single(host.Window.OwnedWindows.OfType<PropertyGridDialog>()));
    });

    /// <summary>
    ///     Validates that node types with long or special titles are listed by their display names in the palette and the Insert menu.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_Palette_AndInsertMenu_UseDisplayNames() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options());
        var meter = host.Get<Control>("NodePalette_demo_plumbing_FlowMeter");
        var envelope = host.Get<Control>("NodePalette_demo_audio_Envelope");
        var menuMeter = host.Get<MenuItem>("Menu_Insert_Node_FlowMeter");
        var menuEnvelope = host.Get<MenuItem>("Menu_Insert_Node_Envelope");

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Flow meter", AutomationProperties.GetName(meter)),
            () => Assert.Equal("Envelope (ADSR)", AutomationProperties.GetName(envelope)),
            () => Assert.Equal("Flow meter", menuMeter.Header),
            () => Assert.Equal("Envelope (ADSR)", menuEnvelope.Header));
    });
}
