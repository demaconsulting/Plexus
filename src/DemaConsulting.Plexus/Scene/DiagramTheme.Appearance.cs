namespace DemaConsulting.Plexus.Scene;

/// <content>
///     Appearance settings: colors, fonts and the sizes of drawn details such as port triangles.
/// </content>
public sealed partial record DiagramTheme
{
    /// <summary>
    ///     Gets or initializes the canvas background color; the default is mid gray <c>#A9A9A9</c>.
    /// </summary>
    public Color CanvasBackground { get; init; } = Color.Parse("#A9A9A9");

    /// <summary>
    ///     Gets or initializes the color of the dots of the background grid; the default is <c>#7A7A7A</c>.
    /// </summary>
    public Color GridDotColor { get; init; } = Color.Parse("#7A7A7A");

    /// <summary>
    ///     Gets or initializes the fill color of node boxes; the default is dark gray <c>#6B6B6B</c>.
    /// </summary>
    public Color NodeFill { get; init; } = Color.Parse("#6B6B6B");

    /// <summary>
    ///     Gets or initializes the border color of node boxes; the default is <c>#4B4B4B</c>.
    /// </summary>
    public Color NodeBorder { get; init; } = Color.Parse("#4B4B4B");

    /// <summary>
    ///     Gets or initializes the border thickness of node boxes; the default is 1.
    /// </summary>
    public double NodeBorderThickness { get; init; } = 1;

    /// <summary>
    ///     Gets or initializes the corner radius of node boxes; the default is 2.
    /// </summary>
    public double NodeCornerRadius { get; init; } = 2;

    /// <summary>
    ///     Gets or initializes the color of the title text in the node header; the default is white.
    /// </summary>
    public Color NodeHeaderText { get; init; } = Color.White;

    /// <summary>
    ///     Gets or initializes the color of the instance name drawn above a node; the default is green <c>#1E7B1E</c>.
    /// </summary>
    public Color NodeNameText { get; init; } = Color.Parse("#1E7B1E");

    /// <summary>
    ///     Gets or initializes the color of the property summary drawn below a node; the default is black.
    /// </summary>
    public Color SummaryText { get; init; } = Color.Black;

    /// <summary>
    ///     Gets or initializes the color of port labels; the default is black.
    /// </summary>
    public Color PortLabelText { get; init; } = Color.Black;

    /// <summary>
    ///     Gets or initializes the color of wires; the default is black.
    /// </summary>
    /// <remarks>
    ///     Every wire uses this color unless the diagram's wire styler overrides it, for example
    ///     <c>WireStylers.ByPortType()</c> colors wires like the port type they carry.
    /// </remarks>
    public Color WireColor { get; init; } = Color.Black;

    /// <summary>
    ///     Gets or initializes the stroke thickness of wires; the default is 2.
    /// </summary>
    public double WireThickness { get; init; } = 2;

    /// <summary>
    ///     Gets or initializes the color of the outline drawn around selected elements; the default is blue <c>#1E90FF</c>.
    /// </summary>
    public Color SelectionColor { get; init; } = Color.Parse("#1E90FF");

    /// <summary>
    ///     Gets or initializes the thickness of the selection outline; the default is 2.
    /// </summary>
    public double SelectionThickness { get; init; } = 2;

    /// <summary>
    ///     Gets or initializes the color of the outline drawn around the hovered element; the default is light blue <c>#80C0FF</c>.
    /// </summary>
    public Color HoverColor { get; init; } = Color.Parse("#80C0FF");

    /// <summary>
    ///     Gets or initializes the default fill of annotations; the default is pale yellow <c>#FFFFC0</c>.
    /// </summary>
    public Color AnnotationFill { get; init; } = Color.Parse("#FFFFC0");

    /// <summary>
    ///     Gets or initializes the border color of annotations; the default is <c>#C8B464</c>.
    /// </summary>
    public Color AnnotationBorder { get; init; } = Color.Parse("#C8B464");

    /// <summary>
    ///     Gets or initializes the text color of annotations; the default is black.
    /// </summary>
    public Color AnnotationText { get; init; } = Color.Black;

    /// <summary>
    ///     Gets or initializes the translucent fill of an expanded group's frame; the default is white at about 19 percent opacity (<c>#30FFFFFF</c>).
    /// </summary>
    public Color GroupFill { get; init; } = Color.Parse("#30FFFFFF");

