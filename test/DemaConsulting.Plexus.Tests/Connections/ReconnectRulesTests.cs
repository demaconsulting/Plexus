namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for reconnect-aware connection rules: the wire under check, the built-in degree limits and the
///     "no change" reconnect.
/// </summary>
public class ReconnectRulesTests
{
    /// <summary>
    ///     Creates a page with one producer feeding two mergers, a spare merger and a second producer.
    /// </summary>
    /// <returns>The page and the nodes.</returns>
    private static (DiagramPage Page, Node Producer, Node MergerA, Node MergerB, Node MergerC, Node Other) Create()
    {
        var page = TestModel.CreateDiagram().Pages.Add("Main");
        return (
            page,
            page.AddNode("acme.Producer", 0, 0),
            page.AddNode("acme.Merger", 300, 0),
            page.AddNode("acme.Merger", 300, 150),
            page.AddNode("acme.Merger", 300, 300),
            page.AddNode("acme.Producer", 0, 300));
    }

    /// <summary>
    ///     Validates that a reconnect with no change is always allowed, even when a host rule would refuse the wire's own ports.
    /// </summary>
    [Fact]
    public void DiagramPage_CanReconnect_NoChange_IsAllowedWithoutAskingTheRule()
    {
        // Arrange: a rule that refuses everything, applied after the wire exists
        var (page, producer, merger, _, _, _) = Create();
        var wire = page.Connect(producer.Port("value"), merger.Port("in"));
        var asked = 0;
        page.Diagram.ConnectionRules = (_, _) =>
        {
            asked++;
            return ConnectionCheck.Denied("never");
        };

        // Act
        var bothNull = page.CanReconnect(wire, null, null);
        var samePorts = page.CanReconnect(wire, wire.Source, wire.Target);
        var moved = page.CanReconnect(wire, null, merger.Port("in"));

        // Assert: "nothing moves" is allowed, a real move is still judged
        Assert.True(bothNull.IsAllowed);
        Assert.True(samePorts.IsAllowed);
        Assert.True(moved.IsAllowed);
        Assert.Equal(0, asked);
        var real = page.CanReconnect(wire, null, page.Nodes.Last(n => n.Type.Key == "acme.Merger").Port("in"));
        Assert.False(real.IsAllowed);
        Assert.Equal(1, asked);
    }

    /// <summary>
    ///     Validates that the wire under check is public, is the moving wire during the rule call and is null otherwise.
    /// </summary>
    [Fact]
    public void DiagramPage_WireBeingReconnected_IsTheMovingWireOnlyDuringTheCheck()
    {
        // Arrange
        var (page, producer, mergerA, mergerB, _, _) = Create();
        var wire = page.Connect(producer.Port("value"), mergerA.Port("in"));
        var seen = new List<Wire?>();
        page.Diagram.ConnectionRules = (from, _) =>
        {
            seen.Add(from.Node.Page!.WireBeingReconnected);
            return ConnectionCheck.Allowed;
        };

        // Act
        page.CanConnect(producer.Port("value"), mergerB.Port("in"));
        page.CanReconnect(wire, null, mergerB.Port("in"));

        // Assert: null for a plain check, the moving wire for a reconnect, null again afterwards
        Assert.Equal(new Wire?[] { null, wire }, seen);
        Assert.Null(page.WireBeingReconnected);
    }

    /// <summary>
    ///     Validates that the output limit refuses a wire from a full output and the reconnect check does not count the moving wire.
    /// </summary>
    [Fact]
    public void ConnectionRules_MaxOutgoingWires_FullOutput_DeniesButMovingAWireOnTheSameOutputIsFine()
    {
        // Arrange: an output that already feeds two inputs, limit two
        var (page, producer, mergerA, mergerB, mergerC, _) = Create();
        page.Diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.MaxOutgoingWires(2));
        var first = page.Connect(producer.Port("value"), mergerA.Port("in"));
        page.Connect(producer.Port("value"), mergerB.Port("in"));

        // Act
        var third = page.CanConnect(producer.Port("value"), mergerC.Port("in"));
        var thirdReversed = page.CanConnect(mergerC.Port("in"), producer.Port("value"));
        var moveTarget = page.CanReconnect(first, null, mergerC.Port("in"));

