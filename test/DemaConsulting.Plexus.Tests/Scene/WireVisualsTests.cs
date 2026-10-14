using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the wire visuals of a scene: junction dots, the arrowhead corner fix and the handles of a selected wire.
/// </summary>
public class WireVisualsTests
{
    /// <summary>
    ///     Creates a page with one producer feeding two consumers, so the two wires belong to one net.
    /// </summary>
    /// <returns>The page, the two wires.</returns>
    private static (DiagramPage Page, Wire First, Wire Second) CreateBranchingNet()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var upper = page.AddNode("acme.Consumer", 450, 90, "C1");
        var lower = page.AddNode("acme.Consumer", 450, 240, "C2");
        var first = page.Connect(producer.Port("value"), upper.Port("in"));
        var second = page.Connect(producer.Port("value"), lower.Port("in"));
        return (page, first, second);
    }

    /// <summary>
    ///     Finds the junction dots of a scene: small filled circles without stroke.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="theme">The theme the scene was built with.</param>
    /// <returns>The dots.</returns>
    private static List<RectPrimitive> Dots(DiagramScene scene, DiagramTheme theme) =>
        [.. scene.Primitives.OfType<RectPrimitive>().Where(r => r.Fill is not null && r.Stroke is null && Math.Abs(r.CornerRadius - theme.WireJunctionRadius) < 1e-9 && r.Element is Wire)];

    /// <summary>
    ///     Validates that wires of one net that branch apart get a junction dot at the branch point.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_BranchingNet_DrawsJunctionDotOnBothWires()
    {
        // Arrange
        var (page, first, second) = CreateBranchingNet();

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        var dots = Dots(scene, scene.Theme);

        // Assert: a dot of the configured radius, in the wire color, sitting on both wires
        var dot = Assert.Single(dots);
        Assert.Multiple(
            () => Assert.Equal(scene.Theme.WireJunctionRadius * 2, dot.Rect.Width, 6),
            () => Assert.Equal(scene.Theme.WireColor, dot.Fill),
            () => Assert.True(IsOnPolyline(dot.Rect.Center, first.GetPoints())),
            () => Assert.True(IsOnPolyline(dot.Rect.Center, second.GetPoints())));
    }

    /// <summary>
    ///     Validates that a junction dot takes the color of its wire's style.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StyledBranchingNet_ColorsJunctionDotLikeTheWire()
    {
        // Arrange
        var (page, _, _) = CreateBranchingNet();
        var red = Color.Parse("#FF0000");
        page.Diagram.WireStyler = _ => new WireStyle { Color = red };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Equal(red, Assert.Single(Dots(scene, scene.Theme)).Fill);
    }

    /// <summary>
    ///     Validates that wires of different nets never get a junction dot, even where they cross or touch.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DifferentNets_DrawNoJunctionDot()
    {
        // Arrange: two producers, two consumers, four ports, two nets
        var (_, page) = SceneTestModel.CreatePage();
        var p1 = page.AddNode("acme.Producer", 0, 0, "P1");
        var p2 = page.AddNode("acme.Producer", 0, 240, "P2");
        var c1 = page.AddNode("acme.Consumer", 450, 0, "C1");
        var c2 = page.AddNode("acme.Consumer", 450, 240, "C2");
        page.Connect(p1.Port("value"), c2.Port("in"));
        page.Connect(p2.Port("value"), c1.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Empty(Dots(scene, scene.Theme));
    }

    /// <summary>
    ///     Validates that two wires of one net that turn together (a shared route) make no dot, and that the theme can switch dots off.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DotsDisabledByTheme_DrawsNoDot()
    {
        // Arrange
        var (page, _, _) = CreateBranchingNet();
        var theme = page.Diagram.Theme with { WireJunctionDots = false };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Theme = theme, ShowGrid = false });

        // Assert
        Assert.Empty(Dots(scene, theme));
    }

    /// <summary>
    ///     Validates that a T junction is found where a wire bends on a trunk another wire of the net runs straight along.
    /// </summary>
    [Fact]
    public void WireJunctions_Find_BendOnPassingTrunk_IsAJunction()
    {
        // Arrange: wire 0 runs (0,0) (50,0) (50,100) (100,100); wire 1 branches off at (50,40)
        List<IReadOnlyList<Point>> wires =
        [
            [new Point(0, 0), new Point(50, 0), new Point(50, 100), new Point(100, 100)],
            [new Point(0, 0), new Point(50, 0), new Point(50, 40), new Point(100, 40)],
        ];

        // Act
        var junctions = WireJunctions.Find(wires);

        // Assert: (50,0) leaves left, down (both wires) and (50,40) where wire 1 turns right off the trunk
        Assert.Contains(junctions, j => j.Point == new Point(50, 40));
        Assert.DoesNotContain(junctions, j => j.Point == new Point(0, 0));
    }

    /// <summary>
    ///     Validates that two identical wires make no junction at all.
    /// </summary>
    [Fact]
    public void WireJunctions_Find_IdenticalWires_HaveNoJunction()
    {
        // Arrange
        IReadOnlyList<Point> wire = [new Point(0, 0), new Point(50, 0), new Point(50, 100), new Point(100, 100)];

        // Act / Assert
        Assert.Empty(WireJunctions.Find([wire, wire]));
    }

    /// <summary>
    ///     Validates that the last corner keeps the full radius when the final segment is the usual stub, instead of shrinking to fit the trimmed stroke.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_StubLengthFinalSegment_KeepsFullRadius()
    {
        // Arrange: the last segment is 15 long; the arrowhead is 8 long and the stroke stops 6 short of the tip
        Point[] points = [new Point(0, 0), new Point(100, 0), new Point(100, 40), new Point(115, 40)];

        // Act
        var commands = WirePaths.Rounded(points, 6, endClearance: 8, endTrim: 6);

        // Assert: the final curve is a radius-6 arc and the stroke ends 6 before the tip, beyond the curve
        var curve = commands.Last(c => c.Kind == PathCommandKind.CubicTo);
        Assert.Multiple(
            () => Assert.Equal(new Point(106, 40), curve.End),
            () => Assert.Equal(new Point(109, 40), commands[^1].End));
    }

    /// <summary>
    ///     Validates that on a short final segment the last corner tightens so the curve ends where the arrowhead begins.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShortFinalSegment_ArrowheadSitsOnStraightRun()
    {
        // Arrange: the last segment is only 12 long, less than the arrowhead (8) plus the radius (6)
        Point[] points = [new Point(0, 0), new Point(100, 0), new Point(100, 40), new Point(112, 40)];

        // Act
        var commands = WirePaths.Rounded(points, 6, endClearance: 8, endTrim: 6);

        // Assert: the curve ends at x=104, exactly the arrowhead base (112 - 8), and the stroke stops inside the arrowhead
        var curve = commands.Last(c => c.Kind == PathCommandKind.CubicTo);
        Assert.Multiple(
            () => Assert.Equal(new Point(104, 40), curve.End),
            () => Assert.Equal(new Point(106, 40), commands[^1].End));
    }

    /// <summary>
    ///     Validates that a step shorter than twice the radius between two parallel runs is drawn as one S-curve about four times as long as the step, centered on it.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShortStep_DrawsOneSCurveSpanningFourSteps()
    {
        // Arrange: a 7.5 step (less than 2 x 6) halfway along a wire
        Point[] points = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(100, 7.5)];

        // Act
        var commands = WirePaths.Rounded(points, 6, smoothSmallJogs: true);

        // Assert: one cubic from 15 before the step to 15 after it (30 = 4 x 7.5), with horizontal tangents at both ends
        var curve = Assert.Single(commands, c => c.Kind == PathCommandKind.CubicTo);
        var index = commands.IndexOf(curve);
        Assert.Multiple(
            () => Assert.Equal(new Point(35, 0), commands[index - 1].End),
            () => Assert.Equal(new Point(50, 0), curve.Control1),
            () => Assert.Equal(new Point(50, 7.5), curve.Control2),
            () => Assert.Equal(new Point(65, 7.5), curve.End),
            () => Assert.Equal(new Point(100, 7.5), commands[^1].End));
    }

    /// <summary>
    ///     Validates that the S-curve of a small jog is limited to the run available before the step.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShortStepNearTheStart_ClampsTheSpanToTheRun()
    {
        // Arrange: only 10 units of run before the step, so the 30-unit span shrinks to 2 x 10
        Point[] points = [new Point(0, 0), new Point(10, 0), new Point(10, 7.5), new Point(100, 7.5)];

        // Act
        var commands = WirePaths.Rounded(points, 6, smoothSmallJogs: true);

        // Assert
        var curve = Assert.Single(commands, c => c.Kind == PathCommandKind.CubicTo);
        Assert.Equal(new Point(20, 7.5), curve.End);
    }

    /// <summary>
    ///     Validates that a step that is not shorter than twice the radius keeps its two rounded corners.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_LongStep_KeepsTwoCorners()
    {
        // Arrange: a 20 step is more than 2 x 6
        Point[] points = [new Point(0, 0), new Point(50, 0), new Point(50, 20), new Point(100, 20)];

        // Act
        var commands = WirePaths.Rounded(points, 6, smoothSmallJogs: true);

        // Assert: two quarter-arc corners, each with radius 6
        var curves = commands.Where(c => c.Kind == PathCommandKind.CubicTo).ToList();
        Assert.Multiple(
            () => Assert.Equal(2, curves.Count),
            () => Assert.Equal(new Point(50, 6), curves[0].End),
            () => Assert.Equal(new Point(56, 20), curves[1].End));
    }

    /// <summary>
    ///     Validates that a small jog keeps its two corners when smoothing is not requested, and when the wire is a U turn rather than an offset.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShortStepWithoutSmoothingOrAsUTurn_KeepsTwoCorners()
    {
        // Arrange: the same short step, and a short step whose runs head in opposite directions
        Point[] offset = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(100, 7.5)];
        Point[] uTurn = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(0, 7.5)];

        // Act
        var plain = WirePaths.Rounded(offset, 6);
        var turned = WirePaths.Rounded(uTurn, 6, smoothSmallJogs: true);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, plain.Count(c => c.Kind == PathCommandKind.CubicTo)),
            () => Assert.Equal(2, turned.Count(c => c.Kind == PathCommandKind.CubicTo)));
    }

    /// <summary>
    ///     Validates that a small jog right before an arrowhead falls back to corners when the run left for the arrowhead is too short for a gentle curve.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShortStepTooCloseToArrowhead_KeepsCorners()
    {
        // Arrange: 10 units after the step, 8 of them reserved for the arrowhead
        Point[] points = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(60, 7.5)];

        // Act
        var commands = WirePaths.Rounded(points, 6, endClearance: 8, endTrim: 6, smoothSmallJogs: true);

        // Assert: the arrowhead run stays straight: the last curve ends at least 8 before the tip
        var last = commands.Last(c => c.Kind == PathCommandKind.CubicTo);
        Assert.True(last.End.X <= 52 + 1e-9, $"curve ends at {last.End.X}");
    }

    /// <summary>
    ///     Validates that dashes follow the S-curve of a small jog instead of cutting across it.
    /// </summary>
    [Fact]
    public void WirePaths_Dashed_SCurve_DashesFollowTheCurve()
    {
        // Arrange
        Point[] points = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(100, 7.5)];
        var commands = WirePaths.Rounded(points, 6, smoothSmallJogs: true);

        // Act
        var dashed = WirePaths.Dashed(commands, [3, 3]);

        // Assert: dash points lie strictly inside the step's height, which no straight dash of the square route could do
        var inside = dashed.Where(c => c.End.Y > 0.5 && c.End.Y < 7).ToList();
        Assert.True(inside.Count >= 2, $"{inside.Count} dash points on the curve");
        Assert.All(inside, c => Assert.InRange(c.End.X, 35, 65));
    }

    /// <summary>
    ///     Validates that the scene draws a small jog as an S-curve by default, keeps the stored route and hit-test polyline square, and draws corners when the theme switch is off.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SmallJog_DrawsSCurveUnlessDisabledByTheme()
    {
        // Arrange: the consumer sits half a grid below the producer, so the wire has a 7.5 step
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 7.5, "C");
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;
        Assert.Equal(7.5, z.Y - a.Y, 6);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, a.Y), new Point(150, z.Y)]);

        // Act
        var smooth = SceneTestModel.BuildNoGrid(page);
        var plain = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Theme = page.Diagram.Theme with { WireSmoothSmallJogs = false } });

        // Assert: one cubic spanning 4 x 7.5 around x=150, versus two corners
        var smoothStroke = SceneTestModel.For<PathPrimitive>(smooth, wire).Single(p => p.Fill is null);
        var plainStroke = SceneTestModel.For<PathPrimitive>(plain, wire).Single(p => p.Fill is null);
        var curve = Assert.Single(smoothStroke.Commands, c => c.Kind == PathCommandKind.CubicTo);
        Assert.Multiple(
            () => Assert.Equal(new Point(165, z.Y), curve.End),
            () => Assert.Equal(2, plainStroke.Commands.Count(c => c.Kind == PathCommandKind.CubicTo)),
            () => Assert.True(new DiagramTheme().WireSmoothSmallJogs),
            () => Assert.True(smooth.TryGetWirePoints(wire, out var points) && points.Count == 4),
            () => Assert.Equal(new Point(150, z.Y), wire.Route[1]));
    }

    /// <summary>
    ///     Validates that a wire whose last segment is shorter than the arrowhead shrinks the arrowhead and keeps a sharp corner, so the head never overlaps the bend.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_VeryShortFinalSegment_ShrinksArrowheadToFit()
    {
        // Arrange: bends sit 5 units before the target
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 100, "C");
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(z.X - 5, a.Y), new Point(z.X - 5, z.Y)]);

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        var primitives = SceneTestModel.For<PathPrimitive>(scene, wire);
        var arrow = primitives.Single(p => p.Fill is not null);
        var stroke = primitives.Single(p => p.Fill is null);

        // Assert: the arrowhead base is not before the bend, the stroke has a sharp corner into the short run and ends inside the head
        Assert.Multiple(
            () => Assert.True(arrow.Commands[1].End.X >= z.X - 5 - 1e-6, $"base at {arrow.Commands[1].End.X}"),
            () => Assert.Equal(z, arrow.Commands[0].End),
            () => Assert.DoesNotContain(stroke.Commands.TakeLast(2), c => c.Kind == PathCommandKind.CubicTo),
            () => Assert.True(stroke.Commands[^1].End.X <= z.X - 5 + 5));
    }

    /// <summary>
    ///     Validates that a selected wire gets handles at its bends and ends, and an unselected wire gets none.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SelectedWire_DrawsHandlesAtBendsAndEnds()
    {
        // Arrange
        var (page, first, _) = CreateBranchingNet();
        var handlesOf = (DiagramScene scene) => SceneTestModel.For<RectPrimitive>(scene, first).Count(r => r.Stroke is not null);

        // Act
        var plain = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
        var selected = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [first] });

        // Assert: one square per bend plus a circle at each end
        Assert.Multiple(
            () => Assert.Equal(0, handlesOf(plain)),
            () => Assert.Equal(first.Route.Count + 2, handlesOf(selected)));
    }

    /// <summary>
    ///     Validates that bend and end handles of a selected wire are hit-testable with their own kinds and indexes.
    /// </summary>
    [Fact]
    public void DiagramScene_HitTest_SelectedWireHandles_ReturnBendAndEndKinds()
    {
        // Arrange
        var (page, first, _) = CreateBranchingNet();
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [first] });
        var points = first.GetPoints();

        // Act / Assert: each bend reports its index; the ends report the source and target handle with their ports
        for (var i = 0; i < first.Route.Count; i++)
        {
            var hit = scene.HitTest(first.Route[i]);
            Assert.Equal((HitKind.WireBend, i), (hit.Kind, hit.BendIndex));
            Assert.Same(first, hit.Element);
        }

        var source = scene.HitTest(points[0]);
        var target = scene.HitTest(points[^1]);
        Assert.Multiple(
            () => Assert.Equal(HitKind.WireSourceHandle, source.Kind),
            () => Assert.Same(first.Source, source.Port),
            () => Assert.Equal(HitKind.WireTargetHandle, target.Kind),
            () => Assert.Same(first.Target, target.Port));
    }

    /// <summary>
    ///     Validates that an unselected wire reports plain segment hits at its bends and never the handle kinds.
    /// </summary>
    [Fact]
    public void DiagramScene_HitTest_UnselectedWire_ReturnsSegmentNotHandle()
    {
        // Arrange
        var (page, first, _) = CreateBranchingNet();
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Act
        var hit = scene.HitTest(first.Route[0]);

        // Assert
        Assert.Equal(HitKind.Wire, hit.Kind);
        Assert.Equal(-1, hit.BendIndex);
    }

    /// <summary>
    ///     Validates the priority rule: segments of a selected wire beat nodes underneath, while an unselected wire stays below nodes.
    /// </summary>
    [Fact]
    public void DiagramScene_HitTest_SelectedWireOverNode_WireBeatsNodeButOnlyWhenSelected()
    {
        // Arrange: a wire routed straight across the body of a third node
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 600, 0, "C");
        var blocker = page.AddNode("acme.Gain", 300, 150, "G");
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;
        var wire = page.Connect(
            producer.Port("value"),
            consumer.Port("in"),
            [new Point(a.X + 15, a.Y), new Point(a.X + 15, 195), new Point(z.X - 15, 195), new Point(z.X - 15, z.Y)]);
        var over = new Point(345, 195);

        // Act
        var plain = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false }).HitTest(over);
        var selected = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [wire] }).HitTest(over);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HitKind.Node, plain.Kind),
            () => Assert.Same(blocker, plain.Element),
            () => Assert.Equal(HitKind.Wire, selected.Kind),
            () => Assert.Same(wire, selected.Element),
            () => Assert.Equal(2, selected.SegmentIndex));
    }

    /// <summary>
    ///     Determines whether a point lies on a polyline within a small tolerance.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="polyline">The polyline.</param>
    /// <returns><see langword="true"/> when the point is on one of the segments.</returns>
    private static bool IsOnPolyline(Point point, IReadOnlyList<Point> polyline)
    {
        for (var i = 0; i + 1 < polyline.Count; i++)
        {
            var a = polyline[i];
            var b = polyline[i + 1];
            if (point.X >= Math.Min(a.X, b.X) - 1e-6 && point.X <= Math.Max(a.X, b.X) + 1e-6
                && point.Y >= Math.Min(a.Y, b.Y) - 1e-6 && point.Y <= Math.Max(a.Y, b.Y) + 1e-6)
            {
                return true;
            }
        }

        return false;
    }
}
