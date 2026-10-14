using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Subsystem tests for Routing: the routers working together with the page model that connects wires.
/// </summary>
public class RoutingTests
{
    /// <summary>
    ///     Validates that the diagram uses the orthogonal router by default.
    /// </summary>
    [Fact]
    public void Diagram_Router_Default_IsOrthogonalRouter()
    {
        // Arrange / Act
        var diagram = TestModel.CreateDiagram();

        // Assert
        Assert.IsType<OrthogonalRouter>(diagram.Router);
    }

    /// <summary>
    ///     Validates that wires connected through the model avoid a node placed between their ports.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_NodeBetweenPorts_RoutesAroundIt()
    {
        // Arrange: producer and consumer on one row with a gain node in the way
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var blocker = page.AddNode("acme.Gain", 150, 0, "B");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var points = wire.GetPoints();

        // Assert: orthogonal and clear of the blocker's real box
        Assert.True(RouteAssertions.IsOrthogonal(points));
        Assert.False(RouteAssertions.CrossesAny(points, [blocker.Bounds]));
        Assert.NotEmpty(wire.Route);
    }

    /// <summary>
    ///     Validates that a new wire avoids a node that was moved or added after earlier wires were routed on the page.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_NodeMovedOrAddedAfterEarlierWires_IsAvoided()
    {
        // Arrange: three rows of producer and consumer, and a gain node parked out of the way; one wire is already routed
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var first = page.AddNode("acme.Producer", 0, -300, "P0");
        var firstEnd = page.AddNode("acme.Consumer", 300, -300, "C0");
        var producer = page.AddNode("acme.Producer", 0, 0, "P1");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C1");
        var lower = page.AddNode("acme.Producer", 0, 300, "P2");
        var lowerEnd = page.AddNode("acme.Consumer", 300, 300, "C2");
        var moved = page.AddNode("acme.Gain", 150, 600, "B1");
        page.Connect(first.Port("value"), firstEnd.Port("in"));

        // Act: move the gain node between the second row's ports, then add another between the third row's ports
        page.Move([moved], 0, -600);
        var aroundMoved = page.Connect(producer.Port("value"), consumer.Port("in"));
        var added = page.AddNode("acme.Gain", 150, 300, "B2");
        var aroundAdded = page.Connect(lower.Port("value"), lowerEnd.Port("in"));

        // Assert: each new wire goes around the node in its way, at the node's current position
        Assert.Multiple(
            () => Assert.False(RouteAssertions.CrossesAny(aroundMoved.GetPoints(), [moved.Bounds])),
            () => Assert.False(RouteAssertions.CrossesAny(aroundAdded.GetPoints(), [added.Bounds])),
            () => Assert.NotEmpty(aroundMoved.Route),
            () => Assert.NotEmpty(aroundAdded.Route));
    }

    /// <summary>
    ///     Validates that a page restored by undo after the theme changed routes new wires with the new theme.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_PageRestoredAfterThemeChange_UsesTheNewTheme()
    {
        // Arrange: the same page three times - default theme, a wide obstacle margin from the start, and a page
        // whose earlier wire was routed with the default theme before it was removed, re-themed and restored
        var wide = DiagramTheme.Classic with { ObstacleMargin = 40 };
        static (Diagram Diagram, DiagramPage Page) Build(DiagramTheme theme)
        {
            var diagram = TestModel.CreateDiagram();
            diagram.Theme = theme;
            var page = diagram.Pages.Add("Main");
            diagram.Pages.Add("Other");
            page.AddNode("acme.Producer", 0, 0, "P");
            page.AddNode("acme.Gain", 300, 0, "B");
            page.AddNode("acme.Consumer", 600, 0, "C");
            page.AddNode("acme.Producer", 0, 400, "Q");
            page.AddNode("acme.Consumer", 600, 400, "R");
            page.Connect("Q.value", "R.in");
            return (diagram, page);
        }

        var (_, plain) = Build(DiagramTheme.Classic);
        var (_, fresh) = Build(wide);
        var (restored, page) = Build(DiagramTheme.Classic);

        // Act
        restored.Pages.Remove(page);
        restored.Theme = wide;
        restored.History.Undo();
        var route = page.Connect("P.value", "C.in").Route;

        // Assert: the margin changes the route, and the restored page routes like the page that always had it
        Assert.NotEqual(plain.Connect("P.value", "C.in").Route, fresh.Connect("P.value", "C.in").Route);
        Assert.Equal(fresh.Wires[^1].Route, route);
    }

