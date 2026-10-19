using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Tests for <see cref="Diagram.ToSnapshot"/>, <see cref="Diagram.FromSnapshot"/> and <see cref="PlexusJson"/>.
/// </summary>
public partial class SnapshotTests
{
    /// <summary>
    ///     Matches the type key of the producer node in saved JSON.
    /// </summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex("\"typeKey\"\\s*:\\s*\"acme.Producer\"")]
    private static partial Regex TypeKeyPattern();
    /// <summary>
    ///     Validates that a snapshot of a rich two-page diagram restores to an identical model.
    /// </summary>
    [Fact]
    public void Snapshot_RoundTrip_RichDiagram_RestoresEverythingExactly()
    {
        // Arrange: a diagram using every feature and a snapshot of it
        var original = SerializationFixtures.CreateRichDiagram();
        var snapshot = original.ToSnapshot();

        // Act: restore into a new diagram with an equivalent registry
        var restored = Diagram.FromSnapshot(snapshot, SerializationFixtures.CreateTypes());

        // Assert: ids, order, z-order, bounds, routes, groups, properties and data all match
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(restored));
    }

    /// <summary>
    ///     Validates that the full path through JSON text (indented and compact) restores an identical model.
    /// </summary>
    /// <param name="indented">Whether the JSON is indented.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Snapshot_RoundTrip_ThroughJson_RestoresEverythingExactly(bool indented)
    {
        // Arrange: serialize a rich diagram to text
        var original = SerializationFixtures.CreateRichDiagram();
        var json = PlexusJson.Serialize(original.ToSnapshot(), indented);

        // Act: parse and load it back
        var restored = Diagram.FromSnapshot(PlexusJson.Deserialize(json), SerializationFixtures.CreateTypes());

        // Assert: nothing was lost, and serializing again is byte-for-byte stable
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(restored));
        Assert.Equal(json, PlexusJson.Serialize(restored.ToSnapshot(), indented));
    }

    /// <summary>
    ///     Validates that property values keep their exact kind and value, including ad-hoc and extreme ones.
    /// </summary>
    [Fact]
    public void Snapshot_RoundTrip_PropertiesOfEveryKind_KeepKindAndValue()
    {
        // Arrange: a node with declared and ad-hoc properties of every portable kind
        var original = SerializationFixtures.CreateRichDiagram();
        var producer = original.FindNode("Producer1")!;

        // Act: restore through JSON
        var restored = Diagram.FromSnapshot(
            PlexusJson.Deserialize(PlexusJson.Serialize(original.ToSnapshot())),
            SerializationFixtures.CreateTypes());
        var copy = restored.FindNode("Producer1")!;

        // Assert: every value is equal and has the same CLR type (int stays int, long stays long)
        Assert.Equal(producer.Properties.Count, copy.Properties.Count);
        Assert.Equal(producer.Properties.Keys, copy.Properties.Keys);
        foreach (var (name, value) in producer.Properties)
        {
            Assert.Equal(value, copy.Properties[name]);
            Assert.Equal(value.GetType(), copy.Properties[name].GetType());
        }

        Assert.Equal(long.MaxValue, copy.Properties["Huge"]);
        Assert.Equal(0.1, copy.Properties["Ratio"]);
        Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), copy.Properties["Tint"]);
    }

    /// <summary>
    ///     Validates that payloads are restored through <see cref="NodeType.DataType"/> and through a custom serializer.
    /// </summary>
    [Fact]
    public void Snapshot_RoundTrip_TypedAndCustomData_AreRestored()
    {
        // Arrange: one node per payload mechanism
        var original = SerializationFixtures.CreateRichDiagram();

        // Act
        var restored = Diagram.FromSnapshot(
            PlexusJson.Deserialize(PlexusJson.Serialize(original.ToSnapshot())),
            SerializationFixtures.CreateTypes());

        // Assert: equal values, but new instances
        var sensor = restored.FindNode("Sensor1")!;
        var custom = restored.FindNode("Custom1")!;
        Assert.Equal(new SensorData("front \"left\"", 3), sensor.Data);
        Assert.Equal(new CustomData("hello ünïcode"), custom.Data);
        Assert.NotSame(original.FindNode("Sensor1")!.Data, sensor.Data);
    }

    /// <summary>
    ///     Validates that payloads of types declaring no way to serialize them are omitted from the snapshot.
    /// </summary>
    [Fact]
    public void Snapshot_ToSnapshot_OpaqueData_IsOmitted()
    {
        // Arrange: a node whose type declares neither DataType nor DataSerializer
        var diagram = SerializationFixtures.CreateDiagram();
        var node = diagram.Pages.Add("P").AddNode("acme.Opaque", 0, 0);
        node.Data = new object();

        // Act
        var snapshot = diagram.ToSnapshot();
        var restored = Diagram.FromSnapshot(snapshot, SerializationFixtures.CreateTypes());

        // Assert: nothing is stored and nothing comes back
        Assert.Null(snapshot.Pages[0].Nodes[0].Data);
        Assert.Null(restored.FindNode(node.Name)!.Data);
    }

    /// <summary>
    ///     Validates that a loaded diagram has an empty, clean history and that loading is not undoable.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_HistoryIsEmptyAndClean()
    {
        // Arrange / Act
        var restored = Diagram.FromSnapshot(
            SerializationFixtures.CreateRichDiagram().ToSnapshot(),
            SerializationFixtures.CreateTypes());

        // Assert
        Assert.False(restored.History.CanUndo);
        Assert.False(restored.History.CanRedo);
        Assert.Equal(0, restored.History.UndoCount);
        Assert.False(restored.History.IsDirty);
    }

    /// <summary>
    ///     Validates that a restored diagram is fully functional: edits are undoable and the content is findable by id.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_RestoredDiagramIsEditable()
    {
        // Arrange
        var original = SerializationFixtures.CreateRichDiagram();
        var restored = Diagram.FromSnapshot(original.ToSnapshot(), SerializationFixtures.CreateTypes());
        var node = restored.FindNode("Amplifier")!;
        Assert.Same(node, restored.FindElement(node.Id));

        // Act / Assert: an edit records and undoes exactly
        TestModel.AssertRoundTrip(restored, () => node.Name = "Booster");
    }

    /// <summary>
    ///     Validates that stored routes are restored without consulting the router.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_RoutesAreNotRerouted()
    {
        // Arrange: a route the router would never produce
        var original = SerializationFixtures.CreateRichDiagram();
        var route = original.Pages[0].Wires[0].Route;

        // Act
        var restored = Diagram.FromSnapshot(original.ToSnapshot(), SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(route, restored.Pages[0].Wires[0].Route);
        Assert.Equal(new Point(111.5, 7), restored.Pages[0].Wires[0].Route[0]);
    }

    /// <summary>
    ///     Validates that nested groups and collapsed state are restored.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_NestedCollapsedGroups_AreRestored()
    {
        // Arrange / Act
        var original = SerializationFixtures.CreateRichDiagram();
        var restored = Diagram.FromSnapshot(original.ToSnapshot(), SerializationFixtures.CreateTypes());

        // Assert
        var inner = restored.Pages[0].Groups.Single(g => g.Title == "Inner");
        var outer = restored.Pages[0].Groups.Single(g => g.Title == "Outer");
        Assert.True(inner.IsCollapsed);
        Assert.Same(outer, inner.Parent);
        Assert.Equal(original.Pages[0].Groups.Single(g => g.Title == "Inner").CollapsedPosition, inner.CollapsedPosition);
        Assert.Equal(2, inner.Members.Count);
        Assert.Equal(original.Pages[0].Groups.Single(g => g.Title == "Outer").Bounds, outer.Bounds);
    }

    /// <summary>
    ///     Validates that every missing node type across all pages is listed, not just the first.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_MissingNodeTypes_ListsAllOfThem()
    {
        // Arrange: a registry that lacks two of the types in use
        var snapshot = SerializationFixtures.CreateRichDiagram().ToSnapshot();
        var types = TestModel.CreateTypes();

        // Act
        var exception = Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(snapshot, types));

        // Assert
        Assert.Equal(["acme.Sensor", "acme.Custom", "acme.Opaque"], exception.MissingNodeTypes);
        Assert.Contains("acme.Sensor", exception.Message, StringComparison.Ordinal);
        Assert.Contains("acme.Custom", exception.Message, StringComparison.Ordinal);
        Assert.Contains("acme.Opaque", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unsupported format version is rejected by both the reader and the loader.
    /// </summary>
    [Fact]
    public void Snapshot_UnsupportedVersion_IsRejected()
    {
        // Arrange
        var snapshot = new DiagramSnapshot { FormatVersion = 2 };

        // Act / Assert
        Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(snapshot, SerializationFixtures.CreateTypes()));
        Assert.Throws<SnapshotException>(() => PlexusJson.Deserialize("""{"formatVersion":2,"pages":[]}"""));
    }

    /// <summary>
    ///     Validates that structurally invalid snapshots fail with a clear <see cref="SnapshotException"/>.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_InvalidContent_ThrowsSnapshotException()
    {
        // Arrange: a wire that refers to a node that does not exist
        var snapshot = new DiagramSnapshot
        {
            Pages =
            [
                new PageSnapshot
                {
                    Name = "P",
                    Wires = [new WireSnapshot { Id = "w", SourceNodeId = "x", SourcePort = "value", TargetNodeId = "y", TargetPort = "in" }],
                },
            ],
        };

        // Act
        var exception = Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(snapshot, SerializationFixtures.CreateTypes()));

        // Assert
        Assert.Contains("unknown node", exception.Message, StringComparison.Ordinal);
        Assert.Empty(exception.MissingNodeTypes);
    }

    /// <summary>
    ///     Validates that duplicate element ids across pages are rejected.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_DuplicateIdAcrossPages_Throws()
    {
        // Arrange: the same annotation id on two pages
        var bounds = new RectSnapshot { X = 0, Y = 0, Width = 10, Height = 10 };
        var snapshot = new DiagramSnapshot
        {
            Pages =
            [
                new PageSnapshot { Name = "A", Annotations = [new AnnotationSnapshot { Id = "dup", Text = "a", Bounds = bounds }] },
                new PageSnapshot { Name = "B", Annotations = [new AnnotationSnapshot { Id = "dup", Text = "b", Bounds = bounds }] },
            ],
        };

        // Act / Assert
        Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(snapshot, SerializationFixtures.CreateTypes()));
    }

    /// <summary>
    ///     Validates that a snapshot of a single page matches that page's entry in the diagram snapshot.
    /// </summary>
    [Fact]
    public void Snapshot_PageToSnapshot_MatchesDiagramEntry()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();

        // Act
        var pageJson = PlexusJson.Serialize(new DiagramSnapshot { Pages = [diagram.Pages[1].ToSnapshot()] });
        var diagramJson = PlexusJson.Serialize(new DiagramSnapshot { Pages = [diagram.ToSnapshot().Pages[1]] });

        // Assert
        Assert.Equal(diagramJson, pageJson);
    }

    /// <summary>
    ///     Validates that z-order is preserved even when it interleaves element kinds.
    /// </summary>
    [Fact]
    public void Snapshot_RoundTrip_ZOrder_IsPreserved()
    {
        // Arrange: an annotation created between two nodes
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        page.AddNode("acme.Opaque", 0, 0);
        page.AddAnnotation("middle", new Rect(0, 0, 10, 10));
        page.AddNode("acme.Opaque", 50, 50);

        // Act
        var restored = Diagram.FromSnapshot(diagram.ToSnapshot(), SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(page.Elements.Select(e => e.Id), restored.Pages[0].Elements.Select(e => e.Id));
    }

    /// <summary>
    ///     Validates that the JSON shape is camelCase, free of type names, uses short kinds, #AARRGGBB colors and embedded data.
    /// </summary>
    [Fact]
    public void PlexusJson_Serialize_ProducesStableShape()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();

        // Act
        var json = PlexusJson.Serialize(diagram.ToSnapshot());
        using var document = JsonDocument.Parse(json);

        // Assert: top-level and node shape
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        var main = root.GetProperty("pages")[0];
        Assert.Equal("Main", main.GetProperty("name").GetString());
        var producer = main.GetProperty("nodes")[0];
        Assert.Equal("acme.Producer", producer.GetProperty("typeKey").GetString());
        Assert.Equal(30, producer.GetProperty("bounds").GetProperty("x").GetDouble());
        Assert.True(producer.GetProperty("bounds").TryGetProperty("width", out _));

        // Assert: property values carry short kind names and portable values
        var properties = producer.GetProperty("properties");
        Assert.Equal("double", properties.GetProperty("Gain").GetProperty("kind").GetString());
        Assert.Equal(2.5, properties.GetProperty("Gain").GetProperty("value").GetDouble());
        Assert.Equal("int", properties.GetProperty("Count").GetProperty("kind").GetString());
        Assert.Equal("long", properties.GetProperty("Big").GetProperty("kind").GetString());
        Assert.Equal("bool", properties.GetProperty("Enabled").GetProperty("kind").GetString());
        Assert.Equal("choice", properties.GetProperty("Mode").GetProperty("kind").GetString());
        Assert.Equal("string", properties.GetProperty("Format").GetProperty("kind").GetString());
        Assert.Equal("color", properties.GetProperty("Tint").GetProperty("kind").GetString());
        Assert.Equal("#80112233", properties.GetProperty("Tint").GetProperty("value").GetString());
        Assert.Equal("#FFFF0000", properties.GetProperty("Accent").GetProperty("value").GetString());

        // Assert: payloads are embedded JSON values, not escaped strings; no type names anywhere
        var sensor = main.GetProperty("nodes").EnumerateArray().First(n => n.GetProperty("typeKey").GetString() == "acme.Sensor");
        Assert.Equal(JsonValueKind.Object, sensor.GetProperty("data").ValueKind);
        Assert.Equal(3, sensor.GetProperty("data").GetProperty("Count").GetInt32());
        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
        Assert.False(producer.TryGetProperty("data", out _));

        // Assert: wires, groups and annotations
        var wire = main.GetProperty("wires")[0];
        Assert.Equal("value", wire.GetProperty("sourcePort").GetString());
        Assert.Equal("in", wire.GetProperty("targetPort").GetString());
        Assert.Equal(111.5, wire.GetProperty("route")[0].GetProperty("x").GetDouble());
        var group = main.GetProperty("groups").EnumerateArray().First(g => g.GetProperty("title").GetString() == "Inner");
        Assert.True(group.GetProperty("isCollapsed").GetBoolean());
        Assert.Equal(2, group.GetProperty("memberIds").GetArrayLength());
        Assert.Equal("#7FFFFF00", main.GetProperty("annotations")[0].GetProperty("fill").GetString());
        Assert.Equal(main.GetProperty("nodes").GetArrayLength() + 2 + 2 + 2, main.GetProperty("zOrder").GetArrayLength());
    }

    /// <summary>
    ///     Validates that indented output spans lines and compact output does not.
    /// </summary>
    [Fact]
    public void PlexusJson_Serialize_IndentedOptionControlsWhitespace()
    {
        // Arrange
        var snapshot = SerializationFixtures.CreateRichDiagram().ToSnapshot();

        // Act
        var indented = PlexusJson.Serialize(snapshot);
        var compact = PlexusJson.Serialize(snapshot, indented: false);

        // Assert
        Assert.Contains('\n', indented);
        Assert.DoesNotContain('\n', compact);
        Assert.True(compact.Length < indented.Length);
    }

    /// <summary>
    ///     Validates that malformed JSON, missing required members and absurd nesting are rejected by the reader.
    /// </summary>
    /// <param name="json">The text to parse.</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"pages":[{"nodes":[]}]}""")]
    [InlineData("""{"pages":[{"name":"P","nodes":[{"id":"n","typeKey":"t","name":"n"}]}]}""")]
    [InlineData("""{"pages":[{"name":"P","nodes":[{"id":"n","typeKey":"t","name":"n","bounds":{"x":0,"y":0,"width":1,"height":1},"properties":{"a":{"kind":"decimal","value":1}}}]}]}""")]
    public void PlexusJson_Deserialize_Malformed_ThrowsJsonException(string json)
    {
        // Act / Assert
        Assert.ThrowsAny<JsonException>(() => PlexusJson.Deserialize(json));
    }

    /// <summary>
    ///     Validates that documents nested deeper than the limit are rejected.
    /// </summary>
    [Fact]
    public void PlexusJson_Deserialize_TooDeep_ThrowsJsonException()
    {
        // Arrange: an unknown member holding 100 nested arrays
        var json = "{\"unknown\":" + new string('[', 100) + new string(']', 100) + ",\"pages\":[]}";

        // Act / Assert
        Assert.ThrowsAny<JsonException>(() => PlexusJson.Deserialize(json));
    }

    /// <summary>
    ///     Validates that property kind mismatches with the registry make loading fail.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_PropertyKindMismatch_Throws()
    {
        // Arrange: Count is an int property but the snapshot stores a string
        var diagram = SerializationFixtures.CreateDiagram();
        diagram.Pages.Add("P").AddNode("acme.Producer", 0, 0);
        var snapshot = diagram.ToSnapshot();
        var broken = snapshot.Pages[0].Nodes[0] with
        {
            Properties = new Dictionary<string, PropertySnapshot>
            {
                ["Count"] = new() { Kind = PropertyValueKind.String, Value = JsonSerializer.SerializeToElement("many") },
            },
        };
        var tampered = new DiagramSnapshot { Pages = [snapshot.Pages[0] with { Nodes = [broken] }] };

        // Act / Assert
        var exception = Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(tampered, SerializationFixtures.CreateTypes()));
        Assert.Contains("Count", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that members the reader does not know are ignored, so newer additions do not break older readers.
    /// </summary>
    [Fact]
    public void PlexusJson_Deserialize_UnknownMembers_AreIgnored()
    {
        // Arrange: unknown members at the document and page level, including one that looks like a type name
        const string json = """{"formatVersion":1,"$type":"System.Object","future":{"a":[1,2]},"pages":[{"name":"P","extra":true}]}""";

        // Act
        var snapshot = PlexusJson.Deserialize(json);

        // Assert
        Assert.Equal(DiagramSnapshot.CurrentFormatVersion, snapshot.FormatVersion);
        Assert.Equal("P", Assert.Single(snapshot.Pages).Name);
    }

    /// <summary>
    ///     Validates that wires on ports produced by a port provider survive JSON and tree round trips.
    /// </summary>
    [Fact]
    public void Snapshot_RoundTrip_WireOnDynamicPort_RestoresWireJsonAndTree()
    {
        // Arrange
        var original = SerializationFixtures.CreateDynamicPortDiagram();

        // Act: load back through JSON and through the format-neutral tree
        var fromJson = Diagram.FromJson(original.ToJson(), TestModel.CreateDynamicTypes());
        var fromTree = Diagram.FromTree(original.ToTree(), TestModel.CreateDynamicTypes());

        // Assert: ports, wires and bounds are identical (ports were recomputed, not stored)
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(fromJson));
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(fromTree));
        Assert.Equal("Bus.in2", fromJson.Pages[0].Wires[0].Target.ToString());
        Assert.DoesNotContain("\"inputs\"", original.ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that a stored wire to a port the provider no longer produces is refused like any unknown port.
    /// </summary>
    [Fact]
    public void Snapshot_Load_WireToPortProviderDoesNotProduce_ThrowsSnapshotException()
    {
        // Arrange: a registry whose bus never has more than two inputs
        var json = SerializationFixtures.CreateDynamicPortDiagram().ToJson();
        var types = SerializationFixtures.CreateDynamicTypesWithBusProvider(_ => new PortSet(
            [new PortDefinition("in0", "f32"), new PortDefinition("in1", "f32")],
            [new PortDefinition("out", "f32")]));

        // Act
        var ex = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, types));

        // Assert
        Assert.Contains("no input port named 'in2'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a provider that fails while loading makes the load fail with a snapshot exception.
    /// </summary>
    [Fact]
    public void Snapshot_Load_ProviderThrows_ThrowsSnapshotException()
    {
        // Arrange
        var json = SerializationFixtures.CreateDynamicPortDiagram().ToJson();
        var types = SerializationFixtures.CreateDynamicTypesWithBusProvider(_ => throw new InvalidOperationException("bad channels"));

        // Act
        var ex = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, types));

        // Assert
        Assert.Contains("could not be computed: bad channels", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that restoring directly from a snapshot object recomputes provider ports before resolving wires.
    /// </summary>
    [Fact]
    public void Snapshot_FromSnapshot_WireOnDynamicPort_RestoresWire()
    {
        // Arrange
        var original = SerializationFixtures.CreateDynamicPortDiagram();

        // Act
        var restored = Diagram.FromSnapshot(original.ToSnapshot(), TestModel.CreateDynamicTypes());

        // Assert: the wires on the computed input and output are restored on the recomputed ports
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(restored));
        Assert.Equal(
            ["P.value -> Bus.in2", "R.z -> C.in"],
            restored.Pages[0].Wires.Select(w => $"{w.Source} -> {w.Target}"));
    }

    /// <summary>
    ///     Validates that loading a node with a provider keeps its stored bounds instead of resizing it to its ports.
    /// </summary>
    [Fact]
    public void Snapshot_Load_DynamicPortNode_KeepsStoredBounds()
    {
        // Arrange: a bus the user made much taller than its three ports need
        var original = SerializationFixtures.CreateDynamicPortDiagram();
        var bus = original.FindNode("Bus")!;
        bus.Bounds = bus.Bounds with { Height = bus.Bounds.Height + 300 };
        var stored = bus.Bounds;

        // Act
        var loaded = Diagram.FromJson(original.ToJson(), TestModel.CreateDynamicTypes());

        // Assert
        Assert.Equal(stored, loaded.FindNode("Bus")!.Bounds);
        Assert.Equal(3, loaded.FindNode("Bus")!.Inputs.Count);
    }

    /// <summary>
    ///     Validates that a snapshot of nodes with provider ports stores no port lists.
    /// </summary>
    [Fact]
    public void Snapshot_ToJson_DynamicPortNode_StoresNoPortLists()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDynamicPortDiagram();

        // Act
        using var document = JsonDocument.Parse(diagram.ToJson());

        // Assert: no node entry has a member that lists ports
        foreach (var node in document.RootElement.GetProperty("pages")[0].GetProperty("nodes").EnumerateArray())
        {
            Assert.DoesNotContain(
                node.EnumerateObject(),
                m => m.Name.Contains("port", StringComparison.OrdinalIgnoreCase)
                    || m.Name.Equals("inputs", StringComparison.OrdinalIgnoreCase)
                    || m.Name.Equals("outputs", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Validates that a provider failing with an exception type the library does not know still makes the load fail
    ///     with a snapshot exception naming the node.
    /// </summary>
    [Fact]
    public void Snapshot_Load_ProviderThrowsUnlistedException_ThrowsSnapshotException()
    {
        // Arrange
        var json = SerializationFixtures.CreateDynamicPortDiagram().ToJson();
        var types = SerializationFixtures.CreateDynamicTypesWithBusProvider(_ => throw new HostFailureException("host bug"));

        // Act
        var ex = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, types));

        // Assert
        Assert.Contains("'Bus'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("could not be computed: host bug", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a provider returning null makes the load fail with a snapshot exception.
    /// </summary>
    [Fact]
    public void Snapshot_Load_ProviderReturnsNull_ThrowsSnapshotException()
    {
        // Arrange
        var json = SerializationFixtures.CreateDynamicPortDiagram().ToJson();
        var types = SerializationFixtures.CreateDynamicTypesWithBusProvider(_ => null!);

        // Act
        var ex = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, types));

        // Assert
        Assert.Contains("'Bus'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("could not be computed", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a node without a type key (null, empty or white space) makes the load fail with a snapshot
    ///     exception, also when unknown node types are skipped.
    /// </summary>
    /// <param name="typeKey">The JSON value of the type key.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    public void Snapshot_FromJson_NodeWithoutTypeKey_ThrowsSnapshotException(string typeKey)
    {
        // Arrange: a saved diagram whose only node lost its type key
        var diagram = TestModel.CreateDiagram();
        diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        var json = TypeKeyPattern().Replace(
            diagram.ToJson(),
            "\"typeKey\":" + typeKey);
        var skip = new DiagramOptions { UnknownNodeTypes = UnknownNodeTypeHandling.Skip };

        // Act
        var strict = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, TestModel.CreateTypes()));
        var skipping = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, TestModel.CreateTypes(), skip));

        // Assert: reported as invalid content, not as a missing type
        Assert.Contains("no node type key", strict.Message, StringComparison.Ordinal);
        Assert.Empty(strict.MissingNodeTypes);
        Assert.Contains("no node type key", skipping.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a payload serializer failing with an exception type the library does not know makes the load
    ///     fail with a snapshot exception carrying the serializer's message.
    /// </summary>
    [Fact]
    public void Snapshot_Load_SerializerThrowsUnexpectedException_ThrowsSnapshotException()
    {
        // Arrange: a saved node with a payload whose serializer cannot read it back
        var types = SerializationFixtures.CreateTypes();
        types.Add(new NodeType("acme.Fragile") { DataSerializer = new ThrowingDataSerializer() });
        var diagram = new Diagram(types);
        diagram.Pages.Add("Main").AddNode("acme.Fragile", 0, 0).Data = new object();
        var json = diagram.ToJson();

        // Act
        var ex = Assert.Throws<SnapshotException>(() => Diagram.FromJson(json, types));

        // Assert
        Assert.Contains("serializer exploded", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that colors stored as six hexadecimal digits (opaque) or eight digits (alpha first) are read.
    /// </summary>
    [Fact]
    public void Snapshot_FromJson_SixAndEightDigitColors_AreRead()
    {
        // Arrange: an opaque and a translucent color property, then the opaque one rewritten with six digits
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "Opaque").Properties["Tint"] = Color.FromArgb(0xFF, 0x44, 0x55, 0x66);
        page.AddNode("acme.Producer", 200, 0, "Translucent").Properties["Tint"] = Color.FromArgb(0x80, 0x11, 0x22, 0x33);
        var json = diagram.ToJson();
        Assert.Contains("\"#FF445566\"", json, StringComparison.Ordinal);
        Assert.Contains("\"#80112233\"", json, StringComparison.Ordinal);
        json = json.Replace("\"#FF445566\"", "\"#445566\"", StringComparison.Ordinal);

        // Act
        var loaded = Diagram.FromJson(json, TestModel.CreateTypes());

        // Assert
        Assert.Equal(Color.FromArgb(0xFF, 0x44, 0x55, 0x66), loaded.FindNode("Opaque")!.Properties["Tint"]);
        Assert.Equal(Color.FromArgb(0x80, 0x11, 0x22, 0x33), loaded.FindNode("Translucent")!.Properties["Tint"]);
    }
}
