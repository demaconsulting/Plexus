namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Everything an <see cref="INodeVisual"/> needs to draw one node, and the sink it adds primitives to.
/// </summary>
/// <remarks>
///     A context is created by <see cref="SceneBuilder"/> for each visible node and is only valid
///     during the <see cref="INodeVisual.Build"/> call.
/// </remarks>
public sealed class NodeVisualContext
{
    /// <summary>
    ///     The primitives added so far, in drawing order.
    /// </summary>
    private readonly List<ScenePrimitive> _primitives;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeVisualContext"/> class.
    /// </summary>
    /// <param name="node">The node being drawn.</param>
    /// <param name="layout">The node's computed layout.</param>
    /// <param name="theme">The theme in effect.</param>
    /// <param name="measurer">The text measurer in effect.</param>
    /// <param name="isSelected">Whether the node is selected.</param>
    /// <param name="isHovered">Whether the node is hovered.</param>
    /// <param name="style">The style the diagram's node styler returned, or null for none.</param>
    /// <param name="sink">The list that receives added primitives.</param>
    internal NodeVisualContext(
        Node node,
        NodeLayout layout,
        DiagramTheme theme,
        ITextMeasurer measurer,
        bool isSelected,
        bool isHovered,
        NodeStyle? style,
        List<ScenePrimitive> sink)
    {
        Node = node;
        Layout = layout;
        Theme = theme;
        TextMeasurer = measurer;
        IsSelected = isSelected;
        IsHovered = isHovered;
        Style = style ?? NodeStyle.None;
        _primitives = sink;
    }

    /// <summary>
    ///     Gets the node being drawn.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    ///     Gets the node's computed layout.
    /// </summary>
    public NodeLayout Layout { get; }

    /// <summary>
    ///     Gets the theme in effect.
    /// </summary>
    public DiagramTheme Theme { get; }

    /// <summary>
    ///     Gets the text measurer in effect, for visuals that lay out their own text.
    /// </summary>
    public ITextMeasurer TextMeasurer { get; }

    /// <summary>
    ///     Gets a value indicating whether the node is selected; the selection outline is added by the builder, not the visual.
    /// </summary>
    public bool IsSelected { get; }

    /// <summary>
    ///     Gets a value indicating whether the node is hovered.
    /// </summary>
    public bool IsHovered { get; }

    /// <summary>
    ///     Gets the style the diagram's <see cref="Diagram.NodeStyler"/> returned for the node; <see cref="NodeStyle.None"/> when there is none.
    /// </summary>
    /// <remarks>
    ///     <see cref="StandardNodeVisual"/> honors every member. A custom visual that draws its own
    ///     picture should honor the members it can; <see cref="Fill"/>, <see cref="Border"/>,
    ///     <see cref="BorderThickness"/>, <see cref="HeaderTextStyle"/> and <see cref="Faded(Color)"/> do the
    ///     fallback to the theme and the opacity for it.
    /// </remarks>
    public NodeStyle Style { get; }

    /// <summary>
    ///     Gets the box fill: the style's, else the theme's, with the style's opacity applied.
    /// </summary>
    public Color Fill => Faded(Style.Fill ?? Theme.NodeFill);

    /// <summary>
    ///     Gets the box border color: the style's, else the theme's, with the style's opacity applied.
    /// </summary>
    public Color Border => Faded(Style.Border ?? Theme.NodeBorder);

    /// <summary>
    ///     Gets the box border thickness: the style's, else the theme's.
    /// </summary>
    public double BorderThickness => Style.BorderThickness ?? Theme.NodeBorderThickness;

    /// <summary>
    ///     Gets the title text style: the theme's header style with the style's header color and opacity applied.
    /// </summary>
    public TextStyle HeaderTextStyle => Faded(Theme.HeaderTextStyle with { Color = Style.HeaderText ?? Theme.NodeHeaderText });

    /// <summary>
    ///     Applies the style's opacity to a color, by scaling its alpha.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The color unchanged when the style has no opacity (or it is 1); otherwise the color with its alpha multiplied by the opacity.</returns>
    public Color Faded(Color color) =>
        Style.Opacity is { } opacity and < 1
            ? color with { A = (byte)Math.Round(color.A * opacity) }
            : color;

    /// <summary>
    ///     Applies the style's opacity to the color of a text style.
    /// </summary>
    /// <param name="style">The text style.</param>
    /// <returns>The style with a faded color.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="style"/> is null.</exception>
    public TextStyle Faded(TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return style with { Color = Faded(style.Color) };
    }

    /// <summary>
    ///     Adds a primitive on top of those added so far.
    /// </summary>
    /// <remarks>
    ///     A primitive without an owning element is attributed to the node.
    /// </remarks>
    /// <param name="primitive">The primitive to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="primitive"/> is null.</exception>
    public void Add(ScenePrimitive primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        primitive.Element ??= Node;
        _primitives.Add(primitive);
    }

    /// <summary>
    ///     Gets the color of a port's data type.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns>The registered port type color, or a neutral gray for unregistered types.</returns>
    public Color PortColor(Port port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return Node.Page?.Diagram.Types.PortTypes.Get(port.TypeKey).Color ?? new Color(0x9A, 0x9A, 0x9A);
    }
}
