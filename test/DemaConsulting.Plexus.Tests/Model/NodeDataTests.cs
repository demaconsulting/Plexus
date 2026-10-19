using DemaConsulting.Plexus.Tests.Serialization;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the host payload of a node: the type check of <see cref="Node.Data"/> and the default payload
///     of <see cref="NodeType.CreateData"/>.
/// </summary>
public class NodeDataTests
{
    /// <summary>
    ///     Creates a diagram whose <c>acme.Created</c> type produces a new payload for every node.
    /// </summary>
    /// <param name="counter">Receives the number of payloads the factory produced.</param>
    /// <returns>The diagram.</returns>
    private static Diagram CreateDiagramWithFactory(out Func<int> counter)
    {
        var created = 0;
        var types = SerializationFixtures.CreateTypes();
        types.Add(new NodeType("acme.Created")
        {
            DataType = typeof(SensorData),
            CreateData = () => new SensorData("fresh", ++created),
            Outputs = { new PortDefinition("value", "f32") },
        });
        counter = () => created;
        return new Diagram(types);
    }

    /// <summary>
    ///     Validates that assigning a payload of a type other than the declared data type throws at once and changes nothing.
    /// </summary>
    [Fact]
    public void Node_Data_IncompatibleType_ThrowsArgumentException()
    {
        // Arrange: the sensor type declares SensorData
        var diagram = SerializationFixtures.CreateDiagram();
        var sensor = diagram.Pages.Add("P").AddNode("acme.Sensor", 0, 0);
        var undoDepth = diagram.History.UndoCount;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => sensor.Data = new CustomData("wrong"));

        // Assert: the message names both types and nothing was recorded or stored
        Assert.Contains("SensorData", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CustomData", exception.Message, StringComparison.Ordinal);
        Assert.Null(sensor.Data);
        Assert.Equal(undoDepth, diagram.History.UndoCount);
        Assert.Throws<ArgumentException>(() => sensor.Data = "text");
    }

    /// <summary>
    ///     Validates that the declared type, a derived type and null are accepted.
    /// </summary>
    [Fact]
    public void Node_Data_DeclaredDerivedOrNull_IsAccepted()
    {
        // Arrange: a type that declares a base class as its data type
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.Base") { DataType = typeof(BaseData) },
        };
        var node = new Diagram(types).Pages.Add("P").AddNode("acme.Base", 0, 0);

        // Act
        node.Data = new BaseData { Value = 1 };
        node.Data = new DerivedData { Value = 2, Other = 3 };
        Assert.Equal(3, ((DerivedData)node.Data).Other);
        node.Data = null;