    /// <summary>
    ///     Gets or initializes the border color of group frames; the default is <c>#505050</c>.
    /// </summary>
    public Color GroupBorder { get; init; } = Color.Parse("#505050");

    /// <summary>
    ///     Gets or initializes the fill of an expanded group's title bar; the default is translucent dark gray (<c>#B0404040</c>).
    /// </summary>
    public Color GroupTitleFill { get; init; } = Color.Parse("#B0404040");

    /// <summary>
    ///     Gets or initializes the color of group titles and the collapse toggle glyph; the default is white.
    /// </summary>
    public Color GroupTitleText { get; init; } = Color.White;

    /// <summary>
    ///     Gets or initializes the preferred font family; the default is "Segoe UI". Painters fall back to a default sans-serif when it is unavailable.
    /// </summary>
    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>
    ///     Gets or initializes the font size of node titles (drawn bold); the default is 13.
    /// </summary>
    public double HeaderFontSize { get; init; } = 13;

    /// <summary>
    ///     Gets or initializes the font size of node instance names; the default is 14.
    /// </summary>
    public double NameFontSize { get; init; } = 14;

    /// <summary>
    ///     Gets or initializes the font size of property summaries; the default is 11.
    /// </summary>
    public double SummaryFontSize { get; init; } = 11;

    /// <summary>
    ///     Gets or initializes the font size of port labels; the default is 12.
    /// </summary>
    public double PortLabelFontSize { get; init; } = 12;

    /// <summary>
    ///     Gets or initializes the font size of group titles (drawn bold); the default is 11.
    /// </summary>
    public double GroupTitleFontSize { get; init; } = 11;

    /// <summary>
    ///     Gets or initializes the font size of annotation text; the default is 13.
    /// </summary>
    public double AnnotationFontSize { get; init; } = 13;

    /// <summary>
    ///     Gets or initializes the length of a port triangle, measured along the wire direction; the default is 8.
    /// </summary>
    public double PortTriangleLength { get; init; } = 8;

    /// <summary>
    ///     Gets or initializes the height of a port triangle, measured across the wire direction; the default is 10.
    /// </summary>
    public double PortTriangleHeight { get; init; } = 10;

    /// <summary>
    ///     Gets or initializes the distance from a port anchor within which a pointer counts as hitting the port; the default is 7.
    /// </summary>
    public double PortHitRadius { get; init; } = 7;

    /// <summary>
    ///     Gets or initializes the gap on each side of a port label, between the port triangle and the label and between the label and the point where the wire attaches; the default is 3.
    /// </summary>
    public double PortLabelGap { get; init; } = 3;

