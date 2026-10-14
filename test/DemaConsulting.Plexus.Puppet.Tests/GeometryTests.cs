using System.Text.Json.Nodes;
using DemaConsulting.Plexus.Puppet;

namespace DemaConsulting.Plexus.Puppet.Tests;

/// <summary>
///     Tests for reading the demo's <c>geometry</c> replies and for the geometry of mouse moves (no UI is touched).
/// </summary>
public class GeometryTests
{
    /// <summary>
    ///     Builds a rectangle description with equal scale on both axes (screen = diagram * 2 + 1000).
    /// </summary>
    /// <param name="x">The diagram left edge.</param>
    /// <param name="y">The diagram top edge.</param>
    /// <param name="width">The diagram width.</param>
    /// <param name="height">The diagram height.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject Rect(double x, double y, double width, double height) => new()
    {
        ["diagram"] = new JsonObject { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height },
        ["screen"] = new JsonObject { ["x"] = (x * 2) + 1000, ["y"] = (y * 2) + 1000, ["width"] = width * 2, ["height"] = height * 2 },
        ["visible"] = true,
    };

    /// <summary>
    ///     Builds a node geometry result: a 100x100 box at (100,100) with a name label above it.
    /// </summary>
    /// <param name="portHit">The diagram rectangle of the port hit area, or <see langword="null"/> for no ports.</param>
    /// <returns>The JSON description.</returns>
    private static JsonObject Node(JsonObject? portHit = null)
    {
        var node = new JsonObject
        {
            ["kind"] = "node",
            ["bounds"] = Rect(100, 100, 100, 100),
            ["nameRect"] = Rect(100, 80, 100, 18),
            ["title"] = Rect(110, 105, 80, 14),
            ["ports"] = new JsonArray(),
        };
        if (portHit is not null)
        {
            node["ports"]!.AsArray().Add(new JsonObject { ["name"] = "p", ["hit"] = portHit });
        }

        return node;
    }

