using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Audit tests for wire junctions: dots and square T corners in many net configurations.
/// </summary>
public class WireJunctionAuditTests
{
    /// <summary>
    ///     The tolerance when comparing coordinates.
    /// </summary>
    private const double Tolerance = 1e-6;

    /// <summary>
    ///     Finds the junction dots of a scene: small filled circles without stroke that belong to a wire.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The dots.</returns>
    private static List<RectPrimitive> Dots(DiagramScene scene) =>
        [.. scene.Primitives.OfType<RectPrimitive>().Where(r => r.Element is Wire && r.Fill is not null && r.Stroke is null && r.CornerRadius > 0)];

    /// <summary>
    ///     Gets the stroke of a wire: its solid or dashed line, without highlight or arrowhead.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="wire">The wire.</param>
    /// <returns>The stroke primitive.</returns>
    private static PathPrimitive Stroke(DiagramScene scene, Wire wire) =>
        SceneTestModel.For<PathPrimitive>(scene, wire).Single(p => p.Fill is null && p.Stroke is not null);

    /// <summary>
    ///     Determines whether two points coincide.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><see langword="true"/> when they are within the tolerance of each other.</returns>
    private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < Tolerance && Math.Abs(a.Y - b.Y) < Tolerance;

    /// <summary>
    ///     Determines whether a polyline bends at one of its interior vertices.
    /// </summary>
    /// <param name="points">The polyline.</param>
    /// <param name="i">The index of an interior vertex.</param>
    /// <returns><see langword="true"/> when the segments before and after the vertex are not collinear.</returns>
    private static bool Bends(IReadOnlyList<Point> points, int i)
    {
        var inX = points[i].X - points[i - 1].X;
        var inY = points[i].Y - points[i - 1].Y;
        var outX = points[i + 1].X - points[i].X;
        var outY = points[i + 1].Y - points[i].Y;
        return Math.Abs((inX * outY) - (inY * outX)) > Tolerance;
    }

