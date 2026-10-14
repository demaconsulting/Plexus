using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for node types whose input and output ports share a name, and for the explicit
///     <see cref="Node.Input"/> / <see cref="Node.Output"/> lookups.
/// </summary>
public class SharedPortNameTests
{
    /// <summary>
    ///     Creates a diagram whose <c>acme.Adder</c> type has <c>a</c> and <c>b</c> on both sides.
    /// </summary>
    /// <returns>The diagram and its page.</returns>
    private static (Diagram Diagram, DiagramPage Page) Create()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Adder")
        {
            Inputs = { new PortDefinition("a", "i32"), new PortDefinition("b", "i32") },
            Outputs = { new PortDefinition("a", "i32"), new PortDefinition("b", "i32"), new PortDefinition("sum", "i32") },
        });
        var diagram = new Diagram(types);
        return (diagram, diagram.Pages.Add("Main"));
    }

    /// <summary>
    ///     Validates that Input and Output pick the port of the requested direction.
    /// </summary>
    [Fact]
    public void Node_InputAndOutput_SameName_ReturnPortsOfTheRequestedDirection()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Act
        var input = adder.Input("a");
        var output = adder.Output("a");

        // Assert
        Assert.NotSame(input, output);
        Assert.Equal(PortDirection.Input, input.Direction);
        Assert.Equal(PortDirection.Output, output.Direction);
        Assert.Equal("a", input.Name);
        Assert.Equal("a", output.Name);
        Assert.Same(input, adder.Inputs[0]);
        Assert.Same(output, adder.Outputs[0]);
    }

    /// <summary>
    ///     Validates that Port resolves unique names and rejects ambiguous ones with a helpful message.
    /// </summary>
    [Fact]
    public void Node_Port_AmbiguousName_ThrowsInvalidOperationWithHint()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Act
        var ambiguous = Assert.Throws<InvalidOperationException>(() => adder.Port("a"));

        // Assert
        Assert.Contains("ambiguous", ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains("Input", ambiguous.Message, StringComparison.Ordinal);
        Assert.Contains("Output", ambiguous.Message, StringComparison.Ordinal);
        Assert.Same(adder.Outputs[2], adder.Port("sum"));
        Assert.Throws<ArgumentException>(() => adder.Port("nope"));
    }

    /// <summary>
    ///     Validates that Input and Output reject names that exist only in the other direction, and null.
    /// </summary>
    [Fact]
    public void Node_InputAndOutput_WrongDirectionOrMissing_Throw()
    {
        // Arrange
        var (_, page) = Create();
        var adder = page.AddNode("acme.Adder", 0, 0);

        // Assert
        Assert.Throws<ArgumentException>(() => adder.Input("sum"));
        Assert.Throws<ArgumentException>(() => adder.Output("missing"));
        Assert.Throws<ArgumentNullException>(() => adder.Input(null!));
        Assert.Throws<ArgumentNullException>(() => adder.Output(null!));
    }

    /// <summary>
    ///     Validates that wires on same-named ports survive a snapshot round trip with the right directions.
    /// </summary>
    [Fact]
    public void Diagram_Snapshot_SameNamedPorts_RoundTripsWiresByDirection()
    {
        // Arrange
        var (diagram, page) = Create();
        var first = page.AddNode("acme.Adder", 0, 0, "First");
        var second = page.AddNode("acme.Adder", 400, 0, "Second");
        first.Output("a").ConnectTo(second.Input("a"));
        first.Output("b").ConnectTo(second.Input("b"));

        // Act
        var json = diagram.ToJson();
        var loaded = Diagram.FromJson(json, diagram.Types);

        // Assert
        var loadedPage = loaded.Pages[0];
        var wires = loadedPage.Wires;
        Assert.Equal(2, wires.Count);
        Assert.All(wires, w => Assert.Equal(PortDirection.Output, w.Source.Direction));
        Assert.All(wires, w => Assert.Equal(PortDirection.Input, w.Target.Direction));
        Assert.Contains(wires, w => w.Source.Name == "a" && w.Target.Name == "a");
        Assert.Contains(wires, w => w.Source.Name == "b" && w.Target.Name == "b");
    }

    /// <summary>
    ///     Validates that the clipboard also keeps same-named port wires apart by direction.
    /// </summary>
    [Fact]
    public void PlexusClipboard_CopyPaste_SameNamedPorts_KeepsWires()
    {
        // Arrange
        var (_, page) = Create();
        var first = page.AddNode("acme.Adder", 0, 0, "First");
        var second = page.AddNode("acme.Adder", 400, 0, "Second");
        var wire = first.Output("a").ConnectTo(second.Input("a"));
        var text = PlexusClipboard.Copy([first, second, wire]);

        // Act
        var result = PlexusClipboard.Paste(page, text, new Point(0, 300));

        // Assert
        Assert.True(result.Success, result.Error);
        var pasted = Assert.Single(result.Elements.OfType<Wire>());
        Assert.Equal(PortDirection.Output, pasted.Source.Direction);
        Assert.Equal(PortDirection.Input, pasted.Target.Direction);
        Assert.Equal("a", pasted.Source.Name);
        Assert.Equal("a", pasted.Target.Name);
    }

    /// <summary>
    ///     Validates that a duplicate name within one direction is still rejected at registration.
    /// </summary>
    [Fact]
    public void NodeTypeRegistry_Add_DuplicateNameWithinOutputs_Throws()
    {
        // Arrange
        var registry = new NodeTypeRegistry();
        var type = new NodeType("acme.Dup") { Outputs = { new PortDefinition("x", "f32"), new PortDefinition("x", "f32") } };

        // Assert
        Assert.Throws<ArgumentException>(() => registry.Add(type));
    }
}