    /// <summary>
    ///     Validates that a point description is read in both coordinate systems.
    /// </summary>
    [Fact]
    public void GeometryReader_ReadPoint_ReadsBothSystemsAndVisibility()
    {
        // Arrange
        var json = JsonNode.Parse("""{"diagram":{"x":10.5,"y":20},"screen":{"x":1031.2,"y":1040},"visible":false}""");

        // Act
        var point = GeometryReader.ReadPoint(json);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(10.5, point.DiagramX),
            () => Assert.Equal(20, point.DiagramY),
            () => Assert.Equal(1031, point.Screen.PixelX),
            () => Assert.Equal(1040, point.Screen.PixelY),
            () => Assert.False(point.Visible));
    }

    /// <summary>
    ///     Validates that an incomplete description is reported, not defaulted.
    /// </summary>
    [Fact]
    public void GeometryReader_ReadPoint_Incomplete_Throws() =>
        Assert.Throws<InvalidOperationException>(() => GeometryReader.ReadPoint(JsonNode.Parse("""{"diagram":{"x":1,"y":2}}""")));

    /// <summary>
    ///     Validates that a rectangle description is read and its screen center computed.
    /// </summary>
    [Fact]
    public void GeometryReader_ReadRect_ReadsRectangleAndCenter()
    {
        // Arrange / Act
        var rect = GeometryReader.ReadRect(Rect(100, 100, 50, 40));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(50, rect.DiagramWidth),
            () => Assert.Equal(1200, rect.Screen.X),
            () => Assert.Equal(new ScreenPoint(1250, 1240), rect.Screen.Center),
            () => Assert.True(rect.Screen.Contains(new ScreenPoint(1250, 1240))),
            () => Assert.False(rect.Screen.Contains(new ScreenPoint(1199, 1240))));
    }

    /// <summary>
    ///     Validates that the body point of a free node is its center, in both coordinate systems.
    /// </summary>
    [Fact]
    public void GeometryReader_BodyPoint_FreeNode_IsTheCenter()
    {
        // Arrange: the title label sits at the top, the name above the box, so the center is free
        var node = Node();

        // Act
        var point = GeometryReader.BodyPoint(node);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(150, point.DiagramX),
            () => Assert.Equal(150, point.DiagramY),
            () => Assert.Equal(1300, point.Screen.X),
            () => Assert.Equal(1300, point.Screen.Y));
    }

    /// <summary>
    ///     Validates that the body point avoids a port hit area that covers the center.
    /// </summary>
    [Fact]
    public void GeometryReader_BodyPoint_PortOverCenter_MovesAway()
    {
        // Arrange: a port hit area over the upper-middle of the box
        var node = Node(Rect(120, 120, 60, 30));

        // Act
        var point = GeometryReader.BodyPoint(node);

        // Assert: outside the hit rectangle (screen y 1240..1300), inside the box (1200..1400)
        var hit = GeometryReader.ReadRect(node["ports"]![0]!["hit"]).Screen;
        var bounds = GeometryReader.ReadRect(node["bounds"]).Screen;
        Assert.Multiple(
            () => Assert.False(hit.Contains(point.Screen)),
            () => Assert.True(bounds.Contains(point.Screen)),
            () => Assert.NotEqual(1300, point.Screen.Y));
    }

    /// <summary>
    ///     Validates that a node that is not drawn has no body point.
    /// </summary>
    [Fact]
    public void GeometryReader_BodyPoint_HiddenNode_Throws()
    {
        // Arrange
        var node = Node();
        node["hidden"] = true;

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => GeometryReader.BodyPoint(node));
    }

    /// <summary>
    ///     Validates that a port press point is preferred over its anchor.
    /// </summary>
    [Fact]
    public void GeometryReader_PortPressPoint_PrefersPressOverAnchor()
    {
        // Arrange
        string withPress = """{"anchor":{"diagram":{"x":0,"y":0},"screen":{"x":1,"y":1}},"press":{"diagram":{"x":5,"y":5},"screen":{"x":50,"y":60}}}""";
        string anchorOnly = """{"anchor":{"diagram":{"x":0,"y":0},"screen":{"x":1,"y":2}}}""";

        // Act
        var press = GeometryReader.PortPressPoint(JsonNode.Parse(withPress)!);
        var anchor = GeometryReader.PortPressPoint(JsonNode.Parse(anchorOnly)!);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(50, press.Screen.X),
            () => Assert.Equal(1, anchor.Screen.X));
    }

    /// <summary>
    ///     Validates that a mouse move is split into small steps ending exactly at the target.
    /// </summary>
    [Fact]
    public void InputDriver_Interpolate_StepsAreSmallAndEndAtTarget()
    {
        // Arrange / Act
        var points = InputDriver.Interpolate(new ScreenPoint(0, 0), new ScreenPoint(120, 0), 12);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(10, points.Count),
            () => Assert.Equal(new ScreenPoint(12, 0), points[0]),
            () => Assert.Equal(new ScreenPoint(120, 0), points[^1]));
    }

    /// <summary>
    ///     Validates that a zero-length move still yields the destination.
    /// </summary>
    [Fact]
    public void InputDriver_Interpolate_NoDistance_YieldsDestinationOnly()
    {
        // Arrange / Act
        var points = InputDriver.Interpolate(new ScreenPoint(5, 5), new ScreenPoint(5, 5));

        // Assert
        Assert.Equal([new ScreenPoint(5, 5)], points);
    }

    /// <summary>
    ///     Validates that diagonal moves keep every step within the step size.
    /// </summary>
    [Fact]
    public void InputDriver_Interpolate_Diagonal_StepsNeverExceedStepSize()
    {
        // Arrange / Act
        var points = InputDriver.Interpolate(new ScreenPoint(0, 0), new ScreenPoint(90, 120), 12);
        var previous = new ScreenPoint(0, 0);
        var longest = 0.0;
        foreach (var point in points)
        {
            longest = Math.Max(longest, Math.Sqrt(Math.Pow(point.X - previous.X, 2) + Math.Pow(point.Y - previous.Y, 2)));
            previous = point;
        }

        // Assert
        Assert.True(longest <= 12.0001, $"longest step {longest}");
    }
}
