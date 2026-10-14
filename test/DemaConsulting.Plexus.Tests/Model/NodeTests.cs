using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the Node unit: the element base class, nodes and their ports.
/// </summary>
public class NodeTests
{
    /// <summary>
    ///     Creates a page with a Producer, a Gain and a Consumer node.
    /// </summary>
    /// <returns>The diagram, page and the three nodes.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Gain, Node Consumer) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (
            diagram,
            page,
            page.AddNode("acme.Producer", 0, 0, "P"),
            page.AddNode("acme.Gain", 200, 0, "G"),
            page.AddNode("acme.Consumer", 400, 0, "C"));
    }

    /// <summary>
    ///     Validates that a port reports the inputs-then-outputs order and its owner, direction and row.
    /// </summary>
    [Fact]
    public void Node_Ports_InputsThenOutputs_ReportOwnerDirectionAndIndex()
    {
        // Arrange
        var (_, page, _, _, _) = Create();

        // Act
        var mixer = page.AddNode("acme.Mixer", 0, 200);

        // Assert: four inputs in declaration order followed by the output
        Assert.Equal(["a", "b", "c", "d", "out"], mixer.Ports.Select(p => p.Name));
        Assert.Equal(["a", "b", "c", "d"], mixer.Inputs.Select(p => p.Name));
        Assert.Equal(["out"], mixer.Outputs.Select(p => p.Name));
        Assert.Equal([0, 1, 2, 3], mixer.Inputs.Select(p => p.Index));
        Assert.Equal(0, mixer.Outputs[0].Index);
        Assert.All(mixer.Ports, p => Assert.Same(mixer, p.Node));
    }

    /// <summary>
    ///     Validates that a port anchor is derived from the node's current bounds.
    /// </summary>
    [Fact]
    public void Port_Anchor_NodeMoved_AnchorFollowsNode()
    {
        // Arrange
        var (_, _, _, gain, _) = Create();
        var before = gain.Port("out").Anchor;

        // Act
        gain.MoveTo(250, 80);

        // Assert
        Assert.Equal(new Point(before.X + 50, before.Y + 80), gain.Port("out").Anchor);
    }

    /// <summary>
    ///     Validates that node and port wire lists contain the attached wires in page order and that ConnectTo adds to them.
    /// </summary>
    [Fact]
    public void Port_ConnectTo_OutputToInput_WireIsListedOnPortsAndNodes()
    {
        // Arrange
        var (_, _, producer, gain, consumer) = Create();

        // Act
        var first = producer.Port("value").ConnectTo(gain.Port("in"));
        var second = gain.Port("out").ConnectTo(consumer.Port("in"));

        // Assert
        Assert.Equal(new[] { first }, producer.Port("value").Wires);
        Assert.Equal(new[] { first }, gain.Port("in").Wires);
        Assert.Equal(new[] { second }, gain.Port("out").Wires);
        Assert.Equal(new[] { first, second }, gain.Wires);
        Assert.Equal(new[] { second }, consumer.Wires);
        Assert.Same(producer.Port("value"), first.Source);
        Assert.Same(gain.Port("in"), first.Target);
    }

    /// <summary>
    ///     Validates that a removed node lists no wires and that undo restores the lists.
    /// </summary>
    [Fact]
    public void Node_Wires_RemovedNode_IsEmptyUntilUndo()
    {
        // Arrange
        var (diagram, page, producer, gain, _) = Create();
        var wire = producer.Port("value").ConnectTo(gain.Port("in"));

        // Act
        gain.Remove();

        // Assert
        Assert.Empty(gain.Wires);
        Assert.Empty(gain.Port("in").Wires);
        Assert.Empty(producer.Wires);

        // Act
        diagram.History.Undo();

        // Assert
        Assert.Equal(new[] { wire }, gain.Wires);
        Assert.Equal(new[] { wire }, producer.Port("value").Wires);
        Assert.Contains(wire, page.Wires);
    }

    /// <summary>
    ///     Validates that the bounds and data setters reject a removed node.
    /// </summary>
    [Fact]
    public void Node_BoundsAndData_Set_OnRemovedNode_ThrowsInvalidOperationException()
    {
        // Arrange
        var (_, _, producer, _, _) = Create();
        producer.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => producer.Bounds = new Rect(1, 1, 90, 60));
        Assert.Throws<InvalidOperationException>(() => producer.MoveTo(5, 5));
        Assert.Throws<InvalidOperationException>(() => producer.Data = "x");
        Assert.Null(producer.Page);
    }

    /// <summary>
    ///     Validates that an element keeps its id across removal and undo and can be found by it again.
    /// </summary>
    [Fact]
    public void DiagramElement_Id_RemoveAndUndo_StaysTheSame()
    {
        // Arrange
        var (diagram, _, producer, _, _) = Create();
        var id = producer.Id;

        // Act
        producer.Remove();
        var removed = diagram.FindElement(id);
        diagram.History.Undo();

        // Assert
        Assert.Null(removed);
        Assert.Equal(id, producer.Id);
        Assert.Same(producer, diagram.FindElement(id));
    }

    /// <summary>
    ///     Validates that a node raises PropertyChanged for name, bounds and data changes, including on undo and redo.
    /// </summary>
    [Fact]
    public void Node_PropertyChanged_NameBoundsAndData_RaisedForSetUndoAndRedo()
    {
        // Arrange
        var (diagram, _, producer, _, _) = Create();
        var raised = new List<string?>();
        producer.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        producer.Name = "Renamed";
        producer.Bounds = new Rect(10, 20, 90, 60);
        producer.Data = "payload";

        // Assert
        Assert.Contains(nameof(Node.Name), raised);
        Assert.Contains(nameof(Node.Bounds), raised);
        Assert.Contains(nameof(Node.Data), raised);

        // Act: undo and redo the data change
        raised.Clear();
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal([nameof(Node.Data), nameof(Node.Data)], raised);
    }

    /// <summary>
    ///     Validates that a node change is also reported to the diagram with the node as the element.
    /// </summary>
    [Fact]
    public void Node_Changed_BoundsSet_DiagramReportsElementChanged()
    {
        // Arrange
        var (diagram, _, producer, _, _) = Create();
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);

        // Act
        producer.MoveTo(40, 40);

        // Assert
        Assert.Contains(
            events,
            e => e.Kind == DiagramChangeKind.ElementChanged
                && ReferenceEquals(e.Element, producer)
                && e.PropertyName == nameof(Node.Bounds));
    }
}
