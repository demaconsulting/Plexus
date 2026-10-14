using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DemaConsulting.Plexus.Avalonia;
using DemaConsulting.Plexus.Demo.Gui.Dialogs;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The demo's main window: menu bar, node palette, page tabs, the diagram editor and a status bar.
/// </summary>
/// <remarks>
///     The window is built in code and is a thin layer over <see cref="DemoSession"/>, which owns the
///     document and implements the commands. This file holds the layout and the wiring; the menus,
///     the command handlers and the context menu live in the other parts of the class.
/// </remarks>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    ///     The zoom the palette draws its items at: small enough to show several node types at once.
    /// </summary>
    private const double PaletteZoom = 0.75;

    /// <summary>
    ///     The initial width of the palette pane, in device-independent pixels: one column of items.
    /// </summary>
    internal const double PaletteWidth = 205;

    /// <summary>
    ///     The narrowest the palette pane can be dragged, in device-independent pixels.
    /// </summary>
    /// <remarks>
    ///     Equal to the starting width, which is one full palette column at the demo's palette zoom: any narrower and
    ///     the single column would be clipped behind a horizontal scroll bar.
    /// </remarks>
    internal const double MinPaletteWidth = PaletteWidth;

    /// <summary>
    ///     The narrowest the editor pane can be left when the palette is dragged wider, in device-independent pixels.
    /// </summary>
    internal const double MinEditorWidth = 300;

    /// <summary>
    ///     The command-line options the window was started with.
    /// </summary>
    private readonly GuiOptions _options;

    /// <summary>
    ///     The page tab strip.
    /// </summary>
    private readonly TabStrip _tabs = new();

    /// <summary>
    ///     The status bar message.
    /// </summary>
    private readonly TextBlock _statusText = new();

    /// <summary>
    ///     The status bar selection count.
    /// </summary>
    private readonly TextBlock _statusSelection = new();

    /// <summary>
    ///     The status bar zoom percentage.
    /// </summary>
    private readonly TextBlock _statusZoom = new();

    /// <summary>
    ///     The status bar dirty marker.
    /// </summary>
    private readonly TextBlock _statusDirty = new();

    /// <summary>
    ///     Whether the window may close without asking about unsaved changes.
    /// </summary>
    private bool _closeWithoutPrompt;

    /// <summary>
    ///     Whether the tab selection is being updated from the editor (so it must not update the editor back).
    /// </summary>
    private bool _syncingTabs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="options">The command-line options.</param>
    public MainWindow(GuiOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Width = 1280;
        Height = 800;

        // The window can be shrunk to the palette's and the editor's minimum widths and no further; on a small screen
        // it is also what placement never shrinks the window below
        MinWidth = 640;
        MinHeight = 420;
        WindowPlacement.KeepOnScreen(this);
        this.WithId(AutomationIds.MainWindow);

        Editor = new DiagramEditor { ShowGrid = true, SnapToGrid = true }.WithId(AutomationIds.Editor);
        Session = new DemoSession(Editor, options.Types);

        // The palette follows the editor: it lists the node types of the editor's current document (New and Open
        // replace it), draws with its theme, adds a node at the canvas center on double-click and has its own search
        // box. It keeps a fixed zoom rather than the editor's, so it stays one comfortable size while the canvas zooms.
        Palette = new NodePalette { Editor = Editor, Zoom = PaletteZoom }.WithId(AutomationIds.Palette);

        // The slider editor that the Task host data and some node properties name; shared by the dialog and the panel
        DemoEditors.Register();
        Content = BuildLayout();
        WireEvents();
        RefreshAll();
        WireAltShortcut();
    }

    /// <summary>
    ///     Gets the diagram editor control.
    /// </summary>
    public DiagramEditor Editor { get; }

    /// <summary>
    ///     Gets the node palette control.
    /// </summary>
    public NodePalette Palette { get; }

    /// <summary>
    ///     Gets the column of the window's body that holds the palette; its width is what dragging <see cref="PaletteSplitter"/> changes.
    /// </summary>
    public ColumnDefinition PaletteColumn { get; } = new(PaletteWidth, GridUnitType.Pixel) { MinWidth = MinPaletteWidth };

    /// <summary>
    ///     Gets the splitter between the palette and the editor, which the user drags to resize the palette.
    /// </summary>
    public GridSplitter PaletteSplitter { get; private set; } = null!;

    /// <summary>
    ///     Gets the document session.
    /// </summary>
    public DemoSession Session { get; }

    /// <summary>
    ///     Closes the window without asking about unsaved changes (used by the automation <c>quit</c> command).
    /// </summary>
    public void CloseWithoutPrompt()
    {
        _closeWithoutPrompt = true;
        Close();
    }

    /// <summary>
    ///     Builds the window content.
    /// </summary>
    /// <returns>The root control.</returns>
    private DockPanel BuildLayout()
    {
        var root = new DockPanel();

        var menu = BuildMenuBar();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        var status = BuildStatusBar();
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);

        var body = new Grid { ColumnDefinitions = { PaletteColumn, new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) { MinWidth = MinEditorWidth } } };
        var palette = BuildPalette();
        body.Children.Add(palette);

        // The splitter between the palette and the canvas: dragging it resizes the palette column, and a wide
        // palette shows its items in several columns
        PaletteSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            Width = 5,
            Background = new SolidColorBrush(Colors.Gray, 0.35),
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        }.WithId(AutomationIds.PaletteSplitter);
        Grid.SetColumn(PaletteSplitter, 1);
        body.Children.Add(PaletteSplitter);

        ConfigureTabs();
        var editorPane = new DockPanel();
        DockPanel.SetDock(_tabs, Dock.Top);
        editorPane.Children.Add(_tabs);
        editorPane.Children.Add(Editor);
        Grid.SetColumn(editorPane, 2);
        body.Children.Add(editorPane);
        AddPropertiesPanel(body);
        root.Children.Add(body);
        return root;
    }

    /// <summary>
    ///     Builds the left pane: a heading over the library's <see cref="NodePalette"/>.
    /// </summary>
    /// <remarks>
    ///     The palette does the rest by itself: it draws every type as the canvas draws the node, groups them under
    ///     their category headers, filters them with its own search box, starts the drag that creates a node on the
    ///     editor and adds a node at the canvas center on a double-click. It lays the items of a category out in as
    ///     many columns as the pane is wide enough for, so dragging the splitter beside it shows more at once.
    /// </remarks>
    /// <returns>The pane.</returns>
    private Control BuildPalette()
    {
        ToolTip.SetTip(Palette, "Drag a node type onto the canvas, or double-click it, to add the node");

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock { Text = "Node types", FontWeight = FontWeight.SemiBold, Margin = new Thickness(8, 6) }.DockTop(),
                    Palette,
                },
            },
        };
    }

    /// <summary>
    ///     Builds the status bar.
    /// </summary>
    /// <returns>The status bar.</returns>
    private Control BuildStatusBar()
    {
        _statusText.WithId(AutomationIds.StatusText);
        _statusSelection.WithId(AutomationIds.StatusSelection);
        _statusZoom.WithId(AutomationIds.StatusZoom);
        _statusDirty.WithId(AutomationIds.StatusDirty);
        foreach (var text in new[] { _statusSelection, _statusZoom, _statusDirty })
        {
            text.Margin = new Thickness(16, 0, 0, 0);
        }

        // The message takes the remaining width and trims with an ellipsis (full text in its tooltip), so a long
        // message such as a paste failure listing many node types never pushes the fixed fields out of view
        _statusText.TextTrimming = TextTrimming.CharacterEllipsis;
        Session.MessageChanged += (_, _) => ToolTip.SetTip(_statusText, string.IsNullOrEmpty(Session.Message) ? null : Session.Message);
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
            Margin = new Thickness(8, 3),
        };
        var fields = new Control[] { _statusText, _statusSelection, _statusZoom, _statusDirty };
        for (var column = 0; column < fields.Length; column++)
        {
            Grid.SetColumn(fields[column], column);
            bar.Children.Add(fields[column]);
        }

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = bar,
        }.WithId(AutomationIds.StatusBar);
    }

    /// <summary>
    ///     Configures the page tabs: bound to the diagram's pages, selecting a tab shows that page.
    /// </summary>
    private void ConfigureTabs()
    {
        _tabs.WithId(AutomationIds.PagesTabs);
        _tabs.ItemsSource = Session.Diagram.Pages;
        _tabs.ItemTemplate = new FuncDataTemplate<DiagramPage>((_, _) =>
        {
            var caption = new TextBlock { Margin = new Thickness(4, 2) };
            caption.Bind(TextBlock.TextProperty, new Binding(nameof(DiagramPage.Name)));
            return caption;
        });
        _tabs.SelectedItem = Editor.Page;
        _tabs.SelectionChanged += (_, _) =>
        {
            if (!_syncingTabs && _tabs.SelectedItem is DiagramPage { IsAttached: true } page && ReferenceEquals(page.Diagram, Session.Diagram) && !ReferenceEquals(page, Editor.Page))
            {
                Session.SelectPage(page);
            }
        };
        _tabs.ContainerPrepared += (_, e) =>
        {
            if (e.Container is Control container)
            {
                AutomationProperties.SetAutomationId(container, AutomationIds.PageTab(e.Index));
                container.DoubleTapped += (_, _) => Fire(RenamePageAsync);
            }
        };
        _tabs.ContainerIndexChanged += (_, e) =>
        {
            if (e.Container is Control container)
            {
                AutomationProperties.SetAutomationId(container, AutomationIds.PageTab(e.NewIndex));
            }
        };
    }

    /// <summary>
    ///     Connects the session, the editor and the window to each other.
    /// </summary>
    private void WireEvents()
    {
        Session.StateChanged += (_, _) => RefreshAll();
        Session.MessageChanged += (_, _) => _statusText.Text = Session.Message;
        Session.DiagramReplaced += (_, _) =>
        {
            // A new document has its own page list; a new document starts with the whole diagram in view
            _syncingTabs = true;
            _tabs.ItemsSource = Session.Diagram.Pages;
            _tabs.SelectedItem = Editor.Page;
            _syncingTabs = false;
            Editor.Zoom = 1;
            Editor.Offset = default;
            Editor.ZoomToFit();
            UpdatePropertiesPanel();
        };
        Editor.SelectionChanged += (_, _) =>
        {
            RefreshAll();
            UpdatePropertiesPanel();
        };
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Editor.NodeDoubleClicked += (_, e) => Fire(() => ActivateNodeAsync(e.Node));
        Editor.ClipboardFailed += (_, e) => Session.SetMessage(ClipboardMessage.Describe(e.Error));
        Editor.DiagramContextRequested += OnContextRequested;
        Editor.ToolTipRequested += OnToolTipRequested;

        Opened += (_, _) => ApplyStartup();
        Closing += OnClosing;
    }

    /// <summary>
    ///     Reacts to editor property changes: keeps the tabs, the zoom display and the toggles in step.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="e">What changed.</param>
    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DiagramEditor.PageProperty)
        {
            _syncingTabs = true;
            _tabs.SelectedItem = Editor.Page;
            _syncingTabs = false;
            RefreshAll();
        }
        else if (e.Property == DiagramEditor.ZoomProperty)
        {
            _statusZoom.Text = $"Zoom: {Editor.Zoom * 100:0}%";
        }
        else if (e.Property == DiagramEditor.ShowGridProperty)
        {
            _showGrid.IsChecked = Editor.ShowGrid;
        }
        else if (e.Property == DiagramEditor.SnapToGridProperty)
        {
            _snapToGrid.IsChecked = Editor.SnapToGrid;
        }
        else if (e.Property == DiagramEditor.ShowToolTipsProperty)
        {
            _showToolTips.IsChecked = Editor.ShowToolTips;
        }
    }

    /// <summary>
    ///     Adds the live logic value to the tooltip of a boolean wire, for example <c>SwitchA.out → Sum.a = on</c>.
    /// </summary>
    /// <remarks>
    ///     Node and port tooltips come from the node types (descriptions and live <c>NodeType.ToolTip</c> functions); this
    ///     handler shows the other way a host customizes tooltips: overriding the editor's default text in the event.
    /// </remarks>
    /// <param name="sender">The editor.</param>
    /// <param name="e">The tooltip request.</param>
    private static void OnToolTipRequested(object? sender, DiagramToolTipRequestedEventArgs e)
    {
        if (e.Element is Wire wire && e.Content is string text && DescribeLogicValue(wire) is { } value)
        {
            e.Content = $"{text} = {value}";
        }
    }

    /// <summary>
    ///     Evaluates the logic value a boolean wire carries.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>"on" or "off" for a wire leaving a logic node's boolean output; otherwise null.</returns>
    internal static string? DescribeLogicValue(Wire wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (wire.Source.TypeKey != Model.DemoNodeTypes.Bool
            || wire.Page is not { } page
            || !Model.LogicEvaluator.Evaluate(page).TryGetValue(wire.Source, out var value))
        {
            return null;
        }

        return value ? "on" : "off";
    }

    /// <summary>
    ///     Opens the document the command line asked for (or the default one).
    /// </summary>
    private void ApplyStartup()
    {
        try
        {
            if (_options.OpenFile is { } file)
            {
                Session.Open(file);
            }
            else if (_options.Sample is { } sample)
            {
                Session.OpenSample(sample);
            }
            else if (_options.Types == NodeTypeSet.Full)
            {
                Session.OpenSample(GuiOptions.DefaultSample);
            }
            else
            {
                // The default sample needs every node type, which the basic set does not have
                Session.SetMessage("Basic node types: started with an empty diagram.");
            }
        }
        catch (Exception ex) when (IsDocumentError(ex))
        {
            Session.SetMessage($"Could not open the startup document: {ex.Message}");
        }

        // The window now has its real size, so the first fit is meaningful
        Editor.ZoomToFit();
    }

    /// <summary>
    ///     Refreshes everything that mirrors session or editor state: title, status bar, menu enablement and the tab ids.
    /// </summary>
    private void RefreshAll()
    {
        Title = Session.Title;
        _statusText.Text = Session.Message;
        _statusSelection.Text = $"Selected: {Editor.Selection.Count}";
        _statusZoom.Text = $"Zoom: {Editor.Zoom * 100:0}%";
        _statusDirty.Text = Session.IsDirty ? "Modified *" : "Saved";
        RefreshMenus();
    }

    /// <summary>
    ///     Runs an asynchronous command from a synchronous event handler, reporting failures in the status bar.
    /// </summary>
    /// <param name="command">The command.</param>
    private void Fire(Func<Task> command) => _ = RunReportingAsync(command);

    /// <summary>
    ///     Awaits a command and turns its expected failures into a status message.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that never faults for expected failures.</returns>
    private async Task RunReportingAsync(Func<Task> command)
    {
        try
        {
            await command().ConfigureAwait(true);
        }
        catch (Exception ex) when (IsDocumentError(ex) || ex is InvalidOperationException)
        {
            Session.SetMessage($"Error: {ex.Message}");
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            // The operating system clipboard can be locked by another process or by a locked session
            Session.SetMessage(ClipboardMessage.Describe(ex));
        }
    }

    /// <summary>
    ///     Tells whether an exception is an expected failure of opening, saving or editing (as opposed to a bug).
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>True for I/O, format and snapshot problems.</returns>
    private static bool IsDocumentError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or SnapshotException or ArgumentException or NotSupportedException;

    /// <summary>
    ///     Asks about unsaved changes when the user closes the window.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The closing arguments.</param>
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeWithoutPrompt || !Session.IsDirty)
        {
            return;
        }

        // Closing cannot wait for a dialog, so cancel now and close again if the user agrees
        e.Cancel = true;
        Fire(async () =>
        {
            if (await ConfirmDiscardAsync().ConfigureAwait(true))
            {
                CloseWithoutPrompt();
            }
        });
    }
}

/// <summary>
///     Layout helpers.
/// </summary>
internal static class LayoutExtensions
{
    /// <summary>
    ///     Docks a control to the top of a <see cref="DockPanel"/>.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="control">The control.</param>
    /// <returns>The same control, for chaining.</returns>
    public static T DockTop<T>(this T control)
        where T : Control
    {
        DockPanel.SetDock(control, Dock.Top);
        return control;
    }
}
