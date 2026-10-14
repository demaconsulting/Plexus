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
///         <see cref="NodePalette"/> lists the node types of the editor's diagram (or of any <c>NodeTypeRegistry</c> given as
///         <see cref="NodePalette.Types"/>), draws each exactly as it looks on the canvas, and lets the user drag one onto the editor.
///     </para>
///     <para>
///         <b>Where to read next.</b> The <see cref="DiagramEditor"/> page lists every built-in mouse and keyboard gesture and holds a complete first
///         application (menus, page tabs, open and save, clipboard, grouping, a context menu and a properties dialog). The
///         <c>DemaConsulting.Plexus</c> namespace (package <c>DemaConsulting.Plexus</c>) describes the model the editor shows: node types,
///         diagrams, undo, JSON and <c>DiagramOptions</c>. A properties dialog for a double-clicked node comes ready-made in the package
///         <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c>: <c>NodePropertiesDialog.ShowAsync(window, e.Node)</c>, see the example below.
///         To render a page to a PNG without any UI, use the package <c>DemaConsulting.Plexus.Rendering</c>.
///     </para>
///     <para>
///         The editor opens no dialogs or menus: it raises events and you decide what happens. The main extension points are the events
///         (<see cref="DiagramEditor.NodeDoubleClicked"/>, <see cref="DiagramEditor.DiagramContextRequested"/>,
///         <see cref="DiagramEditor.SelectionChanged"/>, <see cref="DiagramEditor.PasteFailed"/>, <see cref="DiagramEditor.ClipboardFailed"/>,
///         <see cref="NodePalette.NodeTypeActivated"/>), the appearance settings of the diagram itself (<see cref="DiagramEditor.DiagramTheme"/>,
///         <c>Diagram.WireStyler</c>, <c>NodeType.Visual</c>), a custom <see cref="IDiagramClipboard"/>, and <see cref="PlexusIcons"/> to show node
///         icons in your own controls. Use these controls on the UI thread. The application must include a full Avalonia theme such as
///         <c>&lt;FluentTheme /&gt;</c> in App.axaml (the palette's search box, menus and dialogs use its control styles).
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
///         The smallest useful window: a node palette next to an editor, and the ready-made properties dialog on double-click. App.axaml (a full theme is required):
///     </para>
///     <code lang="xml"><![CDATA[
///     <Application xmlns="https://github.com/avaloniaui"
///                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///                  x:Class="MyApp.App">
///         <Application.Styles>
///             <FluentTheme />
///         </Application.Styles>
///     </Application>
///     ]]></code>
///     <para>
///         MainWindow.axaml; the <c>plexus</c> prefix covers <c>DiagramEditor</c> and <c>NodePalette</c>, the palette follows the editor it is bound to,
///         and the splitter resizes the palette column (so the palette has no <c>Width</c> of its own):
///     </para>
///     <code lang="xml"><![CDATA[
///     <Window xmlns="https://github.com/avaloniaui"
///             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///             xmlns:plexus="https://github.com/demaconsulting/plexus"
///             x:Class="MyApp.MainWindow"
///             Title="My diagram" Width="1000" Height="650">
///         <Grid ColumnDefinitions="220,5,*">
///             <plexus:NodePalette Grid.Column="0" Editor="{Binding #Editor}" />
///             <GridSplitter Grid.Column="1" ResizeDirection="Columns" />
///             <plexus:DiagramEditor Grid.Column="2" x:Name="Editor" />
///         </Grid>
///     </Window>
///     ]]></code>
///     <para>
///         MainWindow.axaml.cs. The node types are your domain (here two tiny ones); the palette lists them and the user drags them onto the editor.
///         Double-clicking a node opens <c>NodePropertiesDialog</c> (package <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c>), which edits the name,
///         properties and host data and applies them as one undo entry:
///     </para>
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///     using DemaConsulting.Plexus.Geometry;
///
///     namespace MyApp;
///
///     public partial class MainWindow : Window
///     {
///         public MainWindow()
///         {
///             InitializeComponent();                              // generated from the XAML: Editor exists now
///
///             var types = new NodeTypeRegistry();
///             types.PortTypes.Add(new PortType("number", Color.Parse("#FF8C00")));   // port types are strings you define
///             types.Add(new NodeType("demo.Source")
///             {
///                 Outputs = { new PortDefinition("out", "number") },
///                 Properties = { new PropertyDefinition("Value", 1.0) { ShowInSummary = true } },
///             });
///             types.Add(new NodeType("demo.Sink") { Inputs = { new PortDefinition("in", "number") } });
///
///             var diagram = new Diagram(types);
///             Editor.Page = diagram.Pages.Add("Main");            // the palette follows the editor's diagram
///
///             Editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
///         }
///     }
///     ]]></code>
///     <para>
///         The <see cref="DiagramEditor"/> page extends this to a complete application: page tabs, open and save, undo and redo, copy and paste,
///         grouping, moving a selection to another page and a context menu.
///     </para>
/// </example>
internal static class NamespaceDoc
{
}
