using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     An interactive editor for a Plexus <c>DiagramPage</c>: renders it and lets the user select, move, connect, rename, copy and delete.
/// </summary>
/// <remarks>
///     <para>
///         The control works with zero configuration: <c>&lt;plexus:DiagramEditor Page="{Binding CurrentPage}"/&gt;</c>
///         (with <c>xmlns:plexus="https://github.com/demaconsulting/plexus"</c>) shows the page with the diagram's
///         own theme and makes it editable. Every edit goes through the Core model API, so it is undoable and
///         raises <c>Diagram.Changed</c>; the editor owns only view state (zoom, offset, selection, hover).
///     </para>
///     <para>
///         The view maps a diagram point <c>d</c> to the screen point <c>(d - Offset) * Zoom</c>, so
///         <see cref="Offset"/> is the diagram coordinate shown at the control's top-left corner. The scene
///         (<c>DiagramScene</c>) is rebuilt lazily, at most once per frame, whenever the model, the
///         selection or the visible area changes; painting and hit testing both read that same scene.
///     </para>
///     <para>
///         The editor opens no dialogs or menus: hosts react to <see cref="NodeDoubleClicked"/>,
///         <see cref="DiagramContextRequested"/>, <see cref="SelectionChanged"/>, <see cref="ToolTipRequested"/>, <see cref="PasteFailed"/> and <see cref="ClipboardFailed"/>.
///         For a properties dialog on double-click use <c>NodePropertiesDialog.ShowAsync(window, e.Node)</c> from the <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c> package.
///         The application must include a full Avalonia theme such as <c>&lt;FluentTheme /&gt;</c> in App.axaml.
///         The editor derives from Avalonia's <c>Control</c> (a <c>Visual</c> and <c>InputElement</c>), so all of that is available too and is not repeated in the member tables of this page: for example
///         <c>Bounds</c>, <c>IsVisible</c>, <c>IsEnabled</c>, <c>Width</c>, <c>Height</c>, <c>Focusable</c>, <c>Focus()</c>, <c>InvalidateVisual()</c> and <c>ContextMenu</c>.
///         It must be used on the UI thread, like the model it shows. Its AutomationId defaults to
///         <c>DiagramEditor</c>; give it an <c>x:Name</c> or <c>AutomationProperties.AutomationId</c> when a
///         window holds more than one. The editor shows exactly one page: bind a tab strip to <c>Diagram.Pages</c> and set <see cref="Page"/> from the selected tab
///         (see the complete application in the example below). While the editor is in the visual tree it follows the removal of the shown page, including undoing "Add page": it switches to the nearest remaining page
///         (the page now at the same index, or the last one) or to none when no page is left, and the selection is cleared. It never switches back when the page returns through undo or redo, and it does not
///         jump when pages are added or reordered. <see cref="ZoomToFit(double)"/>, <see cref="ZoomToFit(IEnumerable{DiagramElement}, double)"/> and <see cref="BringIntoView(DiagramElement)"/>
///         may be called before the first layout (for example in a window constructor): they are applied once the editor has a size (the latest request wins).
///     </para>
///     <para>
///         <b>Tooltips.</b> Hovering a node, a port or a wire shows a standard Avalonia tooltip after the usual delay: a port shows its label, the display name of
///         its type and its <c>PortDefinition.Description</c>; a node shows its type's <c>NodeType.GetToolTip</c> text (live state or validation messages
///         from <c>NodeType.ToolTip</c>, else <c>NodeType.Description</c>); a wire shows "source → target". Hosts override or suppress the text in
///         <see cref="ToolTipRequested"/>, or turn tooltips off with <see cref="ShowToolTips"/>. Tooltips are hidden during drags, connection gestures, inline
///         editing and context menus, and when the pointer leaves the element.
///     </para>
///     <para>
///         <b>Navigation.</b> <see cref="BringIntoView(DiagramElement)"/> shows any element (switching <see cref="Page"/> when needed, and showing the collapsed
///         group that hides it), and <see cref="ZoomToFit(IEnumerable{DiagramElement}, double)"/> fits a set of elements, for example the selection or search results.
///     </para>
///     <para>
///         Built-in gestures ("Ctrl" means Ctrl, or Cmd on macOS). A drag starts after the pointer moves about 3 pixels, so a plain click never creates an undo entry,
///         and one drag is one undo entry; Escape during a drag cancels it and rolls back. A read-only editor (<see cref="IsReadOnly"/>) still selects, pans, zooms and copies.
///     </para>
///     <list type="bullet">
///         <item><description>Click an element: select it. Shift+click or Ctrl+click adds it to the selection (or removes it when it was selected). Click empty canvas: clear the selection.</description></item>
///         <item><description>Drag on empty canvas: rubber-band selection of the nodes, annotations and collapsed groups it touches (expanded groups only when fully inside); Shift or Ctrl keeps the old selection. Wires are selected by clicking.</description></item>
///         <item><description>Drag a selected or unselected node, annotation or group title bar: move it (the whole selection moves together; groups bring their members; wires follow). Positions snap to the grid when <see cref="SnapToGrid"/> is on. Ctrl+drag duplicates the selection first and moves the copies.</description></item>
///         <item><description>Drag from a port: connect. A preview wire follows the pointer; over another port it turns green when the connection is allowed or red with the refusal reason next to the pointer; releasing over an allowed port connects, anywhere else cancels. You may start from an input or an output.</description></item>
///         <item><description>Selected wire: drag a bend handle to move that bend, drag a segment sideways to move it (the wire stays orthogonal), drag the end handle at either port to another port to reconnect it (same green/red feedback). Double-click a segment to add a draggable jog.</description></item>
///         <item><description>Double-click the node name, or select one node and press F2: rename in place (Enter commits, Escape cancels; a refused name shows its reason as a tooltip and keeps editing, see <see cref="RenameError"/>). Double-click the node body: raises <see cref="NodeDoubleClicked"/>.</description></item>
///         <item><description>Annotations: double-click, or select one and press F2, to edit the text in place (Ctrl+Enter or clicking elsewhere commits, Escape cancels, Enter inserts a line break); drag the corner handle of a selected annotation to resize it.</description></item>
///         <item><description>Groups: click the toggle in the title bar to collapse or expand the group (one undo entry); drag the title bar to move the group.</description></item>
///         <item><description>Pan: drag with the middle mouse button, or hold Space and drag with the left button. Wheel: scroll vertically (Shift+wheel horizontally). Ctrl+wheel: zoom around the cursor (10 percent per notch, between <see cref="MinZoom"/> and <see cref="MaxZoom"/>).</description></item>
///         <item><description>Right-click: selects the element under the pointer (or clears the selection on empty canvas) and raises <see cref="DiagramContextRequested"/> when the button is released.</description></item>
///         <item><description>Drop a node type from a <see cref="NodePalette"/> (or any control set up with <see cref="EnableNodeTypeDrag"/>): creates a node at the drop position.</description></item>
///         <item><description>Keyboard: Delete deletes the selection; Ctrl+Z undoes; Ctrl+Y or Ctrl+Shift+Z redoes; Ctrl+C, Ctrl+X, Ctrl+V copy, cut and paste (paste lands at the pointer when it is over the editor); Ctrl+A selects all; Ctrl+D duplicates the selection one grid cell away; F2 renames or edits text; Escape cancels the drag in progress. There is no arrow-key nudging.</description></item>
///     </list>
/// </remarks>
/// <example>
///     The editor can also be created and configured from code (<c>types</c> is the registry of the <c>DemaConsulting.Plexus</c> namespace example);
///     in XAML it is just <c>&lt;plexus:DiagramEditor Page="{Binding CurrentPage}"/&gt;</c>. The complete application below the sample shows it in a window.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Scene;
///     using PlexusColor = DemaConsulting.Plexus.Geometry.Color;   // Avalonia has a Color too
///
///     static class EditorSample
///     {
///         public static async Task RunAsync(NodeTypeRegistry types)
///         {
///             var diagram = new Diagram(types);
///             var page = diagram.Pages.Add("Main");
///             var a = page.AddNode("demo.Source", 0, 0, name: "A");
///
///             var editor = new DiagramEditor
///             {
///                 Page = page,                                          // the Plexus page to show; editable immediately
///                 ShowGrid = true,
///                 SnapToGrid = true,
///                 IsReadOnly = false,                                   // true: select, pan, zoom and copy only
///                 DiagramTheme = DiagramTheme.Classic with { NodeFill = PlexusColor.Parse("#223344") },   // optional override of Diagram.Theme
///             };
///             editor.ZoomToFit();                                       // may be called before the first layout: applied once the editor has a size
///
///             editor.NodeDoubleClicked += (_, e) => Console.WriteLine($"edit {e.Node.Name}");
///             editor.DiagramContextRequested += (_, e) => Console.WriteLine(e.Element?.Id ?? "canvas");
///             editor.SelectionChanged += (_, _) => Console.WriteLine($"{editor.Selection.Count} selected");
///
///             editor.AddNodeAtCenter(types.Get("demo.Source"));         // the same as dropping a type from a palette
///             editor.Select(a);                                         // selection is view state
///             await editor.CopyAsync();                                 // uses the system clipboard by default
///             await editor.PasteAsync();
///             if (editor.Undo()) { Console.WriteLine("undone"); }       // the editor shares the diagram's undo history
///         }
///     }
///     ]]></code>
///     <para>
///         <b>A complete first application</b>: a window with a node palette next to an editor, page tabs, File Open/Save, Edit Undo/Redo/Copy/Paste,
///         Group selection, Move selection to page 2, Zoom to fit, the ready-made node properties dialog on double-click, a context menu on right-click, and a
///         status bar for failures. It was compiled against Avalonia 12 with <c>ImplicitUsings</c> and <c>Nullable</c> enabled and the packages
///         <c>DemaConsulting.Plexus.Avalonia</c> and <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c> (the second one provides <c>NodePropertiesDialog</c>).
///         The application needs a full Avalonia theme, so App.axaml contains <c>&lt;FluentTheme /&gt;</c> in its styles (the palette's search box, context menus
///         and dialogs use the theme's control styles). First the XAML for MainWindow.axaml:
///     </para>
///     <code lang="xml"><![CDATA[
///     <Window xmlns="https://github.com/avaloniaui"
///             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///             xmlns:plexus="https://github.com/demaconsulting/plexus"
///             xmlns:core="clr-namespace:DemaConsulting.Plexus;assembly=DemaConsulting.Plexus"
///             x:Class="MyApp.MainWindow"
///             Title="My diagram" Width="1100" Height="700">
///         <DockPanel>
///             <Menu DockPanel.Dock="Top">
///                 <MenuItem Header="_File">
///                     <MenuItem Header="_Open..." Click="OnOpen" />
///                     <MenuItem Header="_Save..." Click="OnSave" />
///                 </MenuItem>
///                 <MenuItem Header="_Edit">
///                     <MenuItem x:Name="UndoItem" Header="_Undo" InputGesture="Ctrl+Z" Click="OnUndo" />
///                     <MenuItem x:Name="RedoItem" Header="_Redo" InputGesture="Ctrl+Y" Click="OnRedo" />
///                     <Separator />
///                     <MenuItem Header="_Copy" Click="OnCopy" />
///                     <MenuItem Header="_Paste" Click="OnPaste" />
///                     <Separator />
///                     <MenuItem Header="_Group selection" Click="OnGroup" />
///                     <MenuItem Header="_Add page" Click="OnAddPage" />
///                     <MenuItem Header="Move selection to page _2" Click="OnMoveToPage2" />
///                 </MenuItem>
///                 <MenuItem Header="_View">
///                     <MenuItem Header="Zoom to _fit" Click="OnZoomToFit" />
///                 </MenuItem>
///             </Menu>
///             <Border DockPanel.Dock="Bottom" Padding="6,3" Background="#22808080">
///                 <TextBlock x:Name="Status" Text="Ready" />
///             </Border>
///             <!-- The splitter resizes the palette column; do not set a Width on the palette itself -->
///             <Grid ColumnDefinitions="220,5,*">
///                 <plexus:NodePalette Grid.Column="0" Editor="{Binding #Editor}" />
///                 <GridSplitter Grid.Column="1" ResizeDirection="Columns" />
///                 <DockPanel Grid.Column="2">
///                     <!-- One tab per page: bind the tabs to Diagram.Pages (set in code) and two-way bind the selected tab to the editor's page,
///                          so choosing a tab shows that page and the editor switching pages by itself (a removed page) moves the tab -->
///                     <TabControl x:Name="Tabs" DockPanel.Dock="Top" Height="38" SelectedItem="{Binding #Editor.Page, Mode=TwoWay}">
///                         <TabControl.ItemTemplate>
///                             <DataTemplate x:DataType="core:DiagramPage">
///                                 <TextBlock Text="{Binding Name}" />
///                             </DataTemplate>
///                         </TabControl.ItemTemplate>
///                     </TabControl>
///                     <plexus:DiagramEditor x:Name="Editor" />
///                 </DockPanel>
///             </Grid>
///         </DockPanel>
///     </Window>
///     ]]></code>
///     <para>
///         The node types of the application (MyTypes.cs). Your own domain goes here; see <c>NodeType</c> for everything a type can declare.
///         Port types are strings you define, nothing is predefined:
///     </para>
///     <code><![CDATA[
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     namespace MyApp;
///
///     public static class MyTypes
///     {
///         public static NodeTypeRegistry Create()
///         {
///             var types = new NodeTypeRegistry();
///             types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));
///             types.Add(new NodeType("demo.Source")
///             {
///                 Title = "SOURCE",
///                 Category = "Demo",
///                 Outputs = { new PortDefinition("out", "number") },
///                 Properties =
///                 {
///                     new PropertyDefinition("Value", 1.0) { ShowInSummary = true },
///                     new PropertyDefinition("Mode", "Fast") { Choices = ["Fast", "Slow"] },
///                     new PropertyDefinition("Enabled", true),
///                 },
///             });
///             types.Add(new NodeType("demo.Sink")
///             {
///                 Title = "SINK",
///                 Category = "Demo",
///                 Inputs = { new PortDefinition("in", "number") },
///             });
///             return types;
///         }
///     }
///     ]]></code>
///     <para>
///         The code-behind (MainWindow.axaml.cs). The editor shows what you set as <c>Editor.Page</c> (here the tab strip does that through its binding); the palette follows the editor, so it needs nothing more.
///         The editor follows the removal of its page by itself (for example when the user undoes "Add page"), so no page handling is needed. <c>ZoomToFit</c> may be called in the constructor:
///         it is applied when the editor gets its first layout.
///         Open and Save use the window's storage provider and <c>Diagram.ToJson</c> / <c>Diagram.FromJson</c>; rules, stylers, the theme and event handlers are not part of the
///         JSON, so configure a loaded diagram again (here all of it is in one <c>DiagramOptions</c> that every new or opened diagram receives).
///         Menu handlers that touch the clipboard catch platform failures (<c>CopyAsync</c> and <c>PasteAsync</c> let
///         exceptions from the operating system clipboard reach you; a shortcut such as Ctrl+C reports them through <c>ClipboardFailed</c> instead).
///         The properties dialog is one line: <c>NodePropertiesDialog.ShowAsync</c> edits the node's name, properties and host data in a staged
///         property grid and applies them as one undo entry.
///     </para>
///     <code><![CDATA[
///     using Avalonia;
///     using Avalonia.Controls;
///     using Avalonia.Controls.Primitives.PopupPositioning;
///     using Avalonia.Interactivity;
///     using Avalonia.Platform.Storage;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///     using DemaConsulting.Plexus.Scene;
///
///     namespace MyApp;
///
///     public partial class MainWindow : Window
///     {
///         private static readonly FilePickerFileType JsonFiles = new("Diagram JSON") { Patterns = ["*.json"] };
///         private readonly NodeTypeRegistry _types = MyTypes.Create();
///         private readonly DiagramOptions _options;
///         private Diagram _diagram;
///
///         public MainWindow()
///         {
///             InitializeComponent();                      // generated from the XAML: Editor, Tabs, Status, UndoItem, RedoItem exist now
///
///             // Everything that is not saved in the file, configured once for new and opened diagrams alike
///             _options = new DiagramOptions
///             {
///                 WireStyler = WireStylers.ByPortType(),
///                 Initialize = diagram => diagram.NodeRenaming += (_, e) =>   // event handlers belong to one Diagram instance
///                 {
///                     if (e.ProposedName.Contains(' '))
///                     {
///                         e.Cancel = true;
///                         e.Reason = "Names cannot contain spaces.";
///                     }
///                 },
///             };
///             _diagram = new Diagram(_types, _options);
///             _diagram.Pages.Add("Main");
///             ShowDiagram(_diagram);
///
///             Editor.SelectionChanged += (_, _) => Status.Text = $"{Editor.Selection.Count} selected";
///             Editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
///             Editor.DiagramContextRequested += (_, e) => ShowContextMenu(e.Element, e.ScreenPosition);
///             Editor.PasteFailed += (_, e) => Status.Text = $"Paste failed: {e.Result.Error}";
///             Editor.ClipboardFailed += (_, e) => Status.Text = $"Clipboard failed: {e.Error.Message}";
///         }
///
///         private void ShowDiagram(Diagram diagram)
///         {
///             _diagram = diagram;
///             Tabs.ItemsSource = diagram.Pages;           // PageCollection implements INotifyCollectionChanged, so tabs follow add/remove/undo
///             Editor.Page = diagram.Pages[0];             // the tab selection follows through the binding
///             Editor.ZoomToFit();                         // fine before the window is shown: applied after the first layout
///
///             // Enable Undo/Redo from the history; the editor's Undo() throws only while a host transaction is open
///             diagram.History.Changed += (_, _) => UpdateEditMenu();
///             UpdateEditMenu();
///         }
///
///         private void UpdateEditMenu()
///         {
///             UndoItem.IsEnabled = _diagram.History.CanUndo;
///             RedoItem.IsEnabled = _diagram.History.CanRedo;
///         }
///
///         // The editor raises DiagramContextRequested on right-button release and has selected the element under the pointer.
///         // MenuFlyout.ShowAt(Editor, true) would open at TopLevel.LastPointerPosition (the pointer right now); a ContextMenu
///         // with a PlacementRect opens exactly at the position the event reports, which is relative to the editor.
///         private void ShowContextMenu(DiagramElement? element, Point position)
///         {
///             var menu = new ContextMenu();
///             var delete = new MenuItem { Header = "Delete", IsEnabled = element is not null };
///             delete.Click += (_, _) => Editor.Delete([element!]);
///             menu.Items.Add(delete);
///
///             var rename = new MenuItem { Header = "Rename", IsEnabled = element is Node };
///             rename.Click += (_, _) => Editor.BeginRename((Node)element!);
///             menu.Items.Add(rename);
///
///             // The menu's top-left corner goes to the click point
///             menu.PlacementTarget = Editor;
///             menu.Placement = PlacementMode.AnchorAndGravity;
///             menu.PlacementAnchor = PopupAnchor.TopLeft;
///             menu.PlacementGravity = PopupGravity.BottomRight;
///             menu.PlacementRect = new Rect(position, new Size(1, 1));
///             menu.Open(Editor);
///         }
///
///         private void OnGroup(object? sender, RoutedEventArgs e)
///         {
///             // One undo entry; null when nothing that can be grouped is selected (wires cannot be grouped)
///             Status.Text = Editor.GroupSelection("Group") is null ? "Select the nodes to group first" : "Grouped";
///         }
///
///         private void OnAddPage(object? sender, RoutedEventArgs e) =>
///             _diagram.Pages.Add($"Page {_diagram.Pages.Count + 1}");   // the tab strip follows; undoable like everything else
///
///         private void OnMoveToPage2(object? sender, RoutedEventArgs e)
///         {
///             if (_diagram.Pages.Count < 2)
///             {
///                 Status.Text = "Add a second page first";
///                 return;
///             }
///
///             // One undo entry. Wires between moved nodes travel with them; wires to nodes that stay behind are removed.
///             // Returns false (and changes nothing) when nothing is selected or the editor is read-only.
///             Status.Text = Editor.MoveSelectionToPage(_diagram.Pages[1]) ? "Moved to page 2" : "Select elements first";
///         }
///
///         private void OnUndo(object? sender, RoutedEventArgs e) => Editor.Undo();
///
///         private void OnRedo(object? sender, RoutedEventArgs e) => Editor.Redo();
///
///         private void OnZoomToFit(object? sender, RoutedEventArgs e) => Editor.ZoomToFit();
///
///         private async void OnCopy(object? sender, RoutedEventArgs e)
///         {
///             try
///             {
///                 await Editor.CopyAsync();
///             }
///             catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Runtime.InteropServices.ExternalException)
///             {
///                 Status.Text = $"Clipboard failed: {ex.Message}";
///             }
///         }
///
///         private async void OnPaste(object? sender, RoutedEventArgs e)
///         {
///             try
///             {
///                 await Editor.PasteAsync();               // bad clipboard content never throws: PasteFailed is raised
///             }
///             catch (Exception ex) when (ex is InvalidOperationException or IOException or System.Runtime.InteropServices.ExternalException)
///             {
///                 Status.Text = $"Clipboard failed: {ex.Message}";
///             }
///         }
///
///         private async void OnSave(object? sender, RoutedEventArgs e)
///         {
///             var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { DefaultExtension = "json", FileTypeChoices = [JsonFiles] });
///             if (file is null)
///             {
///                 return;
///             }
///
///             await using var stream = await file.OpenWriteAsync();
///             stream.SetLength(0);
///             await using var writer = new StreamWriter(stream);
///             await writer.WriteAsync(_diagram.ToJson());
///             _diagram.History.MarkClean();
///             Status.Text = $"Saved {file.Name}";
///         }
///
///         private async void OnOpen(object? sender, RoutedEventArgs e)
///         {
///             var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { FileTypeFilter = [JsonFiles] });
///             if (files.Count == 0)
///             {
///                 return;
///             }
///
///             try
///             {
///                 await using var stream = await files[0].OpenReadAsync();
///                 using var reader = new StreamReader(stream);
///                 // The registry must know every node type in the file (or set UnknownNodeTypes = Skip in the options to open the rest)
///                 ShowDiagram(Diagram.FromJson(await reader.ReadToEndAsync(), _types, _options));
///                 Status.Text = $"Opened {files[0].Name}";
///             }
///             catch (Exception ex) when (ex is SnapshotException or System.Text.Json.JsonException)
///             {
///                 Status.Text = $"Cannot open: {ex.Message}";
///             }
///         }
///     }
///     ]]></code>
/// </example>
public sealed partial class DiagramEditor : Control
{
    /// <summary>
    ///     The smallest allowed <see cref="Zoom"/>.
    /// </summary>
    public const double MinZoom = 0.1;

