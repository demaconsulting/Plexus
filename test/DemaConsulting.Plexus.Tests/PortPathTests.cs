using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="NodeTypeRegistry.Remove"/>, the port lookup by path
///     (<see cref="Node.TryGetPort"/>, <see cref="Diagram.FindPort"/>, <see cref="DiagramPage.FindPort"/>) and <see cref="DiagramPage.Connect(string, string, IReadOnlyList{Point}?, string?)"/>.
/// </summary>
public class PortPathTests
{
    /// <summary>
    ///     Creates a diagram whose <c>acme.Adder</c> type has <c>a</c> on both sides, plus two pages.
    /// </summary>
    /// <returns>The diagram and its first page.</returns>
    private static (Diagram Diagram, DiagramPage Page) Create()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Adder")
        {
            Inputs = { new PortDefinition("a", "f32"), new PortDefinition("b", "f32") },
            Outputs = { new PortDefinition("a", "f32"), new PortDefinition("sum", "f32") },
        });
        var diagram = new Diagram(types);
        return (diagram, diagram.Pages.Add("Main"));
    }

    /// <summary>
    ///     Validates that Remove unregisters a type and reports whether it existed.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Remove_RegisteredKey_RemovesTypeAndReturnsTrue()
    {
        // Arrange
        var types = TestModel.CreateTypes();
        var count = types.Count;

        // Act
        var removed = types.Remove("acme.Gain");
        var removedAgain = types.Remove("acme.Gain");

        // Assert
        Assert.Multiple(
            () => Assert.True(removed),
            () => Assert.False(removedAgain),
            () => Assert.False(types.Contains("acme.Gain")),
            () => Assert.Equal(count - 1, types.Count),
            () => Assert.DoesNotContain(types, t => t.Key == "acme.Gain"),
            () => Assert.False(types.Remove("never.registered")));
    }

    /// <summary>
    ///     Validates that a removed type can be registered again and that a null key is rejected.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Remove_ThenAddAgain_Works()
    {
        // Arrange
        var types = TestModel.CreateTypes();
        types.Remove("acme.Consumer");

        // Act
        types.Add(new NodeType("acme.Consumer") { Inputs = { new PortDefinition("in", "f32") } });

        // Assert
        Assert.Multiple(
            () => Assert.True(types.Contains("acme.Consumer")),
            () => Assert.Throws<ArgumentNullException>(() => types.Remove(null!)));
    }

    /// <summary>
    ///     Validates that a removed type can no longer be added to a page.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Remove_RemovedTypeCannotBeAdded()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        diagram.Types.Remove("acme.Producer");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Producer", 0, 0));
    }

    /// <summary>
    ///     Validates that TryGetPort finds a unique port with or without a direction.
    /// </summary>
    [Fact]
    public void Node_TryGetPort_UniqueName_FindsPort()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Act
        var found = adder.TryGetPort("sum", null, out var sum);
        var foundInput = adder.TryGetPort("b", PortDirection.Input, out var b);

        // Assert
        Assert.Multiple(
            () => Assert.True(found),
            () => Assert.Same(adder.Output("sum"), sum),
            () => Assert.True(foundInput),
            () => Assert.Same(adder.Input("b"), b));
    }

    /// <summary>
    ///     Validates that TryGetPort is false for an ambiguous name without a direction and resolves it with one.
    /// </summary>
    [Fact]
    public void Node_TryGetPort_AmbiguousName_NeedsDirection()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Act
        var ambiguous = adder.TryGetPort("a", null, out var none);
        var asInput = adder.TryGetPort("a", PortDirection.Input, out var input);
        var asOutput = adder.TryGetPort("a", PortDirection.Output, out var output);

        // Assert
        Assert.Multiple(
            () => Assert.False(ambiguous),
            () => Assert.Null(none),
            () => Assert.True(asInput),
            () => Assert.Same(adder.Input("a"), input),
            () => Assert.True(asOutput),
            () => Assert.Same(adder.Output("a"), output));
    }

    /// <summary>
    ///     Validates that TryGetPort is false for unknown names, wrong directions, and rejects null.
    /// </summary>
    [Fact]
    public void Node_TryGetPort_MissingOrWrongDirection_ReturnsFalse()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Act
        var missing = adder.TryGetPort("nope", null, out var none);
        var wrongDirection = adder.TryGetPort("sum", PortDirection.Input, out var wrong);

        // Assert
        Assert.Multiple(
            () => Assert.False(missing),
            () => Assert.Null(none),
            () => Assert.False(wrongDirection),
            () => Assert.Null(wrong),
            () => Assert.Throws<ArgumentNullException>(() => adder.TryGetPort(null!, null, out _)));
    }

    /// <summary>
    ///     Validates that FindPort resolves node names and node ids, on the diagram and on a page.
    /// </summary>
    [Fact]
    public void Diagram_FindPort_NameOrId_ResolvesPort()
    {
        // Arrange
        var (diagram, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0, "MainAdder");

        // Act / Assert
        Assert.Multiple(
            () => Assert.Same(adder.Output("sum"), diagram.FindPort("MainAdder.sum")),
            () => Assert.Same(adder.Output("sum"), page.FindPort("MainAdder.sum")),
            () => Assert.Same(adder.Input("b"), diagram.FindPort($"{adder.Id}.b")),
            () => Assert.Same(adder.Input("a"), diagram.FindPort("MainAdder.a", PortDirection.Input)),
            () => Assert.Same(adder.Output("a"), page.FindPort("MainAdder.a", PortDirection.Output)));
    }

    /// <summary>
    ///     Validates that FindPort returns null for unknown nodes, unknown ports and malformed paths.
    /// </summary>
    [Fact]
    public void Diagram_FindPort_UnknownOrMalformed_ReturnsNull()
    {
        // Arrange
        var (diagram, page) = Create();
        page.AddNode("acme.Adder", 0, 0, "M");

        // Act / Assert
        Assert.Multiple(
            () => Assert.Null(diagram.FindPort("Nope.a")),
            () => Assert.Null(diagram.FindPort("M.nope")),
            () => Assert.Null(diagram.FindPort("M")),
            () => Assert.Null(diagram.FindPort(".a")),
            () => Assert.Null(diagram.FindPort("M.sum", PortDirection.Input)),
            () => Assert.Throws<ArgumentNullException>(() => diagram.FindPort(null!)));
    }

    /// <summary>
    ///     Validates that an ambiguous path throws with a hint, and a direction resolves it.
    /// </summary>
    [Fact]
    public void Diagram_FindPort_AmbiguousWithoutDirection_ThrowsWithHint()
    {
        // Arrange
        var (diagram, page) = Create();
        page.AddNode("acme.Adder", 0, 0, "M");

        // Act
        var error = Assert.Throws<InvalidOperationException>(() => diagram.FindPort("M.a"));

        // Assert
        Assert.Contains("PortDirection", error.Message, StringComparison.Ordinal);
        Assert.NotNull(diagram.FindPort("M.a", PortDirection.Output));
    }

    /// <summary>
    ///     Validates that node names containing dots are split at the right dot.
    /// </summary>
    [Fact]
    public void Diagram_FindPort_NodeNameWithDots_SplitsAtMatchingDot()
    {
        // Arrange
        var (diagram, page) = Create();
        var node = page.AddNode("acme.Adder", 0, 0, "Main.Adder.One");

        // Act / Assert
        Assert.Same(node.Output("sum"), diagram.FindPort("Main.Adder.One.sum"));
    }

    /// <summary>
    ///     Validates that Diagram.FindPort searches every page while DiagramPage.FindPort stays on its own page.
    /// </summary>
    [Fact]
    public void Diagram_FindPort_SearchesAllPages_PageFindPortStaysLocal()
    {
        // Arrange
        var (diagram, first) = Create();
        var second = diagram.Pages.Add("Second");
        var node = second.AddNode("acme.Adder", 0, 0, "Remote");

        // Act / Assert
        Assert.Multiple(
            () => Assert.Same(node.Output("sum"), diagram.FindPort("Remote.sum")),
            () => Assert.Null(first.FindPort("Remote.sum")),
            () => Assert.Same(node.Output("sum"), second.FindPort("Remote.sum")));
    }

    /// <summary>
    ///     Validates that the string Connect resolves shared names: the source prefers its output, the target its input.
    /// </summary>
    [Fact]
    public void DiagramPage_ConnectByPath_SharedPortName_PrefersOutputThenInput()
    {
        // Arrange: two adders, both with port a on both sides
        var (_, page) = Create();
        var a = page.AddNode("acme.Adder", 0, 0, "A");
        var b = page.AddNode("acme.Adder", 300, 0, "B");

        // Act
        var wire = page.Connect("A.a", "B.a");

        // Assert
        Assert.Multiple(
            () => Assert.Same(a.Output("a"), wire.Source),
            () => Assert.Same(b.Input("a"), wire.Target),
            () => Assert.Single(page.Wires));
    }

    /// <summary>
    ///     Validates that the string Connect is one undoable step and accepts ids and a route.
    /// </summary>
    [Fact]
    public void DiagramPage_ConnectByPath_IsOneUndoEntryAndAcceptsIdAndRoute()
    {
        // Arrange
        var (diagram, page) = Create();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () =>
        {
            var wire = page.Connect($"{producer.Id}.value", "C.in", [new Point(150, 0)], "wire1");
            Assert.Equal("wire1", wire.Id);
            Assert.Equal([new Point(150, 0)], wire.Route);
            Assert.Same(consumer.Input("in"), wire.Target);
        });
    }

    /// <summary>
    ///     Validates that the string Connect reports unknown paths with the offending argument and still validates the connection.
    /// </summary>
    [Fact]
    public void DiagramPage_ConnectByPath_UnknownPathOrDeniedConnection_Throws()
    {
        // Arrange
        var (_, page) = Create();
        page.AddNode("acme.Producer", 0, 0, "P");
        page.AddNode("acme.IntConsumer", 300, 0, "I");

        // Act
        var unknownSource = Assert.Throws<ArgumentException>(() => page.Connect("Nope.value", "I.in"));
        var unknownTarget = Assert.Throws<ArgumentException>(() => page.Connect("P.value", "Nope.in"));
        var self = Assert.Throws<InvalidOperationException>(() => page.Connect("P.value", "P.value"));

        // Assert
        Assert.Multiple(
            () => Assert.Equal("from", unknownSource.ParamName),
            () => Assert.Equal("to", unknownTarget.ParamName),
            () => Assert.NotEmpty(self.Message),
            () => Assert.Throws<ArgumentNullException>(() => page.Connect(null!, "I.in")),
            () => Assert.Throws<ArgumentNullException>(() => page.Connect("P.value", null!)));
    }
}
