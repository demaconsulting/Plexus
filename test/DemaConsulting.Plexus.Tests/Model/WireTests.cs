using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the Wire unit: endpoints, route, polyline, bounds, route editing and removal.
/// </summary>
public class WireTests
{
    /// <summary>
    ///     Creates a page with a Producer and a Consumer.
    /// </summary>
    /// <returns>The diagram, page and the two nodes.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Consumer) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (
            diagram,
            page,
            page.AddNode("acme.Producer", 0, 0, "P"),
            page.AddNode("acme.Consumer", 300, 100, "C"));
    }

    /// <summary>
    ///     Validates that a wire runs from the output port to the input port even when connected in reverse order.
    /// </summary>
    [Fact]
    public void Wire_SourceAndTarget_ReversedConnect_RunFromOutputToInput()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();

        // Act
        var wire = page.Connect(consumer.Port("in"), producer.Port("value"));

        // Assert
        Assert.Same(producer.Port("value"), wire.Source);
        Assert.Same(consumer.Port("in"), wire.Target);
        Assert.Equal(PortDirection.Output, wire.Source.Direction);
        Assert.Equal(PortDirection.Input, wire.Target.Direction);
    }

    /// <summary>
    ///     Validates that the stored route is a copy of the supplied list.
    /// </summary>
    [Fact]
    public void Wire_Route_SuppliedList_IsCopied()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();
        var bends = new List<Point> { new(150, 30), new(150, 130) };

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), bends);
        bends[0] = new Point(999, 999);
        bends.Add(new Point(1, 1));

        // Assert
        Assert.Equal([new Point(150, 30), new Point(150, 130)], wire.Route);
    }

    /// <summary>
    ///     Validates that the polyline end points follow the ports when a node moves.
    /// </summary>
    [Fact]
    public void Wire_GetPoints_EndNodeMoved_AnchorsFollowPorts()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, 30)]);

        // Act
        consumer.MoveTo(500, 300);
        var points = wire.GetPoints();

        // Assert: first and last points are the current anchors, whatever route the move produced
        Assert.Equal(producer.Port("value").Anchor, points[0]);
        Assert.Equal(consumer.Port("in").Anchor, points[^1]);
        Assert.Equal(wire.Route.Count + 2, points.Count);
    }

    /// <summary>
    ///     Validates that a wire's bounds are reported by DiagramElement.GetBounds through the base type.
    /// </summary>
    [Fact]
    public void Wire_GetBounds_ThroughBaseType_EqualsPolylineBox()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();
        DiagramElement wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(60, 400)]);

        // Act
        var bounds = wire.GetBounds();

        // Assert
        Assert.Equal(400, bounds.Bottom);
        Assert.Equal(consumer.Port("in").Anchor.X, bounds.Right);
    }

    /// <summary>
    ///     Validates that SetRoute stores a copy of the supplied points.
    /// </summary>
    [Fact]
    public void Wire_SetRoute_SuppliedList_IsCopied()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), []);
        var bends = new List<Point> { new(150, 30) };

        // Act
        wire.SetRoute(bends);
        bends.Clear();

        // Assert
        Assert.Equal([new Point(150, 30)], wire.Route);
    }

    /// <summary>
    ///     Validates that Reroute replaces a manual route with the route the router computes, undoably.
    /// </summary>
    [Fact]
    public void Wire_Reroute_ManualRoute_RestoresRouterRouteAndUndoes()
    {
        // Arrange
        var (diagram, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var routed = wire.Route.ToArray();
        wire.SetRoute([new Point(10, 500), new Point(20, 500)]);

        // Act
        wire.Reroute();

        // Assert
        Assert.Equal(routed, wire.Route);
        diagram.History.Undo();
        Assert.Equal([new Point(10, 500), new Point(20, 500)], wire.Route);
    }

    /// <summary>
    ///     Validates that removing a wire detaches it from the page and its ports and leaves the nodes in place.
    /// </summary>
    [Fact]
    public void Wire_Remove_AttachedWire_DetachesFromPageAndPorts()
    {
        // Arrange
        var (_, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        wire.Remove();

        // Assert
        Assert.Null(wire.Page);
        Assert.DoesNotContain(wire, page.Wires);
        Assert.Empty(producer.Port("value").Wires);
        Assert.Empty(consumer.Port("in").Wires);
        Assert.Contains(producer, page.Nodes);
        Assert.Throws<InvalidOperationException>(() => wire.Remove());
    }

    /// <summary>
    ///     Validates that route edits are rejected while the wire is removed and allowed again after undo.
    /// </summary>
    [Fact]
    public void Wire_SetRouteAndReroute_RemovedWire_ThrowsInvalidOperationExceptionUntilUndo()
    {
        // Arrange
        var (diagram, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        wire.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => wire.SetRoute([new Point(1, 1)]));
        Assert.Throws<InvalidOperationException>(wire.Reroute);
        diagram.History.Undo();
        wire.SetRoute([new Point(1, 1)]);
        Assert.Equal([new Point(1, 1)], wire.Route);
    }

    /// <summary>
    ///     Validates that a route change raises PropertyChanged and a diagram change, also for undo.
    /// </summary>
    [Fact]
    public void Wire_SetRoute_RouteChanged_RaisesPropertyChangedForSetAndUndo()
    {
        // Arrange
        var (diagram, page, producer, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), []);
        var raised = new List<string?>();
        var diagramEvents = new List<DiagramChangedEventArgs>();
        wire.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        diagram.Changed += (_, e) => diagramEvents.Add(e);

        // Act
        wire.SetRoute([new Point(150, 30)]);
        diagram.History.Undo();

        // Assert
        Assert.Equal([nameof(Wire.Route), nameof(Wire.Route)], raised);
        Assert.Equal(2, diagramEvents.Count(e => e.Kind == DiagramChangeKind.ElementChanged && ReferenceEquals(e.Element, wire)));
    }
}