    /// <summary>
    ///     The largest allowed <see cref="Zoom"/>.
    /// </summary>
    public const double MaxZoom = 8;

    /// <summary>
    ///     Defines the <see cref="Page"/> property.
    /// </summary>
    public static readonly StyledProperty<PlexusPage?> PageProperty =
        AvaloniaProperty.Register<DiagramEditor, PlexusPage?>(nameof(Page));

    /// <summary>
    ///     Defines the <see cref="DiagramTheme"/> property.
    /// </summary>
    public static readonly StyledProperty<DiagramTheme?> DiagramThemeProperty =
        AvaloniaProperty.Register<DiagramEditor, DiagramTheme?>(nameof(DiagramTheme));

    /// <summary>
    ///     Defines the <see cref="Zoom"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<DiagramEditor, double>(nameof(Zoom), 1.0, coerce: CoerceZoom);

    /// <summary>
    ///     Defines the <see cref="Offset"/> property.
    /// </summary>
    public static readonly StyledProperty<AvaloniaPoint> OffsetProperty =
        AvaloniaProperty.Register<DiagramEditor, AvaloniaPoint>(nameof(Offset));

    /// <summary>
    ///     Defines the <see cref="ShowGrid"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<DiagramEditor, bool>(nameof(ShowGrid), true);

