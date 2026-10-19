using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for type registries, node types, icons, the theme, node geometry and the simple router.
/// </summary>
public class TypesAndGeometryTests
{
    /// <summary>
    ///     Validates that port types resolve by key and unknown keys fall back to neutral gray.
    /// </summary>
    [Fact]
    public void PortTypeRegistry_Get_RegisteredAndUnknownKeys()
    {
        // Arrange
        var registry = new PortTypeRegistry
        {
            new PortType("f32", Color.Parse("#FF8C00")),
        };

        // Act
        var known = registry.Get("f32");
        var unknown = registry.Get("mystery");

        // Assert
        Assert.Equal(Color.Parse("#FF8C00"), known.Color);
        Assert.Equal("mystery", unknown.Key);
        Assert.Equal(new Color(0x9A, 0x9A, 0x9A), unknown.Color);
        Assert.True(registry.Contains("f32"));
        Assert.False(registry.Contains("mystery"));
        Assert.True(registry.TryGet("f32", out var found));
        Assert.Same(known, found);
        Assert.False(registry.TryGet("mystery", out _));
        Assert.Single(registry);
        Assert.Throws<ArgumentException>(() => registry.Add(new PortType("f32", Color.Red)));
        Assert.Throws<ArgumentException>(() => new PortType(" ", Color.Red));
    }

    /// <summary>
    ///     Validates node type registration, lookup and duplicate detection.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_AddGetTryGetContains_WorkAndRejectDuplicates()
    {
        // Arrange
        var registry = new NodeTypeRegistry();
        var type = new NodeType("acme.Thing");

        // Act
        registry.Add(type);

        // Assert
        Assert.Same(type, registry.Get("acme.Thing"));
        Assert.True(registry.TryGet("acme.Thing", out var found));
        Assert.Same(type, found);
        Assert.False(registry.TryGet("nope", out _));
        Assert.True(registry.Contains("acme.Thing"));
        Assert.Equal(1, registry.Count);
        Assert.Equal(new[] { type }, registry);
        Assert.Throws<KeyNotFoundException>(() => registry.Get("nope"));
        Assert.Throws<ArgumentException>(() => registry.Add(new NodeType("acme.Thing")));
        Assert.Throws<ArgumentNullException>(() => registry.Add(null!));
    }

