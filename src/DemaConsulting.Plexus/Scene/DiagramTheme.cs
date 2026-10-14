namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     The complete look and geometry of a diagram: every metric, color and font that the model, the wire routers and the painters share.
/// </summary>
/// <remarks>
///     <para>
///         The model, the wire routers and every painter read geometry from the same theme so a node's
///         port anchors, obstacle boxes and drawn boxes always agree. A theme is immutable,
///         so an instance such as <see cref="Classic"/> can be shared safely, and a customized copy is one
///         expression: <c>DiagramTheme.Classic with { NodeFill = Color.Parse("#336699") }</c> (use <c>with</c> to derive
///         variants; an object initializer, <c>new DiagramTheme { ... }</c>, starts from the same classic values). Every property
///         below is read with <c>theme.Property</c> and set only through <c>with</c> or an object initializer.
///     </para>
///     <para>
///         What <see cref="Classic"/> looks like: a mid-gray canvas (<c>#A9A9A9</c>) with a regular grid of darker gray dots
///         every 15 units; mid-dark gray nodes (<c>#6B6B6B</c> with a thin dark border and slightly rounded corners) with a white bold
///         title in a 30-unit header band, an optional icon, the instance name in green above the box and black property
///         summary lines below it; ports on the left (inputs) and right (outputs) edges, centered vertically, drawn as small
///         right-pointing triangles colored by their port type with a black label beside them; black 2-unit orthogonal wires with
///         rounded corners and a small arrowhead at the input end, that hop over wires of other nets they cross and show a dot
///         where wires of one net branch apart; selection is a blue outline; annotations are pale-yellow notes; groups are
///         translucent white frames with a dark title bar and a white title. Fonts are Segoe UI (falling back to the platform's sans-serif).
///     </para>
///     <para>
///         Key metrics (diagram units, defaults of <see cref="Classic"/>): <see cref="GridSize"/> 15 (background dots, snapping,
///         router grid), <see cref="DefaultNodeWidth"/> 90 and <see cref="DefaultNodeHeight"/> 90 (grown to fit the ports),
///         <see cref="HeaderHeight"/> 30, <see cref="PortPitch"/> 15 (distance between port rows), <see cref="WireStubLength"/> 15 (straight piece
///         where a wire leaves or enters a port), <see cref="ObstacleMargin"/> 15 (clearance wires keep around nodes),
///         <see cref="GroupPadding"/> 30 and <see cref="GroupTitleHeight"/> 24 (group frames), <see cref="WireThickness"/> 2.
///         Properties are grouped by prefix: <c>Node*</c>, <c>Port*</c>, <c>Wire*</c>, <c>Group*</c>, <c>Annotation*</c>, <c>Selection*</c>/<c>Hover*</c>,
///         <c>*FontSize</c> and the <c>*TextStyle</c> helpers.
///     </para>
///     <para>
///         Two presets exist: <see cref="Classic"/> and <see cref="Dark"/> (a dark canvas). If you derive your own dark variant from <see cref="Classic"/>,
///         change the text colors along with the canvas: the instance name (<see cref="NodeNameText"/>, green), the summary lines
///         (<see cref="SummaryText"/>, black) and the port labels (<see cref="PortLabelText"/>, black) are drawn straight on the canvas and
///         are unreadable on a dark background; <see cref="Dark"/> already does this.
///     </para>
///     <para>
///         Assign a theme to <see cref="Diagram.Theme"/> before adding content; the editor, the hit tester and the renderers pick it up.
///         Replacing it later does not move existing nodes or re-route existing wires, see <see cref="DiagramPage.RerouteAll"/>.
///     </para>
/// </remarks>
/// <example>
///     Derive a dark variant of the classic theme and use it for the diagram and for a scene.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     // Copy the classic theme with a few changes; the original is never modified
///     var dark = DiagramTheme.Classic with
///     {
///         CanvasBackground = Color.Parse("#101820"),
///         GridDotColor = Color.Parse("#2A3644"),
///         NodeFill = Color.Parse("#223344"),
///         WireThickness = 3,
///         WireArrowheads = true,
///         GridSize = 20,
///     };
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     diagram.Theme = dark;                                     // used by routing, hit testing and every painter
///     var page = diagram.Pages.Add("Main");
///     var scene = SceneBuilder.Build(page, new SceneOptions { Theme = dark });   // or choose the theme per scene
///     </code>
/// </example>
public sealed partial record DiagramTheme
{
    /// <summary>
    ///     Backing store for <see cref="Classic"/>; created once because the theme is immutable.
    /// </summary>
    private static readonly DiagramTheme ClassicTheme = new();

