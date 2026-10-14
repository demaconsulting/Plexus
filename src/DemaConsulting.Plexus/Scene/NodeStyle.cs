namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The appearance of one node: any member left null falls back to the <see cref="DiagramTheme"/> (or to the node type's own look).
/// </summary>
/// <remarks>
///     <para>
///         Hosts return a style from <see cref="Diagram.NodeStyler"/> to light up, outline, tint or fade
///         individual nodes. Because the styler is evaluated for every visible node each time a scene
///         is built, the style follows whatever the node holds: its properties, its connections or
///         host data. <see cref="StandardNodeVisual"/> honors every member, and custom visuals can read
///         the resolved style from <see cref="NodeVisualContext.Style"/>.
///     </para>
///     <para>
///         A style is immutable and validated when created: <see cref="BorderThickness"/> must be a
///         finite number greater than zero and <see cref="Opacity"/> a finite number from 0 to 1.
///     </para>
///     <para>
///         <see cref="IconTint"/> recolors the icon as a silhouette: every pixel the icon paints takes
///         the tint color, and the icon's own alpha (times the tint's alpha) decides how much of it shows,
///         so the icon's shape is kept while its original colors are lost.
///     </para>
///     <para>
///         Derive variants with <c>style with { Opacity = 0.5 }</c>; a null member means "use the theme". Combine two styles with <see cref="WithOverrides"/>.
///     </para>
/// </remarks>
/// <example>
///     Styles for individual nodes.
///     <code>
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var active = new NodeStyle { Fill = Color.Parse("#7A2E1E"), Glow = Color.Parse("#FF6A3D") };   // red-hot node with a halo
///     var faded = new NodeStyle { Opacity = 0.4 };                                                    // everything drawn at 40 percent
///     var fadedActive = active with { Opacity = 0.4 };                                                // derive a variant (a record)
///     var merged = active.WithOverrides(faded);                                                       // both effects
///     </code>
/// </example>
public sealed record NodeStyle
{
    /// <summary>
    ///     A style that sets nothing, so every member comes from the theme.
    /// </summary>
    public static NodeStyle None { get; } = new();

    /// <summary>
    ///     The border thickness, or null to use the theme.
    /// </summary>
    private readonly double? _borderThickness;

    /// <summary>
    ///     The opacity, or null for fully opaque.
    /// </summary>
    private readonly double? _opacity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeStyle"/> class as a style that sets nothing, so every member comes from the theme.
    /// </summary>
    public NodeStyle()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the box fill color; null (the default) uses <see cref="DiagramTheme.NodeFill"/>.
    /// </summary>
    public Color? Fill { get; init; }

    /// <summary>
    ///     Gets or initializes the box border color; null (the default) uses <see cref="DiagramTheme.NodeBorder"/>.
    /// </summary>
    public Color? Border { get; init; }

    /// <summary>
    ///     Gets or initializes the box border thickness; null (the default) uses <see cref="DiagramTheme.NodeBorderThickness"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero, a negative number or a non-finite number.</exception>
    public double? BorderThickness
    {
        get => _borderThickness;
        init
        {
            if (value is { } thickness && (!double.IsFinite(thickness) || thickness <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The border thickness must be greater than zero.");
            }

            _borderThickness = value;
        }
    }

    /// <summary>
    ///     Gets or initializes the color of the title text in the header; null (the default) uses <see cref="DiagramTheme.NodeHeaderText"/>.
    /// </summary>
    public Color? HeaderText { get; init; }

    /// <summary>
    ///     Gets or initializes the color the node icon is recolored to; null (the default) draws the icon as authored.
    /// </summary>
    /// <remarks>
    ///     The icon becomes a silhouette in this color; see the type remarks.
    /// </remarks>
    public Color? IconTint { get; init; }

    /// <summary>
    ///     Gets or initializes the color of a soft halo drawn around the box; null (the default) means no halo.
    /// </summary>
    /// <remarks>
    ///     The halo is a few concentric translucent rings that fade outward, so it looks the same in
    ///     every painter. The color's own alpha scales the halo's strength.
    /// </remarks>
    public Color? Glow { get; init; }

    /// <summary>
    ///     Gets or initializes the opacity of everything the standard visual draws for the node (box, texts, ports, icon and halo), from 0 (invisible) to 1; null (the default) means fully opaque.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside the range 0 to 1 or to a non-finite number.</exception>
    public double? Opacity
    {
        get => _opacity;
        init
        {
            if (value is { } opacity && (!double.IsFinite(opacity) || opacity < 0 || opacity > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The opacity must be between 0 and 1.");
            }

            _opacity = value;
        }
    }

    /// <summary>
    ///     Gets a style whose set members come from <paramref name="overrides"/> and whose other members come from this style.
    /// </summary>
    /// <param name="overrides">The style that wins where it has a value; null leaves this style unchanged.</param>
    /// <returns>The combined style.</returns>
    public NodeStyle WithOverrides(NodeStyle? overrides) =>
        overrides is null
            ? this
            : new NodeStyle
            {
                Fill = overrides.Fill ?? Fill,
                Border = overrides.Border ?? Border,
                BorderThickness = overrides.BorderThickness ?? BorderThickness,
                HeaderText = overrides.HeaderText ?? HeaderText,
                IconTint = overrides.IconTint ?? IconTint,
                Glow = overrides.Glow ?? Glow,
                Opacity = overrides.Opacity ?? Opacity,
            };
}