        // Assert: a third wire is refused whichever order the ports come in, moving an existing one keeps the count at two
        Assert.False(third.IsAllowed);
        Assert.False(thirdReversed.IsAllowed);
        Assert.Contains(producer.Name, third.Reason, StringComparison.Ordinal);
        Assert.True(moveTarget.IsAllowed);
    }

    /// <summary>
    ///     Validates that moving the source of a wire onto a full output is refused, while moving it to a free one is allowed.
    /// </summary>
    [Fact]
    public void ConnectionRules_MaxOutgoingWires_ReconnectSourceOntoFullOutput_IsDenied()
    {
        // Arrange: producer feeds one input with limit one, another producer has a wire of its own
        var (page, producer, mergerA, mergerB, mergerC, other) = Create();
        page.Diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.MaxOutgoingWires(1));
        page.Connect(producer.Port("value"), mergerA.Port("in"));
        var wire = page.Connect(other.Port("value"), mergerB.Port("in"));
        var spare = page.AddNode("acme.Producer", 0, 600);

        // Act
        var ontoFull = page.CanReconnect(wire, producer.Port("value"), null);
        var ontoFree = page.CanReconnect(wire, spare.Port("value"), null);

        // Assert
        Assert.False(ontoFull.IsAllowed);
        Assert.True(ontoFree.IsAllowed);
        Assert.False(page.CanConnect(producer.Port("value"), mergerC.Port("in")).IsAllowed);
    }

    /// <summary>
    ///     Validates that the input limit refuses a wire into a full fan-in input and ignores the moving wire.
    /// </summary>
    [Fact]
    public void ConnectionRules_MaxIncomingWires_FullInput_DeniesButTheMovingWireIsNotCounted()
    {
        // Arrange: a merger input that takes two wires
        var (page, producer, mergerA, mergerB, _, other) = Create();
        var third = page.AddNode("acme.Producer", 0, 600);
        page.Diagram.ConnectionRules = ConnectionRules.MaxIncomingWires(2);
        var first = page.Connect(producer.Port("value"), mergerA.Port("in"));
        page.Connect(other.Port("value"), mergerA.Port("in"));
        var elsewhere = page.Connect(third.Port("value"), mergerB.Port("in"));

        // Act
        var full = page.CanConnect(third.Port("value"), mergerA.Port("in"));
        var moveOntoFull = page.CanReconnect(elsewhere, null, mergerA.Port("in"));
        var moveSourceOfFull = page.CanReconnect(first, third.Port("value"), null);

        // Assert: full input refuses another wire, but a wire already on it may change its source
        Assert.False(full.IsAllowed);
        Assert.Contains(mergerA.Name, full.Reason, StringComparison.Ordinal);
        Assert.False(moveOntoFull.IsAllowed);
        Assert.True(moveSourceOfFull.IsAllowed);
    }

    /// <summary>
    ///     Validates that the degree-limit rules reject a limit below one.
    /// </summary>
    [Fact]
    public void ConnectionRules_MaxWires_LimitBelowOne_ThrowsArgumentOutOfRange()
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectionRules.MaxOutgoingWires(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectionRules.MaxIncomingWires(-1));
    }

    /// <summary>
    ///     Validates that every rule call receives the output first and the input second, for connects and reconnects, whichever order the caller used.
    /// </summary>
    [Fact]
    public void ConnectionRules_HostRule_AlwaysReceivesOutputThenInput()
    {
        // Arrange
        var (page, producer, merger, mergerB, _, _) = Create();
        var directions = new List<(PortDirection From, PortDirection To)>();
        page.Diagram.ConnectionRules = (from, to) =>
        {
            directions.Add((from.Direction, to.Direction));
            return ConnectionCheck.Allowed;
        };
        var wire = page.Connect(merger.Port("in"), producer.Port("value"));

        // Act
        page.CanConnect(mergerB.Port("in"), producer.Port("value"));
        page.CanReconnect(wire, null, mergerB.Port("in"));

        // Assert
        Assert.Equal(3, directions.Count);
        Assert.All(directions, d => Assert.Equal((PortDirection.Output, PortDirection.Input), d));
    }
}
