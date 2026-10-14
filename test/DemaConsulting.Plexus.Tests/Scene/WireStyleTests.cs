namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for dynamic wire styling: <see cref="WireStyle"/>, <see cref="Diagram.WireStyler"/>,
///     <see cref="WireStylers"/> and the way the scene builder applies them, including core-side dashing.
/// </summary>
public class WireStyleTests
{
    /// <summary>
    ///     Validates that a diagram without a styler draws wires from the theme exactly as before.
    /// </summary>
    [Fact]
    public void Diagram_WireStyler_Default_IsNullAndWiresUseTheme()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Null(diagram.WireStyler);
        var stroke = StrokedPath(scene, wire);
        Assert.Equal(diagram.Theme.WireColor, stroke.Stroke);
        Assert.Equal(diagram.Theme.WireThickness, stroke.StrokeThickness);
        Assert.Single(stroke.Commands, c => c.Kind == PathCommandKind.MoveTo);
        var arrow = Assert.Single(SceneTestModel.For<PathPrimitive>(scene, wire), p => p.Fill is not null);
        Assert.Equal(diagram.Theme.WireColor, arrow.Fill);
    }

    /// <summary>
    ///     Validates that a styler's color, thickness and arrowhead reach the stroke and the arrowhead.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StylerColorAndThickness_AreApplied()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        var red = Color.Parse("#FF0000");
        diagram.WireStyler = _ => new WireStyle { Color = red, Thickness = 5 };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: stroke and arrowhead take the color; the head grows with the thickness
        var stroke = StrokedPath(scene, wire);
        Assert.Equal(red, stroke.Stroke);
        Assert.Equal(5, stroke.StrokeThickness);
        var arrow = Assert.Single(SceneTestModel.For<PathPrimitive>(scene, wire), p => p.Fill is not null);
        Assert.Equal(red, arrow.Fill);
        Assert.True(arrow.Bounds.Height > diagram.Theme.WireArrowWidth);
    }

    /// <summary>
    ///     Validates that members a style leaves null fall back to the theme and a null style changes nothing.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NullMembersAndNullStyle_FallBackToTheme()
    {
        // Arrange: only the thickness is styled
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = _ => new WireStyle { Thickness = 4 };

        // Act / Assert: color and arrowhead come from the theme
        var scene = SceneTestModel.BuildNoGrid(page);
        var stroke = StrokedPath(scene, wire);
        Assert.Equal(diagram.Theme.WireColor, stroke.Stroke);
        Assert.Equal(4, stroke.StrokeThickness);
        Assert.Single(SceneTestModel.For<PathPrimitive>(scene, wire), p => p.Fill is not null);

        // A null style is the same as no styler
        diagram.WireStyler = _ => null;
        var plain = StrokedPath(SceneTestModel.BuildNoGrid(page), wire);
        Assert.Equal(diagram.Theme.WireThickness, plain.StrokeThickness);
    }

    /// <summary>
    ///     Validates that a style can switch the arrowhead off and, when the theme has none, on.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StylerArrowhead_OverridesTheme()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = _ => new WireStyle { Arrowhead = false };

        // Act / Assert: no filled head, and the line runs all the way to the anchor
        var scene = SceneTestModel.BuildNoGrid(page);
        Assert.DoesNotContain(SceneTestModel.For<PathPrimitive>(scene, wire), p => p.Fill is not null);
        Assert.Equal(wire.Target.Anchor, StrokedPath(scene, wire).Commands[^1].End);

        // The same style turns the head on for a theme without heads
        diagram.Theme = diagram.Theme with { WireArrowheads = false };
        diagram.WireStyler = _ => new WireStyle { Arrowhead = true };
        Assert.Single(SceneTestModel.For<PathPrimitive>(SceneTestModel.BuildNoGrid(page), wire), p => p.Fill is not null);
    }

    /// <summary>
    ///     Validates that a dash pattern splits a straight wire into figures of the requested lengths and spacing.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StylerDash_SplitsWireIntoFigures()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = _ => new WireStyle { Dash = [10, 5], Arrowhead = false };

        // Act
        var stroke = StrokedPath(SceneTestModel.BuildNoGrid(page), wire);

        // Assert: several figures, each 10 long and starting 15 after the previous one
        var figures = Figures(stroke.Commands);
        Assert.True(figures.Count > 5);
        var start = wire.Source.Anchor;
        for (var i = 0; i < figures.Count - 1; i++)
        {
            Assert.Equal(start.X + (15.0 * i), figures[i][0].X, 6);
            Assert.Equal(10, Length(figures[i]), 6);
        }

        // The pattern is cut at the end of the wire: the last dash stops within one gap of the anchor
        Assert.InRange(figures[^1][^1].X, wire.Target.Anchor.X - 5 - 1e-6, wire.Target.Anchor.X + 1e-6);
        Assert.True(Length(figures[^1]) <= 10 + 1e-6);
    }

    /// <summary>
    ///     Validates that an odd-length pattern repeats like SVG, so <c>[8]</c> means 8 on and 8 off.
    /// </summary>
    [Fact]
    public void WireStyle_Dash_OddLength_RepeatsPattern()
    {
        // Arrange
        var style = new WireStyle { Dash = [8] };

        // Act
        var effective = style.GetEffectiveDash();

        // Assert
        Assert.Equal([8, 8], effective);
        Assert.Empty(new WireStyle().GetEffectiveDash());
        Assert.Empty(new WireStyle { Dash = [] }.GetEffectiveDash());
        Assert.Equal([1, 2, 3, 1, 2, 3], new WireStyle { Dash = [1, 2, 3] }.GetEffectiveDash());
    }

    /// <summary>
    ///     Validates that a dash running around a corner stays one figure and that the lengths add up.
    /// </summary>
    [Fact]
    public void WirePaths_Dashed_AroundCorner_KeepsDashContinuous()
    {
        // Arrange: right 25 then down 40, sharp corner
        var commands = WirePaths.Rounded([new Point(0, 0), new Point(25, 0), new Point(25, 40)], 0);

        // Act
        var dashed = WirePaths.Dashed(commands, [10, 10]);

        // Assert: on 0-10, off 10-20, on 20-30 (through the corner), off 30-40, on 40-50, off 50-60, on 60-65
        var figures = Figures(dashed);
        Assert.Equal(4, figures.Count);
        Assert.Equal([new Point(20, 0), new Point(25, 0), new Point(25, 5)], figures[1]);
        Assert.Equal(35, figures.Sum(Length), 6);
    }

    /// <summary>
    ///     Validates that curved (rounded) corners are followed by the dashes and nothing is lost.
    /// </summary>
    [Fact]
    public void WirePaths_Dashed_RoundedCorner_CoversExpectedLength()
    {
        // Arrange
        var commands = WirePaths.Rounded([new Point(0, 0), new Point(100, 0), new Point(100, 100)], 20);

        // Act: a pattern with equal on and off lengths covers roughly half of the path
        var dashed = WirePaths.Dashed(commands, [4, 4]);

        // Assert
        var total = 160 + (Math.PI * 20 / 2);
        var covered = Figures(dashed).Sum(Length);
        Assert.InRange(covered, (total / 2) - 6, (total / 2) + 6);
        Assert.All(Figures(dashed), f => Assert.True(f.Count >= 2));
    }

    /// <summary>
    ///     Validates that an absurdly fine pattern leaves the wire solid instead of emitting millions of dashes.
    /// </summary>
    [Fact]
    public void WirePaths_Dashed_TooFinePattern_LeavesPathSolid()
    {
        // Arrange
        var commands = WirePaths.Rounded([new Point(0, 0), new Point(500, 0)], 0);

        // Act
        var dashed = WirePaths.Dashed(commands, [0.001, 0.001]);

        // Assert
        Assert.Equal(commands, dashed);
    }

    /// <summary>
    ///     Validates the style validation rules.
    /// </summary>
    [Fact]
    public void WireStyle_Validation_RejectsNonPositiveValues()
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Thickness = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Thickness = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Thickness = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Dash = [4, 0] });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Dash = [-2, 4] });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WireStyle { Dash = [double.PositiveInfinity] });
        Assert.Null(new WireStyle { Thickness = null, Dash = null }.Thickness);
    }

    /// <summary>
    ///     Validates that the style copies the caller's dash list.
    /// </summary>
    [Fact]
    public void WireStyle_Dash_IsCopied()
    {
        // Arrange
        var list = new List<double> { 3, 3 };
        var style = new WireStyle { Dash = list };

        // Act
        list[0] = 99;

        // Assert
        Assert.Equal([3, 3], style.Dash);
    }

    /// <summary>
    ///     Validates that <see cref="WireStylers.ByPortType"/> colors a wire with its source port's type color.
    /// </summary>
    [Fact]
    public void WireStylers_ByPortType_UsesSourcePortTypeColor()
    {
        // Arrange: an f32 wire and an i32 wire
        var (diagram, page) = SceneTestModel.CreatePage();
        var f32 = page.Connect(page.AddNode("acme.Producer", 0, 0, "P").Port("value"), page.AddNode("acme.Consumer", 300, 0, "C").Port("in"));
        var i32 = page.Connect(page.AddNode("acme.Iconic", 0, 200, "I").Port("out"), page.AddNode("acme.IntConsumer", 300, 200, "J").Port("in"));
        diagram.WireStyler = WireStylers.ByPortType();

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Equal(Color.Parse("#FF8C00"), StrokedPath(scene, f32).Stroke);
        Assert.Equal(Color.Parse("#1E90FF"), StrokedPath(scene, i32).Stroke);
    }

    /// <summary>
    ///     Validates that changing a property of a connected node changes the wire style on the next build.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_PropertyChangeOnConnectedNode_RestylesWire()
    {
        // Arrange: wires from a producer are dashed while its Enabled property is false
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = WireStylers.BySourceNodeProperty(
            "Enabled",
            value => value is false ? new WireStyle { Dash = [6, 4], Color = Color.Parse("#808080") } : null);
        Assert.Single(Figures(StrokedPath(SceneTestModel.BuildNoGrid(page), wire).Commands));

        // Act
        wire.Source.Node.Properties["Enabled"] = false;
        var dashed = StrokedPath(SceneTestModel.BuildNoGrid(page), wire);

        // Assert
        Assert.True(Figures(dashed.Commands).Count > 5);
        Assert.Equal(Color.Parse("#808080"), dashed.Stroke);

        // And back again
        wire.Source.Node.Properties["Enabled"] = true;
        Assert.Single(Figures(StrokedPath(SceneTestModel.BuildNoGrid(page), wire).Commands));
    }

    /// <summary>
    ///     Validates the target-node property helper and a node without the property.
    /// </summary>
    [Fact]
    public void WireStylers_ByTargetNodeProperty_ReadsTargetNode()
    {
        // Arrange
        var (diagram, _, wire) = CreateWiredPage();
        object? seen = "unset";
        var styler = WireStylers.ByTargetNodeProperty("Enabled", value =>
        {
            seen = value;
            return new WireStyle { Thickness = 3 };
        });

        // Act: the consumer has no Enabled property
        var style = styler(wire);

        // Assert
        Assert.Null(seen);
        Assert.Equal(3, style?.Thickness);
        Assert.Null(WireStylers.When(_ => false, new WireStyle { Thickness = 3 })(wire));
        Assert.Equal(3, WireStylers.When(w => ReferenceEquals(w, wire), new WireStyle { Thickness = 3 })(wire)?.Thickness);
        Assert.NotNull(diagram);
    }

    /// <summary>
    ///     Validates that later stylers override the members of earlier ones while keeping the others.
    /// </summary>
    [Fact]
    public void WireStylers_Combine_LaterStylersOverrideEarlierNonNullMembers()
    {
        // Arrange
        var (_, _, wire) = CreateWiredPage();
        var combined = WireStylers.Combine(
            _ => new WireStyle { Color = Color.Parse("#FF0000"), Thickness = 3 },
            _ => null,
            _ => new WireStyle { Color = Color.Parse("#00FF00"), Dash = [4, 4] });

        // Act
        var style = combined(wire);

        // Assert
        Assert.NotNull(style);
        Assert.Equal(Color.Parse("#00FF00"), style.Color);
        Assert.Equal(3, style.Thickness);
        Assert.Equal([4, 4], style.Dash);
        Assert.Null(style.Arrowhead);
        Assert.Null(WireStylers.Combine(_ => null, _ => null)(wire));
        Assert.Null(WireStylers.Combine()(wire));
    }

    /// <summary>
    ///     Validates the argument checks of the styler helpers.
    /// </summary>
    [Fact]
    public void WireStylers_NullArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => WireStylers.BySourceNodeProperty(null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => WireStylers.BySourceNodeProperty("X", null!));
        Assert.Throws<ArgumentNullException>(() => WireStylers.ByTargetNodeProperty(null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => WireStylers.ByTargetNodeProperty("X", null!));
        Assert.Throws<ArgumentNullException>(() => WireStylers.When(null!, new WireStyle()));
        Assert.Throws<ArgumentNullException>(() => WireStylers.When(_ => true, null!));
        Assert.Throws<ArgumentNullException>(() => WireStylers.Combine(null!));
        Assert.Throws<ArgumentNullException>(() => WireStylers.Combine(_ => null, null!));
    }

    /// <summary>
    ///     Validates that a styler that throws does not break scene building and the wire falls back to the theme.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StylerThrows_FallsBackToTheme()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = _ => throw new InvalidOperationException("host bug");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var stroke = StrokedPath(scene, wire);
        Assert.Equal(diagram.Theme.WireColor, stroke.Stroke);
        Assert.Equal(diagram.Theme.WireThickness, stroke.StrokeThickness);
    }

    /// <summary>
    ///     Validates that setting the styler and refreshing raise settings changes without touching the undo history.
    /// </summary>
    [Fact]
    public void Diagram_WireStylerAndRefreshAppearance_RaiseChangedWithoutHistory()
    {
        // Arrange
        var (diagram, _, _) = CreateWiredPage();
        var undoCount = diagram.History.UndoCount;
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);

        // Act
        diagram.WireStyler = _ => null;
        diagram.RefreshAppearance();

        // Assert
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(DiagramChangeKind.SettingsChanged, e.Kind));
        Assert.Equal(nameof(Diagram.WireStyler), events[0].PropertyName);
        Assert.Equal("Appearance", events[1].PropertyName);
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that wires attached to a collapsed group use the same styler for their display routes.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupProxyWire_UsesStyler()
    {
        // Arrange: producer inside a group, consumer outside, group collapsed
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var group = page.CreateGroup([producer], "G");
        group.IsCollapsed = true;
        diagram.WireStyler = _ => new WireStyle { Color = Color.Parse("#00AA00"), Thickness = 3 };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var stroke = StrokedPath(scene, wire);
        Assert.Equal(Color.Parse("#00AA00"), stroke.Stroke);
        Assert.Equal(3, stroke.StrokeThickness);
    }

    /// <summary>
    ///     Validates that a thick wire's hit target tolerates a wider click.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StyledThickness_WidensHitTarget()
    {
        // Arrange
        var (diagram, page, wire) = CreateWiredPage();
        diagram.WireStyler = _ => new WireStyle { Thickness = 9 };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: the wire is still hit at its midpoint
        var mid = wire.Source.Anchor.X + ((wire.Target.Anchor.X - wire.Source.Anchor.X) / 2);
        Assert.Same(wire, scene.HitTest(new Point(mid, wire.Source.Anchor.Y)).Element);
    }

    /// <summary>
    ///     Creates a diagram with a producer wired to a consumer on the same row.
    /// </summary>
    /// <returns>The diagram, its page and the wire.</returns>
    private static (Diagram Diagram, DiagramPage Page, Wire Wire) CreateWiredPage()
    {
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 30, 30, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 30, "C");
        return (diagram, page, page.Connect(producer.Port("value"), consumer.Port("in")));
    }

    /// <summary>
    ///     Gets the stroked (unfilled) path of a wire, excluding highlights.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="wire">The wire.</param>
    /// <returns>The wire's stroke primitive.</returns>
    private static PathPrimitive StrokedPath(DiagramScene scene, Wire wire) =>
        SceneTestModel.For<PathPrimitive>(scene, wire).Last(p => p.Fill is null);

    /// <summary>
    ///     Splits path commands into figures of straight points (curves contribute their end point).
    /// </summary>
    /// <param name="commands">The commands.</param>
    /// <returns>The points of each figure.</returns>
    private static List<List<Point>> Figures(IReadOnlyList<PathCommand> commands)
    {
        var figures = new List<List<Point>>();
        foreach (var command in commands)
        {
            if (command.Kind == PathCommandKind.MoveTo)
            {
                figures.Add([command.End]);
            }
            else if (command.Kind != PathCommandKind.Close)
            {
                figures[^1].Add(command.End);
            }
        }

        return figures;
    }

    /// <summary>
    ///     Measures the length of a polyline.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The summed segment lengths.</returns>
    private static double Length(List<Point> points)
    {
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            total += Math.Sqrt(Math.Pow(points[i].X - points[i - 1].X, 2) + Math.Pow(points[i].Y - points[i - 1].Y, 2));
        }

        return total;
    }
}