    /// <summary>
    ///     Backing store for <see cref="Dark"/>; created once because the theme is immutable.
    /// </summary>
    private static readonly DiagramTheme DarkTheme = new()
    {
        CanvasBackground = Color.Parse("#1E2228"),
        GridDotColor = Color.Parse("#3B424D"),
        NodeFill = Color.Parse("#3A4252"),
        NodeBorder = Color.Parse("#7C8799"),
        NodeHeaderText = Color.White,
        NodeNameText = Color.Parse("#7FD67F"),
        SummaryText = Color.Parse("#D3D8E0"),
        PortLabelText = Color.Parse("#D3D8E0"),
        WireColor = Color.Parse("#C9CFDA"),
        SelectionColor = Color.Parse("#4DA3FF"),
        HoverColor = Color.Parse("#2F6EA8"),
        AnnotationFill = Color.Parse("#4B4630"),
        AnnotationBorder = Color.Parse("#9A8C45"),
        AnnotationText = Color.Parse("#F3ECC4"),
        GroupFill = Color.Parse("#1CFFFFFF"),
        GroupBorder = Color.Parse("#8A94A4"),
        GroupTitleFill = Color.Parse("#D0323A47"),
        GroupTitleText = Color.Parse("#F2F4F8"),
    };

    /// <summary>
    ///     Gets the default theme that reproduces the classic block-diagram look (see the type remarks for what that looks like).
    /// </summary>
    /// <remarks>It is the value of <see cref="Diagram.Theme"/> until you assign another; derive variants with <c>Classic with { ... }</c>.</remarks>
    public static DiagramTheme Classic => ClassicTheme;