    /// <summary>
    ///     Gets or initializes a value indicating whether wires end in a small filled arrowhead at their target (input) end; the default is true.
    /// </summary>
    public bool WireArrowheads { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the length of a wire arrowhead, measured along the wire; the default is 8.
    /// </summary>
    public double WireArrowLength { get; init; } = 8;

    /// <summary>
    ///     Gets or initializes the width of a wire arrowhead, measured across the wire; the default is 7.
    /// </summary>
    public double WireArrowWidth { get; init; } = 7;

    /// <summary>
    ///     Gets or initializes a value indicating whether a filled dot marks every point where wires of one net branch apart; the default is true.
    /// </summary>
    /// <remarks>
    ///     A net is every wire that starts at the same output port. Where two or more of its wires share
    ///     a trunk and then continue in different directions, the dot shows that they are connected.
    ///     The dot is drawn in the color of the wire.
    /// </remarks>
    public bool WireJunctionDots { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the radius of a wire junction dot (see <see cref="WireJunctionDots"/>); the default is 3.
    /// </summary>
    public double WireJunctionRadius { get; init; } = 3;

    /// <summary>
    ///     Gets or initializes a value indicating whether a small step in a wire is drawn as one gentle S-curve; the default is true.
    /// </summary>
    /// <remarks>
    ///     A step between two parallel runs that is shorter than twice <see cref="WireCornerRadius"/> would
    ///     need two corners with no straight piece between them, which looks like a kink. When true (the
    ///     default) the wire is drawn with a single S-curve about four times as long as the step instead.
    ///     Only the drawing changes; the stored route, hit testing and handles keep the square corners.
    /// </remarks>
    public bool WireSmoothSmallJogs { get; init; } = true;

    /// <summary>
    ///     Gets or initializes a value indicating whether a wire hops over a wire of a different net it crosses; the default is true.
    /// </summary>
    /// <remarks>
    ///     Where a horizontal run of one net crosses a vertical run of another net at a point inside both
    ///     runs, the horizontal wire is drawn with a small semicircular hop upward so the two read as
    ///     passing each other rather than connected. Crossings at a bend or end of either wire, too close
    ///     to a corner or arrowhead for the hop to fit, and crossings between wires of the same net get no
    ///     hop. Crossings closer together than two hop radii share one wider hop. Only the drawing changes;
    ///     the stored route and hit testing keep the straight segments.
    /// </remarks>
    public bool WireCrossingJumps { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the radius of a crossing hop (see <see cref="WireCrossingJumps"/>); the default is 4.
    /// </summary>
    /// <remarks>
    ///     The radius grows with the wire: a wire thicker than <see cref="WireThickness"/> hops with a
    ///     proportionally larger radius, and a hop is never smaller than what clears the wire it crosses.
    /// </remarks>
    public double WireJumpRadius { get; init; } = 4;

    /// <summary>
    ///     Gets or initializes the side length of the handles drawn at the bends and ends of a selected wire; the default is 8.
    /// </summary>
    public double WireHandleSize { get; init; } = 8;

    /// <summary>
    ///     Gets or initializes the gap between a node box and its name above or summary below; the default is 3.
    /// </summary>
    public double TextGap { get; init; } = 3;

    /// <summary>
    ///     Gets or initializes the inner margin between an annotation's border and its text; the default is 5.
    /// </summary>
    public double AnnotationPadding { get; init; } = 5;

    /// <summary>
    ///     Gets or initializes the side length of an annotation's resize handle; the default is 8.
    /// </summary>
    public double ResizeHandleSize { get; init; } = 8;

    /// <summary>
    ///     Gets or initializes the side length of a group's collapse toggle; the default is 12.
    /// </summary>
    public double GroupToggleSize { get; init; } = 12;

    /// <summary>
    ///     Gets the style of node titles: <see cref="FontFamily"/>, <see cref="HeaderFontSize"/>, <see cref="NodeHeaderText"/>, centered. Derived from the other settings.
    /// </summary>
    public TextStyle HeaderTextStyle => new(FontFamily, HeaderFontSize, NodeHeaderText, false, TextAlignment.Center);

    /// <summary>
    ///     Gets the style of node instance names: <see cref="FontFamily"/>, <see cref="NameFontSize"/>, <see cref="NodeNameText"/>, centered. Derived from the other settings.
    /// </summary>
    public TextStyle NameTextStyle => new(FontFamily, NameFontSize, NodeNameText, false, TextAlignment.Center);

    /// <summary>
    ///     Gets the style of property summary lines: <see cref="FontFamily"/>, <see cref="SummaryFontSize"/>, <see cref="SummaryText"/>, centered. Derived from the other settings.
    /// </summary>
    public TextStyle SummaryTextStyle => new(FontFamily, SummaryFontSize, SummaryText, false, TextAlignment.Center);

    /// <summary>
    ///     Gets the style of port labels, aligned left; input labels use <see cref="TextAlignment.Right"/> (derive it with <c>with { Alignment = TextAlignment.Right }</c>). Derived from the other settings.
    /// </summary>
    public TextStyle PortLabelTextStyle => new(FontFamily, PortLabelFontSize, PortLabelText, false, TextAlignment.Left);

    /// <summary>
    ///     Gets the style of group titles: bold, <see cref="GroupTitleFontSize"/>, <see cref="GroupTitleText"/>, left aligned. Derived from the other settings.
    /// </summary>
    public TextStyle GroupTitleTextStyle => new(FontFamily, GroupTitleFontSize, GroupTitleText, true, TextAlignment.Left);

    /// <summary>
    ///     Gets the style of annotation text: <see cref="AnnotationFontSize"/>, <see cref="AnnotationText"/>, left aligned. Derived from the other settings.
    /// </summary>
    public TextStyle AnnotationTextStyle => new(FontFamily, AnnotationFontSize, AnnotationText, false, TextAlignment.Left);
}
