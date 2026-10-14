using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for wire editing in the model: route normalization, <see cref="Wire.Reconnect"/>, <see cref="Wire.AddJog"/> and net-aware routing.
/// </summary>
public class WireEditingTests
{
    /// <summary>
    ///     Tolerance for coordinate comparisons.
    /// </summary>
    private const double Epsilon = 1e-6;

    /// <summary>
    ///     Creates a page with a producer, a gain, two consumers (one integer) and a mixer.
    /// </summary>
    /// <returns>The diagram, page and nodes.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Gain, Node Consumer, Node IntConsumer) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (
            diagram,
            page,
            page.AddNode("acme.Producer", 0, 0, "P"),
            page.AddNode("acme.Gain", 300, 150, "G"),
            page.AddNode("acme.Consumer", 600, 0, "C"),
            page.AddNode("acme.IntConsumer", 600, 300, "I"));
    }

    /// <summary>
    ///     Validates that SetRoute removes collinear bends and zero-length segments but never changes the drawn shape.
    /// </summary>
    [Fact]
    public void Wire_SetRoute_CollinearAndZeroLengthBends_AreNormalized()
    {
        // Arrange: the integer consumer sits lower, so the anchors are not level
        var (_, page, producer, _, _, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), []);
        var a = wire.Source.Anchor;
        var z = wire.Target.Anchor;

        // Act: a collinear bend, a duplicated bend and a bend sitting on the target anchor
        wire.SetRoute([new Point(a.X + 15, a.Y), new Point(a.X + 30, a.Y), new Point(a.X + 30, z.Y), new Point(a.X + 30, z.Y), z]);

        // Assert
        Assert.Equal([new Point(a.X + 30, a.Y), new Point(a.X + 30, z.Y)], wire.Route);
    }

    /// <summary>
    ///     Validates that a route supplied to Connect is normalized like every stored route.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_RouteWithCollinearBend_StoresNormalizedRoute()
    {
        // Arrange
        var (_, page, producer, _, _, consumer) = Create();
        var a = producer.Port("value").Anchor;
        var z = consumer.Port("in").Anchor;

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(a.X + 15, a.Y), new Point(a.X + 30, a.Y), new Point(a.X + 30, z.Y)]);

        // Assert
        Assert.Equal([new Point(a.X + 30, a.Y), new Point(a.X + 30, z.Y)], wire.Route);
    }

    /// <summary>
    ///     Validates that an edit that flattens the route to its current shape records nothing.
    /// </summary>
    [Fact]
    public void Wire_SetRoute_SameShapeAfterNormalizing_RecordsNothing()
    {
        // Arrange
        var (diagram, page, producer, _, _, consumer) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var undo = diagram.History.UndoCount;
        var padded = new List<Point>(wire.Route);
        padded.Insert(1, padded[0]);

        // Act
        wire.SetRoute(padded);

        // Assert
        Assert.Equal(undo, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that Reconnect moves one end to another port, keeps the id and re-routes the wire.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_ValidTarget_MovesEndKeepsIdAndReroutes()
    {
        // Arrange
        var (_, page, producer, gain, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), null, "w1");
        var oldRoute = wire.Route.ToArray();

        // Act
        wire.Reconnect(null, gain.Port("in"));

        // Assert: same wire object and id, new target, a route that ends at the new target
        Assert.Multiple(
            () => Assert.Equal("w1", wire.Id),
            () => Assert.Same(wire, Assert.Single(page.Wires)),
            () => Assert.Same(producer.Port("value"), wire.Source),
            () => Assert.Same(gain.Port("in"), wire.Target),
            () => Assert.Equal(gain.Port("in").Anchor, wire.GetPoints()[^1]),
            () => Assert.NotEqual(oldRoute, wire.Route));
    }

    /// <summary>
    ///     Validates that Reconnect can move the source end to another output.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_ValidSource_MovesSourceEnd()
    {
        // Arrange: the gain's output can feed the consumer, too
        var (_, page, producer, gain, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        wire.Reconnect(gain.Port("out"), null);

        // Assert
        Assert.Multiple(
            () => Assert.Same(gain.Port("out"), wire.Source),
            () => Assert.Same(consumer.Port("in"), wire.Target),
            () => Assert.Equal([wire], gain.Port("out").Wires),
            () => Assert.Empty(producer.Port("value").Wires));
    }

    /// <summary>
    ///     Validates that a reconnect is one undo entry that restores ends and route exactly and can be redone.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_UndoRedo_RestoresEndsAndRouteAsOneEntry()
    {
        // Arrange
        var (diagram, page, producer, gain, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var oldRoute = wire.Route.ToArray();
        var undo = diagram.History.UndoCount;

        // Act
        wire.Reconnect(null, gain.Port("in"));
        var newRoute = wire.Route.ToArray();
        var recorded = diagram.History.UndoCount;
        diagram.History.Undo();
        var afterUndo = (wire.Target, wire.Route.ToArray());
        diagram.History.Redo();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(undo + 1, recorded),
            () => Assert.Same(consumer.Port("in"), afterUndo.Target),
            () => Assert.Equal(oldRoute, afterUndo.Item2),
            () => Assert.Same(gain.Port("in"), wire.Target),
            () => Assert.Equal(newRoute, wire.Route),
            () => Assert.Same(wire, Assert.Single(page.Wires)));
    }

    /// <summary>
    ///     Validates that reconnecting to the ports the wire already has records nothing.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_SamePorts_RecordsNothing()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var undo = diagram.History.UndoCount;

        // Act
        wire.Reconnect(producer.Port("value"), consumer.Port("in"));
        wire.Reconnect(null, null);

        // Assert
        Assert.Equal(undo, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that Reconnect rejects invalid moves with the reason and leaves the wire untouched.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_InvalidMoves_ThrowWithReasonAndChangeNothing()
    {
        // Arrange: a wire from the gain to the consumer, plus wires that make a duplicate possible; the host allows
        // fan-in here so the consumer can hold two wires and the duplicate check is what refuses the move
        var (diagram, page, producer, gain, consumer, intConsumer) = Create();
        diagram.ConnectionRules = static (_, _) => ConnectionCheck.Allowed;
        var wire = page.Connect(gain.Port("out"), consumer.Port("in"));
        var other = page.Connect(producer.Port("value"), gain.Port("in"));
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var undo = diagram.History.UndoCount;

        // Act
        var self = Assert.Throws<InvalidOperationException>(() => wire.Reconnect(null, gain.Port("in")));
        var direction = Assert.Throws<InvalidOperationException>(() => wire.Reconnect(consumer.Port("in"), null));
        var duplicate = Assert.Throws<InvalidOperationException>(() => other.Reconnect(null, consumer.Port("in")));
        diagram.ConnectionRules = ConnectionRules.SameType;
        var host = Assert.Throws<InvalidOperationException>(() => wire.Reconnect(null, intConsumer.Port("in")));

        // Assert
        Assert.Multiple(
            () => Assert.Contains("itself", self.Message),
            () => Assert.Contains("output to an input", direction.Message),
            () => Assert.Contains("already connected", duplicate.Message),
            () => Assert.False(string.IsNullOrWhiteSpace(host.Message)),
            () => Assert.Same(consumer.Port("in"), wire.Target),
            () => Assert.Same(gain.Port("out"), wire.Source),
            () => Assert.Equal(undo, diagram.History.UndoCount));
    }

    /// <summary>
    ///     Validates that CanReconnect, unlike CanConnect, does not count the wire itself as a duplicate and still reports real duplicates.
    /// </summary>
    [Fact]
    public void DiagramPage_CanReconnect_ExcludesTheWireItselfFromDuplicateCheck()
    {
        // Arrange: two wires from the producer, to the gain and to the consumer
        var (_, page, producer, gain, consumer, _) = Create();
        var toConsumer = page.Connect(producer.Port("value"), consumer.Port("in"));
        page.Connect(producer.Port("value"), gain.Port("in"));

        // Act
        var own = page.CanReconnect(toConsumer, null, consumer.Port("in"));
        var plain = page.CanConnect(producer.Port("value"), consumer.Port("in"));
        var duplicate = page.CanReconnect(toConsumer, null, gain.Port("in"));

        // Assert
        Assert.Multiple(
            () => Assert.True(own.IsAllowed),
            () => Assert.False(plain.IsAllowed),
            () => Assert.False(duplicate.IsAllowed),
            () => Assert.Contains("already connected", duplicate.Reason));
    }

    /// <summary>
    ///     Validates that reconnecting a removed wire is rejected until the removal is undone.
    /// </summary>
    [Fact]
    public void Wire_Reconnect_RemovedWire_ThrowsInvalidOperationException()
    {
        // Arrange
        var (_, page, producer, gain, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        wire.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => wire.Reconnect(null, gain.Port("in")));
    }

    /// <summary>
    ///     Validates that AddJog adds a draggable bump as one undo entry.
    /// </summary>
    [Fact]
    public void Wire_AddJog_StraightSegment_AddsBumpAsOneUndoEntry()
    {
        // Arrange: a wire with an empty route is a straight line between two level ports
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 450, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), []);
        var before = wire.Route.ToArray();
        var middle = (wire.Source.Anchor.X + wire.Target.Anchor.X) / 2;
        var undo = diagram.History.UndoCount;

        // Act
        var added = wire.AddJog(0, new Point(middle, wire.Source.Anchor.Y));
        var recorded = diagram.History.UndoCount;
        diagram.History.Undo();

        // Assert
        Assert.Multiple(
            () => Assert.True(added),
            () => Assert.Equal(undo + 1, recorded),
            () => Assert.Equal(before, wire.Route));
        diagram.History.Redo();
        Assert.Equal(4, wire.Route.Count);
    }

    /// <summary>
    ///     Validates that AddJog reports false for a segment that does not exist.
    /// </summary>
    [Fact]
    public void Wire_AddJog_UnknownSegment_ReturnsFalse()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var undo = diagram.History.UndoCount;

        // Act
        var added = wire.AddJog(99, new Point(0, 0));

        // Assert
        Assert.False(added);
        Assert.Equal(undo, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that wires of different nets are routed on separate tracks, while the same wires from one output may share.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_TwoNetsToAdjacentInputs_DoNotRunAlongEachOther()
    {
        // Arrange: two producers feeding two inputs of one mixer
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var p1 = page.AddNode("acme.Producer", 0, 0, "P1");
        var p2 = page.AddNode("acme.Producer", 0, 300, "P2");
        var mixer = page.AddNode("acme.Mixer", 450, 120, "M");

        // Act
        var first = page.Connect(p1.Port("value"), mixer.Port("a"));
        var second = page.Connect(p2.Port("value"), mixer.Port("b"));

        // Assert
        Assert.True(OverlapLength(first.GetPoints(), second.GetPoints()) < Epsilon);
    }

    /// <summary>
    ///     Validates that re-routing a whole page keeps different nets apart and is repeatable.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_TwoNets_KeepsNetsApartAndIsRepeatable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var p1 = page.AddNode("acme.Producer", 0, 0, "P1");
        var p2 = page.AddNode("acme.Producer", 0, 300, "P2");
        var mixer = page.AddNode("acme.Mixer", 450, 120, "M");
        var first = page.Connect(p1.Port("value"), mixer.Port("a"));
        var second = page.Connect(p2.Port("value"), mixer.Port("b"));

        // Act
        page.RerouteAll();
        var once = (first.Route.ToArray(), second.Route.ToArray());
        page.RerouteAll();

        // Assert
        Assert.Multiple(
            () => Assert.True(OverlapLength(first.GetPoints(), second.GetPoints()) < Epsilon),
            () => Assert.Equal(once.Item1, first.Route),
            () => Assert.Equal(once.Item2, second.Route));
    }

    /// <summary>
    ///     Measures how much of two polylines runs on top of each other.
    /// </summary>
    /// <param name="a">The first polyline.</param>
    /// <param name="b">The second polyline.</param>
    /// <returns>The total collinear overlap length.</returns>
    private static double OverlapLength(IReadOnlyList<Point> a, IReadOnlyList<Point> b)
    {
        var total = 0.0;
        for (var i = 0; i + 1 < a.Count; i++)
        {
            for (var j = 0; j + 1 < b.Count; j++)
            {
                if (Math.Abs(a[i].X - a[i + 1].X) < Epsilon && Math.Abs(b[j].X - b[j + 1].X) < Epsilon && Math.Abs(a[i].X - b[j].X) < Epsilon)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a[i].Y, a[i + 1].Y), Math.Max(b[j].Y, b[j + 1].Y)) - Math.Max(Math.Min(a[i].Y, a[i + 1].Y), Math.Min(b[j].Y, b[j + 1].Y)));
                }
                else if (Math.Abs(a[i].Y - a[i + 1].Y) < Epsilon && Math.Abs(b[j].Y - b[j + 1].Y) < Epsilon && Math.Abs(a[i].Y - b[j].Y) < Epsilon)
                {
                    total += Math.Max(0, Math.Min(Math.Max(a[i].X, a[i + 1].X), Math.Max(b[j].X, b[j + 1].X)) - Math.Max(Math.Min(a[i].X, a[i + 1].X), Math.Min(b[j].X, b[j + 1].X)));
                }
            }
        }

        return total;
    }
}
