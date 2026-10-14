using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Tests for <see cref="DiagramPage.Duplicate"/>.
/// </summary>
public class DuplicateTests
{
    /// <summary>
    ///     Validates that duplicate clones nodes and their wire at an offset as a single undoable step.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_NodesAndWire_ClonesAtOffsetAsOneUndoEntry()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 45);
        var gain = page.AddNode("acme.Gain", 240, 45);
        page.Connect(producer.Port("value"), gain.Port("in"), [new Point(100, 20)]);

        // Act / Assert: one entry, exact undo and redo
        IReadOnlyList<DiagramElement> copies = [];
        TestModel.AssertRoundTrip(diagram, () => copies = page.Duplicate([producer, gain], 60, 90));
        Assert.Equal("Duplicate", diagram.History.UndoDescription);

        // Assert: copies are new elements at the offset (checked after the round trip, which ends redone)
        Assert.Equal(3, copies.Count);
        var copiedProducer = copies.OfType<Node>().Single(n => n.Type.Key == "acme.Producer");
        Assert.Equal(new Point(90, 135), copiedProducer.Position);
        Assert.Equal([new Point(160, 110)], copies.OfType<Wire>().Single().Route);
        Assert.NotEqual(producer.Id, copiedProducer.Id);
        Assert.Equal("Producer2", copiedProducer.Name);
        Assert.Equal(6, page.Elements.Count);
    }

    /// <summary>
    ///     Validates that duplicating a group clones its members, nesting and collapsed state.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_Group_ClonesMembersAndStructure()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var page = source.Pages[0];
        var outer = page.Groups.Single(g => g.Title == "Outer");
        var nodeCount = page.Nodes.Count;

        // Act
        var copies = page.Duplicate([outer], 15, 15);

        // Assert: 2 groups + 2 nodes + 1 annotation + 1 wire were cloned
        Assert.Equal(6, copies.Count);
        Assert.Equal(nodeCount + 2, page.Nodes.Count);
        Assert.Equal(4, page.Groups.Count);
        var newInner = copies.OfType<Group>().Single(g => g.Title == "Inner");
        var newOuter = copies.OfType<Group>().Single(g => g.Title == "Outer");
        Assert.Same(newOuter, newInner.Parent);
        Assert.True(newInner.IsCollapsed);
        Assert.NotSame(outer, newOuter);
        Assert.Contains(newInner, newOuter.Members);
    }

    /// <summary>
    ///     Validates that payloads are deep-copied through the declared serializer, and shared by reference for types without one.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_Data_DeepCopiedWhenSerializableOtherwiseShared()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var sensor = page.AddNode("acme.Sensor", 0, 0);
        sensor.Data = new SensorData("s", 1);
        var custom = page.AddNode("acme.Custom", 100, 0);
        custom.Data = new CustomData("c");
        var opaque = page.AddNode("acme.Opaque", 200, 0);
        var shared = new object();
        opaque.Data = shared;

        // Act
        var copies = page.Duplicate([sensor, custom, opaque], 0, 50);

        // Assert: equal but distinct for serializable payloads, identical reference otherwise
        var sensorCopy = copies.OfType<Node>().Single(n => n.Type.Key == "acme.Sensor");
        var customCopy = copies.OfType<Node>().Single(n => n.Type.Key == "acme.Custom");
        var opaqueCopy = copies.OfType<Node>().Single(n => n.Type.Key == "acme.Opaque");
        Assert.Equal(sensor.Data, sensorCopy.Data);
        Assert.NotSame(sensor.Data, sensorCopy.Data);
        Assert.Equal(custom.Data, customCopy.Data);
        Assert.NotSame(custom.Data, customCopy.Data);
        Assert.Same(shared, opaqueCopy.Data);
    }

    /// <summary>
    ///     Validates that duplicated properties are copied and independent of the original.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_Properties_AreCopiedIndependently()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var producer = page.AddNode("acme.Producer", 0, 0);
        producer.Properties["Gain"] = 4.5;
        producer.Properties["Extra"] = "x";

        // Act
        var copy = (Node)page.Duplicate([producer], 10, 10)[0];
        copy.Properties["Gain"] = 9.0;

        // Assert
        Assert.Equal(4.5, producer.Properties["Gain"]);
        Assert.Equal("x", copy.Properties["Extra"]);
    }

    /// <summary>
    ///     Validates that an empty selection duplicates nothing and records nothing.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_EmptySelection_DoesNothing()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var undoBefore = diagram.History.UndoCount;

        // Act
        var copies = page.Duplicate([], 10, 10);

        // Assert
        Assert.Empty(copies);
        Assert.Equal(undoBefore, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates argument checking of duplicate.
    /// </summary>
    [Fact]
    public void DiagramPage_Duplicate_InvalidArguments_Throw()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("A");
        var other = diagram.Pages.Add("B");
        var node = page.AddNode("acme.Opaque", 0, 0);
        var foreign = other.AddNode("acme.Opaque", 0, 0);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.Duplicate(null!, 1, 1));
        Assert.Throws<ArgumentException>(() => page.Duplicate([foreign], 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.Duplicate([node], double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.Duplicate([node], 1, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.Duplicate([node], 2e9, 1));
    }
}
