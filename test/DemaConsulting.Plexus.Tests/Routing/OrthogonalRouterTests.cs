using System.Diagnostics;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="OrthogonalRouter"/>: shape, stubs, obstacle avoidance, determinism, fallback and speed.
/// </summary>
public class OrthogonalRouterTests
{
    /// <summary>
    ///     Validates that an unobstructed forward wire becomes a centered Z shape.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_NoObstacles_ForwardTarget_ProducesCenteredZShape()
    {
        // Arrange: the end lies ahead and lower with nothing in between
        var request = new RouteRequest { Start = new Point(100, 50), End = new Point(400, 200) };

        // Act
        var route = new OrthogonalRouter().Route(request);

        // Assert: the vertical run is centered between the two stubs (250), snapped to the 15 unit grid (255)
        Assert.Equal(new[] { new Point(255, 50), new Point(255, 200) }, route);
    }

    /// <summary>
    ///     Validates that ports on the same row are joined by a straight wire with no bend points.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_SameRow_ReturnsNoBendPoints()
    {
        // Arrange
        var request = new RouteRequest { Start = new Point(0, 50), End = new Point(200, 50) };

        // Act
        var route = new OrthogonalRouter().Route(request);

        // Assert
        Assert.Empty(route);
    }

    /// <summary>
    ///     Validates that a wire routes around a node box that sits directly between its ports.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ObstacleBetweenPorts_AvoidsObstacleInterior()
    {
        // Arrange: a tall box blocks the straight path
        var obstacle = new Rect(150, -100, 100, 300);
        var request = new RouteRequest
        {
            Start = new Point(100, 50),
            End = new Point(400, 50),
            Obstacles = [obstacle],
        };

        // Act
        var route = new OrthogonalRouter().Route(request);
        var polyline = RouteAssertions.Polyline(request, route);

        // Assert: orthogonal, detours, and never enters the obstacle
        Assert.NotEmpty(route);
        Assert.True(RouteAssertions.IsOrthogonal(polyline));
        Assert.False(RouteAssertions.CrossesAny(polyline, [obstacle]));
    }

    /// <summary>
    ///     Validates that a wire whose target lies behind its source still leaves right and enters from the left.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_TargetBehindSource_UsesStubsOutAndIn()
    {
        // Arrange: the end is left of the start and the nodes' own margin boxes are obstacles
        var request = new RouteRequest
        {
            Start = new Point(300, 50),
            End = new Point(100, 200),
            Obstacles = [new Rect(195, 5, 120, 120), new Rect(85, 155, 120, 120)],
        };

        // Act
        var route = new OrthogonalRouter().Route(request);
        var polyline = RouteAssertions.Polyline(request, route);

        // Assert: the first segment runs right for at least one stub, the last segment enters from the left
        Assert.True(RouteAssertions.IsOrthogonal(polyline));
        Assert.Equal(request.Start.Y, polyline[1].Y);
        Assert.True(polyline[1].X >= request.Start.X + request.StubLength - 1e-9);
        Assert.Equal(request.End.Y, polyline[^2].Y);
        Assert.True(polyline[^2].X <= request.End.X - request.StubLength + 1e-9);
    }

    /// <summary>
    ///     Validates that top and bottom sides are honoured as stub directions.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_BottomStartSide_LeavesDownward()
    {
        // Arrange
        var request = new RouteRequest
        {
            Start = new Point(100, 100),
            End = new Point(300, 300),
            StartDirection = PortSide.Bottom,
            EndDirection = PortSide.Top,
        };

        // Act
        var route = new OrthogonalRouter().Route(request);
        var polyline = RouteAssertions.Polyline(request, route);

        // Assert: the first segment goes straight down by at least the stub length
        Assert.True(RouteAssertions.IsOrthogonal(polyline));
        Assert.Equal(request.Start.X, polyline[1].X);
        Assert.True(polyline[1].Y >= request.Start.Y + request.StubLength - 1e-9);
    }

    /// <summary>
    ///     Validates that routing the same request repeatedly (and with a new router) yields identical points.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_SameRequest_IsDeterministic()
    {
        // Arrange
        var request = new RouteRequest
        {
            Start = new Point(100, 50),
            End = new Point(400, 50),
            Obstacles = [new Rect(150, -100, 100, 300), new Rect(260, 20, 60, 60)],
        };

        // Act
        var first = new OrthogonalRouter().Route(request);
        var second = new OrthogonalRouter().Route(request);
        var third = new OrthogonalRouter().Route(request);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    /// <summary>
    ///     Validates that a walled-in end port falls back to the simple elbow route.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_EndWalledIn_FallsBackToSimpleRouter()
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

        // Act
        var route = new OrthogonalRouter().Route(request);

        // Assert
        Assert.Equal(new SimpleRouter().Route(request), route);
    }

