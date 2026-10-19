using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Tests for crossing jumps: the hops a horizontal wire draws over wires of other nets.
/// </summary>
public class WireCrossingTests
{
    /// <summary>
    ///     The tolerance when comparing coordinates.
    /// </summary>
    private const double Tolerance = 1e-6;

    /// <summary>
    ///     Creates a wire run for crossing detection.
    /// </summary>
    /// <param name="net">The net identity.</param>
    /// <param name="thickness">The stroke thickness.</param>
    /// <param name="points">The polyline.</param>
    /// <returns>The run.</returns>
    private static WireRun Run(object net, double thickness, params Point[] points) => new(points, net, thickness, []);

    /// <summary>
    ///     Finds the hops with the default theme values.
    /// </summary>
    /// <param name="runs">The wires.</param>
    /// <returns>The hops of each wire.</returns>
    private static List<WireHop>[] Find(params WireRun[] runs) => WireCrossings.Find(runs, 4, 2, 6, 3);

    /// <summary>
    ///     Counts the cubic commands of a path.
    /// </summary>
    /// <param name="commands">The commands.</param>
    /// <returns>The number of cubics.</returns>
    private static int Cubics(IEnumerable<PathCommand> commands) => commands.Count(c => c.Kind == PathCommandKind.CubicTo);

    /// <summary>
    ///     Validates that an interior crossing of two different nets is reported to the horizontal wire only.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_InteriorCrossing_IsReportedOnTheHorizontalWire()
    {
        // Arrange
        var horizontal = Run("a", 2, new Point(0, 0), new Point(100, 0));
        var vertical = Run("b", 2, new Point(50, -50), new Point(50, 50));

        // Act
        var hops = Find(horizontal, vertical);

        // Assert
        Assert.Equal(new WireHop(50, 0, 4), Assert.Single(hops[0]));
        Assert.Empty(hops[1]);
    }

    /// <summary>
    ///     Validates that wires of the same net never hop over each other.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_SameNet_HasNoHop()
    {
        // Arrange
        var horizontal = Run("a", 2, new Point(0, 0), new Point(100, 0));
        var vertical = Run("a", 2, new Point(50, -50), new Point(50, 50));

        // Act / Assert
        Assert.All(Find(horizontal, vertical), hops => Assert.Empty(hops));
    }

    /// <summary>
    ///     Validates that a touch at an end or a bend is not a crossing.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_EndsAndBends_HaveNoHop()
    {
        // Arrange: the vertical wire ends on the horizontal one; a second vertical passes a bend of the horizontal wire
        var straight = Run("a", 2, new Point(0, 0), new Point(100, 0));
        var ending = Run("b", 2, new Point(30, -50), new Point(30, 0));
        var bent = Run("c", 2, new Point(0, 100), new Point(60, 100), new Point(60, 200));
        var passing = Run("d", 2, new Point(60, 50), new Point(60, 250));

        // Act
        var hops = Find(straight, ending, bent, passing);

        // Assert: no wire hops; the horizontal run of "bent" meets the end of "passing" only at its own bend
        Assert.All(hops, h => Assert.Empty(h));
    }

    /// <summary>
    ///     Validates that a crossing too close to the end of the vertical wire (where its corner curve is) is not a hop.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_CrossingNearVerticalCorner_HasNoHop()
    {
        // Arrange: the vertical wire starts 3 above the horizontal one; its corner radius is 6
        var horizontal = Run("a", 2, new Point(0, 0), new Point(100, 0));
        var vertical = Run("b", 2, new Point(50, -3), new Point(50, 50));

        // Act / Assert
        Assert.All(Find(horizontal, vertical), hops => Assert.Empty(hops));
    }

