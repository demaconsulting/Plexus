namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Avalonia controls for Plexus: an interactive <see cref="DiagramEditor"/> and a drag-and-drop <see cref="NodePalette"/>.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="DiagramEditor"/> shows one <c>DemaConsulting.Plexus.DiagramPage</c> (<see cref="DiagramEditor.Page"/>) with the diagram's
///         own theme, and handles selecting, moving, resizing, connecting, rewiring, renaming, copy and paste
///         (<see cref="IDiagramClipboard"/>; the system clipboard by default), undo and redo (Ctrl+Z and Ctrl+Y), zoom and pan.
///         It owns only view state (<see cref="DiagramEditor.Zoom"/>, <see cref="DiagramEditor.Offset"/>, <see cref="DiagramEditor.Selection"/>);
///         every edit goes through the Core model API, so the editor and your code stay in sync through <c>Diagram.Changed</c>.
///         The full list of built-in mouse and keyboard gestures is on the <see cref="DiagramEditor"/> page.
///         <see cref="NodePalette"/> lists the node types of the editor's diagram (or of any <c>NodeTypeRegistry</c> given as
///         <see cref="NodePalette.Types"/>), draws each exactly as it looks on the canvas, and lets the user drag one onto the editor.
///     </para>
///     <para>
///         The editor opens no dialogs or menus: it raises events and you decide what happens. The main extension points are the events
///         (<see cref="DiagramEditor.NodeDoubleClicked"/>, <see cref="DiagramEditor.DiagramContextRequested"/>,
///         <see cref="DiagramEditor.SelectionChanged"/>, <see cref="DiagramEditor.PasteFailed"/>, <see cref="DiagramEditor.ClipboardFailed"/>,
///         <see cref="NodePalette.NodeTypeActivated"/>), the appearance settings of the diagram itself (<see cref="DiagramEditor.DiagramTheme"/>,
///         <c>Diagram.WireStyler</c>, <c>NodeType.Visual</c>), a custom <see cref="IDiagramClipboard"/>, and <see cref="PlexusIcons"/> to show node
///         icons in your own controls. Use these controls on the UI thread.
///     </para>
///     <para>
///         Name collisions: Plexus has its own geometry types, and several short names are also Avalonia names: <c>Point</c>, <c>Size</c>,
///         <c>Rect</c> and <c>Color</c> (Avalonia.Media.Color). In a file that imports both <c>Avalonia</c> and <c>DemaConsulting.Plexus.Geometry</c>, alias the
///         Plexus ones (<c>using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;</c>) or qualify them. Coordinates differ too: <see cref="DiagramEditor.Offset"/>,
///         <see cref="DiagramEditor.ScreenToDiagram"/> and <see cref="DiagramContextRequestedEventArgs.Position"/> use Avalonia types, while
///         <c>DiagramEditor.PasteAsync(Point)</c> and everything in the Core model take the Plexus <c>Point</c>. Convert with the
///         <see cref="PlexusAvaloniaExtensions"/> methods <c>ToAvalonia()</c> and <c>ToPlexus()</c>. Since the Core page type is named <c>DiagramPage</c>,
///         it does not collide with <c>Avalonia.Controls.Page</c>. In XAML, reference Core types with
///         <c>xmlns:core="clr-namespace:DemaConsulting.Plexus;assembly=DemaConsulting.Plexus"</c>.
///     </para>
/// </remarks>
/// <example>
///     <para>
///         A complete first application: a window with a node palette next to an editor, page tabs, File Open/Save, Edit Undo/Redo/Copy/Paste,
///         Zoom to fit, a properties dialog on double-click, a context menu on right-click, and a status bar for failures. The code was compiled
///         against Avalonia 12 with <c>ImplicitUsings</c> and <c>Nullable</c> enabled. First the XAML for MainWindow.axaml. NOTE: this first block is XAML,
///         not C#, even though the generated page fences every example as csharp:
///     </para>
///     <code><![CDATA[
///     <!-- XAML (MainWindow.axaml), not C# -->
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
///         Open and Save use the window's storage provider and <c>Diagram.ToJson</c> / <c>Diagram.FromJson</c>; rules, stylers and the theme are not part of the
///         JSON, so configure a loaded diagram again. Menu handlers that touch the clipboard catch platform failures (<c>CopyAsync</c> and <c>PasteAsync</c> let
///         exceptions from the operating system clipboard reach you; a shortcut such as Ctrl+C reports them through <c>ClipboardFailed</c> instead):
///     </para>
///     <code><![CDATA[
///     using Avalonia;
///     using Avalonia.Controls;
///     using Avalonia.Controls.Primitives.PopupPositioning;
///     using Avalonia.Interactivity;
///     using Avalonia.Platform.Storage;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///
///     namespace MyApp;
///
///     public partial class MainWindow : Window
///     {
///         private static readonly FilePickerFileType JsonFiles = new("Diagram JSON") { Patterns = ["*.json"] };
///         private readonly NodeTypeRegistry _types = MyTypes.Create();
///         private Diagram _diagram;
///
///         public MainWindow()
///         {
///             InitializeComponent();                      // generated from the XAML: Editor, Tabs, Status, UndoItem, RedoItem exist now
///             _diagram = new Diagram(_types);
///             _diagram.Pages.Add("Main");
///             ShowDiagram(_diagram);
///
///             Editor.SelectionChanged += (_, _) => Status.Text = $"{Editor.Selection.Count} selected";
///             Editor.NodeDoubleClicked += async (_, e) => await new PropertiesWindow(e.Node).ShowDialog(this);
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
///         private void OnMoveToPage2(object? sender, RoutedEventArgs e)
///         {
///             if (_diagram.Pages.Count < 2 || Editor.Selection.Count == 0)
///             {
///                 Status.Text = "Select elements and add a second page first";
///                 return;
///             }
///
///             // One undo entry. Copy the selection first: moved elements leave the editor's selection while the move runs.
///             // Wires between moved nodes travel with them; wires to nodes that stay behind are removed.
///             _diagram.MoveToPage(Editor.Selection.ToArray(), _diagram.Pages[1]);
///             Status.Text = "Moved to page 2";
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
///                 ShowDiagram(Diagram.FromJson(await reader.ReadToEndAsync(), _types));   // the registry must know every node type in the file
///                 Status.Text = $"Opened {files[0].Name}";
///             }
///             catch (Exception ex) when (ex is SnapshotException or System.Text.Json.JsonException)
///             {
///                 Status.Text = $"Cannot open: {ex.Message}";
///             }
///         }
///     }
///     ]]></code>
///     <para>
///         A properties dialog built from <c>NodeType.Properties</c>: the value kind of each definition picks the editor, and
///         <see cref="PropertyBag.TrySetAll"/> reads the text of the boxes, validates everything and applies it as one undo entry, or
///         applies nothing and says why for each property, which the dialog shows instead of closing:
///     </para>
///     <code><![CDATA[
///     using System.Globalization;
///     using Avalonia;
///     using Avalonia.Controls;
///     using Avalonia.Interactivity;
///     using Avalonia.Layout;
///     using Avalonia.Media;
///     using DemaConsulting.Plexus;
///
///     namespace MyApp;
///
///     public sealed class PropertiesWindow : Window
///     {
///         private readonly Node _node;
///         private readonly Dictionary<string, Control> _editors = [];
///         private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
///
///         public PropertiesWindow(Node node)
///         {
///             _node = node;
///             Title = $"Properties - {node.Name}";
///             Width = 340;
///             SizeToContent = SizeToContent.Height;
///             WindowStartupLocation = WindowStartupLocation.CenterOwner;
///
///             var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), Margin = new Thickness(12) };
///             var row = 0;
///             foreach (var definition in node.Type.Properties)
///             {
///                 grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
///                 var label = new TextBlock { Text = definition.DisplayName, VerticalAlignment = VerticalAlignment.Center };
///                 Grid.SetRow(label, row);
///                 var current = node.Properties[definition.Name];
///
///                 // The kind of the value decides the control; a Color property would use a ColorPicker the same way
///                 Control editor = definition.ValueKind switch
///                 {
///                     PropertyValueKind.Boolean => new CheckBox { IsChecked = (bool)current },
///                     PropertyValueKind.Choice => new ComboBox { ItemsSource = definition.Choices, SelectedItem = current },
///                     _ => new TextBox { Text = PropertyDefinition.FormatForEdit(current) },
///                 };
///                 Grid.SetRow(editor, row);
///                 Grid.SetColumn(editor, 1);
///                 grid.Children.Add(label);
///                 grid.Children.Add(editor);
///                 _editors[definition.Name] = editor;
///                 row++;
///             }
///
///             var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 70 };
///             ok.Click += OnOk;
///             var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 70 };
///             cancel.Click += (_, _) => Close();
///             var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
///             buttons.Children.Add(ok);
///             buttons.Children.Add(cancel);
///             Content = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 12, 12), Children = { grid, _error, buttons } };
///         }
///
///         private void OnOk(object? sender, RoutedEventArgs e)
///         {
///             // The texts of the boxes go in as they are: TrySetAll parses them against the definitions
///             var values = _node.Type.Properties.ToDictionary(d => d.Name, d => Read(d, _editors[d.Name]));
///             if (_node.Properties.TrySetAll(values, out var errors))      // all or nothing, one undo entry
///             {
///                 Close();
///                 return;
///             }
///
///             _error.Text = string.Join("\n", errors.Select(error => $"{error.Key}: {error.Value}"));
///         }
///
///         private static object? Read(PropertyDefinition definition, Control editor) => editor switch
///         {
///             CheckBox box => box.IsChecked == true,
///             ComboBox combo => combo.SelectedItem ?? definition.DefaultValue,
///             TextBox text => text.Text ?? "",
///             _ => definition.DefaultValue,
///         };
///     }
///     ]]></code>
/// </example>
internal static class NamespaceDoc
{
}
