using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     One configuration of the wiring audit: its wires and what must be drawn for them.
/// </summary>
/// <param name="Name">The configuration name.</param>
/// <param name="Top">The top of the page band the configuration occupies.</param>
/// <param name="Bottom">The bottom of the page band the configuration occupies.</param>
/// <param name="Wires">The wires of the configuration in page order.</param>
/// <param name="Dots">The exact points where junction dots are expected.</param>
/// <param name="SharpCorners">The points where a wire of the configuration bends and the corner must be square.</param>
/// <param name="Hops">The number of hops expected to be drawn over the configuration's crossings, in total.</param>
internal sealed record AuditConfig(
    string Name,
    double Top,
    double Bottom,
    IReadOnlyList<Wire> Wires,
    IReadOnlyList<Point> Dots,
    IReadOnlyList<Point> SharpCorners,
    int Hops);

/// <summary>
///     A synthetic page that covers the wire junction and crossing configurations a diagram can contain.
/// </summary>
/// <remarks>
///     Every configuration sits in a band of its own, far enough from the others that wires never meet.
///     Routes are explicit, so the geometry does not depend on the router. Each configuration records the
///     junction dots, square corners and hops it must produce, which is what the tests compare against.
/// </remarks>
internal sealed class WiringAudit
{
    /// <summary>
    ///     The height of the page band of one configuration.
    /// </summary>
    private const double BandHeight = 900;

    /// <summary>
    ///     The distance from a source's anchor to the first vertical run of a wire.
    /// </summary>
    private const double TrunkGap = 60;

    /// <summary>
    ///     The X coordinate of every source node.
    /// </summary>
    private const double SourceX = 0;

    /// <summary>
    ///     The X coordinate of the left of every sink node column.
    /// </summary>
    private const double SinkX = 420;

    /// <summary>
    ///     The vertical offset of an output anchor below the top of a producer node.
    /// </summary>
    private static readonly double OutputOffset = MeasureOutputOffset();

    /// <summary>
    ///     The vertical offset of an input anchor below the top of a consumer node.
    /// </summary>
    private static readonly double InputOffset = MeasureInputOffset();

    /// <summary>
    ///     The horizontal offset of an input anchor right of the left of a consumer node (negative: the anchor is left of the node).
    /// </summary>
    private static readonly double InputXOffset = MeasureInputXOffset();

    /// <summary>
    ///     The configurations added so far.
    /// </summary>
    private readonly List<AuditConfig> _configs = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="WiringAudit"/> class holding every configuration.
    /// </summary>
    public WiringAudit()
    {
        var (diagram, page) = SceneTestModel.CreatePage();
        Diagram = diagram;
        Page = page;
        AddFanOutOnStraightTrunk();
        AddSplitAtCorner();
        AddFanOut3();
        AddFanOut4();
        AddFanInTwoNets();
        AddCrossing();
        AddTouchingNets();
        AddEndOnTrunk();
        AddStyledFanOut();
    }

    /// <summary>
    ///     Gets the diagram that owns the audit page.
    /// </summary>
    public Diagram Diagram { get; }

    /// <summary>
    ///     Gets the audit page.
    /// </summary>
    public DiagramPage Page { get; }

    /// <summary>
    ///     Gets the configurations in page order.
    /// </summary>
    public IReadOnlyList<AuditConfig> Configs => _configs;

    /// <summary>
    ///     Finds a configuration by name.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    /// <returns>The configuration.</returns>
    public AuditConfig Get(string name) => _configs.Single(c => c.Name == name);

    /// <summary>
    ///     Creates a producer whose output anchor is at a given height.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <param name="anchorY">The wanted Y of the output anchor.</param>
    /// <returns>The node.</returns>
    private Node Source(string name, double anchorY) =>
        Page.AddNode("acme.Producer", SourceX, anchorY - OutputOffset, name);

    /// <summary>
    ///     Creates a consumer whose input anchor is at a given height.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <param name="anchorY">The wanted Y of the input anchor.</param>
    /// <param name="x">The left of the node.</param>
    /// <returns>The node.</returns>
    private Node Sink(string name, double anchorY, double x = SinkX) =>
        Page.AddNode("acme.Consumer", x, anchorY - InputOffset, name);

    /// <summary>
    ///     Gets the X of the first vertical run of wires leaving a source.
    /// </summary>
    /// <param name="source">The source node.</param>
    /// <returns>The trunk X.</returns>
    private static double TrunkX(Node source) => source.Port("value").Anchor.X + TrunkGap;

