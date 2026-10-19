using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the demo's toolbar: its buttons exist, run the same commands as their menu items and mirror the items' state.
/// </summary>
public class ToolbarTests
{
    /// <summary>
    ///     Every toolbar command with the menu item it mirrors, in toolbar order.
    /// </summary>
    private static readonly (string Command, string MenuId)[] Commands =
    [
        ("New", "Menu_File_New"), ("Open", "Menu_File_Open"), ("Save", "Menu_File_Save"),
        ("Undo", "Menu_Edit_Undo"), ("Redo", "Menu_Edit_Redo"),
        ("Cut", "Menu_Edit_Cut"), ("Copy", "Menu_Edit_Copy"), ("Paste", "Menu_Edit_Paste"), ("Delete", "Menu_Edit_Delete"),
        ("Group", "Menu_Insert_Group"), ("Ungroup", "Menu_Insert_Ungroup"), ("ToggleCollapse", "Menu_Insert_ToggleCollapse"), ("Annotation", "Menu_Insert_Annotation"),
        ("ZoomIn", "Menu_View_ZoomIn"), ("ZoomOut", "Menu_View_ZoomOut"), ("ZoomToFit", "Menu_View_ZoomToFit"), ("ShowGrid", "Menu_View_ShowGrid"), ("SnapToGrid", "Menu_View_SnapToGrid"),
        ("Properties", "Menu_Edit_Properties"), ("PropertiesPanel", "Menu_View_PropertiesPanel"),
        ("ExportPng", "Menu_File_ExportPng"),
    ];

    /// <summary>
    ///     Validates that the toolbar and a button per command exist, with the AutomationIds the README documents.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Buttons_AllDocumentedIdsExist() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));