        // Assert
        Assert.Null(node.Data);
    }

    /// <summary>
    ///     Validates that a type with only a serializer accepts any payload, and that a type with neither declaration does too.
    /// </summary>
    [Fact]
    public void Node_Data_SerializerOnlyOrUndeclared_AcceptsAnyObject()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var custom = page.AddNode("acme.Custom", 0, 0);
        var opaque = page.AddNode("acme.Opaque", 100, 0);

        // Act
        custom.Data = new CustomData("c");
        opaque.Data = 42;

        // Assert
        Assert.Equal(new CustomData("c"), custom.Data);
        Assert.Equal(42, opaque.Data);
    }

    /// <summary>
    ///     Validates that every node created through the API starts with its own payload from the factory.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_AddNode_GivesEveryNodeFreshData()
    {
        // Arrange
        var diagram = CreateDiagramWithFactory(out var counter);
        var page = diagram.Pages.Add("P");

        // Act
        var first = page.AddNode("acme.Created", 0, 0);
        var second = page.AddNode("acme.Created", 100, 0);
        var plain = page.AddNode("acme.Sensor", 200, 0);

        // Assert: distinct payload objects, the factory ran once per node of its type, other types are untouched
        Assert.Equal(new SensorData("fresh", 1), first.Data);
        Assert.Equal(new SensorData("fresh", 2), second.Data);
        Assert.NotSame(first.Data, second.Data);
        Assert.Equal(2, counter());
        Assert.Null(plain.Data);
    }

    /// <summary>
    ///     Validates that undoing and redoing the creation keeps the same payload instance instead of running the factory again.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_UndoRedoAdd_KeepsThePayload()
    {
        // Arrange
        var diagram = CreateDiagramWithFactory(out var counter);
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Created", 0, 0);
        var payload = node.Data;

        // Act
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Same(payload, node.Data);
        Assert.Equal(1, counter());
    }

    /// <summary>
    ///     Validates that a factory result of the wrong type is reported when the node is added, without adding anything.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_WrongTypeFromFactory_ThrowsInvalidOperation()
    {
        // Arrange
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.Broken") { DataType = typeof(SensorData), CreateData = () => "oops" },
        };
        var page = new Diagram(types).Pages.Add("P");

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Broken", 0, 0));
        Assert.Contains("CreateData", exception.Message, StringComparison.Ordinal);
        Assert.Empty(page.Nodes);
    }

    /// <summary>
    ///     Validates that a factory returning null starts the node without data.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_ReturningNull_StartsWithoutData()
    {
        // Arrange
        var types = new NodeTypeRegistry
        {
            new NodeType("acme.Empty") { DataType = typeof(SensorData), CreateData = () => null! },
        };

        // Act
        var node = new Diagram(types).Pages.Add("P").AddNode("acme.Empty", 0, 0);

        // Assert
        Assert.Null(node.Data);
    }

    /// <summary>
    ///     Validates that pasting and duplicating a node that carried no data gives the new node fresh data, while data that exists is copied.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_PasteAndDuplicate_FillOnlyMissingData()
    {
        // Arrange: one node with data and one whose data was cleared
        var diagram = CreateDiagramWithFactory(out var counter);
        var page = diagram.Pages.Add("P");
        var withData = page.AddNode("acme.Created", 0, 0);
        var cleared = page.AddNode("acme.Created", 100, 0);
        cleared.Data = null;
        var before = counter();

        // Act
        var json = PlexusClipboard.Copy([withData, cleared]);
        var result = PlexusClipboard.Paste(page, json);
        var duplicates = page.Duplicate([withData, cleared], 0, 150).OfType<Node>().ToList();

        // Assert: a pasted or duplicated node without data got a fresh payload; the one with data got a copy of its payload
        Assert.True(result.Success);
        var pasted = page.Nodes.Where(n => n.Name.StartsWith("Created", StringComparison.Ordinal) && n != withData && n != cleared && !duplicates.Contains(n)).ToList();
        Assert.Equal(2, pasted.Count);
        Assert.All(pasted, n => Assert.NotNull(n.Data));
        Assert.All(duplicates, n => Assert.NotNull(n.Data));
        Assert.Equal(before + 2, counter());
        Assert.Contains(pasted, n => Equals(n.Data, withData.Data));
        Assert.DoesNotContain(pasted.Concat(duplicates), n => ReferenceEquals(n.Data, withData.Data));
    }

    /// <summary>
    ///     Validates that loading keeps what was stored: a node saved without data loads without data, a node saved with data loads that data.
    /// </summary>
    [Fact]
    public void NodeType_CreateData_Load_KeepsStoredDataIncludingNone()
    {
        // Arrange
        var diagram = CreateDiagramWithFactory(out var counter);
        var page = diagram.Pages.Add("P");
        var withData = page.AddNode("acme.Created", 0, 0, "A");
        var cleared = page.AddNode("acme.Created", 100, 0, "B");
        cleared.Data = null;
        var json = diagram.ToJson();
        var before = counter();

        // Act
        var loaded = Diagram.FromJson(json, diagram.Types);

        // Assert: the factory did not run, stored data is restored and none stays none
        Assert.Equal(before, counter());
        Assert.Equal(withData.Data, loaded.FindNode("A")!.Data);
        Assert.Null(loaded.FindNode("B")!.Data);
    }

    /// <summary>
    ///     A base payload type for the derived-type test.
    /// </summary>
    private class BaseData
    {
        /// <summary>
        ///     Gets or sets a value.
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    ///     A derived payload type for the derived-type test.
    /// </summary>
    private sealed class DerivedData : BaseData
    {
        /// <summary>
        ///     Gets or sets a second value.
        /// </summary>
        public int Other { get; set; }
    }
}