    /// <summary>
    ///     Validates that each audit configuration draws a junction dot at exactly the expected points, and nowhere else.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    [Theory]
    [InlineData("TOnStraightTrunk")]
    [InlineData("SplitAtCorner")]
    [InlineData("FanOut3")]
    [InlineData("FanOut4")]
    [InlineData("FanInTwoNets")]
    [InlineData("CrossingDifferentNets")]
    [InlineData("TouchingDifferentNets")]
    [InlineData("EndOnTrunk")]
    [InlineData("StyledFanOut")]
    public void SceneBuilder_Build_AuditConfiguration_DrawsDotsExactlyAtJunctions(string name)
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get(name);

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);
        var actual = Dots(scene)
            .Select(d => d.Rect.Center)
            .Where(p => p.Y >= config.Top && p.Y < config.Bottom)
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();

        // Assert
        var expected = config.Dots.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(Near(expected[i], actual[i]), $"dot {i}: expected {expected[i]}, drew {actual[i]}");
        }
    }

    /// <summary>
    ///     Validates that automatically routed wires from two outputs into an input that allows fan-in get a junction dot where they merge, where they merge.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_AutoRoutedFanIn_DrawsMergeDot()
    {
        // Arrange: two producers above and below a merger, routed by the default router
        var (_, page) = SceneTestModel.CreatePage();
        var upper = page.AddNode("acme.Producer", 0, 0, "Up");
        var lower = page.AddNode("acme.Producer", 0, 450, "Down");
        var merger = page.AddNode("acme.Merger", 420, 225, "Merge");
        page.Connect(upper.Port("value"), merger.Port("in"));
        page.Connect(lower.Port("value"), merger.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        var dots = Dots(scene);

        // Assert: the only possible junction is where the two nets merge, and it sits left of the merging input
        var dot = Assert.Single(dots);
        Assert.True(dot.Rect.Center.X < merger.Port("in").Anchor.X);
    }

    /// <summary>
    ///     Validates that a page with only plain single-wire inputs draws no junction dots.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_PlainInputsWithoutFanOut_DrawNoDot()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var first = page.AddNode("acme.Producer", 0, 0, "A");
        var second = page.AddNode("acme.Producer", 0, 300, "B");
        var sinkA = page.AddNode("acme.Consumer", 420, 150, "SA");
        var sinkB = page.AddNode("acme.Consumer", 420, 450, "SB");
        page.Connect(first.Port("value"), sinkA.Port("in"));
        page.Connect(second.Port("value"), sinkB.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Empty(Dots(scene));
    }

    /// <summary>
    ///     Validates that wires of different nets never get a dot, even where they cross or touch.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    [Theory]
    [InlineData("CrossingDifferentNets")]
    [InlineData("TouchingDifferentNets")]
    public void SceneBuilder_Build_DifferentNetConfigurations_DrawNoDot(string name)
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get(name);

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);

        // Assert
        Assert.DoesNotContain(Dots(scene), d => d.Rect.Center.Y >= config.Top && d.Rect.Center.Y < config.Bottom);
    }

    /// <summary>
    ///     Validates that a corner at a junction is drawn square while every other corner stays rounded.
    /// </summary>
    /// <param name="name">The configuration name.</param>
    [Theory]
    [InlineData("TOnStraightTrunk")]
    [InlineData("SplitAtCorner")]
    [InlineData("FanOut3")]
    [InlineData("FanOut4")]
    [InlineData("FanInTwoNets")]
    [InlineData("EndOnTrunk")]
    [InlineData("StyledFanOut")]
    public void SceneBuilder_Build_AuditConfiguration_JunctionCornersAreSquareAndOthersRounded(string name)
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get(name);

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);

        // Assert
        foreach (var wire in config.Wires)
        {
            Assert.True(scene.TryGetWirePoints(wire, out var points));
            var commands = Stroke(scene, wire).Commands;
            var bends = 0;
            var sharp = 0;
            for (var i = 1; i < points.Count - 1; i++)
            {
                if (!Bends(points, i))
                {
                    continue;
                }

                bends++;
                if (config.SharpCorners.Any(c => Near(c, points[i])))
                {
                    sharp++;
                    Assert.Contains(commands, c => c.Kind == PathCommandKind.LineTo && Near(c.End, points[i]));
                }
            }

            // Each bend that is not a junction is one curve; the junction bends add none
            Assert.Equal(bends - sharp, commands.Count(c => c.Kind == PathCommandKind.CubicTo));
        }
    }

    /// <summary>
    ///     Validates that the switch for dots does not change the square T: with dots off the corner is still square.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DotsOff_JunctionCornerStaysSquare()
    {
        // Arrange
        var audit = new WiringAudit();
        var config = audit.Get("SplitAtCorner");
        var theme = audit.Diagram.Theme with { WireJunctionDots = false };

        // Act
        var scene = SceneBuilder.Build(audit.Page, new SceneOptions { ShowGrid = false, Theme = theme });

        // Assert
        Assert.Empty(Dots(scene));
        Assert.All(
            config.Wires,
            wire => Assert.Contains(Stroke(scene, wire).Commands, c => c.Kind == PathCommandKind.LineTo && Near(c.End, config.SharpCorners[0])));
    }

    /// <summary>
    ///     Validates that dashed, thick and colored branches of one net keep the same dots, and that the dots grow with the heaviest wire and follow the owner's color.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StyledNet_KeepsDotsScaledAndColored()
    {
        // Arrange: the first sink's wire is dashed, the second sink's wire is thick and red
        var audit = new WiringAudit();
        var config = audit.Get("StyledFanOut");
        var red = Color.Parse("#FF0000");
        audit.Diagram.WireStyler = wire => wire.Target.Node.Name switch
        {
            "StyledA" => new WireStyle { Dash = [6, 4] },
            "StyledB" => new WireStyle { Color = red, Thickness = 4 },
            _ => null,
        };

        // Act
        var scene = SceneTestModel.BuildNoGrid(audit.Page);
        var dots = Dots(scene).Where(d => d.Rect.Center.Y >= config.Top && d.Rect.Center.Y < config.Bottom).ToList();
        var dashed = Stroke(scene, config.Wires[0]);

        // Assert: two dots, each twice the default radius (thickness 4 against 2), colored like a wire of the net
        Assert.Multiple(
            () => Assert.Equal(2, dots.Count),
            () => Assert.All(dots, d => Assert.Equal(scene.Theme.WireJunctionRadius * 2, d.CornerRadius, 6)),
            () => Assert.All(dots, d => Assert.Contains(d.Fill, new Color?[] { scene.Theme.WireColor, red })),
            () => Assert.True(dashed.Commands.Count(c => c.Kind == PathCommandKind.MoveTo) > 3),
            () => Assert.Equal(config.Wires.Count, config.Wires.Count(w => Stroke(scene, w).Commands.Count > 0)));
    }

    /// <summary>
    ///     Validates that a wire fan-out from a collapsed group's proxy port gets dots and square T corners from its display routes.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupFanOut_DotsAndSquareCornersFollowDisplayRoutes()
    {
        // Arrange: a collapsed group holds the producer; two consumers outside are fed through the proxy
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 150, "P");
        var upper = page.AddNode("acme.Consumer", 600, 0, "C1");
        var lower = page.AddNode("acme.Consumer", 600, 300, "C2");
        var first = page.Connect(producer.Port("value"), upper.Port("in"));
        var second = page.Connect(producer.Port("value"), lower.Port("in"));
        page.CreateGroup([producer], "G").IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: every junction of the displayed wires has a dot and a square corner on each wire that bends there
        Assert.True(scene.TryGetWirePoints(first, out var firstPoints));
        Assert.True(scene.TryGetWirePoints(second, out var secondPoints));
        var junctions = WireJunctions.Find([firstPoints, secondPoints]).Select(j => j.Point).ToList();
        var dots = Dots(scene).Select(d => d.Rect.Center).ToList();
        Assert.NotEmpty(junctions);
        Assert.Equal(junctions.Count, dots.Count);
        foreach (var junction in junctions)
        {
            Assert.Contains(dots, d => Near(d, junction));
            foreach (var (wire, points) in new[] { (first, firstPoints), (second, secondPoints) })
            {
                for (var i = 1; i < points.Count - 1; i++)
                {
                    if (Near(points[i], junction) && Bends(points, i))
                    {
                        Assert.Contains(Stroke(scene, wire).Commands, c => c.Kind == PathCommandKind.LineTo && Near(c.End, junction));
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Validates that building the audit page twice draws exactly the same primitives.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_AuditPage_IsDeterministic()
    {
        // Arrange / Act
        var first = Describe(SceneTestModel.BuildNoGrid(new WiringAudit().Page));
        var second = Describe(SceneTestModel.BuildNoGrid(new WiringAudit().Page));

        // Assert
        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that wires that run together without any branching make no junction: a wire that ends on a bend of another, and a wire that is a prefix of another.
    /// </summary>
    [Fact]
    public void WireJunctions_Find_CollinearOverlapWithoutDivergence_HasNoJunction()
    {
        // Arrange: the second wire is the first part of the first wire, ending on its bend
        List<IReadOnlyList<Point>> wires =
        [
            [new Point(0, 0), new Point(100, 0), new Point(100, 50), new Point(200, 50)],
            [new Point(0, 0), new Point(100, 0)],
        ];

        // Act / Assert
        Assert.Empty(WireJunctions.Find(wires));
    }

    /// <summary>
    ///     Validates that a wire ending exactly on another wire's straight run is a junction at that point.
    /// </summary>
    [Fact]
    public void WireJunctions_Find_WireEndingOnTrunk_IsAJunctionAtTheEnd()
    {
        // Arrange: the second wire arrives horizontally at (100,25), in the middle of the first wire's vertical run
        List<IReadOnlyList<Point>> wires =
        [
            [new Point(0, 0), new Point(100, 0), new Point(100, 50), new Point(200, 50)],
            [new Point(0, 0), new Point(0, 25), new Point(100, 25)],
        ];

        // Act
        var junctions = WireJunctions.Find(wires).Select(j => j.Point).ToList();

        // Assert
        Assert.Contains(new Point(100, 25), junctions);
    }

    /// <summary>
    ///     Validates that wires crossing as a "+" within one net are a junction, and that the result does not depend on how often it is asked.
    /// </summary>
    [Fact]
    public void WireJunctions_Find_PlusOfOneNet_IsAJunctionAndStable()
    {
        // Arrange: a "+" made of two wires of one net has four directions at the crossing; asked twice
        List<IReadOnlyList<Point>> wires =
        [
            [new Point(0, 50), new Point(200, 50)],
            [new Point(100, 0), new Point(100, 50), new Point(100, 100)],
        ];

        // Act
        var first = WireJunctions.Find(wires);
        var second = WireJunctions.Find(wires);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(new Point(100, 50), Assert.Single(first).Point);
    }

    /// <summary>
    ///     Validates that a square junction corner is drawn with no curve and that the neighboring corner then uses its whole segment.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_SharpCorner_HasNoCurveAndLetsTheNextCornerUseTheSegment()
    {
        // Arrange: the first corner (100,0) is a junction; the next corner is only 8 away, where 4 would have been the limit
        Point[] points = [new Point(0, 0), new Point(100, 0), new Point(100, 8), new Point(200, 8)];

        // Act
        var commands = WirePaths.Rounded(points, 6, sharpCorners: [new Point(100, 0)]);

        // Assert: a square corner at (100,0), one quarter arc of the full radius at (100,8)
        var curve = Assert.Single(commands, c => c.Kind == PathCommandKind.CubicTo);
        Assert.Multiple(
            () => Assert.Contains(commands, c => c.Kind == PathCommandKind.LineTo && c.End == new Point(100, 0)),
            () => Assert.Equal(new Point(106, 8), curve.End));
    }

    /// <summary>
    ///     Validates that a junction corner takes no part in small-jog smoothing, so the T stays square.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_SharpCornerOnSmallJog_IsNotSmoothed()
    {
        // Arrange: a 7.5 step would become an S-curve, but its first corner is a junction
        Point[] points = [new Point(0, 0), new Point(50, 0), new Point(50, 7.5), new Point(100, 7.5)];

        // Act
        var commands = WirePaths.Rounded(points, 6, smoothSmallJogs: true, sharpCorners: [new Point(50, 0)]);

        // Assert: the square corner is kept and no cubic starts before the step
        Assert.Contains(commands, c => c.Kind == PathCommandKind.LineTo && c.End == new Point(50, 0));
        Assert.DoesNotContain(commands, c => c.Kind == PathCommandKind.CubicTo && c.Control1.X < 50 - Tolerance);
    }

    /// <summary>
    ///     Describes every primitive of a scene as text, for comparing two scenes.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>One line per path or dot primitive.</returns>
    private static List<string> Describe(DiagramScene scene)
    {
        var lines = new List<string>();
        foreach (var primitive in scene.Primitives)
        {
            switch (primitive)
            {
                case PathPrimitive path:
                    lines.Add("path " + string.Join(';', path.Commands.Select(c => $"{c.Kind}:{c.End.X:R},{c.End.Y:R},{c.Control1.X:R},{c.Control1.Y:R}")));
                    break;
                case RectPrimitive { Element: Wire } rect:
                    lines.Add($"rect {rect.Rect.X:R},{rect.Rect.Y:R},{rect.Rect.Width:R},{rect.CornerRadius:R}");
                    break;
            }
        }

        return lines;
    }
}