    /// <summary>
    ///     Gets a preset for a dark canvas: every text, wire, grid and frame color is chosen to stay readable on a near-black background.
    /// </summary>
    /// <remarks>
    ///     Only colors differ from <see cref="Classic"/>; all metrics are identical, so a diagram can switch between the two without
    ///     moving anything. The canvas is dark blue-gray (<c>#1E2228</c>) with subtle grid dots; nodes are a lighter slate with a light border and
    ///     a white title; the instance name is light green, summaries and port labels light gray; wires are light gray; selection is a bright blue;
    ///     annotations are dim yellow notes with pale text; groups are faint light frames with a dark title bar. Port types keep the colors of the
    ///     port type registry, so choose type colors that read on dark when you use this theme. Derive variants with <c>Dark with { ... }</c>.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Rendering;
    ///     using DemaConsulting.Plexus.Scene;
    ///
    ///     var diagram = new Diagram(new NodeTypeRegistry());
    ///     var page = diagram.Pages.Add("Main");
    ///     diagram.Theme = DiagramTheme.Dark;                               // before adding content; the renderer and editor pick it up
    ///     byte[] png = PlexusRenderer.RenderPng(page);                     // or: new RenderOptions { Theme = DiagramTheme.Dark }
    ///     </code>
    /// </example>
    public static DiagramTheme Dark => DarkTheme;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramTheme"/> class with the classic theme values.
    /// </summary>
    public DiagramTheme()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Gets or initializes the spacing of the background grid dots, which is also the step used for snapping and routing; the default is 15.
    /// </summary>
    /// <remarks>
    ///     The grid size drives the dotted background (<see cref="GridDotColor"/>), the snapping of drags in the
    ///     editor, the grid the wire router lays routes on, the size of the bump that a wire jog adds, and the offset of pasted content. It does
    ///     NOT drive node sizes or port spacing: those come from <see cref="DefaultNodeWidth"/>, <see cref="DefaultNodeHeight"/> and
    ///     <see cref="PortPitch"/>. A value of 0 or less hides the grid.
    /// </remarks>
    public double GridSize { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes the minimum width of new nodes whose type declares no <see cref="NodeType.DefaultSize"/>; the default is 90.
    /// </summary>
    /// <remarks>The width grows, in steps of <see cref="GridSize"/>, when the type's title does not fit the header on one line; see <see cref="NodeGeometry.DefaultNodeSize"/>.</remarks>
    public double DefaultNodeWidth { get; init; } = 90;

    /// <summary>
    ///     Gets or initializes the minimum height of new nodes whose type declares no <see cref="NodeType.DefaultSize"/>; nodes with many ports grow beyond it. The default is 90.
    /// </summary>
    public double DefaultNodeHeight { get; init; } = 90;

    /// <summary>
    ///     Gets or initializes the height of the title area at the top of a node box, above the first port; the default is 30.
    /// </summary>
    public double HeaderHeight { get; init; } = 30;

    /// <summary>
    ///     Gets or initializes the vertical distance between consecutive port anchors on one edge; the default is 15.
    /// </summary>
    public double PortPitch { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes how a node's input and output port stacks are positioned vertically along the box edges; the default is <see cref="Scene.PortAlignment.Center"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="Scene.PortAlignment.Center"/> (the default) centers each stack on the node box;
    ///     <see cref="Scene.PortAlignment.Top"/> starts each stack just below the header.
    /// </remarks>
    public PortAlignment PortAlignment { get; init; } = PortAlignment.Center;

    /// <summary>
    ///     Gets or initializes the minimum clearance between the bottom of the header and the first port anchor; the default is 0.
    /// </summary>
    /// <remarks>
    ///     With <see cref="Scene.PortAlignment.Top"/> the first anchor lies exactly this far below the
    ///     header. With <see cref="Scene.PortAlignment.Center"/> it is the clearance the default node
    ///     height reserves between the header and the topmost anchor of the longer stack.
    /// </remarks>
    public double FirstPortOffset { get; init; } = 0;

    /// <summary>
    ///     Gets or initializes the free space kept below the last port anchor when a node grows to fit its ports; the default is 15.
    ///     With <see cref="Scene.PortAlignment.Center"/> the header clearance usually dominates.
    /// </summary>
    public double PortBottomPadding { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes the side length of the square box reserved for a node icon; the default is 56.
    /// </summary>
    public double IconBoxSize { get; init; } = 56;

    /// <summary>
    ///     Gets or initializes the radius used to round orthogonal wire corners when drawing; the default is 6.
    /// </summary>
    public double WireCornerRadius { get; init; } = 6;

    /// <summary>
    ///     Gets or initializes the clearance added around node boxes when they are handed to routers as obstacles; the default is 15.
    /// </summary>
    public double ObstacleMargin { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes a value indicating whether group frames are routing obstacles for wires that do not belong to the group; the default is true.
    /// </summary>
    /// <remarks>
    ///     When true (the default) a wire is routed around the frame of every visible group (expanded:
    ///     the whole frame including the title bar; collapsed: the collapsed box, both inflated by
    ///     <see cref="ObstacleMargin"/>) unless one of the wire's two end nodes lies inside that group
    ///     at any depth; such a wire may cross the frame of the groups it starts or ends in. Routes
    ///     are only computed when a wire is created or reconnected, when one of its end nodes is
    ///     moved, and by <see cref="DiagramPage.RerouteAll"/>; moving a group does not re-route unrelated
    ///     wires, so call <see cref="DiagramPage.RerouteAll"/> afterwards when they should avoid the new
    ///     position. Set to false to route through group frames as if groups were not there.
    /// </remarks>
    public bool GroupsAreObstacles { get; init; } = true;

    /// <summary>
    ///     Gets or initializes the length of the straight stub a wire leaves or enters a port with; the default is 15.
    /// </summary>
    public double WireStubLength { get; init; } = 15;

    /// <summary>
    ///     Gets or initializes the padding between an expanded group's frame and its members; the default is 30.
    /// </summary>
    public double GroupPadding { get; init; } = 30;

    /// <summary>
    ///     Gets or initializes the height of an expanded group's title bar; the default is 24.
    /// </summary>
    public double GroupTitleHeight { get; init; } = 24;

    /// <summary>
    ///     Gets or initializes the width of a collapsed group's box; the default is 120.
    /// </summary>
    public double CollapsedGroupWidth { get; init; } = 120;

    /// <summary>
    ///     Gets or initializes the height of a collapsed group's box; the default is 45.
    /// </summary>
    public double CollapsedGroupHeight { get; init; } = 45;
}