    /// <summary>
    ///     Defines the <see cref="SnapToGrid"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> SnapToGridProperty =
        AvaloniaProperty.Register<DiagramEditor, bool>(nameof(SnapToGrid), true);

    /// <summary>
    ///     Defines the <see cref="IsReadOnly"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DiagramEditor, bool>(nameof(IsReadOnly));

    /// <summary>
    ///     Paints scene primitives; owns the brush, pen, text and icon caches.
    /// </summary>
    private readonly ScenePainter _painter = new();

    /// <summary>
    ///     Measures text with Avalonia so scene layout matches the painted glyphs.
    /// </summary>
    private readonly AvaloniaTextMeasurer _measurer = new();

    /// <summary>
    ///     The scene currently shown, or null when nothing is built yet.
    /// </summary>
    private DiagramScene? _scene;

    /// <summary>
    ///     Whether the model, selection or hover changed since the scene was built.
    /// </summary>
    private bool _sceneDirty = true;

    /// <summary>
    ///     The culling area the current scene was built for, or null when it was built without culling.
    /// </summary>
    private PlexusRect? _sceneArea;

    /// <summary>
    ///     The diagram whose <see cref="Diagram.Changed"/> event the editor currently listens to.
    /// </summary>
    private Diagram? _subscribedDiagram;

