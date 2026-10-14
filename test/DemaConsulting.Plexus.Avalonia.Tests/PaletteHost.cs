using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;
using PlexusPage = DemaConsulting.Plexus.DiagramPage;

namespace DemaConsulting.Plexus.Avalonia.Tests;

/// <summary>
///     A custom node visual: the standard template plus an unmistakable accent badge, standing in for a host's own look.
/// </summary>
public sealed class BadgeNodeVisual : StandardNodeVisual
{
    /// <inheritdoc />
    public override void Build(NodeVisualContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Standard box, title, ports and labels first, then the accent drawn over the box's top-right corner
        base.Build(context);
        var box = context.Layout.Box;
        context.Add(new RectPrimitive(new Geometry.Rect(box.Right - 22, box.Top + 5, 16, 16), PlexusColor.Parse("#E0A800"), PlexusColor.Parse("#7A5C00"), 1, 8));
    }
}

/// <summary>
///     Hosts a <see cref="NodePalette"/> beside a <see cref="DiagramEditor"/> in a headless window, with varied node types.
/// </summary>
/// <remarks>
///     The registry holds <c>test.Source</c> (category Sources, f32 output, summary), <c>test.Mixer</c>
///     (category Processing, display name "Signal Mixer", icon, two inputs, one output, summary),
///     <c>test.Badge</c> (category Processing, a custom <see cref="INodeVisual"/>), <c>test.Sink</c>
///     (category Sinks, i32 input) and <c>test.Gain</c> (no category, summary). The palette fills the
///     left 340 pixels of the window and the editor the rest, so editor points are window points minus 340.
/// </remarks>
internal sealed class PaletteHost : IDisposable
{
    /// <summary>
    ///     The width of the palette column in device-independent pixels.
    /// </summary>
    internal const double PaletteWidth = 340;

    /// <summary>
    ///     A dark-disc icon in SVG form.
    /// </summary>
    private const string DiscSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='30' viewBox='0 0 30 30'>" +
        "<circle cx='15' cy='15' r='13' fill='#2b2b2b'/>" +
        "<path d='M6 15 H24 M19 10 L24 15 L19 20' stroke='#ffc800' stroke-width='2' fill='none'/></svg>";

    /// <summary>
    ///     Initializes a new instance of the <see cref="PaletteHost"/> class.
    /// </summary>
    /// <param name="theme">The theme for the diagram, editor and palette, or null for the classic theme.</param>
    /// <param name="zoom">The zoom for the editor and the palette.</param>
    /// <param name="followEditor">Whether the palette is given only the editor (and so follows its diagram and theme) instead of the registry and theme.</param>
    /// <param name="paletteWidth">The width of the palette column in device-independent pixels.</param>
    public PaletteHost(DiagramTheme? theme = null, double zoom = 1, bool followEditor = false, double paletteWidth = PaletteWidth)
    {
        Types = CreateTypes();
        Diagram = new Diagram(Types) { Theme = theme ?? DiagramTheme.Classic };
        Page = Diagram.Pages.Add("Main");
        Editor = new DiagramEditor { Page = Page, Zoom = zoom, ShowGrid = false, DiagramTheme = theme };
        Palette = followEditor
            ? new NodePalette { Editor = Editor, Zoom = zoom }
            : new NodePalette { Types = Types, Zoom = zoom, DiagramTheme = theme };
        Columns = new ColumnDefinitions($"{paletteWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)},*");
        var grid = new Grid { ColumnDefinitions = Columns };
        Grid.SetColumn(Editor, 1);
        grid.Children.Add(Palette);
        grid.Children.Add(Editor);
        Window = new Window { Width = 1240, Height = 900, Content = grid };
        Window.Show();
        Flush();
    }

    /// <summary>
    ///     Gets the column definitions of the grid that holds the palette (first column) and the editor.
    /// </summary>
    public ColumnDefinitions Columns { get; }

    /// <summary>
    ///     Gets the registry shown by the palette and used by the diagram.
    /// </summary>
    public NodeTypeRegistry Types { get; }

