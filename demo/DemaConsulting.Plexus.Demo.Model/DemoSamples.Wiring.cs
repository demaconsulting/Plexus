using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The wiring audit page: every junction and crossing configuration the renderer has to get right.
/// </content>
public static partial class DemoSamples
{
    /// <summary>
    ///     The path of the third fan source's output port.
    /// </summary>
    private const string FanCValue = "FanC.value";

    /// <summary>
    ///     The name of a source node's output port.
    /// </summary>
    private const string ValuePort = "value";

    /// <summary>
    ///     The name of a target node's input port.
    /// </summary>
    private const string SignalPort = "signal";

    /// <summary>
    ///     How far right of an output port's anchor the vertical run of an explicitly routed wire sits between close columns.
    /// </summary>
    private const double TrunkOffset = 30;

    /// <summary>
    ///     How far right of an output port's anchor the vertical runs sit in the crossing block, whose columns are further apart.
    /// </summary>
    private const double WideTrunkOffset = 90;

    /// <summary>
    ///     Adds the wiring page: fan-outs, splits, crossings and other net configurations side by side.
    /// </summary>
    /// <remarks>
    ///     Rules shown on this page: wires of one net (one output port) that branch apart get a dot at the
    ///     branch, drawn as a square "T"; wires of different nets never get a dot, even where they cross or
    ///     touch, and a horizontal wire hops over a vertical wire of another net it crosses. Fan-in is the
    ///     exception that joins nets: wires from two outputs that merge into an input that allows several wires
    ///     (<see cref="PortDefinition.AllowsMultipleWires"/>) make one net and get a dot where they merge.
    ///     Any other input accepts a single wire.
    /// </remarks>
    /// <param name="page">The page to fill.</param>
    private static void AddWiringPage(DiagramPage page)
    {
        AddFanOutBlocks(page);
        AddSplitAndFanInBlocks(page);
        AddCrossingBlock(page);
        AddNetStyleBlocks(page);
    }

    /// <summary>
    ///     Adds the three automatically routed fan-out blocks: two, three and four inputs.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddFanOutBlocks(DiagramPage page)
    {
        page.AddAnnotation("Fan-out to 2 inputs: one dot where the branch leaves the trunk;\nthe branch corner is a square T.", new Rect(45, 15, 450, 45));
        AddNode(page, DemoNodeTypes.Constant, "FanA", 45, 195);
        AddNode(page, DemoNodeTypes.Scope, "FanA1", 285, 105);
        AddNode(page, DemoNodeTypes.Scope, "FanA2", 285, 285);
        page.Connect("FanA.value", "FanA1.signal");
        page.Connect("FanA.value", "FanA2.signal");

        page.AddAnnotation("Fan-out to 3 inputs at different offsets,\nincluding a small step.", new Rect(585, 15, 450, 45));
        AddNode(page, DemoNodeTypes.Constant, "FanB", 585, 285);
        AddNode(page, DemoNodeTypes.Scope, "FanB1", 825, 105);
        AddNode(page, DemoNodeTypes.Add, "FanB2", 825, 285);
        AddNode(page, DemoNodeTypes.Scope, "FanB3", 825, 465);
        page.Connect("FanB.value", "FanB1.signal");
        page.Connect("FanB.value", "FanB2.a");
        page.Connect("FanB.value", "FanB3.signal");

        page.AddAnnotation("Fan-out to 4 inputs: two nodes, two inputs each.", new Rect(1125, 15, 450, 45));
        AddNode(page, DemoNodeTypes.Constant, "FanC", 1125, 255);
        AddNode(page, DemoNodeTypes.Add, "FanCAdd", 1365, 120);
        AddNode(page, DemoNodeTypes.Multiply, "FanCMul", 1365, 330);
        page.Connect(FanCValue, "FanCAdd.a");
        page.Connect(FanCValue, "FanCAdd.b");
        page.Connect(FanCValue, "FanCMul.a");
        page.Connect(FanCValue, "FanCMul.b");
    }

    /// <summary>
    ///     Adds the block where both branches turn at the split and the block where two nets merge into an input that allows fan-in.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddSplitAndFanInBlocks(DiagramPage page)
    {
        page.AddAnnotation("A split at a corner: both branches turn at the same\npoint, so the dot sits on the bend.", new Rect(45, 645, 450, 45));
        var split = AddNode(page, DemoNodeTypes.Switch, "SplitSw", 45, 795, ("On", true));
        var up = AddNode(page, DemoNodeTypes.Lamp, "SplitUp", 285, 735);
        var down = AddNode(page, DemoNodeTypes.Lamp, "SplitDown", 285, 915);
        var splitX = split.Port("out").Anchor.X + TrunkOffset;
        ConnectVertical(page, split.Port("out"), up.Input("in"), splitX);
        ConnectVertical(page, split.Port("out"), down.Input("in"), splitX);

        page.AddAnnotation("Fan-in needs an opted-in input (AllowsMultipleWires): here two\nnets merge at one dot. Other inputs accept a single wire.", new Rect(585, 645, 450, 45));
        AddNode(page, DemoNodeTypes.Constant, "InA", 585, 735);
        AddNode(page, DemoNodeTypes.Constant, "InB", 585, 915);
        AddNode(page, DemoNodeTypes.Scope, "InScope", 825, 825);
        page.Connect("InA.value", "InScope.signal");
        page.Connect("InB.value", "InScope.signal");
    }