    /// <summary>
    ///     Whether the editor is in the visual tree, which is when it listens to the model.
    /// </summary>
    private bool _isAttached;

    /// <summary>
    ///     The latest view request (<see cref="ZoomToFit(double)"/>, <see cref="ZoomToFit(IEnumerable{DiagramElement}, double)"/> or
    ///     <see cref="BringIntoView(DiagramElement)"/>) that could not be applied yet (no size or no page), or null.
    /// </summary>
    /// <remarks>
    ///     The function applies the request and returns <see langword="true"/> once it was applied or became obsolete, so it can
    ///     be dropped; <see langword="false"/> means it must wait for the next layout or page change.
    /// </remarks>
    private Func<bool>? _pendingView;

    /// <summary>
    ///     The page collection whose <c>CollectionChanged</c> event the editor currently listens to.
    /// </summary>
    private PageCollection? _subscribedPages;

    /// <summary>
    ///     The element under the pointer, drawn highlighted.
    /// </summary>
    private DiagramElement? _hover;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramEditor"/> class.
    /// </summary>
    public DiagramEditor()
    {
        // Keyboard shortcuts need focus, and painting must never spill outside the control
        Focusable = true;
        ClipToBounds = true;
        _selectionView = new System.Collections.ObjectModel.ReadOnlyObservableCollection<DiagramElement>(_selection);

        // A stable default lets UI automation find the editor; hosts may override it
        AutomationProperties.SetAutomationId(this, "DiagramEditor");
        InitializeNodeDrop();
    }