    /// <summary>
    ///     Gets the diagram.
    /// </summary>
    public Diagram Diagram { get; }

    /// <summary>
    ///     Gets the page the editor shows.
    /// </summary>
    public PlexusPage Page { get; }

    /// <summary>
    ///     Gets the editor.
    /// </summary>
    public DiagramEditor Editor { get; }

    /// <summary>
    ///     Gets the palette under test.
    /// </summary>
    public NodePalette Palette { get; }

    /// <summary>
    ///     Gets the window.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Creates the registry the host uses.
    /// </summary>
    /// <returns>A registry with varied node types.</returns>
    public static NodeTypeRegistry CreateTypes()
    {
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", PlexusColor.Parse("#FF8C00")));
        types.PortTypes.Add(new PortType("i32", PlexusColor.Parse("#1E90FF")));
        types.Add(new NodeType("test.Source")
        {
            Title = "SOURCE",
            Category = "Sources",
            Outputs = { new PortDefinition("value", "f32") },
            Properties = { new PropertyDefinition("Format", "F32") { ShowInSummary = true } },
        });
        types.Add(new NodeType("test.Mixer")
        {
            Title = "MIX",
            DisplayName = "Signal Mixer",
            Category = "Processing",
            Icon = IconSource.FromSvg(DiscSvg),
            Inputs = { new PortDefinition("left", "f32"), new PortDefinition("right", "f32") },
            Outputs = { new PortDefinition("mix", "f32") },
            Properties = { new PropertyDefinition("Gain", 1.5) { ShowInSummary = true } },
        });
        types.Add(new NodeType("test.Badge")
        {
            Title = "BADGE",
            Category = "Processing",
            Visual = new BadgeNodeVisual(),
            Inputs = { new PortDefinition("in", "i32") },
        });
        types.Add(new NodeType("test.Sink")
        {
            Title = "SINK",
            Category = "Sinks",
            Inputs = { new PortDefinition("value", "i32") },
        });
        types.Add(new NodeType("test.Gain")
        {
            Title = "GAIN",
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            Properties = { new PropertyDefinition("Factor", 2) { ShowInSummary = true } },
        });
        return types;
    }

    /// <summary>
    ///     Sets the width of the palette column, as dragging a splitter would, and lets the layout settle.
    /// </summary>
    /// <param name="width">The new width in device-independent pixels.</param>
    public void SetPaletteWidth(double width)
    {
        Columns[0].Width = new GridLength(width);
        Flush();
    }

    /// <summary>
    ///     Lets pending layout, input and rendering work finish.
    /// </summary>
    public void Flush()
    {
        Window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>
    ///     Gets the palette item of a node type.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <returns>The item.</returns>
    public NodePaletteItem ItemFor(string key) => Palette.Items.Single(item => item.NodeType.Key == key);

    /// <summary>
    ///     Converts a point of the editor into a window point.
    /// </summary>
    /// <param name="editorPoint">The point relative to the editor.</param>
    /// <returns>The point relative to the window.</returns>
    public Point ToWindow(Point editorPoint) => Editor.TranslatePoint(editorPoint, Window)!.Value;

    /// <summary>
    ///     Gets the center of a palette item in window coordinates.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The window point.</returns>
    public Point CenterOf(NodePaletteItem item) => item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), Window)!.Value;

    /// <summary>
    ///     Sends a drag event of a node-type payload over the editor.
    /// </summary>
    /// <param name="type">The raw drag event kind.</param>
    /// <param name="key">The node type key carried by the payload.</param>
    /// <param name="editorPoint">The pointer position relative to the editor.</param>
    public void DragEvent(RawDragEventType type, string key, Point editorPoint)
    {
        using var transfer = DiagramEditor.CreateNodeTypeDataTransfer(key);
        Window.DragDrop(ToWindow(editorPoint), type, transfer, DragDropEffects.Copy, RawInputModifiers.None);
        Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();
        GC.SuppressFinalize(this);
    }
}