    /// <summary>
    ///     Adds the block where a horizontal wire crosses two vertical wires of other nets.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddCrossingBlock(DiagramPage page)
    {
        page.AddAnnotation("Different nets cross: the horizontal wire hops. Crossings\ncloser than two hop radii share one wider hop.", new Rect(1125, 645, 450, 45));
        var outer = AddNode(page, DemoNodeTypes.Constant, "HopOuter", 1125, 735);
        var inner = AddNode(page, DemoNodeTypes.Constant, "HopInner", 1125, 885);
        var across = AddNode(page, DemoNodeTypes.Constant, "HopAcross", 1125, 1035);
        var acrossTarget = AddNode(page, DemoNodeTypes.Scope, "HopAcrossT", 1485, 1035);
        var outerTarget = AddNode(page, DemoNodeTypes.Scope, "HopOuterT", 1485, 1200);
        var innerTarget = AddNode(page, DemoNodeTypes.Scope, "HopInnerT", 1485, 1365);

        // The horizontal wire is straight; the two vertical wires nest (so they never cross each other) and run
        // 6 units apart, close enough to share one hop
        var x = outer.Port(ValuePort).Anchor.X + WideTrunkOffset;
        page.Connect(across.Port(ValuePort), acrossTarget.Input(SignalPort));
        ConnectVertical(page, outer.Port(ValuePort), outerTarget.Input(SignalPort), x);
        ConnectVertical(page, inner.Port(ValuePort), innerTarget.Input(SignalPort), x - 6);
    }

    /// <summary>
    ///     Adds the blocks for nets that touch, dashed and heavy same-net branches, and a collapsed-group fan-out.
    /// </summary>
    /// <param name="page">The page to fill.</param>
    private static void AddNetStyleBlocks(DiagramPage page)
    {
        page.AddAnnotation("Different nets that touch: no dot and no hop, because\nneither wire crosses the other. Avoid in real layouts.", new Rect(45, 1530, 450, 45));
        var upper = AddNode(page, DemoNodeTypes.Constant, "TouchU", 45, 1620);
        var lower = AddNode(page, DemoNodeTypes.Constant, "TouchV", 45, 1800);
        var upperTarget = AddNode(page, DemoNodeTypes.Scope, "TouchUT", 285, 1830);
        var lowerTarget = AddNode(page, DemoNodeTypes.Scope, "TouchVT", 285, 1650);
        var touchX = upper.Port(ValuePort).Anchor.X + TrunkOffset;
        ConnectVertical(page, upper.Port(ValuePort), upperTarget.Input(SignalPort), touchX);
        ConnectVertical(page, lower.Port(ValuePort), lowerTarget.Input(SignalPort), touchX);

        page.AddAnnotation("Dashed steam fan-out: the dashes and the dots\nfollow the same wires.", new Rect(585, 1530, 450, 45));
        AddNode(page, DemoNodeTypes.Tank, "SteamTank", 585, 1740, ("Fluid", "Steam"), ("Level", 65));
        AddNode(page, DemoNodeTypes.Valve, "SteamV1", 825, 1620);
        AddNode(page, DemoNodeTypes.Valve, "SteamV2", 825, 1800);
        AddNode(page, DemoNodeTypes.Drain, "SteamDrain", 825, 1980);
        page.Connect("SteamTank.out", "SteamV1.in");
        page.Connect("SteamTank.out", "SteamV2.in");
        page.Connect("SteamTank.out", "SteamDrain.in");

        page.AddAnnotation("A collapsed group fans out from its box: its display\nroutes make the same T junctions.", new Rect(1125, 1530, 450, 45));
        var source = AddNode(page, DemoNodeTypes.Constant, "GroupSource", 1125, 1680);
        var sum = AddNode(page, DemoNodeTypes.Add, "GroupSum", 1125, 1830);
        source.Port(ValuePort).ConnectTo(sum.Input("a"));
        var group = page.CreateGroup([source, sum], "Inner");
        group.IsCollapsed = true;
        var first = AddNode(page, DemoNodeTypes.Scope, "GroupS1", 1485, 1620);
        var second = AddNode(page, DemoNodeTypes.Scope, "GroupS2", 1485, 1800);
        var third = AddNode(page, DemoNodeTypes.Scope, "GroupS3", 1485, 1980);
        sum.Port("sum").ConnectTo(first.Input(SignalPort));
        sum.Port("sum").ConnectTo(second.Input(SignalPort));
        sum.Port("sum").ConnectTo(third.Input(SignalPort));
    }

    /// <summary>
    ///     Connects two ports with a route that leaves the source horizontally, runs vertically at a given X and enters the target horizontally.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="from">The output port.</param>
    /// <param name="to">The input port.</param>
    /// <param name="x">The X coordinate of the vertical run.</param>
    private static void ConnectVertical(DiagramPage page, Port from, Port to, double x) =>
        page.Connect(from, to, [new Point(x, from.Anchor.Y), new Point(x, to.Anchor.Y)]);
}