    /// <summary>
    ///     Validates that an obstacle covering a stub point (nodes closer than twice the margin) does not stop routing.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_ObstacleCoversStubPoint_StillRoutes()
    {
        // Arrange: a neighbor's margin box reaches over the start stub point (115,50)
        var request = new RouteRequest
        {
            Start = new Point(100, 50),
            End = new Point(400, 50),
            Obstacles = [new Rect(105, -50, 100, 200)],
        };

        // Act
        var route = new OrthogonalRouter().Route(request);
        var polyline = RouteAssertions.Polyline(request, route);

        // Assert: a path exists and is orthogonal (the fallback is also orthogonal, so check it is not the fallback elbow)
        Assert.True(RouteAssertions.IsOrthogonal(polyline));
        Assert.NotEqual(new SimpleRouter().Route(request), route);
    }

    /// <summary>
    ///     Validates that when the two port rows differ only slightly the small vertical step sits in the middle of the free run between the stubs.
    /// </summary>
    /// <param name="offset">The vertical distance between the ports.</param>
    /// <param name="startX">The x of the start port.</param>
    /// <param name="endX">The x of the end port.</param>
    [Theory]
    [InlineData(7.5, 100, 500)]
    [InlineData(-7.5, 100, 500)]
    [InlineData(15, 100, 260)]
    [InlineData(7.5, 0, 1000)]
    [InlineData(22.5, 40, 400)]
    public void OrthogonalRouter_Route_SmallRowOffset_PlacesStepInMiddleOfRun(double offset, double startX, double endX)
    {
        // Arrange: no obstacles, ports a fraction of a grid apart
        var request = new RouteRequest { Start = new Point(startX, 50), End = new Point(endX, 50 + offset) };

        // Act
        var route = new OrthogonalRouter().Route(request);

        // Assert: one vertical step, at most half a grid away from the middle of the run between the two stubs
        var middle = ((startX + request.StubLength) + (endX - request.StubLength)) / 2;
        Assert.Equal(2, route.Count);
        Assert.Equal(route[0].X, route[1].X);
        Assert.InRange(route[0].X, middle - (request.GridSize / 2), middle + (request.GridSize / 2));
    }

    /// <summary>
    ///     Validates that the mid-run step stays in the middle when the end nodes' margin boxes flank the run, and is repeatable.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_SmallRowOffsetBetweenNodes_StepInMiddleAndDeterministic()
    {
        // Arrange: the margin boxes of the two nodes end at the stubs; the free run is 130 .. 370
        var request = new RouteRequest
        {
            Start = new Point(115, 50),
            End = new Point(385, 57.5),
            Obstacles = [new Rect(-100, 0, 215, 100), new Rect(385, 0, 215, 100)],
        };

        // Act
        var first = new OrthogonalRouter().Route(request);
        var second = new OrthogonalRouter().Route(request);

        // Assert: the step is not hugging the source: it lies within half a grid of the middle of the stubs (250)
        Assert.Equal(first, second);
        Assert.Equal(2, first.Count);
        Assert.InRange(first[0].X, 250 - (request.GridSize / 2), 250 + (request.GridSize / 2));
    }

    /// <summary>
    ///     Validates that a null request is rejected.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new OrthogonalRouter().Route(null!));
    }

    /// <summary>
    ///     Validates that two hundred wires among three hundred nodes route quickly and never cross a node's margin box.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Route_TwoHundredWiresAmongThreeHundredNodes_FastAndClear()
    {
        // Arrange: a 20 x 15 lattice of 90 x 90 nodes at pitch 150, each inflated by the 15 unit margin
        const int columns = 20;
        const int nodeCount = 300;
        const double stub = 15;
        var obstacles = new List<Rect>(nodeCount);
        for (var i = 0; i < nodeCount; i++)
        {
            obstacles.Add(new Rect(i % columns * 150, i / columns * 150, 90, 90).Inflate(15));
        }

        var router = new OrthogonalRouter();
        var requests = new List<RouteRequest>();
        for (var k = 0; k < 200; k++)
        {
            var from = k * 37 % nodeCount;
            var to = ((k * 91) + 7) % nodeCount;
            if (from == to)
            {
                to = (to + 1) % nodeCount;
            }

            requests.Add(new RouteRequest
            {
                Start = new Point((from % columns * 150) + 90, (from / columns * 150) + 45),
                End = new Point(to % columns * 150, (to / columns * 150) + 45),
                Obstacles = obstacles,
            });
        }

        // Warm up so JIT time is not billed to the measurement
        router.Route(requests[0]);

        // Act
        var watch = Stopwatch.StartNew();
        var routes = requests.Select(r => router.Route(r)).ToList();
        watch.Stop();

        // Assert: generous bound (10 ms per wire; typically under 1 ms) and every path clear of every obstacle
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Routing took {watch.ElapsedMilliseconds} ms");
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            List<Point> polyline = [request.Start.Offset(stub, 0), .. routes[i], request.End.Offset(-stub, 0)];
            Assert.True(RouteAssertions.IsOrthogonal(polyline), $"Wire {i} is not orthogonal");
            Assert.False(RouteAssertions.CrossesAny(polyline, obstacles), $"Wire {i} crosses an obstacle");
        }
    }
}
