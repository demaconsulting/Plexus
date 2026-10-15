using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Puppet;

namespace DemaConsulting.Plexus.Puppet.Tests;

/// <summary>
///     Tests for the planning behind the hover commands: their options, where the cursor parks first, where it rests on a
///     wire and which wire joins two ports (no UI is touched).
/// </summary>
public class HoverTests
{
    /// <summary>
    ///     Builds a <c>geometry</c> point description with the same diagram and screen coordinates.
    /// </summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The JSON point.</returns>
    private static JsonObject Point(double x, double y) => new()
    {
        ["diagram"] = new JsonObject { ["x"] = x, ["y"] = y },
        ["screen"] = new JsonObject { ["x"] = x * 2, ["y"] = y * 2 },
        ["visible"] = true,
    };

    /// <summary>
    ///     Validates that without options a hover waits longer than Avalonia's default 400 ms tooltip delay and takes no shot.
    /// </summary>
    [Fact]
    public void HoverOptions_Parse_Defaults_WaitLongerThanToolTipDelay()
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["hover-node", "Osc1"]);

        // Act
        var options = HoverOptions.Parse(parsed);

        // Assert
        Assert.Equal(HoverOptions.DefaultWaitMs, options.WaitMs);
        Assert.True(options.WaitMs > 400);
        Assert.Null(options.ShotPath);
    }

    /// <summary>
    ///     Validates that <c>--wait</c> and <c>--shot</c> are read.
    /// </summary>
    [Fact]
    public void HoverOptions_Parse_WaitAndShot_AreRead()
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["hover-port", "Osc1.out", "--wait", "1200", "--shot", "tip.png"]);

        // Act
        var options = HoverOptions.Parse(parsed);

        // Assert
        Assert.Equal(new HoverOptions(1200, "tip.png"), options);
        Assert.Equal(["Osc1.out"], parsed.Positional);
    }

    /// <summary>
    ///     Validates that a wait that is not a non-negative whole number, or a shot without a path, is a usage error.
    /// </summary>
    /// <param name="arguments">The options after the command and its target.</param>
    [Theory]
    [InlineData("--wait", "-5")]
    [InlineData("--wait", "-1")]
    [InlineData("--wait", "abc")]
    [InlineData("--wait", "1.5")]
    [InlineData("--wait")]
    [InlineData("--shot")]
    public void HoverOptions_Parse_BadOption_IsUsageError(params string[] arguments)
    {
        // Arrange
        var parsed = CommandLineParser.Parse(["hover-node", "Osc1", .. arguments]);

        // Act
        var error = Assert.Throws<PuppetException>(() => HoverOptions.Parse(parsed));

        // Assert
        Assert.Equal(ExitCodes.UsageError, error.ExitCode);
    }

    /// <summary>
    ///     Validates that negative coordinates of <c>hover-at</c> stay positional, before and after the options.
    /// </summary>
    [Fact]
    public void CommandLineParser_Parse_HoverAtNegativeCoordinates_KeepsPositionals()
    {
        // Arrange / Act
        var parsed = CommandLineParser.Parse(["hover-at", "-40", "-20.5", "--wait", "900"]);

        // Assert
        Assert.Equal(["-40", "-20.5"], parsed.Positional);
        Assert.Equal(900, HoverOptions.Parse(parsed).WaitMs);
    }

    /// <summary>
    ///     Validates that on empty canvas the cursor parks at the nearest candidate (straight above the target).
    /// </summary>
    [Fact]
    public void HoverPlanner_ChooseParkPoint_FreeCanvas_IsFirstCandidate()
    {
        // Arrange / Act
        var park = HoverPlanner.ChooseParkPoint(100, 100, [], []);

        // Assert
        Assert.Equal(HoverPlanner.ParkCandidates(100, 100)[0], park);
        Assert.Equal((100.0, 60.0), park);
    }

    /// <summary>
    ///     Validates that the park point is outside every obstacle and away from every wire.
    /// </summary>
    [Fact]
    public void HoverPlanner_ChooseParkPoint_AvoidsNodesAndWires()
    {
        // Arrange: the target node covers the nearest ring; a wire runs straight up through the target; a node sits above
        DiagramBox[] obstacles = [new(50, 50, 100, 100), new(60, -100, 80, 100)];
        IReadOnlyList<(double X, double Y)>[] wires = [[(100, -500), (100, 500)]];

        // Act
        var park = HoverPlanner.ChooseParkPoint(100, 100, obstacles, wires);

        // Assert: a free point, not inside a box and at least the clearance away from the wire
        var (x, y) = Assert.NotNull(park);
        Assert.DoesNotContain(obstacles, box => box.Contains(x, y));
        Assert.True(Math.Abs(x - 100) >= 12);
        Assert.Contains((x, y), HoverPlanner.ParkCandidates(100, 100));
    }

    /// <summary>
    ///     Validates that no park point is chosen when every candidate is taken.
    /// </summary>
    [Fact]
    public void HoverPlanner_ChooseParkPoint_NothingFree_IsNull()
    {
        // Arrange: one huge box over everything around the target
        DiagramBox[] obstacles = [new(-1000, -1000, 2000, 2000)];

        // Act / Assert
        Assert.Null(HoverPlanner.ChooseParkPoint(0, 0, obstacles, []));
        Assert.Empty(HoverPlanner.FreeParkPoints(0, 0, obstacles, []));
    }

    /// <summary>
    ///     Validates that a wire is hovered at the midpoint of its longest segment, in both coordinate systems.
    /// </summary>
    [Fact]
    public void HoverPlanner_WirePoint_IsMidpointOfLongestSegment()
    {
        // Arrange: segments of length 10, 100 and 20
        var geometry = new JsonObject { ["points"] = new JsonArray(Point(0, 0), Point(10, 0), Point(10, 100), Point(30, 100)) };

        // Act
        var point = HoverPlanner.WirePoint(geometry);

        // Assert
        Assert.Equal(10, point.DiagramX);
        Assert.Equal(50, point.DiagramY);
        Assert.Equal(new ScreenPoint(20, 100), point.Screen);
        Assert.True(point.Visible);
        Assert.Throws<InvalidOperationException>(() => HoverPlanner.WirePoint(new JsonObject { ["points"] = new JsonArray(Point(0, 0)) }));
    }

    /// <summary>
    ///     Validates that the wire joining two ports is found by its endpoints, ignoring an <c>@in</c>/<c>@out</c> suffix.
    /// </summary>
    [Fact]
    public void HoverPlanner_FindWireId_MatchesEndpointsIgnoringSide()
    {
        // Arrange
        var wires = new JsonArray(
            new JsonObject { ["id"] = "w1", ["source"] = "Osc1.out", ["target"] = "Mix.a" },
            new JsonObject { ["id"] = "w2", ["source"] = "Meter.flow", ["target"] = "Split.in" });

        // Act / Assert
        Assert.Equal("w1", HoverPlanner.FindWireId(wires, "Osc1.out", "Mix.a"));
        Assert.Equal("w2", HoverPlanner.FindWireId(wires, "Meter.flow@out", "Split.in@in"));
    }

    /// <summary>
    ///     Validates that no wire is found when none joins the two ports (or the list is missing).
    /// </summary>
    [Fact]
    public void HoverPlanner_FindWireId_NoMatch_IsNull()
    {
        // Arrange
        var wires = new JsonArray(new JsonObject { ["id"] = "w1", ["source"] = "Osc1.out", ["target"] = "Mix.a" });

        // Act / Assert: the direction matters too
        Assert.Null(HoverPlanner.FindWireId(wires, "Mix.a", "Osc1.out"));
        Assert.Null(HoverPlanner.FindWireId(wires, "Osc2.out", "Mix.a"));
        Assert.Null(HoverPlanner.FindWireId(null, "Osc1.out", "Mix.a"));
    }

    /// <summary>
    ///     Validates that a port reference is split into its path and its optional side.
    /// </summary>
    [Fact]
    public void HoverPlanner_SplitPortReference_SeparatesSide()
    {
        // Arrange / Act / Assert
        Assert.Equal(("Meter.flow", "in"), HoverPlanner.SplitPortReference("Meter.flow@in"));
        Assert.Equal(("Meter.flow", "out"), HoverPlanner.SplitPortReference("Meter.flow@out"));
        Assert.Equal(("Osc1.out", (string?)null), HoverPlanner.SplitPortReference("Osc1.out"));
    }
}