    /// <summary>
    ///     Validates that a hop is never placed where a junction dot is: neither on the crossing nor within the hop of a dot on the horizontal wire.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_JunctionAtCrossing_HasNoHop()
    {
        // Arrange: the horizontal wire's net has a junction 5 beside the crossing, the vertical wire's net one on it
        var horizontal = new WireRun([new Point(0, 0), new Point(100, 0)], "a", 2, [new Point(55, 0)]);
        var vertical = Run("b", 2, new Point(50, -50), new Point(50, 50));
        var other = new WireRun([new Point(0, 20), new Point(100, 20)], "c", 2, []);
        var onCrossing = new WireRun([new Point(50, -50), new Point(50, 50)], "d", 2, [new Point(50, 20)]);

        // Act
        var beside = Find(horizontal, vertical);
        var at = Find(other, onCrossing);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(beside[0]),
            () => Assert.Empty(at[0]));
    }

    /// <summary>
    ///     Validates that the hop radius follows the thickness of the hopping wire and clears a thick wire it crosses.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_ThickWires_GrowTheRadius()
    {
        // Arrange: a 4-thick wire (twice the reference) over a thin one, and a thin wire over a 10-thick one
        var thick = Run("a", 4, new Point(0, 0), new Point(100, 0));
        var thin = Run("b", 2, new Point(50, -50), new Point(50, 50));
        var thinAcross = Run("c", 2, new Point(0, 200), new Point(100, 200));
        var heavy = Run("d", 10, new Point(50, 150), new Point(50, 250));

        // Act
        var hops = Find(thick, thin, thinAcross, heavy);

        // Assert: 4 x 2 = 8, and (2 + 10) / 2 + 1 = 7
        Assert.Multiple(
            () => Assert.Equal(8, Assert.Single(hops[0]).Radius, 6),
            () => Assert.Equal(7, Assert.Single(hops[2]).Radius, 6));
    }

    /// <summary>
    ///     Validates that every crossing of a lattice of many wires is reported, in the order of the crossed wires rather than of their positions.
    /// </summary>
    [Fact]
    public void WireCrossings_Find_ManyWires_ReportsEveryCrossingInWireOrder()
    {
        // Arrange: thirty vertical wires listed from right to left, then thirty horizontal wires crossing all of them
        const int count = 30;
        var runs = new List<WireRun>();
        for (var j = count; j >= 1; j--)
        {
            runs.Add(Run($"v{j}", 2, new Point(j * 10.0, -50), new Point(j * 10.0, 400)));
        }

        for (var i = 1; i <= count; i++)
        {
            runs.Add(Run($"h{i}", 2, new Point(0, i * 10.0), new Point(400, i * 10.0)));
        }

        // Act
        var hops = WireCrossings.Find(runs, 4, 2, 6, 3);

        // Assert: no vertical wire hops; each horizontal wire hops every vertical one, right to left as they were listed
        var expected = Enumerable.Range(1, count).Reverse().Select(j => j * 10.0).ToList();
        Assert.All(hops.Take(count), Assert.Empty);
        Assert.All(hops.Skip(count), h => Assert.Equal(expected, h.Select(hop => hop.X)));
    }

    /// <summary>
    ///     Validates the shape of a single hop: a semicircle of the hop radius, as two cubic quarter arcs, over the crossing.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_SingleHop_IsASemicircleOverTheCrossing()
    {
        // Arrange
        Point[] points = [new Point(0, 0), new Point(100, 0)];

        // Act
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(50, 0, 4)]);

        // Assert: line to the foot, up to the apex, down to the other foot, on to the end
        Assert.Equal(5, commands.Count);
        Assert.Multiple(
            () => Assert.Equal(new Point(46, 0), commands[1].End),
            () => Assert.Equal(PathCommandKind.CubicTo, commands[2].Kind),
            () => Assert.Equal(new Point(50, -4), commands[2].End),
            () => Assert.Equal(new Point(46, -4 * 0.5522847498), commands[2].Control1),
            () => Assert.Equal(new Point(50 - (4 * 0.5522847498), -4), commands[2].Control2),
            () => Assert.Equal(new Point(54, 0), commands[3].End),
            () => Assert.Equal(new Point(100, 0), commands[4].End));
    }

    /// <summary>
    ///     Validates that a hop on a wire that runs right to left mirrors correctly.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_RightToLeftWire_HopsOverTheCrossing()
    {
        // Arrange
        Point[] points = [new Point(100, 0), new Point(0, 0)];

        // Act
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(50, 0, 4)]);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(new Point(54, 0), commands[1].End),
            () => Assert.Equal(new Point(50, -4), commands[2].End),
            () => Assert.Equal(new Point(46, 0), commands[3].End),
            () => Assert.Equal(new Point(0, 0), commands[^1].End));
    }

    /// <summary>
    ///     Validates the merge rule: crossings closer than the sum of their radii share one wider hop with a flat top.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_CloseCrossings_MergeIntoOneWiderHop()
    {
        // Arrange: 6 apart with radius 4, so their spans overlap
        Point[] points = [new Point(0, 0), new Point(100, 0)];

        // Act
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(50, 0, 4), new WireHop(56, 0, 4)]);

        // Assert: one hop from 46 to 60, flat on top between x=50 and x=56
        Assert.Equal(2, Cubics(commands));
        Assert.Multiple(
            () => Assert.Equal(new Point(46, 0), commands[1].End),
            () => Assert.Equal(new Point(50, -4), commands[2].End),
            () => Assert.Equal(new Point(56, -4), commands[3].End),
            () => Assert.Equal(PathCommandKind.LineTo, commands[3].Kind),
            () => Assert.Equal(new Point(60, 0), commands[4].End));
    }

    /// <summary>
    ///     Validates that crossings far enough apart each get their own hop.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_DistantCrossings_GetSeparateHops()
    {
        // Arrange
        Point[] points = [new Point(0, 0), new Point(100, 0)];

        // Act
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(70, 0, 4), new WireHop(30, 0, 4)]);

        // Assert: four arcs, in travel order
        var apexes = commands.Where(c => c.Kind == PathCommandKind.CubicTo && c.End.Y < 0).Select(c => c.End.X).ToList();
        Assert.Equal([30, 70], apexes);
    }

    /// <summary>
    ///     Validates that a hop that does not fit on its straight line (too near the start, a corner curve or the arrowhead) is skipped.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_HopsThatDoNotFit_AreSkipped()
    {
        // Arrange: a bend at x=100 with a 6 corner curve, an arrowhead needing 8 at the end of the last run
        Point[] bent = [new Point(0, 0), new Point(100, 0), new Point(100, 100)];
        Point[] straight = [new Point(0, 0), new Point(100, 0)];

        // Act
        var nearCorner = WirePaths.Rounded(bent, 6, hops: [new WireHop(98, 0, 4)]);
        var nearStart = WirePaths.Rounded(straight, 6, hops: [new WireHop(3, 0, 4)]);
        var nearTip = WirePaths.Rounded(straight, 6, endClearance: 8, endTrim: 6, hops: [new WireHop(95, 0, 4)]);
        var clear = WirePaths.Rounded(straight, 6, endClearance: 8, endTrim: 6, hops: [new WireHop(85, 0, 4)]);

        // Assert: the corner curve is the only cubic of the bent wire; the near-tip hop is skipped, the clear one drawn
        Assert.Multiple(
            () => Assert.Equal(1, Cubics(nearCorner)),
            () => Assert.Equal(0, Cubics(nearStart)),
            () => Assert.Equal(0, Cubics(nearTip)),
            () => Assert.Equal(2, Cubics(clear)));
    }

    /// <summary>
    ///     Validates that a crossing is only hopped on a horizontal line, never on a vertical one.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_VerticalRun_NeverHops()
    {
        // Arrange
        Point[] points = [new Point(0, 0), new Point(0, 100)];

        // Act
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(0, 50, 4)]);

        // Assert
        Assert.Equal(0, Cubics(commands));
    }

    /// <summary>
    ///     Validates that dashes follow the hop instead of cutting across it.
    /// </summary>
    [Fact]
    public void WirePaths_Dashed_Hop_DashesFollowTheArc()
    {
        // Arrange
        Point[] points = [new Point(0, 0), new Point(100, 0)];
        var commands = WirePaths.Rounded(points, 6, hops: [new WireHop(50, 0, 4)]);

        // Act
        var dashed = WirePaths.Dashed(commands, [3, 3]);

        // Assert: dash ends lie on the arc, above the line, between the feet of the hop
        var onArc = dashed.Where(c => c.End.Y < -1).ToList();
        Assert.True(onArc.Count >= 2, $"{onArc.Count} dash points on the arc");
        Assert.All(onArc, c => Assert.InRange(c.End.X, 46 - Tolerance, 54 + Tolerance));
    }

    /// <summary>
    ///     Validates that, by default, the horizontal wire of the audit crossing hops over the vertical wire of another net, which stays square.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DifferentNetCrossing_HorizontalWireHopsByDefault()
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get("CrossingDifferentNets");
        var horizontal = config.Wires[0];
        var vertical = config.Wires[1];
        var crossingX = vertical.Route[0].X;
        var y = horizontal.Source.Anchor.Y;

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);
        var hopping = SceneTestModel.For<PathPrimitive>(scene, horizontal).Single(p => p.Fill is null).Commands;
        var plain = SceneTestModel.For<PathPrimitive>(scene, vertical).Single(p => p.Fill is null).Commands;

        // Assert: two arcs meeting at the apex 4 above the wire, over the vertical wire; the vertical wire only has its own corners
        Assert.Multiple(
            () => Assert.True(new DiagramTheme().WireCrossingJumps),
            () => Assert.Equal(4, new DiagramTheme().WireJumpRadius),
            () => Assert.Equal(2, Cubics(hopping)),
            () => Assert.Contains(hopping, c => c.Kind == PathCommandKind.CubicTo && Math.Abs(c.End.X - crossingX) < Tolerance && Math.Abs(c.End.Y - (y - 4)) < Tolerance),
            () => Assert.Equal(2, Cubics(plain)),
            () => Assert.DoesNotContain(plain, c => c.End.Y < vertical.Source.Anchor.Y - 1));
    }

    /// <summary>
    ///     Validates that the theme can switch jumps off, and that touching nets never hop.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_JumpsOffOrTouchingNets_DrawNoHop()
    {
        // Arrange
        var audit = new WiringAudit();
        var crossing = audit.Get("CrossingDifferentNets").Wires[0];
        var touching = audit.Get("TouchingDifferentNets").Wires[0];
        var off = audit.Diagram.Theme with { WireCrossingJumps = false };

        // Act
        var on = SceneTestModel.BuildNoGrid(audit.Page);
        var disabled = SceneBuilder.Build(audit.Page, new SceneOptions { ShowGrid = false, Theme = off });

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, Cubics(SceneTestModel.For<PathPrimitive>(on, crossing).Single(p => p.Fill is null).Commands)),
            () => Assert.Equal(0, Cubics(SceneTestModel.For<PathPrimitive>(disabled, crossing).Single(p => p.Fill is null).Commands)),
            () => Assert.Equal(0, Cubics(SceneTestModel.For<PathPrimitive>(on, touching).Single(p => p.Fill is null).Commands)));
    }

    /// <summary>
    ///     Validates that the hop grows with the wire's thickness and that its dashes follow it.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ThickDashedHoppingWire_ScalesAndDashesTheHop()
    {
        // Arrange: the horizontal wire is 4 thick and dashed
        var audit = new WiringAudit();
        var horizontal = audit.Get("CrossingDifferentNets").Wires[0];
        var y = horizontal.Source.Anchor.Y;
        audit.Diagram.WireStyler = wire => ReferenceEquals(wire, horizontal) ? new WireStyle { Thickness = 4, Dash = [5, 3] } : null;

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);
        var commands = SceneTestModel.For<PathPrimitive>(scene, horizontal).Single(p => p.Fill is null).Commands;

        // Assert: apex 8 above the wire (4 x 2), and dash points on the arc
        Assert.Multiple(
            () => Assert.Contains(commands, c => Math.Abs(c.End.Y - (y - 8)) < 0.5 && c.Kind == PathCommandKind.LineTo),
            () => Assert.Contains(commands, c => c.End.Y < y - 3 && c.End.Y > y - 8 + Tolerance));
    }

    /// <summary>
    ///     Validates that jumps leave the stored route, the displayed polyline and hit testing as straight segments.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Hop_LeavesPolylineAndHitTestingStraight()
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get("CrossingDifferentNets");
        var horizontal = config.Wires[0];
        var y = horizontal.Source.Anchor.Y;
        var crossingX = config.Wires[1].Route[0].X;

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);
        var overHop = scene.HitTest(new Point(crossingX - 20, y));
        var aboveHop = scene.HitTest(new Point(crossingX - 20, y - 7));

        // Assert: two points, and the straight line is hit where the arc is not
        Assert.Multiple(
            () => Assert.True(scene.TryGetWirePoints(horizontal, out var points) && points.Count == 2),
            () => Assert.Empty(horizontal.Route),
            () => Assert.Equal(HitKind.Wire, overHop.Kind),
            () => Assert.Same(horizontal, overHop.Element),
            () => Assert.NotSame(horizontal, aboveHop.Element));
    }

    /// <summary>
    ///     Validates that wires drawn to a collapsed group through a display route take part in crossings like any other wire.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupDisplayRoute_ParticipatesInCrossings()
    {
        // Arrange: a straight wire runs between two columns while a collapsed group's wire has to get across it
        var (_, page) = SceneTestModel.CreatePage();
        var inside = page.AddNode("acme.Producer", 0, 0, "Inside");
        var target = page.AddNode("acme.Consumer", 600, 600, "Target");
        var across = page.AddNode("acme.Producer", -3000, 300, "Across");
        var acrossTarget = page.AddNode("acme.Consumer", 3600, 300, "AcrossTarget");
        var straight = page.Connect(across.Port("value"), acrossTarget.Port("in"), []);
        var grouped = page.Connect(inside.Port("value"), target.Port("in"));
        page.CreateGroup([inside], "G").IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        scene.TryGetWirePoints(straight, out var straightPoints);
        scene.TryGetWirePoints(grouped, out var groupedPoints);
        var expected = WireCrossings.Find(
            [new WireRun(straightPoints!, straight.Source, 2, []), new WireRun(groupedPoints!, grouped.Source, 2, [])],
            4,
            2,
            6,
            3)[0].Count;

        // Assert: the display route crosses the straight wire, which hops once per crossing
        Assert.True(expected >= 1, "the display route does not cross the straight wire");
        Assert.Equal(2 * expected, Cubics(SceneTestModel.For<PathPrimitive>(scene, straight).Single(p => p.Fill is null).Commands));
    }

    /// <summary>
    ///     Validates that jumps are deterministic: the same page draws the same hops every time.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Hops_AreDeterministic()
    {
        // Arrange
        var audit = new WiringAudit();
        var horizontal = audit.Get("CrossingDifferentNets").Wires[0];

        // Act
        var first = SceneTestModel.For<PathPrimitive>(SceneTestModel.BuildNoGrid(audit.Page), horizontal).Single(p => p.Fill is null).Commands;
        var second = SceneTestModel.For<PathPrimitive>(SceneTestModel.BuildNoGrid(audit.Page), horizontal).Single(p => p.Fill is null).Commands;

        // Assert
        Assert.Equal(first, second);
    }
}