        // Assert
        var missing = Commands.Select(c => $"Toolbar_{c.Command}").Append("Toolbar").Append("Menu_View_ShowToolbar").Where(id => host.Find(id) is null).ToList();
        Assert.True(missing.Count == 0, $"Missing AutomationIds: {string.Join(", ", missing)}");
        Assert.Equal(["ShowGrid", "SnapToGrid", "PropertiesPanel"], Commands.Select(c => c.Command).Where(c => host.Find($"Toolbar_{c}") is ToggleButton));
    });

    /// <summary>
    ///     Validates that every button's enabled state follows its menu item as the selection and the history change.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Enablement_MirrorsTheMenuItems() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "groups"));
        var mismatches = new List<string>();
        void Check(string state)
        {
            host.Flush();
            mismatches.AddRange(Commands
                .Where(c => host.Get<Button>($"Toolbar_{c.Command}").IsEnabled != host.Get<MenuItem>(c.MenuId).IsEnabled)
                .Select(c => $"{state}: {c.Command}"));
        }

        // Act: nothing selected, a node, everything, then after an edit and its undo
        Check("none");
        host.Editor.Select(host.Session.RequirePage().Nodes[0]);
        Check("node");
        host.Editor.SelectAll();
        Check("all");
        host.Choose("Menu_Insert_Node_Mixer");
        Check("edited");
        host.Choose("Menu_Edit_Undo");
        Check("undone");

        // Assert
        Assert.Empty(mismatches);
        Assert.False(host.Get<Button>("Toolbar_Group").IsEnabled);
    });

    /// <summary>
    ///     Validates that Undo and Redo are disabled until there is something to undo or redo, run the history and name the action in their tooltips.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_UndoRedo_FollowTheHistoryAndRunIt() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var undo = host.Get<Button>("Toolbar_Undo");
        var redo = host.Get<Button>("Toolbar_Redo");
        var nodes = host.Session.RequirePage().Nodes.Count;
        var initial = (Undo: undo.IsEnabled, Redo: redo.IsEnabled);

        // Act
        host.Choose("Menu_Insert_Node_Mixer");
        var description = host.Session.Diagram.History.UndoDescription;
        var edited = (Undo: undo.IsEnabled, Redo: redo.IsEnabled, Tip: ToolTip.GetTip(undo), Count: host.Session.RequirePage().Nodes.Count);
        Click(host, undo);
        var undone = (Undo: undo.IsEnabled, Redo: redo.IsEnabled, Tip: ToolTip.GetTip(redo), Count: host.Session.RequirePage().Nodes.Count);
        Click(host, redo);

        // Assert
        Assert.Multiple(
            () => Assert.Equal((false, false), initial),
            () => Assert.Equal((true, false, $"Undo {description} (Ctrl+Z)", nodes + 1), (edited.Undo, edited.Redo, edited.Tip, edited.Count)),
            () => Assert.Equal((false, true, $"Redo {description} (Ctrl+Y)", nodes), (undone.Undo, undone.Redo, undone.Tip, undone.Count)),
            () => Assert.Equal(nodes + 1, host.Session.RequirePage().Nodes.Count));
    });

    /// <summary>
    ///     Validates that the zoom buttons zoom the canvas and Delete removes the selection.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_ZoomAndDelete_RunTheirCommands() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        host.Editor.Zoom = 1;
        host.Flush();

        // Act
        Click(host, host.Get<Button>("Toolbar_ZoomIn"));
        var zoomedIn = host.Editor.Zoom;
        Click(host, host.Get<Button>("Toolbar_ZoomOut"));
        var zoomedOut = host.Editor.Zoom;
        var nodes = host.Session.RequirePage().Nodes.Count;
        host.Editor.Select(host.Session.RequirePage().Nodes[0]);
        host.Flush();
        Click(host, host.Get<Button>("Toolbar_Delete"));

        // Assert
        Assert.Multiple(
            () => Assert.True(zoomedIn > 1),
            () => Assert.Equal(1, zoomedOut, 3),
            () => Assert.Equal(nodes - 1, host.Session.RequirePage().Nodes.Count));
    });

    /// <summary>
    ///     Validates that the annotation button adds an annotation.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Annotation_AddsAnAnnotation() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var before = host.Session.RequirePage().Annotations.Count;

        // Act
        Click(host, host.Get<Button>("Toolbar_Annotation"));

        // Assert
        Assert.Equal(before + 1, host.Session.RequirePage().Annotations.Count);
    });

    /// <summary>
    ///     Validates that Group is enabled only with a suitable selection and groups the selection once the title is confirmed.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Group_NeedsASelectionAndGroupsIt() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var group = host.Get<Button>("Toolbar_Group");
        var disabledWithoutSelection = !group.IsEnabled;
        var groups = host.Session.RequirePage().Groups.Count;
        host.Editor.SelectAll();
        host.Flush();
        var enabledWithSelection = group.IsEnabled;

        // Act: the group command asks for a title in a dialog, which is confirmed here
        Click(host, group);
        var dialog = host.Window.OwnedWindows.OfType<PromptDialog>().Single();
        host.Get<Button>(AutomationIds.PromptOk, dialog).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();

        // Assert
        Assert.Multiple(
            () => Assert.True(disabledWithoutSelection),
            () => Assert.True(enabledWithSelection),
            () => Assert.Equal(groups + 1, host.Session.RequirePage().Groups.Count));
    });

    /// <summary>
    ///     Validates that the toggle buttons flip the grid, snapping and the properties panel, and show the state when it changes elsewhere.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Toggles_FlipTheStateAndReflectIt() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var grid = host.Get<ToggleButton>("Toolbar_ShowGrid");
        var snap = host.Get<ToggleButton>("Toolbar_SnapToGrid");
        var panel = host.Get<ToggleButton>("Toolbar_PropertiesPanel");
        var initial = (Grid: grid.IsChecked, Snap: snap.IsChecked, Panel: panel.IsChecked);

        // Act
        Click(host, grid);
        Click(host, snap);
        Click(host, panel);
        var flipped = (host.Editor.ShowGrid, host.Editor.SnapToGrid, host.Window.IsPropertiesPanelVisible, Grid: grid.IsChecked, Snap: snap.IsChecked, Panel: panel.IsChecked);
        host.Editor.ShowGrid = true;
        host.Get<MenuItem>("Menu_View_SnapToGrid").IsChecked = true;
        host.Flush();
        var restored = (Grid: grid.IsChecked, Snap: snap.IsChecked);

        // Assert
        Assert.Multiple(
            () => Assert.Equal((true, true, false), initial),
            () => Assert.Equal((false, false, true, false, false, true), flipped),
            () => Assert.Equal((true, true), restored));
    });

    /// <summary>
    ///     Validates that View &gt; Show Toolbar hides and shows the toolbar.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_ShowToolbarMenuItem_HidesAndShowsTheBar() => GuiTest.Run(() =>
    {
        // Arrange
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        var toolbar = host.Get<Control>("Toolbar");
        var item = host.Get<MenuItem>("Menu_View_ShowToolbar");
        var initial = (Visible: toolbar.IsVisible, item.IsChecked);

        // Act
        item.IsChecked = false;
        var hidden = toolbar.IsVisible;
        item.IsChecked = true;

        // Assert
        Assert.Multiple(
            () => Assert.Equal((true, true), initial),
            () => Assert.False(hidden),
            () => Assert.True(toolbar.IsVisible));
    });

    /// <summary>
    ///     Validates that the tooltips name the command and show its shortcut when it has one.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Tooltips_NameTheCommandAndShowTheShortcut() => GuiTest.Run(() =>
    {
        // Arrange / Act
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        string? Tip(string command) => ToolTip.GetTip(host.Get<Button>($"Toolbar_{command}")) as string;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("New (Ctrl+N)", Tip("New")),
            () => Assert.Equal("Save (Ctrl+S)", Tip("Save")),
            () => Assert.Equal("Cut (Ctrl+X)", Tip("Cut")),
            () => Assert.Equal("Delete (Delete)", Tip("Delete")),
            () => Assert.Equal("Group Selection (Ctrl+G)", Tip("Group")),
            () => Assert.Equal("Ungroup (Ctrl+Shift+G)", Tip("Ungroup")),
            () => Assert.Equal("Toggle Collapse", Tip("ToggleCollapse")),
            () => Assert.Equal("Add Annotation", Tip("Annotation")),
            () => Assert.Equal("Zoom In (Ctrl++)", Tip("ZoomIn")),
            () => Assert.Equal("Zoom Out (Ctrl+-)", Tip("ZoomOut")),
            () => Assert.Equal("Zoom To Fit (Ctrl+0)", Tip("ZoomToFit")),
            () => Assert.Equal("Show Grid", Tip("ShowGrid")),
            () => Assert.Equal("Properties (Alt+Enter)", Tip("Properties")),
            () => Assert.Equal("Properties Panel (F4)", Tip("PropertiesPanel")),
            () => Assert.Equal("Export Page PNG", Tip("ExportPng")));
    });

    /// <summary>
    ///     Renders the main window with its toolbar to a PNG in the temp folder, so the look can be inspected.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task Toolbar_Window_RendersToAScreenshot() => GuiTest.Run(() =>
    {
        // Arrange: an edit made and a node selected, so enabled and disabled buttons both show
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "math"));
        host.Choose("Menu_Insert_Node_Mixer");
        host.Editor.Select(host.Session.RequirePage().Nodes[0]);
        host.Flush();

        // Act: the dark theme (the demo's usual look) goes to plexus-toolbar.png, the light theme beside it
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        var original = application.RequestedThemeVariant;
        try
        {
            Capture(host, application, ThemeVariant.Light, "plexus-toolbar-light.png");
            Capture(host, application, ThemeVariant.Dark, "plexus-toolbar.png");
        }
        finally
        {
            application.RequestedThemeVariant = original;
        }

        // Assert
        Assert.True(new System.IO.FileInfo(System.IO.Path.Join(System.IO.Path.GetTempPath(), "plexus-toolbar.png")).Length > 0);
    });

    /// <summary>
    ///     Renders the window under a theme variant to a PNG in the temp folder.
    /// </summary>
    /// <param name="host">The window host.</param>
    /// <param name="application">The application.</param>
    /// <param name="variant">The theme variant.</param>
    /// <param name="fileName">The file name.</param>
    private static void Capture(MainWindowHost host, Application application, ThemeVariant variant, string fileName)
    {
        application.RequestedThemeVariant = variant;
        host.Flush();
        using var frame = host.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(System.IO.Path.Join(System.IO.Path.GetTempPath(), fileName), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>
    ///     Presses a toolbar button the way the user does: raises its click.
    /// </summary>
    /// <param name="host">The window host, flushed afterwards.</param>
    /// <param name="button">The button.</param>
    private static void Click(MainWindowHost host, Button button)
    {
        Assert.True(button.IsEnabled, $"{AutomationProperties.GetAutomationId(button)} is disabled");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        host.Flush();
    }
}