    /// <summary>
    ///     Connects a source to a sink with a route that runs vertically at a given X.
    /// </summary>
    /// <param name="source">The source node.</param>
    /// <param name="sink">The sink node.</param>
    /// <param name="x">The X of the vertical run.</param>
    /// <returns>The wire.</returns>
    private Wire Vertical(Node source, Node sink, double x)
    {
        var a = source.Port("value").Anchor;
        var z = sink.Port("in").Anchor;
        return Page.Connect(source.Port("value"), sink.Port("in"), [new Point(x, a.Y), new Point(x, z.Y)]);
    }

    /// <summary>
    ///     Records a configuration.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    /// <param name="band">The band index.</param>
    /// <param name="wires">The wires.</param>
    /// <param name="dots">The expected dots.</param>
    /// <param name="sharp">The expected square corners.</param>
    /// <param name="hops">The expected hops.</param>
    private void Record(string name, int band, Wire[] wires, Point[] dots, Point[] sharp, int hops = 0) =>
        _configs.Add(new AuditConfig(name, band * BandHeight, (band + 1) * BandHeight, wires, dots, sharp, hops));

    /// <summary>
    ///     A net with one wire straight on and one that branches off it: the classic T.
    /// </summary>
    private void AddFanOutOnStraightTrunk()
    {
        var source = Source("TSource", 300);
        var straight = Sink("TStraight", source.Port("value").Anchor.Y);
        var below = Sink("TBelow", 500);
        var x = TrunkX(source);
        var corner = new Point(x, source.Port("value").Anchor.Y);
        var first = Page.Connect(source.Port("value"), straight.Port("in"), []);
        var second = Vertical(source, below, x);
        Record("TOnStraightTrunk", 0, [first, second], [corner], [corner]);
    }

    /// <summary>
    ///     A net that splits at a corner: one branch turns up and one turns down at the same point.
    /// </summary>
    private void AddSplitAtCorner()
    {
        var source = Source("SplitSource", BandHeight + 450);
        var up = Sink("SplitUp", BandHeight + 200);
        var down = Sink("SplitDown", BandHeight + 700);
        var x = TrunkX(source);
        var corner = new Point(x, source.Port("value").Anchor.Y);
        Record("SplitAtCorner", 1, [Vertical(source, up, x), Vertical(source, down, x)], [corner], [corner]);
    }

    /// <summary>
    ///     A net with three inputs: one above the source and two below, so there is a dot where the lower pair separates.
    /// </summary>
    private void AddFanOut3()
    {
        var top = 2 * BandHeight;
        var source = Source("Fan3Source", top + 300);
        var first = Sink("Fan3A", top + 100);
        var second = Sink("Fan3B", top + 450);
        var third = Sink("Fan3C", top + 650);
        var x = TrunkX(source);
        var a = source.Port("value").Anchor.Y;
        var branch = new Point(x, second.Port("in").Anchor.Y);
        Record(
            "FanOut3",
            2,
            [Vertical(source, first, x), Vertical(source, second, x), Vertical(source, third, x)],
            [new Point(x, a), branch],
            [new Point(x, a), branch]);
    }

    /// <summary>
    ///     A net with four inputs, two above and two below the source.
    /// </summary>
    private void AddFanOut4()
    {
        var top = 3 * BandHeight;
        var source = Source("Fan4Source", top + 450);
        var sinks = new[] { top + 100, top + 250, top + 650, top + 800 }.Select((y, i) => Sink($"Fan4_{i}", y)).ToArray();
        var x = TrunkX(source);
        var a = source.Port("value").Anchor.Y;
        var dots = new[] { new Point(x, a), new Point(x, sinks[1].Port("in").Anchor.Y), new Point(x, sinks[2].Port("in").Anchor.Y) };
        Record("FanOut4", 3, [.. sinks.Select(s => Vertical(source, s, x))], dots, dots);
    }

    /// <summary>
    ///     Two nets feeding one input that allows fan-in: they merge into one net, so the point where the second joins the first has a dot.
    /// </summary>
    private void AddFanInTwoNets()
    {
        var top = 4 * BandHeight;
        var first = Source("FanInA", top + 200);
        var second = Source("FanInB", top + 700);
        var sink = Page.AddNode("acme.Merger", SinkX, top + 450 - InputOffset, "FanInSink");
        var secondX = TrunkX(second) + 30;
        Record(
            "FanInTwoNets",
            4,
            [Vertical(first, sink, TrunkX(first)), Vertical(second, sink, secondX)],
            [new Point(secondX, sink.Port("in").Anchor.Y)],
            [new Point(secondX, sink.Port("in").Anchor.Y)]);
    }