    /// <summary>
    ///     Gets or sets the page to show and edit; null shows an empty canvas.
    /// </summary>
    /// <remarks>
    ///     Changing the page clears the selection and cancels any gesture or rename in progress. When the shown page is removed from its diagram the editor switches to the nearest
    ///     remaining page by itself (see the class remarks).
    /// </remarks>
    public PlexusPage? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>
    ///     Gets or sets the theme to draw with; null (the default) uses the page's <see cref="Diagram.Theme"/>.
    /// </summary>
    /// <remarks>
    ///     An override changes the look only: port positions, port triangles, port hit areas and wire ends always
    ///     follow the diagram's own theme, so wires stay attached; set port metrics on <see cref="Diagram.Theme"/>.
    /// </remarks>
    public DiagramTheme? DiagramTheme
    {
        get => GetValue(DiagramThemeProperty);
        set => SetValue(DiagramThemeProperty, value);
    }

    /// <summary>
    ///     Gets or sets the zoom factor (1 = actual size), clamped to <see cref="MinZoom"/>..<see cref="MaxZoom"/>.
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    ///     Gets or sets the diagram coordinate shown at the control's top-left corner.
    /// </summary>
    public AvaloniaPoint Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the background grid dots are drawn (default true).
    /// </summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether dragging snaps to the theme's grid size (default true).
    /// </summary>
    public bool SnapToGrid
    {
        get => GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the user is prevented from changing the diagram (default false).
    /// </summary>
    /// <remarks>
    ///     A read-only editor still selects, pans, zooms and copies; it never moves, connects, deletes,
    ///     pastes, renames, resizes or collapses anything.
    /// </remarks>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    ///     Gets the scene the editor currently shows, rebuilding it first when it is stale.
    /// </summary>
    /// <remarks>
    ///     Exposed so hosts and tests can inspect exactly what is drawn. The scene is an immutable
    ///     snapshot: keep no reference across model changes.
    /// </remarks>
    public DiagramScene? Scene => EnsureScene();

    /// <summary>
    ///     Gets how many times the scene has been rebuilt; lets tests prove that rebuilds are coalesced.
    /// </summary>
    internal int SceneBuildCount { get; private set; }

    /// <summary>
    ///     Gets the theme in effect: the explicit <see cref="DiagramTheme"/>, else the page's diagram theme, else the classic theme.
    /// </summary>
    internal DiagramTheme EffectiveTheme => DiagramTheme ?? Page?.Diagram.Theme ?? DiagramTheme.Classic;

    /// <summary>
    ///     Converts a point relative to the control into diagram coordinates.
    /// </summary>
    /// <param name="screen">The point, in device-independent pixels relative to the control.</param>
    /// <returns>The same location in diagram coordinates.</returns>
    public AvaloniaPoint ScreenToDiagram(AvaloniaPoint screen) =>
        new((screen.X / Zoom) + Offset.X, (screen.Y / Zoom) + Offset.Y);

    /// <summary>
    ///     Converts a diagram coordinate into a point relative to the control.
    /// </summary>
    /// <param name="diagram">The point in diagram coordinates.</param>
    /// <returns>The same location relative to the control, in device-independent pixels.</returns>
    public AvaloniaPoint DiagramToScreen(AvaloniaPoint diagram) =>
        new((diagram.X - Offset.X) * Zoom, (diagram.Y - Offset.Y) * Zoom);

    /// <summary>
    ///     Changes the zoom while keeping the diagram point under a screen point fixed (zoom around the cursor).
    /// </summary>
    /// <param name="screen">The point relative to the control that must not move.</param>
    /// <param name="zoom">The requested zoom factor; clamped to the allowed range.</param>
    public void ZoomAt(AvaloniaPoint screen, double zoom)
    {
        // Remember what is under the pointer, change the zoom, then scroll so it is still under the pointer
        var anchor = ScreenToDiagram(screen);
        Zoom = zoom;
        Offset = new AvaloniaPoint(anchor.X - (screen.X / Zoom), anchor.Y - (screen.Y / Zoom));
    }

    /// <summary>
    ///     Zooms and scrolls so the whole diagram is visible, centered in the control.
    /// </summary>
    /// <remarks>
    ///     The zoom is chosen so the content (nodes with their names, summaries and port labels, annotations and groups) fits with the margin, but never above 1: a small diagram
    ///     is centered at actual size rather than magnified. Does nothing when the page is empty. It may be called at any time, including from a window constructor:
    ///     when the editor has no size yet (before its first layout) or shows no page, the request is remembered and applied as soon as the editor gets a size and a page
    ///     (the latest request wins, and the page's content at that moment is fitted). A pending request is dropped once applied.
    /// </remarks>
    /// <param name="margin">The empty space, in device-independent pixels, to keep around the content.</param>
    public void ZoomToFit(double margin = 30)
    {
        // Without a size or a page the request waits for the first layout (or the first page); a newer request replaces it
        _pendingView = () => TryZoomToFit(margin);
        ApplyPendingView();
    }

    /// <summary>
    ///     Applies the pending view request when it can be applied now, and forgets it once applied.
    /// </summary>
    /// <remarks>
    ///     Called when the editor gets a size and when the page changes, and right after a request is made. The request is
    ///     only forgotten when it is still the pending one, so a request that made a newer one while running keeps the newer.
    /// </remarks>
    private void ApplyPendingView()
    {
        if (_pendingView is { } pending && pending() && ReferenceEquals(_pendingView, pending))
        {
            _pendingView = null;
        }
    }

    /// <summary>
    ///     Fits the content into the control when that is possible.
    /// </summary>
    /// <param name="margin">The empty space to keep around the content.</param>
    /// <returns><see langword="false"/> when there is no page or no usable size yet, so the request must wait; otherwise <see langword="true"/> (even when the page is empty and nothing changed).</returns>
    private bool TryZoomToFit(double margin)
    {
        if (Page is not { } page || Bounds.Width <= 2 * margin || Bounds.Height <= 2 * margin)
        {
            return false;
        }

        if (ContentBounds(page) is { } box)
        {
            FitBox(box, margin);
        }

        return true;
    }

    /// <summary>
    ///     Zooms (never above 1) and scrolls so a diagram rectangle fits the control with a margin, centered.
    /// </summary>
    /// <remarks>
    ///     Shared by both <c>ZoomToFit</c> overloads. The caller has checked that the control is larger than twice the margin.
    /// </remarks>
    /// <param name="box">The rectangle to show, in diagram coordinates.</param>
    /// <param name="margin">The empty space, in device-independent pixels, to keep around it.</param>
    private void FitBox(PlexusRect box, double margin)
    {
        // Pick the zoom that fits both axes, then center the content in the control
        var zoom = Math.Min((Bounds.Width - (2 * margin)) / Math.Max(box.Width, 1), (Bounds.Height - (2 * margin)) / Math.Max(box.Height, 1));
        Zoom = Math.Min(zoom, 1);
        CenterOn(box);
    }

    /// <summary>
    ///     Scrolls so the center of a diagram rectangle is at the center of the control, keeping the zoom.
    /// </summary>
    /// <param name="box">The rectangle to center, in diagram coordinates.</param>
    private void CenterOn(PlexusRect box) =>
        Offset = new AvaloniaPoint(
            box.Center.X - (Bounds.Width / 2 / Zoom),
            box.Center.Y - (Bounds.Height / 2 / Zoom));

    /// <summary>
    ///     Determines what lies under a point of the control.
    /// </summary>
    /// <param name="screen">The point relative to the control.</param>
    /// <returns>What was hit; <see cref="HitResult.Canvas"/> when nothing was.</returns>
    public HitResult HitTest(AvaloniaPoint screen) => HitTestDiagram(ScreenToDiagram(screen).ToPlexus());

    /// <summary>
    ///     Gets the bounds of an element relative to the control (for automation and overlays); for a node this is its box, not its name or summary text.
    /// </summary>
    /// <param name="element">The element, which must be on the shown page.</param>
    /// <returns>The element's bounds in device-independent pixels relative to the control.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public AvaloniaRect GetElementScreenBounds(DiagramElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // Every element knows its own box; wires report the bounding box of their polyline
        var area = element.GetBounds();
        var topLeft = DiagramToScreen(new AvaloniaPoint(area.Left, area.Top));
        return new AvaloniaRect(topLeft, new AvaloniaSize(area.Width * Zoom, area.Height * Zoom));
    }