    /// <summary>
    ///     Validates that connecting a dense page wire by wire, including wires that merge at a fan-in input, gives the routes that re-routing the whole page gives.
    /// </summary>
    [Fact]
    public void DiagramPage_ConnectWireByWire_MatchesRerouteAll()
    {
        // Arrange: a 5 x 6 lattice of mixers wired to the next column, and the last column merging into one fan-in input
        const int columns = 5;
        const int rows = 6;
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var nodes = new Node[columns, rows];
        for (var c = 0; c < columns; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                nodes[c, r] = page.AddNode("acme.Mixer", c * 240, r * 180, $"M{c}_{r}");
            }
        }

        var merger = page.AddNode("acme.Merger", columns * 240, rows * 90, "Merge");
        for (var c = 0; c < columns - 1; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                page.Connect(nodes[c, r].Port("out"), nodes[c + 1, r].Port("a"));
                if (r + 1 < rows)
                {
                    page.Connect(nodes[c, r].Port("out"), nodes[c + 1, r + 1].Port("b"));
                }
            }
        }

        for (var r = 0; r < rows; r++)
        {
            page.Connect(nodes[columns - 1, r].Port("out"), merger.Port("in"));
        }

        var connected = page.Wires.Select(w => w.Route.ToArray()).ToList();
        var undoCount = diagram.History.UndoCount;

        // Act
        page.RerouteAll();

        // Assert: no route changed, so re-routing recorded nothing
        Assert.Equal(undoCount, diagram.History.UndoCount);
        Assert.Equal(connected, page.Wires.Select(w => w.Route.ToArray()));
    }

    /// <summary>
    ///     Validates that wires connected under either shipped router consist only of horizontal and vertical segments.
    /// </summary>
    [Fact]
    public void Routing_ConnectedWires_UnderEitherRouter_AreOrthogonal()
    {
        // Arrange: a producer, a consumer behind it, and one ahead and lower
        foreach (IWireRouter router in new IWireRouter[] { new OrthogonalRouter(), new SimpleRouter() })
        {
            var diagram = TestModel.CreateDiagram();
            diagram.Router = router;
            var page = diagram.Pages.Add("Main");
            var producer = page.AddNode("acme.Producer", 300, 0, "P");
            var behind = page.AddNode("acme.Consumer", 0, 150, "Behind");
            var ahead = page.AddNode("acme.Consumer", 600, 300, "Ahead");

            // Act
            var wireBehind = page.Connect(producer.Port("value"), behind.Port("in"));
            var wireAhead = page.Connect(producer.Port("value"), ahead.Port("in"));

            // Assert
            Assert.True(RouteAssertions.IsOrthogonal(wireBehind.GetPoints()), router.GetType().Name);
            Assert.True(RouteAssertions.IsOrthogonal(wireAhead.GetPoints()), router.GetType().Name);
        }
    }

    /// <summary>
    ///     Validates that a host-supplied router is asked to route new wires and receives the port anchors, the stub length and the node boxes as obstacles.
    /// </summary>
    [Fact]
    public void Routing_HostRouter_ReceivesRequestForNewWires()
    {
        // Arrange
        var router = new RecordingRouter([new Point(111, 222)]);
        var diagram = TestModel.CreateDiagram();
        diagram.Router = router;
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert: the wire carries exactly what the host router returned
        Assert.Equal(new[] { new Point(111, 222) }, wire.Route);

        // The request describes the wire's ends, the theme's stub length and the node boxes
        var request = Assert.Single(router.Requests);
        Assert.Equal(producer.Port("value").Anchor, request.Start);
        Assert.Equal(consumer.Port("in").Anchor, request.End);
        Assert.Equal(PortSide.Right, request.StartDirection);
        Assert.Equal(PortSide.Left, request.EndDirection);
        Assert.Equal(diagram.Theme.WireStubLength, request.StubLength);
        Assert.Contains(request.Obstacles, o => o.Contains(producer.Bounds.Center));
        Assert.Contains(request.Obstacles, o => o.Contains(consumer.Bounds.Center));
    }

    /// <summary>
    ///     Validates that both shipped routers give identical results for repeated identical requests.
    /// </summary>
    [Fact]
    public void Routing_SameRequest_BothRouters_AreDeterministic()
    {
        // Arrange
        var request = new RouteRequest
        {
            Start = new Point(100, 50),
            End = new Point(500, 250),
            Obstacles = [new Rect(200, 0, 100, 150), new Rect(300, 150, 100, 150)],
        };

        // Act / Assert
        Assert.Equal(new OrthogonalRouter().Route(request), new OrthogonalRouter().Route(request));
        Assert.Equal(new SimpleRouter().Route(request), new SimpleRouter().Route(request));
    }

    /// <summary>
    ///     Validates that wires connected under either shipped router leave outputs to the right and enter inputs from the left through horizontal stubs.
    /// </summary>
    [Fact]
    public void Routing_ConnectedWires_LeaveAndEnterPortsHorizontallyThroughStubs()
    {
        foreach (IWireRouter router in new IWireRouter[] { new OrthogonalRouter(), new SimpleRouter() })
        {
            // Arrange: one wire to a target ahead and lower, one to a target behind
            var diagram = TestModel.CreateDiagram();
            diagram.Router = router;
            var page = diagram.Pages.Add("Main");
            var producer = page.AddNode("acme.Producer", 300, 0, "P");
            var ahead = page.AddNode("acme.Consumer", 700, 300, "Ahead");
            var behind = page.AddNode("acme.Consumer", 0, 200, "Behind");
            var stub = diagram.Theme.WireStubLength;

            // Act
            foreach (var wire in new[] { page.Connect(producer.Port("value"), ahead.Port("in")), page.Connect(producer.Port("value"), behind.Port("in")) })
            {
                var points = wire.GetPoints();

                // Assert: the first segment runs right by at least one stub, the last one runs right into the input after at least one stub
                var name = $"{router.GetType().Name} -> {wire.Target}";
                Assert.Equal(points[0].Y, points[1].Y);
                Assert.True(points[1].X >= points[0].X + stub - 1e-9, name);
                Assert.Equal(points[^2].Y, points[^1].Y);
                Assert.True(points[^2].X <= points[^1].X - stub + 1e-9, name);
            }
        }
    }

    /// <summary>
    ///     Validates that the default router still returns an orthogonal route when no obstacle-free path exists.
    /// </summary>
    [Fact]
    public void Routing_NoObstacleFreePath_StillReturnsAnOrthogonalRoute()
    {
        // Arrange: four walls enclose the end stub point (385,100) in a small pocket
        var request = new RouteRequest
        {
            Start = new Point(0, 0),
            End = new Point(400, 100),
            Obstacles =
            [
                new Rect(360, 60, 50, 30),
                new Rect(360, 110, 50, 30),
                new Rect(360, 60, 15, 80),
                new Rect(395, 60, 15, 80),
            ],
        };
        IWireRouter router = new OrthogonalRouter();

        // Act
        var route = router.Route(request);

        // Assert: a route exists, is orthogonal, and is the simple elbow
        Assert.NotNull(route);
        Assert.True(RouteAssertions.IsOrthogonal(RouteAssertions.Polyline(request, route)));
        Assert.Equal(new SimpleRouter().Route(request), route);
    }

    /// <summary>
    ///     Validates that both shipped routers reject a null request.
    /// </summary>
    [Fact]
    public void Routing_NullRequest_BothRouters_ThrowArgumentNullException()
    {
        // Arrange
        IWireRouter[] routers = [new OrthogonalRouter(), new SimpleRouter()];

        // Act / Assert
        Assert.All(routers, router => Assert.Throws<ArgumentNullException>(() => router.Route(null!)));
    }

    /// <summary>
    ///     Validates that every wire of a densely connected page is created and routed orthogonally.
    /// </summary>
    [Fact]
    public void Routing_DenseConnectedPage_RoutesEveryWireOrthogonally()
    {
        // Arrange: a 6 x 10 lattice of mixers, each wired to the next column on its own row and on the row below
        const int columns = 6;
        const int rows = 10;
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var nodes = new Node[columns, rows];
        for (var c = 0; c < columns; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                nodes[c, r] = page.AddNode("acme.Mixer", c * 240, r * 180, $"M{c}_{r}");
            }
        }

        // Act
        var wires = new List<Wire>();
        for (var c = 0; c < columns - 1; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                wires.Add(page.Connect(nodes[c, r].Port("out"), nodes[c + 1, r].Port("a")));
                if (r + 1 < rows)
                {
                    wires.Add(page.Connect(nodes[c, r].Port("out"), nodes[c + 1, r + 1].Port("b")));
                }
            }
        }

        // Assert: every wire was created and each is orthogonal
        Assert.Equal(((columns - 1) * rows) + ((columns - 1) * (rows - 1)), wires.Count);
        Assert.All(wires, wire => Assert.True(RouteAssertions.IsOrthogonal(wire.GetPoints())));
    }

    /// <summary>
    ///     A router for tests that records every request and returns fixed bend points.
    /// </summary>
    private sealed class RecordingRouter : IWireRouter
    {
        /// <summary>
        ///     The bend points returned for every request.
        /// </summary>
        private readonly Point[] _route;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RecordingRouter"/> class.
        /// </summary>
        /// <param name="route">The bend points to return.</param>
        public RecordingRouter(Point[] route) => _route = route;

        /// <summary>
        ///     Gets the requests received so far.
        /// </summary>
        public List<RouteRequest> Requests { get; } = [];

        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request)
        {
            Requests.Add(request);
            return _route;
        }
    }
}
