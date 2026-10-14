using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for connection validation (<see cref="ConnectionRules"/>, <see cref="DiagramPage.CanConnect"/>) and wire creation.
/// </summary>
public class ConnectionTests
{
    /// <summary>
    ///     Creates a diagram with a producer, a gain, a consumer and an integer consumer on one page.
    /// </summary>
    /// <returns>The diagram, page and nodes.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Gain, Node Consumer, Node IntConsumer) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (
            diagram,
            page,
            page.AddNode("acme.Producer", 0, 0),
            page.AddNode("acme.Gain", 150, 0),
            page.AddNode("acme.Consumer", 300, 0),
            page.AddNode("acme.IntConsumer", 300, 150));
    }

    /// <summary>
    ///     Validates that connecting an output to an input creates a wire with the right ends.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_OutputToInput_CreatesWire()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert
        Assert.Same(producer.Port("value"), wire.Source);
        Assert.Same(consumer.Port("in"), wire.Target);
        Assert.Contains(wire, page.Wires);
        Assert.Same(wire, diagram.FindElement(wire.Id));
        Assert.Equal(new[] { wire }, producer.Port("value").Wires);
        Assert.Equal(new[] { wire }, consumer.Wires);
    }

    /// <summary>
    ///     Validates that reversed ports are ordered output-to-input automatically.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_ReversedPorts_AutoOrdersOutputToInput()
    {
        // Arrange
        var (_, page, producer, _, consumer, _) = Create();

        // Act: pass the input first
        var wire = consumer.Port("in").ConnectTo(producer.Port("value"));

        // Assert
        Assert.Same(producer.Port("value"), wire.Source);
        Assert.Same(consumer.Port("in"), wire.Target);
        Assert.Single(page.Wires);
    }

    /// <summary>
    ///     Validates the structural rejections: same direction, self-loop and duplicates.
    /// </summary>
    [Fact]
    public void DiagramPage_CanConnect_StructuralViolations_AreDeniedWithReasons()
    {
        // Arrange
        var (_, page, producer, gain, consumer, _) = Create();
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var twoInputs = page.CanConnect(gain.Port("in"), consumer.Port("in"));
        var twoOutputs = page.CanConnect(producer.Port("value"), gain.Port("out"));
        var selfLoop = page.CanConnect(gain.Port("out"), gain.Port("in"));
        var duplicate = page.CanConnect(producer.Port("value"), consumer.Port("in"));
        var allowed = page.CanConnect(producer.Port("value"), gain.Port("in"));

        // Assert
        Assert.All(new[] { twoInputs, twoOutputs, selfLoop, duplicate }, c =>
        {
            Assert.False(c.IsAllowed);
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
        });
        Assert.True(allowed.IsAllowed);
        Assert.Null(allowed.Reason);
        Assert.Contains("already connected", duplicate.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that ports of nodes on different pages cannot be connected.
    /// </summary>
    [Fact]
    public void DiagramPage_CanConnect_PortsOnDifferentPages_IsDenied()
    {
        // Arrange
        var (diagram, page, producer, _, _, _) = Create();
        var other = diagram.Pages.Add("Other");
        var elsewhere = other.AddNode("acme.Consumer", 0, 0);

        // Act
        var check = page.CanConnect(producer.Port("value"), elsewhere.Port("in"));

        // Assert
        Assert.False(check.IsAllowed);
        Assert.Throws<InvalidOperationException>(() => page.Connect(producer.Port("value"), elsewhere.Port("in")));
    }

    /// <summary>
    ///     Validates that Connect throws with the denial reason as the message.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_Denied_ThrowsInvalidOperationExceptionWithReason()
    {
        // Arrange
        var (_, page, _, gain, _, _) = Create();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => page.Connect(gain.Port("out"), gain.Port("in")));

        // Assert
        Assert.Contains("itself", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the SameType rule rejects mismatched port types and accepts equal ones.
    /// </summary>
    [Fact]
    public void ConnectionRules_SameType_RejectsMismatchedTypes()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, intConsumer) = Create();
        diagram.ConnectionRules = ConnectionRules.SameType;

        // Act
        var mismatch = page.CanConnect(producer.Port("value"), intConsumer.Port("in"));
        var match = page.CanConnect(producer.Port("value"), consumer.Port("in"));

        // Assert
        Assert.False(mismatch.IsAllowed);
        Assert.Contains("f32", mismatch.Reason, StringComparison.Ordinal);
        Assert.True(match.IsAllowed);
    }

    /// <summary>
    ///     Validates that the SingleInputWire rule allows only one wire per input.
    /// </summary>
    [Fact]
    public void ConnectionRules_SingleInputWire_RejectsSecondWireToInput()
    {
        // Arrange
        var (diagram, page, producer, gain, consumer, _) = Create();
        diagram.ConnectionRules = ConnectionRules.SingleInputWire;
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var second = page.CanConnect(gain.Port("out"), consumer.Port("in"));
        var other = page.CanConnect(producer.Port("value"), gain.Port("in"));

        // Assert
        Assert.False(second.IsAllowed);
        Assert.True(other.IsAllowed);
    }

    /// <summary>
    ///     Validates that the default rules refuse a second wire into an input, with a clear reason, while fan-out stays allowed.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_DefaultRules_RefuseFanInAndAllowFanOut()
    {
        // Arrange: the diagram still has its initial rule
        var (diagram, page, producer, gain, consumer, _) = Create();
        var first = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var check = page.CanConnect(gain.Port("out"), consumer.Port("in"));
        var thrown = Assert.Throws<InvalidOperationException>(() => page.Connect(gain.Port("out"), consumer.Port("in")));
        var fanOut = page.Connect(producer.Port("value"), gain.Port("in"));

        // Assert
        Assert.Multiple(
            () => Assert.Same(ConnectionRules.Default, diagram.ConnectionRules),
            () => Assert.False(check.IsAllowed),
            () => Assert.Equal($"Input '{consumer.Name}.in' already has a wire; it does not accept more than one.", check.Reason),
            () => Assert.Equal(check.Reason, thrown.Message),
            () => Assert.Equal(new[] { first }, consumer.Port("in").Wires),
            () => Assert.Equal(2, producer.Port("value").Wires.Count),
            () => Assert.NotNull(fanOut));
    }

    /// <summary>
    ///     Validates that an input whose definition allows fan-in accepts any number of wires.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_InputAllowsMultipleWires_AcceptsSeveralWires()
    {
        // Arrange
        var (_, page, producer, gain, _, _) = Create();
        var merger = page.AddNode("acme.Merger", 450, 0);

        // Act
        page.Connect(producer.Port("value"), merger.Port("in"));
        var second = page.CanConnect(gain.Port("out"), merger.Port("in"));
        page.Connect(gain.Port("out"), merger.Port("in"));

        // Assert
        Assert.Multiple(
            () => Assert.True(merger.Port("in").AllowsMultipleWires),
            () => Assert.True(merger.Port("in").Definition.AllowsMultipleWires),
            () => Assert.True(second.IsAllowed),
            () => Assert.Equal(2, merger.Port("in").Wires.Count));
    }

    /// <summary>
    ///     Validates that fan-in is off by default for inputs and that outputs always report fan-out as allowed.
    /// </summary>
    [Fact]
    public void Port_AllowsMultipleWires_DefaultsToFalseForInputsAndTrueForOutputs()
    {
        // Arrange
        var (_, _, producer, _, consumer, _) = Create();

        // Assert
        Assert.Multiple(
            () => Assert.False(new PortDefinition("x", "f32").AllowsMultipleWires),
            () => Assert.False(consumer.Port("in").AllowsMultipleWires),
            () => Assert.True(producer.Port("value").AllowsMultipleWires));
    }

    /// <summary>
    ///     Validates that a host that replaces the rules without the default may allow fan-in everywhere.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_HostRuleWithoutDefault_AllowsFanInEverywhere()
    {
        // Arrange
        var (diagram, page, producer, gain, consumer, _) = Create();
        diagram.ConnectionRules = ConnectionRules.SameType;

        // Act
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var second = page.Connect(gain.Port("out"), consumer.Port("in"));

        // Assert
        Assert.Equal(2, consumer.Port("in").Wires.Count);
        Assert.NotNull(second);
    }

    /// <summary>
    ///     Validates that reconnecting a wire onto an occupied input is refused unless the input allows fan-in, and that keeping a wire on its own target is fine.
    /// </summary>
    [Fact]
    public void DiagramPage_CanReconnect_OntoOccupiedInput_FollowsFanInRule()
    {
        // Arrange: a wire into the consumer, another into the gain, and a merging input
        var (_, page, producer, gain, consumer, _) = Create();
        var merger = page.AddNode("acme.Merger", 450, 0);
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var second = page.AddNode("acme.Producer", 0, 300);
        var moving = page.Connect(second.Port("value"), gain.Port("in"));
        page.Connect(gain.Port("out"), merger.Port("in"));

        // Act
        var onto = page.CanReconnect(moving, null, consumer.Port("in"));
        var thrown = Assert.Throws<InvalidOperationException>(() => moving.Reconnect(null, consumer.Port("in")));
        var own = page.CanReconnect(moving, null, gain.Port("in"));
        var newSource = page.CanReconnect(moving, producer.Port("value"), null);
        var intoMerger = page.CanReconnect(moving, null, merger.Port("in"));

        // Assert
        Assert.Multiple(
            () => Assert.False(onto.IsAllowed),
            () => Assert.Contains("does not accept more than one", onto.Reason, StringComparison.Ordinal),
            () => Assert.Equal(onto.Reason, thrown.Message),
            () => Assert.Same(gain.Port("in"), moving.Target),
            () => Assert.True(own.IsAllowed),
            () => Assert.True(newSource.IsAllowed),
            () => Assert.True(intoMerger.IsAllowed));
    }

    /// <summary>
    ///     Validates that All combines rules and reports the first denial.
    /// </summary>
    [Fact]
    public void ConnectionRules_All_CombinesRulesAndReportsFirstDenial()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, intConsumer) = Create();
        diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.SameType, ConnectionRules.SingleInputWire);
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var typeProblem = page.CanConnect(producer.Port("value"), intConsumer.Port("in"));
        var wireProblem = page.CanConnect(page.AddNode("acme.Gain", 0, 300).Port("out"), consumer.Port("in"));

        // Assert
        Assert.False(typeProblem.IsAllowed);
        Assert.Contains("types differ", typeProblem.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(wireProblem.IsAllowed);
        Assert.Contains("already has a wire", wireProblem.Reason, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => ConnectionRules.All(null!));
        Assert.Throws<ArgumentNullException>(() => ConnectionRules.All(ConnectionRules.Default, null!));
    }

    /// <summary>
    ///     Validates that the default rule allows any structurally valid connection and that host rules see output then input.
    /// </summary>
    [Fact]
    public void ConnectionRules_Default_AllowsAndHostRuleReceivesOutputThenInput()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        Assert.Same(ConnectionRules.Default, diagram.ConnectionRules);
        Port? seenFrom = null;
        Port? seenTo = null;
        diagram.ConnectionRules = (from, to) =>
        {
            seenFrom = from;
            seenTo = to;
            return ConnectionCheck.Allowed;
        };

        // Act: ask with reversed ports
        var check = page.CanConnect(consumer.Port("in"), producer.Port("value"));

        // Assert
        Assert.True(check.IsAllowed);
        Assert.Same(producer.Port("value"), seenFrom);
        Assert.Same(consumer.Port("in"), seenTo);
        Assert.Throws<ArgumentNullException>(() => diagram.ConnectionRules = null!);
    }

    /// <summary>
    ///     Validates that a host rule returning a denial without a reason still produces a reason.
    /// </summary>
    [Fact]
    public void DiagramPage_CanConnect_HostDenialWithoutReason_GetsDefaultReason()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        diagram.ConnectionRules = (_, _) => default;

        // Act
        var check = page.CanConnect(producer.Port("value"), consumer.Port("in"));

        // Assert
        Assert.False(check.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(check.Reason));
    }

    /// <summary>
    ///     Validates that a supplied route is stored as-is and that a null route uses the router.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_RouteSuppliedOrNull_UsesSuppliedRouteOrRouter()
    {
        // Arrange
        var (_, page, producer, _, consumer, _) = Create();
        var manual = new[] { new Point(120, 10), new Point(120, 200) };

        // A pair of nodes on one row with nothing between them, far enough apart for their port labels
        var lowerSource = page.AddNode("acme.Producer", 0, 300);
        var lowerTarget = page.AddNode("acme.Gain", 300, 300);

        // Act
        var manualWire = page.Connect(producer.Port("value"), consumer.Port("in"), manual);
        var routed = page.Connect(lowerSource.Port("value"), lowerTarget.Port("in"));

        // Assert: manual route kept; ports at the same height are joined by a straight wire (no bends)
        Assert.Equal(manual, manualWire.Route);
        Assert.Empty(routed.Route);
    }

    /// <summary>
    ///     Validates that GetPoints starts and ends at the port anchors.
    /// </summary>
    [Fact]
    public void Wire_GetPoints_IncludesAnchorsAndBendPoints()
    {
        // Arrange
        var (_, page, producer, _, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(200, 60)]);

        // Act
        var points = wire.GetPoints();

        // Assert
        Assert.Equal(3, points.Count);
        Assert.Equal(producer.Port("value").Anchor, points[0]);
        Assert.Equal(new Point(200, 60), points[1]);
        Assert.Equal(consumer.Port("in").Anchor, points[2]);
    }

    /// <summary>
    ///     Validates that SetRoute and Reroute are undoable and that a no-op SetRoute records nothing.
    /// </summary>
    [Fact]
    public void Wire_SetRouteAndReroute_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(200, 60)]);
        var entries = diagram.History.UndoCount;

        // Act / Assert
        wire.SetRoute([new Point(200, 60)]);
        Assert.Equal(entries, diagram.History.UndoCount);
        TestModel.AssertRoundTrip(diagram, () => wire.SetRoute([new Point(210, 70), new Point(210, 120)]));
        TestModel.AssertRoundTrip(diagram, wire.Reroute);
        Assert.Throws<ArgumentNullException>(() => wire.SetRoute(null!));
    }

    /// <summary>
    ///     Validates that wires can be removed and that undo restores them with their route and id.
    /// </summary>
    [Fact]
    public void Wire_Remove_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, page, producer, _, consumer, _) = Create();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(200, 60)]);

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, wire.Remove);
    }
}