    /// <summary>
    ///     Validates that registration rejects duplicate port names within a direction and duplicate property names, leaving the registry unchanged.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Add_DuplicatePortOrPropertyNames_ThrowsArgumentException()
    {
        // Arrange
        var registry = new NodeTypeRegistry();
        var duplicatePorts = new NodeType("a.Dup") { Inputs = { new PortDefinition("x", "f32"), new PortDefinition("x", "f32") } };
        var duplicateProps = new NodeType("a.Dup2") { Properties = { new PropertyDefinition("p", 1), new PropertyDefinition("p", 2) } };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => registry.Add(duplicatePorts));
        Assert.Throws<ArgumentException>(() => registry.Add(duplicateProps));
        Assert.Equal(0, registry.Count);
    }

    /// <summary>
    ///     Validates that an input and an output may share a port name.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Add_SameNameOnInputAndOutput_IsAllowed()
    {
        // Arrange
        var registry = new NodeTypeRegistry();
        var type = new NodeType("a.Both") { Inputs = { new PortDefinition("pos", "f32") }, Outputs = { new PortDefinition("pos", "f32") } };

        // Act
        registry.Add(type);

        // Assert
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    ///     Validates that a registered node type can no longer be modified.
    /// </summary>
    [Fact]
    public void NodeType_AfterRegistration_ListsAreFrozen()
    {
        // Arrange
        var type = new NodeType("a.Frozen");
        new NodeTypeRegistry().Add(type);

        // Act / Assert
        Assert.Throws<NotSupportedException>(() => type.Inputs.Add(new PortDefinition("x", "f32")));
        Assert.Throws<NotSupportedException>(() => type.Properties.Clear());
        Assert.True(type.Outputs.IsReadOnly);
    }

    /// <summary>
    ///     Validates the default title (last key segment) and an explicit title.
    /// </summary>
    [Fact]
    public void NodeType_Title_DefaultsToLastKeySegment()
    {
        // Act
        var plain = new NodeType("acme.math.Multiply");
        var titled = new NodeType("acme.math.Multiply") { Title = "MULTIPLY" };
        var noDots = new NodeType("Solo");

        // Assert
        Assert.Equal("Multiply", plain.Title);
        Assert.Equal("MULTIPLY", titled.Title);
        Assert.Equal("Solo", noDots.Title);
        Assert.Throws<ArgumentException>(() => new NodeType(""));
        Assert.Null(plain.FindProperty("x"));
    }

    /// <summary>
    ///     Validates that the display name defaults to the title when it is not set, null or empty.
    /// </summary>
    [Fact]
    public void NodeType_DisplayName_DefaultsToTitle()
    {
        // Act
        var plain = new NodeType("acme.Producer") { Title = "PRODUCER" };
        var nullName = new NodeType("acme.Producer") { Title = "PRODUCER", DisplayName = null! };
        var emptyName = new NodeType("acme.Producer") { Title = "PRODUCER", DisplayName = string.Empty };
        var named = new NodeType("acme.Producer") { Title = "PRODUCER", DisplayName = "Producer (F32)" };
        var untitled = new NodeType("acme.Producer");

        // Assert
        Assert.Equal("PRODUCER", plain.DisplayName);
        Assert.Equal("PRODUCER", nullName.DisplayName);
        Assert.Equal("PRODUCER", emptyName.DisplayName);
        Assert.Equal("Producer (F32)", named.DisplayName);
        Assert.Equal("PRODUCER", named.Title);
        Assert.Equal("Producer", untitled.DisplayName);
    }

    /// <summary>
    ///     Validates that SVG icons expose their text and a stable content-based key.
    /// </summary>
    [Fact]
    public void IconSource_FromSvg_HasStableContentKey()
    {
        // Arrange
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>";

        // Act
        var a = IconSource.FromSvg(svg);
        var b = IconSource.FromSvg(svg);
        var c = IconSource.FromSvg(svg + " ");

        // Assert
        Assert.Equal(svg, a.SvgText);
        Assert.Equal(a.Key, b.Key);
        Assert.NotEqual(a.Key, c.Key);
        Assert.StartsWith("svg:", a.Key, StringComparison.Ordinal);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Throws<ArgumentException>(() => IconSource.FromSvg(" "));
    }

    /// <summary>
    ///     Validates that icons load from a file and from an embedded resource, and that missing resources are rejected.
    /// </summary>
    [Fact]
    public void IconSource_FromFileAndResource_LoadContentAndRejectMissing()
    {
        // Arrange
        var path = Path.Join(Path.GetTempPath(), $"plexus-icon-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, "<svg/>");

        try
        {
            // Act
            var fromFile = IconSource.FromSvgFile(path);

            // Assert
            Assert.Equal("<svg/>", fromFile.SvgText);
            Assert.Throws<ArgumentException>(() => IconSource.FromSvgResource(typeof(IconSource).Assembly, "no.such.resource.svg"));
            Assert.ThrowsAny<IOException>(() => IconSource.FromSvgFile(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that the classic theme has the documented geometry metrics.
    /// </summary>
    [Fact]
    public void DiagramTheme_Classic_HasDocumentedGeometryMetrics()
    {
        // Act
        var theme = DiagramTheme.Classic;

        // Assert
        Assert.Same(theme, DiagramTheme.Classic);
        Assert.Equal(15, theme.GridSize);
        Assert.Equal(90, theme.DefaultNodeWidth);
        Assert.Equal(90, theme.DefaultNodeHeight);
        Assert.Equal(15, theme.PortPitch);
        Assert.Equal(15, theme.ObstacleMargin);
    }

    /// <summary>
    ///     Validates the required node height for a number of ports and the default size selection.
    /// </summary>
    /// <param name="ports">The number of ports in the longest column.</param>
    /// <param name="expected">The expected minimum height.</param>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 60)]
    [InlineData(2, 75)]
    [InlineData(3, 90)]
    [InlineData(4, 105)]
    [InlineData(6, 135)]
    public void NodeGeometry_RequiredHeight_GrowsWithPortCount(int ports, double expected)
    {
        // Act
        var height = NodeGeometry.RequiredHeight(ports, DiagramTheme.Classic);

        // Assert
        Assert.Equal(expected, height);
    }

    /// <summary>
    ///     Validates port anchor placement for inputs and outputs of a node box.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortAnchor_PlacesInputsLeftAndOutputsRight()
    {
        // Arrange
        var bounds = new Rect(30, 60, 90, 90);

        // Act
        var firstInput = NodeGeometry.PortAnchor(bounds, PortDirection.Input, 0, 1, "in", DiagramTheme.Classic);
        var thirdOutput = NodeGeometry.PortAnchor(bounds, PortDirection.Output, 2, 3, "out", DiagramTheme.Classic);

        // Assert: anchors lie beyond the box edge by the reach of triangle plus label, on the port row
        Assert.Equal(new Point(30 - NodeGeometry.PortReach("in", DiagramTheme.Classic), 60 + 45), firstInput);
        Assert.Equal(new Point(120 + NodeGeometry.PortReach("out", DiagramTheme.Classic), 60 + 45 + 15), thirdOutput);
        Assert.Throws<ArgumentNullException>(() => NodeGeometry.PortAnchor(bounds, PortDirection.Input, 0, 1, "in", null!));
    }

    /// <summary>
    ///     Validates the box edge point where a port triangle hangs.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortEdge_IsOnTheBoxEdgeAtThePortRow()
    {
        // Arrange
        var bounds = new Rect(30, 60, 90, 90);

        // Act
        var input = NodeGeometry.PortEdge(bounds, PortDirection.Input, 0, 1, DiagramTheme.Classic);
        var output = NodeGeometry.PortEdge(bounds, PortDirection.Output, 2, 3, DiagramTheme.Classic);

        // Assert
        Assert.Equal(new Point(30, 105), input);
        Assert.Equal(new Point(120, 120), output);
    }

    /// <summary>
    ///     Validates the deterministic reach: triangle plus gaps plus a conservative label width, and triangle only without label.
    /// </summary>
    [Fact]
    public void NodeGeometry_PortReach_GrowsWithLabelAndIsTriangleOnlyWhenEmpty()
    {
        // Arrange
        var theme = DiagramTheme.Classic;
        var measured = new ApproximateTextMeasurer().Measure("settled", theme.PortLabelTextStyle).Width;

        // Act
        var empty = NodeGeometry.PortReach(string.Empty, theme);
        var shortLabel = NodeGeometry.PortReach("in", theme);
        var longLabel = NodeGeometry.PortReach("settled", theme);

        // Assert
        Assert.Equal(theme.PortTriangleLength, empty);
        Assert.True(shortLabel > empty);
        Assert.True(longLabel > shortLabel);
        Assert.True(NodeGeometry.PortLabelWidth("settled", theme) >= measured);
        Assert.Equal(theme.PortTriangleLength + (2 * theme.PortLabelGap) + NodeGeometry.PortLabelWidth("settled", theme), longLabel);
    }

    /// <summary>
    ///     Validates that routing obstacles cover the node margin box and the labels of ports whose label reaches beyond it.
    /// </summary>
    [Fact]
    public void NodeGeometry_RoutingObstacles_CoverLongPortLabels()
    {
        // Arrange
        var theme = DiagramTheme.Classic;
        var type = new NodeType("acme.Wide")
        {
            Inputs = { new PortDefinition("a", "f32") { Label = "a rather long label" }, new PortDefinition("b", "f32") },
            Outputs = { new PortDefinition("o", "f32") { Label = "another long label" } },
        };
        var bounds = new Rect(200, 100, 90, 90);

        // Act
        var boxes = NodeGeometry.RoutingObstacles(bounds, type, theme);
        var inputAnchor = NodeGeometry.PortAnchor(bounds, PortDirection.Input, 0, 2, "a rather long label", theme);
        var outputAnchor = NodeGeometry.PortAnchor(bounds, PortDirection.Output, 0, 1, "another long label", theme);

        // Assert
        Assert.Equal(bounds.Inflate(theme.ObstacleMargin), boxes[0]);
        Assert.Contains(boxes, b => b.Contains(new Point(inputAnchor.X + 1, inputAnchor.Y)));
        Assert.Contains(boxes, b => b.Contains(new Point(outputAnchor.X - 1, outputAnchor.Y)));
    }

    /// <summary>
    ///     Validates that a theme can be customized with a with-expression and that the original is untouched.
    /// </summary>
    [Fact]
    public void DiagramTheme_With_CreatesModifiedCopy()
    {
        // Act
        var custom = DiagramTheme.Classic with { NodeFill = Color.Parse("#336699"), WireArrowheads = false };

        // Assert
        Assert.Equal(Color.Parse("#336699"), custom.NodeFill);
        Assert.False(custom.WireArrowheads);
        Assert.Equal(DiagramTheme.Classic.GridSize, custom.GridSize);
        Assert.NotEqual(DiagramTheme.Classic.NodeFill, custom.NodeFill);
        Assert.True(DiagramTheme.Classic.WireArrowheads);
        Assert.NotEqual(DiagramTheme.Classic, custom);
        Assert.Equal(DiagramTheme.Classic, DiagramTheme.Classic with { });
    }

    /// <summary>
    ///     Validates that the simple router returns no bends for aligned ports.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_AlignedPorts_ReturnsStraightWire()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(0, 45), End = new Point(300, 45) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Empty(route);
    }

    /// <summary>
    ///     Validates the three-segment elbow when the end lies ahead of the start.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_EndAhead_ReturnsThreeSegmentElbowAtMidpoint()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(0, 0), End = new Point(300, 150) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert: vertical run at the grid-snapped midpoint
        Assert.Equal(new[] { new Point(150, 0), new Point(150, 150) }, route);
    }

    /// <summary>
    ///     Validates that the vertical run is not snapped outside the stubs.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_NarrowGap_KeepsRunBetweenStubs()
    {
        // Arrange: stubs end at x = 23 on both sides; the midpoint 23 would snap to 30, outside the window
        var request = new RouteRequest { Start = new Point(8, 0), End = new Point(38, 50) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert
        Assert.Equal(new[] { new Point(23, 0), new Point(23, 50) }, route);
    }

    /// <summary>
    ///     Validates that an end behind the start yields a five-segment loop through both stubs.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_EndBehind_LoopsThroughStubs()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(200, 0), End = new Point(100, 90) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert: leave right by the stub, run in a channel, enter from the left stub
        Assert.Equal(
            new[] { new Point(215, 0), new Point(215, 45), new Point(85, 45), new Point(85, 90) },
            route);
        Assert.Throws<ArgumentNullException>(() => new SimpleRouter().Route(null!));
    }

    /// <summary>
    ///     Validates the backward loop when both ports are at the same height.
    /// </summary>
    [Fact]
    public void SimpleRouter_Route_EndBehindSameHeight_UsesChannelBelow()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(200, 50), End = new Point(100, 50) };

        // Act
        var route = new SimpleRouter().Route(request);

        // Assert: the channel lies below both ports
        Assert.Equal(4, route.Count);
        Assert.All(route.Skip(1).Take(2), p => Assert.True(p.Y > 50));
    }

    /// <summary>
    ///     Validates that a custom router assigned to the diagram is used for new wires.
    /// </summary>
    [Fact]
    public void Diagram_Router_CustomRouter_IsUsedForConnect()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Router = new FixedRouter();
        var page = diagram.Pages.Add("P");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 300, 0);

        // Act
        var wire = page.Connect(a.Port("value"), b.Port("in"));

        // Assert
        Assert.Equal(new[] { new Point(1, 2) }, wire.Route);
    }

    /// <summary>
    ///     Validates the request handed to the router: anchors, directions and inflated obstacles.
    /// </summary>
    [Fact]
    public void Diagram_Router_ReceivesAnchorsDirectionsAndInflatedObstacles()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var capture = new CapturingRouter();
        diagram.Router = capture;
        var page = diagram.Pages.Add("P");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 300, 0);

        // Act
        page.Connect(a.Port("value"), b.Port("in"));

        // Assert
        var request = Assert.IsType<RouteRequest>(capture.Last);
        Assert.Equal(new Point(90 + NodeGeometry.PortReach("value", DiagramTheme.Classic), 45), request.Start);
        Assert.Equal(new Point(300 - NodeGeometry.PortReach("in", DiagramTheme.Classic), 45), request.End);
        Assert.Equal(PortSide.Right, request.StartDirection);
        Assert.Equal(PortSide.Left, request.EndDirection);
        Assert.Equal(15, request.GridSize);
        Assert.Contains(new Rect(-15, -15, 120, 120), request.Obstacles);
        Assert.Contains(new Rect(285, -15, 120, 120), request.Obstacles);

        // Both nodes also contribute their port label strips
        Assert.Equal(
            NodeGeometry.RoutingObstacles(a.Bounds, a.Type, DiagramTheme.Classic).Count + NodeGeometry.RoutingObstacles(b.Bounds, b.Type, DiagramTheme.Classic).Count,
            request.Obstacles.Count);
    }

    /// <summary>
    ///     Validates that nodes hidden inside a collapsed group are not handed to routers as obstacles.
    /// </summary>
    [Fact]
    public void Diagram_Router_CollapsedGroupMembers_AreExcludedFromObstacles()
    {
        // Arrange: three nodes, one of them inside a collapsed group
        var diagram = TestModel.CreateDiagram();
        var capture = new CapturingRouter();
        diagram.Router = capture;
        var page = diagram.Pages.Add("P");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 300, 0);
        var hidden = page.AddNode("acme.Gain", 150, 150);
        page.CreateGroup([hidden], "G").IsCollapsed = true;

        // Act
        page.Connect(a.Port("value"), b.Port("in"));

        // Assert
        var request = Assert.IsType<RouteRequest>(capture.Last);
        Assert.DoesNotContain(request.Obstacles, o => o.Intersects(new Rect(150, 150, 90, 90)));
    }

    /// <summary>
    ///     A router that always returns one fixed bend point.
    /// </summary>
    private sealed class FixedRouter : IWireRouter
    {
        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request) => [new Point(1, 2)];
    }

    /// <summary>
    ///     A router that records the last request it received and returns a straight wire.
    /// </summary>
    private sealed class CapturingRouter : IWireRouter
    {
        /// <summary>
        ///     Gets the last request received.
        /// </summary>
        public RouteRequest? Last { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request)
        {
            Last = request;
            return [];
        }
    }
}