    /// <summary>
    ///     A straight wire crossed by a vertical wire of another net: no dot, and the horizontal wire hops.
    /// </summary>
    private void AddCrossing()
    {
        var top = 5 * BandHeight;
        var across = Source("CrossAcross", top + 450);
        var acrossSink = Sink("CrossAcrossSink", across.Port("value").Anchor.Y);
        var down = Source("CrossDown", top + 150);
        var downSink = Sink("CrossDownSink", top + 750);
        var horizontal = Page.Connect(across.Port("value"), acrossSink.Port("in"), []);
        var vertical = Vertical(down, downSink, TrunkX(down) + 40);
        Record("CrossingDifferentNets", 5, [horizontal, vertical], [], [], 1);
    }

    /// <summary>
    ///     Two nets where one wire bends exactly on the other and runs along it: touching, never crossing.
    /// </summary>
    private void AddTouchingNets()
    {
        var top = 6 * BandHeight;
        var across = Source("TouchAcross", top + 450);
        var acrossSink = Sink("TouchAcrossSink", across.Port("value").Anchor.Y);
        var bender = Source("TouchBender", top + 150);
        var benderSink = Sink("TouchBenderSink", top + 750);
        var y = across.Port("value").Anchor.Y;
        var x = TrunkX(bender) + 40;
        var horizontal = Page.Connect(across.Port("value"), acrossSink.Port("in"), []);
        var a = bender.Port("value").Anchor;
        var z = benderSink.Port("in").Anchor;
        var touching = Page.Connect(
            bender.Port("value"),
            benderSink.Port("in"),
            [new Point(x, a.Y), new Point(x, y), new Point(x + 80, y), new Point(x + 80, z.Y)]);
        Record("TouchingDifferentNets", 6, [horizontal, touching], [], []);
    }

    /// <summary>
    ///     A net where one wire ends exactly on the trunk of another: the end is a T with a dot, and the first branch leaves with a T of its own.
    /// </summary>
    private void AddEndOnTrunk()
    {
        var top = 7 * BandHeight;
        var source = Source("EndSource", top + 300);
        var far = Sink("EndFar", top + 750);
        var x = TrunkX(source);
        var a = source.Port("value").Anchor;

        // The second sink's input anchor sits exactly on the first wire's vertical run
        var onTrunk = Page.AddNode("acme.Consumer", x - InputXOffset, top + 500 - InputOffset, "EndOnTrunk");
        var end = onTrunk.Port("in").Anchor;
        var bx = a.X + 25;
        var first = Vertical(source, far, x);
        var second = Page.Connect(source.Port("value"), onTrunk.Port("in"), [new Point(bx, a.Y), new Point(bx, end.Y)]);
        Record("EndOnTrunk", 7, [first, second], [new Point(bx, a.Y), end], [new Point(bx, a.Y)]);
    }

    /// <summary>
    ///     A fan-out of three where one wire is dashed and one is thick and colored.
    /// </summary>
    private void AddStyledFanOut()
    {
        var top = 8 * BandHeight;
        var source = Source("StyledSource", top + 300);
        var first = Sink("StyledA", top + 100);
        var second = Sink("StyledB", top + 450);
        var third = Sink("StyledC", top + 650);
        var x = TrunkX(source);
        var a = source.Port("value").Anchor.Y;
        var branch = new Point(x, second.Port("in").Anchor.Y);
        Record(
            "StyledFanOut",
            8,
            [Vertical(source, first, x), Vertical(source, second, x), Vertical(source, third, x)],
            [new Point(x, a), branch],
            [new Point(x, a), branch]);
    }

    /// <summary>
    ///     Measures where a producer's output anchor lies below the node's top.
    /// </summary>
    /// <returns>The offset.</returns>
    private static double MeasureOutputOffset()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 0, 0, "Probe");
        return node.Port("value").Anchor.Y - node.Bounds.Y;
    }

    /// <summary>
    ///     Measures where a consumer's input anchor lies below the node's top.
    /// </summary>
    /// <returns>The offset.</returns>
    private static double MeasureInputOffset()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Consumer", 0, 0, "Probe");
        return node.Port("in").Anchor.Y - node.Bounds.Y;
    }

    /// <summary>
    ///     Measures where a consumer's input anchor lies relative to the left of the node.
    /// </summary>
    /// <returns>The offset.</returns>
    private static double MeasureInputXOffset()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Consumer", 0, 0, "Probe");
        return node.Port("in").Anchor.X - node.Bounds.X;
    }
}