    /// <summary>
    ///     Gets the position of a port's connection point relative to the control (for automation).
    /// </summary>
    /// <param name="port">The port, which must belong to a node on the shown page.</param>
    /// <returns>The anchor point in device-independent pixels relative to the control.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="port"/> is null.</exception>
    public AvaloniaPoint GetPortScreenPosition(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return DiagramToScreen(port.Anchor.ToAvalonia());
    }

    /// <summary>
    ///     Draws the visible part of the diagram (background, groups, wires, nodes and overlays) for the current view.
    /// </summary>
    /// <param name="context">The drawing context supplied by Avalonia.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The background fill doubles as the hit-test surface, so it is painted even when the page is empty
        var viewport = new AvaloniaRect(Bounds.Size);
        context.FillRectangle(_painter.GetBrush(EffectiveTheme.CanvasBackground), viewport);
        var scene = EnsureScene();
        if (scene is null)
        {
            return;
        }

        // Everything below is expressed in diagram units; the transform maps it onto the screen
        using (context.PushClip(viewport))
        using (context.PushTransform(Matrix.CreateTranslation(-Offset.X, -Offset.Y) * Matrix.CreateScale(Zoom, Zoom)))
        {
            _painter.Paint(context, scene, VisibleArea() ?? default, Zoom);
            _gesture?.Draw(context, _painter, EffectiveTheme, Zoom);
            PaintGhost(context);
        }
    }

    /// <summary>
    ///     Starts listening to the shown page's diagram once the editor is in the visual tree, and rebuilds the scene. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Only listen to the model while shown, so a discarded editor cannot be kept alive by the diagram
        _isAttached = true;
        UpdateSubscription();
        InvalidateScene();
    }

    /// <summary>
    ///     Cancels any gesture in progress and stops listening to the diagram when the editor leaves the visual tree, so a discarded editor is not kept alive by the diagram. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="e">The event data supplied by Avalonia.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelGesture();
        ClearGhost();
        ResetToolTip();
        _isAttached = false;
        UpdateSubscription();
    }

    /// <summary>
    ///     Reacts to a changed editor property: a new <see cref="Page"/> clears the selection and cancels gestures; <see cref="DiagramTheme"/> or <see cref="ShowGrid"/> rebuild the scene;
    ///     <see cref="Zoom"/>, <see cref="Offset"/> and the size repaint; turning on <see cref="IsReadOnly"/> abandons a half-finished edit. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="change">The change description supplied by Avalonia.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        ArgumentNullException.ThrowIfNull(change);

        // Dispatch on the property: model-shaped changes rebuild the scene, view-shaped ones only repaint
        if (change.Property == PageProperty)
        {
            OnPageChanged();
        }
        else if (change.Property == DiagramThemeProperty || change.Property == ShowGridProperty)
        {
            InvalidateScene();
            InvalidateInlineEditors();
        }
        else if (change.Property == ZoomProperty || change.Property == OffsetProperty || change.Property == BoundsProperty)
        {
            InvalidateVisual();
            InvalidateInlineEditors();
            if (change.Property == BoundsProperty)
            {
                // The first layout with a real size is when a view request made earlier can be honored
                ApplyPendingView();
            }
        }
        else if (change.Property == IsReadOnlyProperty && IsReadOnly)
        {
            // Entering read-only mode abandons any edit that is half done
            CancelGesture();
            CancelEdit();
        }
        else if (change.Property == ShowToolTipsProperty && !ShowToolTips)
        {
            // Turning tooltips off hides one that is showing
            ResetToolTip();
        }
    }

    /// <summary>
    ///     Measures the editor: a canvas that takes all the room it is given (nothing when the parent offers infinite space), and sizes the inline edit boxes. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="availableSize">The space the parent offers.</param>
    /// <returns>The size the editor wants.</returns>
    protected override AvaloniaSize MeasureOverride(AvaloniaSize availableSize)
    {
        // The inline edit boxes float freely over the canvas, so they may take their natural size
        _renameEditor?.Measure(AvaloniaSize.Infinity);

        // The annotation box is measured at its final width so its text wraps where it will be drawn
        if (_annotationEditor is not null && ComputeAnnotationEditRect() is { } annotationArea)
        {
            _annotationEditor.Measure(annotationArea.Size);
        }

        // A canvas wants all the room it is given; unbounded parents get nothing rather than infinity
        return new AvaloniaSize(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    /// <summary>
    ///     Arranges the editor and keeps the inline rename and annotation edit boxes glued to their element while the view pans and zooms. Protected override of the Avalonia hook.
    /// </summary>
    /// <param name="finalSize">The space the parent assigned.</param>
    /// <returns>The size used, which is <paramref name="finalSize"/>.</returns>
    protected override AvaloniaSize ArrangeOverride(AvaloniaSize finalSize)
    {
        // Keep the inline edit boxes glued to their element as the view pans and zooms
        if (_renameEditor is not null && ComputeRenameRect() is { } rect)
        {
            _renameEditor.Arrange(rect);
        }

        if (_annotationEditor is not null && ComputeAnnotationEditRect() is { } annotationRect)
        {
            _annotationEditor.FontSize = EffectiveTheme.AnnotationFontSize * Zoom;
            _annotationEditor.Arrange(annotationRect);
        }

        return finalSize;
    }

    /// <summary>
    ///     Creates the UI Automation peer that exposes the editor as a pane. Protected override of the Avalonia hook.
    /// </summary>
    /// <returns>The automation peer.</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new DiagramEditorAutomationPeer(this);

    /// <summary>
    ///     Re-arranges the inline edit boxes (when open) so they follow their element after the view changed.
    /// </summary>
    private void InvalidateInlineEditors()
    {
        if (_renameEditor is not null || _annotationEditor is not null)
        {
            InvalidateArrange();
        }
    }

    /// <summary>
    ///     Determines what lies under a diagram point.
    /// </summary>
    /// <param name="point">The point in diagram coordinates.</param>
    /// <returns>The hit, or <see cref="HitResult.Canvas"/> when nothing is there or no page is shown.</returns>
    internal HitResult HitTestDiagram(PlexusPoint point)
    {
        // Thin targets get a constant on-screen reach regardless of zoom
        return EnsureScene()?.HitTest(point, 4 / Zoom) ?? HitResult.Canvas;
    }

    /// <summary>
    ///     Converts a control-relative point into Plexus diagram coordinates.
    /// </summary>
    /// <param name="screen">The point relative to the control.</param>
    /// <returns>The diagram point.</returns>
    internal PlexusPoint ToDiagram(AvaloniaPoint screen) => ScreenToDiagram(screen).ToPlexus();

    /// <summary>
    ///     Rounds a coordinate to the grid when <see cref="SnapToGrid"/> is on.
    /// </summary>
    /// <param name="value">The coordinate in diagram units.</param>
    /// <returns>The snapped (or unchanged) coordinate.</returns>
    internal double Snap(double value)
    {
        var grid = EffectiveTheme.GridSize;
        return SnapToGrid && grid > 0 ? Math.Round(value / grid) * grid : value;
    }

    /// <summary>
    ///     Marks the scene stale and schedules a repaint; any number of calls before the next frame cost one rebuild.
    /// </summary>
    internal void InvalidateScene()
    {
        _sceneDirty = true;
        InvalidateVisual();
    }

    /// <summary>
    ///     Sets the hovered element, repainting only when it changed.
    /// </summary>
    /// <param name="element">The element under the pointer, or null.</param>
    internal void SetHover(DiagramElement? element)
    {
        if (!ReferenceEquals(_hover, element))
        {
            _hover = element;
            InvalidateScene();
        }
    }

    /// <summary>
    ///     Gets the part of the diagram that is currently visible.
    /// </summary>
    /// <returns>The visible region in diagram units, or null before the control has been given a size.</returns>
    private PlexusRect? VisibleArea() =>
        Bounds.Width <= 0 || Bounds.Height <= 0
            ? null
            : new PlexusRect(Offset.X, Offset.Y, Bounds.Width / Zoom, Bounds.Height / Zoom);

    /// <summary>
    ///     Returns the scene, rebuilding it when the model changed or the view moved outside the culled area.
    /// </summary>
    /// <remarks>
    ///     Scenes are built for the visible area plus half a screen on each side, so ordinary panning
    ///     and small zooms reuse the existing scene instead of rebuilding it every pointer move.
    /// </remarks>
    /// <returns>The scene, or null when no attached page is shown.</returns>
    private DiagramScene? EnsureScene()
    {
        if (Page is not { IsAttached: true } page)
        {
            _scene = null;
            return null;
        }

        // Reuse the scene while it is current and still covers everything visible
        var visible = VisibleArea();
        var covered = _sceneArea is null || (visible is { } area && _sceneArea.Value.Contains(area));
        if (_scene is not null && !_sceneDirty && ReferenceEquals(_scene.Page, page) && covered)
        {
            return _scene;
        }

        // Build with generous margins; culling keeps huge diagrams cheap to rebuild and paint
        var built = visible is { } v ? v.Inflate(Math.Max(v.Width, v.Height) / 2) : (PlexusRect?)null;
        _scene = SceneBuilder.Build(
            page,
            new SceneOptions
            {
                Theme = DiagramTheme,
                TextMeasurer = _measurer,
                Selection = [.. _selection],
                Hover = _hover,
                ShowGrid = ShowGrid,
                Bounds = built,
            });
        _sceneArea = built;
        _sceneDirty = false;
        SceneBuildCount++;
        return _scene;
    }

    /// <summary>
    ///     Reacts to a new page: drops everything tied to the old one and listens to the new one.
    /// </summary>
    private void OnPageChanged()
    {
        CancelGesture();
        CancelEdit();
        _hover = null;
        ResetToolTip();
        SetSelection([]);
        UpdateSubscription();
        InvalidateScene();
        ApplyPendingView();
    }

    /// <summary>
    ///     Makes the model subscription match the page and attachment state.
    /// </summary>
    private void UpdateSubscription()
    {
        var wanted = _isAttached ? Page?.Diagram : null;
        if (ReferenceEquals(wanted, _subscribedDiagram))
        {
            return;
        }

        if (_subscribedDiagram is not null)
        {
            _subscribedDiagram.Changed -= OnDiagramChanged;
        }

        if (_subscribedPages is not null)
        {
            _subscribedPages.CollectionChanged -= OnPagesChanged;
        }

        _subscribedDiagram = wanted;
        _subscribedPages = wanted?.Pages;
        if (_subscribedDiagram is not null)
        {
            _subscribedDiagram.Changed += OnDiagramChanged;
            _subscribedPages!.CollectionChanged += OnPagesChanged;
        }
    }

    /// <summary>
    ///     Follows the removal of the shown page: switches to the nearest remaining page (the one now at the same index, clamped to the last), or to none when no page is left.
    /// </summary>
    /// <remarks>
    ///     Only removals matter: when a page is added or comes back through undo or redo the editor keeps showing what it shows, so it never jumps unexpectedly.
    ///     The page is set with <c>SetCurrentValue</c>, so a binding on <see cref="Page"/> (a two-way binding is written back) stays in place.
    /// </remarks>
    /// <param name="sender">The page collection.</param>
    /// <param name="e">What changed.</param>
    private void OnPagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Remove
            || Page is not { } shown
            || e.OldItems is null
            || !e.OldItems.Contains(shown))
        {
            return;
        }

        var pages = shown.Diagram.Pages;
        var index = Math.Min(Math.Max(e.OldStartingIndex, 0), pages.Count - 1);
        SetCurrentValue(PageProperty, index >= 0 ? pages[index] : null);
    }

    /// <summary>
    ///     Handles any change to the diagram: schedules a rebuild and forgets elements that no longer exist.
    /// </summary>
    /// <param name="sender">The diagram.</param>
    /// <param name="e">The change description.</param>
    private void OnDiagramChanged(object? sender, DiagramChangedEventArgs e)
    {
        // Rebuilding is deferred to paint time, which is what coalesces bursts of changes into one rebuild
        InvalidateScene();
        if (e.Kind is DiagramChangeKind.ElementRemoved or DiagramChangeKind.PageRemoved)
        {
            PruneSelection();
            if (_hover is { } hover && !ReferenceEquals(hover.Page, Page))
            {
                _hover = null;
            }

            if ((_renameNode is { } node && !ReferenceEquals(node.Page, Page))
                || (_editAnnotation is { } annotation && !ReferenceEquals(annotation.Page, Page)))
            {
                CancelEdit();
            }
        }

        // A tooltip about an element or port that is gone must not stay up
        if (e.Kind is DiagramChangeKind.ElementRemoved or DiagramChangeKind.PageRemoved or DiagramChangeKind.ElementChanged)
        {
            ResetToolTipIfStale();
        }
    }

    /// <summary>
    ///     Coerces a requested zoom into the supported range.
    /// </summary>
    /// <param name="sender">The editor.</param>
    /// <param name="value">The requested zoom.</param>
    /// <returns>The clamped zoom; non-finite values fall back to 1.</returns>
    private static double CoerceZoom(AvaloniaObject sender, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, MinZoom, MaxZoom) : 1;

    /// <summary>
    ///     Computes the bounds of everything that has a box on a page (wires follow their nodes).
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The union of the element boxes, or null for an empty page.</returns>
    private PlexusRect? ContentBounds(PlexusPage page)
    {
        PlexusRect? union = null;
        foreach (var element in page.Elements)
        {
            if (element is Wire || !element.IsVisible)
            {
                continue;
            }

            // Nodes also count their name, summary and port labels; everything else is just its box
            var box = element is Node node
                ? NodeLayout.Compute(node, EffectiveTheme, _measurer).Bounds
                : element.GetBounds();
            union = union is { } current ? current.Union(box) : box;
        }

        return union;
    }
}
