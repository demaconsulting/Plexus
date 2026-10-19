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
    ///     Validates that a page re-themed while attached, or restored by undo after the theme changed, routes new wires with the new theme.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_AfterThemeChange_UsesTheNewTheme()
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
        var (live, attached) = Build(DiagramTheme.Classic);
        var (restored, page) = Build(DiagramTheme.Classic);

        // Act
        live.Theme = wide;
        var attachedRoute = attached.Connect("P.value", "C.in").Route;
        restored.Pages.Remove(page);
        restored.Theme = wide;
        restored.History.Undo();
        var route = page.Connect("P.value", "C.in").Route;

        // Assert: the margin changes the route, and both re-themed pages route like the page that always had it
        Assert.NotEqual(plain.Connect("P.value", "C.in").Route, fresh.Connect("P.value", "C.in").Route);
        Assert.Equal(fresh.Wires[^1].Route, attachedRoute);
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
                nodes[c, r] = page.AddNode("acme.Mixer", c * 240.0, r * 180.0, $"M{c}_{r}");
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
    ///     Validates that a host-supplied router is asked to route new wires and receives the port anchors, the stub length,
    ///     the grid size and the node boxes as obstacles.
    /// </summary>
    [Fact]
    public void Routing_HostRouter_ReceivesRequestForNewWires()
    {
        // Arrange: a grid size other than the request's default, so passing it on is observable
        var router = new RecordingRouter([new Point(111, 222)]);
        var diagram = TestModel.CreateDiagram();
        diagram.Theme = DiagramTheme.Classic with { GridSize = 20 };
        diagram.Router = router;
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert: the wire carries exactly what the host router returned
        Assert.Equal(new[] { new Point(111, 222) }, wire.Route);

        // The request describes the wire's ends, the theme's stub length and grid size, and the node boxes
        var request = Assert.Single(router.Requests);
        Assert.Equal(20.0, request.GridSize);
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
        var router = new OrthogonalRouter();

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
                nodes[c, r] = page.AddNode("acme.Mixer", c * 240.0, r * 180.0, $"M{c}_{r}");
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
    ///     Validates that a new wire is routed around the frame of a group between its ports that contains neither end node.
    /// </summary>
    [Fact]
    public void Routing_GroupFrameBetweenPorts_IsRoutedAround()
    {
        // Arrange: a producer and a consumer on one row, and a group framing nodes above and below that row between them
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 800, 0, "C");
        var group = page.CreateGroup(
            [page.AddNode("acme.Gain", 350, -250, "Above"), page.AddNode("acme.Gain", 350, 250, "Below")],
            "G");
        var frame = group.Bounds.Inflate(diagram.Theme.ObstacleMargin);

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var points = wire.GetPoints();

        // Assert: the wire detours orthogonally and never enters the frame with its clearance
        Assert.NotEmpty(wire.Route);
        Assert.True(RouteAssertions.IsOrthogonal(points));
        Assert.False(RouteAssertions.CrossesAny(points, [frame]));
    }

    /// <summary>
    ///     Validates that wires of two nets feeding adjacent inputs use separate tracks when connected and after the page
    ///     is re-routed, while two wires of one net may share their trunk.
    /// </summary>
    [Fact]
    public void Routing_TwoNets_UseSeparateTracksAfterConnectAndReroute()
    {
        // Arrange: two producers feeding two inputs of one mixer, and the first producer also feeding a consumer
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var p1 = page.AddNode("acme.Producer", 0, 0, "P1");
        var p2 = page.AddNode("acme.Producer", 0, 300, "P2");
        var mixer = page.AddNode("acme.Mixer", 450, 120, "M");
        var consumer = page.AddNode("acme.Consumer", 450, 420, "C");

        // Act: connect, then re-route the whole page
        var first = page.Connect(p1.Port("value"), mixer.Port("a"));
        var second = page.Connect(p2.Port("value"), mixer.Port("b"));
        var branch = page.Connect(p1.Port("value"), consumer.Port("in"));
        var connectedOverlap = SharedLength(first.GetPoints(), second.GetPoints());
        page.RerouteAll();

        // Assert: the two nets never run on top of each other; the branch of the first net may share its trunk
        Assert.True(connectedOverlap < 1e-6);
        Assert.True(SharedLength(first.GetPoints(), second.GetPoints()) < 1e-6);
        Assert.True(SharedLength(second.GetPoints(), branch.GetPoints()) < 1e-6);
        Assert.All(page.Wires, w => Assert.True(RouteAssertions.IsOrthogonal(w.GetPoints())));
    }

    /// <summary>
    ///     Validates that every stored route is normalized: collinear bends merge and zero-length segments disappear,
    ///     without changing the drawn shape.
    /// </summary>
    [Fact]
    public void Routing_StoredRoutes_AreNormalized()
    {
        // Arrange: two ports on different rows
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 600, 300, "C");
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;
        Point[] padded = [new(a.X + 15, a.Y), new(a.X + 30, a.Y), new(a.X + 30, z.Y), new(a.X + 30, z.Y), z];

        // Act: the same padded route given on connect and set later
        var connected = page.Connect(producer.Port("value"), consumer.Port("in"), padded);
        var direct = RouteCleaner.Clean(a, padded, z);
        connected.SetRoute([new(a.X + 60, a.Y), new(a.X + 60, a.Y), new(a.X + 60, z.Y)]);

        // Assert: the routing normalization and the stored routes agree, and the shape is unchanged
        Assert.Equal([new Point(a.X + 30, a.Y), new Point(a.X + 30, z.Y)], direct);
        Assert.Equal([new Point(a.X + 60, a.Y), new Point(a.X + 60, z.Y)], connected.Route);
        Assert.True(RouteAssertions.IsOrthogonal(connected.GetPoints()));
    }

    /// <summary>
    ///     Validates that moving a segment, moving a bend and adding a jog keep a route orthogonal and its ends at the ports.
    /// </summary>
    [Fact]
    public void Routing_RouteEditing_KeepsRoutesOrthogonalWithFixedPorts()
    {
        // Arrange: a routed wire between two ports on different rows
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 600, 300, "C");
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;
        var stub = diagram.Theme.WireStubLength;
        IReadOnlyList<Point> route = [new(a.X + 150, a.Y), new(a.X + 150, z.Y)];

        // Act: the three editing operations on that route
        var segmentMoved = RouteEditor.MoveSegment(a, route, z, 1, a.X + 100, stub);
        var bendMoved = RouteEditor.MoveBend(a, route, z, 0, new Point(a.X + 200, a.Y + 40), stub);
        var jogged = RouteEditor.InsertJog(a, route, z, 1, new Point(a.X + 150, (a.Y + z.Y) / 2), 30);

        // Assert: every result is orthogonal from the source anchor to the target anchor
        foreach (var edited in new[] { segmentMoved, bendMoved, jogged })
        {
            Assert.NotNull(edited);
            var polyline = new List<Point> { a };
            polyline.AddRange(edited);
            polyline.Add(z);
            Assert.True(RouteAssertions.IsOrthogonal(polyline));
            Assert.Equal(a, polyline[0]);
            Assert.Equal(z, polyline[^1]);
        }

        Assert.Equal([new Point(a.X + 100, a.Y), new Point(a.X + 100, z.Y)], segmentMoved);
    }

    /// <summary>
    ///     Measures how much of two polylines runs on top of each other.
    /// </summary>
    /// <param name="a">The first polyline.</param>
    /// <param name="b">The second polyline.</param>
    /// <returns>The total collinear overlap length.</returns>
    private static double SharedLength(IReadOnlyList<Point> a, IReadOnlyList<Point> b)
    {
        const double epsilon = 1e-6;
        var total = 0.0;
        for (var i = 0; i + 1 < a.Count; i++)
        {
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var vertical = Math.Abs(a[i].X - a[i + 1].X) < epsilon && Math.Abs(b[j].X - b[j + 1].X) < epsilon && Math.Abs(a[i].X - b[j].X) < epsilon;
                var horizontal = Math.Abs(a[i].Y - a[i + 1].Y) < epsilon && Math.Abs(b[j].Y - b[j + 1].Y) < epsilon && Math.Abs(a[i].Y - b[j].Y) < epsilon;
                if (vertical)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a[i].Y, a[i + 1].Y), Math.Max(b[j].Y, b[j + 1].Y)) - Math.Max(Math.Min(a[i].Y, a[i + 1].Y), Math.Min(b[j].Y, b[j + 1].Y)));
                }
                else if (horizontal)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a[i].X, a[i + 1].X), Math.Max(b[j].X, b[j + 1].X)) - Math.Max(Math.Min(a[i].X, a[i + 1].X), Math.Min(b[j].X, b[j + 1].X)));
                }
            }
        }

        return total;
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
