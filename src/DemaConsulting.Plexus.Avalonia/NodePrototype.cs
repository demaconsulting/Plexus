using Avalonia.Media;
using DemaConsulting.Plexus.Scene;
using PlexusPoint = DemaConsulting.Plexus.Geometry.Point;
using PlexusRect = DemaConsulting.Plexus.Geometry.Rect;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     The rendering of one node type exactly as a freshly created node of that type looks on the canvas.
/// </summary>
/// <remarks>
///     A prototype is built by creating a real node on a scratch page of a private
///     <see cref="Diagram"/> and running the ordinary <see cref="SceneBuilder"/> over it, so the box,
///     header, icon, ports, port labels, summary lines and any custom <see cref="INodeVisual"/> come
///     from the very code the editor uses. The palette and the editor's drop ghost paint the result
///     with the editor's <see cref="ScenePainter"/>. The node sits at the origin; the
///     palette and the ghost move it with a transform. UI thread only.
/// </remarks>
internal sealed class NodePrototype
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePrototype"/> class.
    /// </summary>
    /// <param name="type">The node type.</param>
    /// <param name="scene">The scene of the scratch page.</param>
    /// <param name="box">The node box.</param>
    /// <param name="visualBounds">The deterministic extent of everything drawn for the node.</param>
    private NodePrototype(NodeType type, DiagramScene scene, PlexusRect box, PlexusRect visualBounds)
    {
        Type = type;
        Scene = scene;
        Box = box;
        VisualBounds = visualBounds;
    }

    /// <summary>
    ///     Gets the node type the prototype shows.
    /// </summary>
    public NodeType Type { get; }

    /// <summary>
    ///     Gets the scene holding the single prototype node.
    /// </summary>
    public DiagramScene Scene { get; }

    /// <summary>
    ///     Gets the node box (the filled rectangle) in scene coordinates; its size is what a dropped node starts with.
    /// </summary>
    public PlexusRect Box { get; }

    /// <summary>
    ///     Gets the extent of everything drawn for the node (name, box, ports, labels, summary) in scene coordinates.
    /// </summary>
    public PlexusRect VisualBounds { get; }

    /// <summary>
    ///     Gets the largest horizontal distance from the node box's center to the edge of the visual extent, on either side.
    /// </summary>
    /// <remarks>
    ///     The palette uses the maximum over all its prototypes to size one common column in which every
    ///     node box is centered, so input labels can extend to the left and output labels to the right
    ///     without ever shifting a box.
    /// </remarks>
    public double HorizontalReach => Math.Max(Box.Center.X - VisualBounds.Left, VisualBounds.Right - Box.Center.X);

    /// <summary>
    ///     Creates the prototype of a node type.
    /// </summary>
    /// <remarks>
    ///     The node is named after the type's <see cref="NodeType.DisplayName"/>, so the name label
    ///     above the box tells the user which type it is. The scratch diagram uses
    ///     <paramref name="sizingTheme"/> for the default node size (as
    ///     <see cref="DiagramPage.AddNode(NodeType, PlexusPoint, string?, string?)"/> does for a real node) and the scene is drawn
    ///     with <paramref name="sceneTheme"/> (the editor's theme override, when it has one).
    /// </remarks>
    /// <param name="registry">A registry that contains <paramref name="type"/>.</param>
    /// <param name="type">The node type.</param>
    /// <param name="sizingTheme">The theme whose metrics size the node box.</param>
    /// <param name="sceneTheme">The theme the scene is drawn with.</param>
    /// <param name="measurer">The text measurer, normally the editor's Avalonia measurer.</param>
    /// <returns>The prototype.</returns>
    public static NodePrototype Create(NodeTypeRegistry registry, NodeType type, DiagramTheme sizingTheme, DiagramTheme sceneTheme, ITextMeasurer measurer)
    {
        // A private one-page diagram keeps the host's document untouched and needs no unique-name bookkeeping
        var diagram = new Diagram(registry) { Theme = sizingTheme };
        var page = diagram.Pages.Add("Prototype");
        var name = type.DisplayName.Trim();
        var node = page.AddNode(type, new PlexusPoint(0, 0), name.Length == 0 ? type.Key : name);

        // Same builder, theme and measurer as the canvas; the grid is decoration and is left out
        var scene = SceneBuilder.Build(page, new SceneOptions { Theme = sceneTheme, TextMeasurer = measurer, ShowGrid = false });
        return new NodePrototype(type, scene, node.Bounds, NodeGeometry.VisualBounds(node, sceneTheme));
    }

    /// <summary>
    ///     Paints the prototype with the shared painter.
    /// </summary>
    /// <param name="context">The drawing context, already carrying the transform that places the prototype.</param>
    /// <param name="painter">The painter whose caches (brushes, pens, text, icons) are reused.</param>
    /// <param name="zoom">The zoom factor in effect, passed on to the painter.</param>
    public void Paint(DrawingContext context, ScenePainter painter, double zoom)
    {
        // Nothing is culled: the visible region is the whole scene plus a little slack
        painter.Paint(context, Scene, Scene.Bounds.Inflate(2), zoom);
    }
}
