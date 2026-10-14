using System.Collections;
using System.Text.Json;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Unit tests for the format-neutral tree: <see cref="Diagram.ToTree"/> and <see cref="Diagram.FromTree"/>.
/// </summary>
public class DiagramTreeTests
{
    /// <summary>
    ///     Walks a tree and fails when it holds anything but the allowed vocabulary.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="path">Where the value is, for the failure message.</param>
    private static void AssertPlain(object? value, string path)
    {
        switch (value)
        {
            case null or string or bool or long or double:
                break;
            case IDictionary<string, object?> map:
                Assert.IsType<Dictionary<string, object?>>(map);
                foreach (var (key, item) in map)
                {
                    AssertPlain(item, $"{path}.{key}");
                }

                break;
            case IList<object?> list:
                Assert.IsType<List<object?>>(list);
                for (var i = 0; i < list.Count; i++)
                {
                    AssertPlain(list[i], $"{path}[{i}]");
                }

                break;
            default:
                Assert.Fail($"{path} holds a {value.GetType().FullName}");
                break;
        }
    }

    /// <summary>
    ///     Validates that the tree holds only plain values, including the host data and property values.
    /// </summary>
    [Fact]
    public void Diagram_ToTree_HoldsOnlyPlainValues()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();

        // Act
        var tree = diagram.ToTree();

        // Assert
        AssertPlain(tree, "$");
        Assert.Equal(1L, tree["formatVersion"]);
        var pages = Assert.IsType<List<object?>>(tree["pages"]);
        Assert.Equal(2, pages.Count);
    }

    /// <summary>
    ///     Validates the documented value forms: kinds as strings, colors as #AARRGGBB, doubles as doubles, data as nested values.
    /// </summary>
    [Fact]
    public void Diagram_ToTree_UsesPlainKindsColorsAndNestedData()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();
        var main = diagram.Pages[0];

        // Act
        var tree = diagram.ToTree();

        // Assert
        var page = (Dictionary<string, object?>)((List<object?>)tree["pages"]!)[0]!;
        var nodes = ((List<object?>)page["nodes"]!).Cast<Dictionary<string, object?>>().ToList();
        var producer = nodes.Single(n => (string?)n["typeKey"] == "acme.Producer");
        var properties = (Dictionary<string, object?>)producer["properties"]!;
        var gain = (Dictionary<string, object?>)properties["Gain"]!;
        Assert.Equal("double", gain["kind"]);
        Assert.Equal(2.5, gain["value"]);
        var tint = (Dictionary<string, object?>)properties["Tint"]!;
        Assert.Equal("color", tint["kind"]);
        Assert.Equal("#80112233", tint["value"]);
        var mode = (Dictionary<string, object?>)properties["Mode"]!;
        Assert.Equal("choice", mode["kind"]);
        Assert.Equal(1L << 40, ((Dictionary<string, object?>)properties["Big"]!)["value"]);
        var sensor = nodes.Single(n => (string?)n["typeKey"] == "acme.Sensor");
        var data = Assert.IsType<Dictionary<string, object?>>(sensor["data"]);
        Assert.Equal("front \"left\"", data["Label"]);
        Assert.Equal(3L, data["Count"]);
        Assert.Equal(main.Nodes.Count, nodes.Count);
    }

    /// <summary>
    ///     Validates that a whole-number double property keeps the double type in the tree.
    /// </summary>
    [Fact]
    public void Diagram_ToTree_WholeDoubleProperty_IsADouble()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0).Properties["Gain"] = 2.0;

        // Act
        var tree = diagram.ToTree();

        // Assert
        var page = (Dictionary<string, object?>)((List<object?>)tree["pages"]!)[0]!;
        var node = (Dictionary<string, object?>)((List<object?>)page["nodes"]!)[0]!;
        var gain = (Dictionary<string, object?>)((Dictionary<string, object?>)node["properties"]!)["Gain"]!;
        Assert.IsType<double>(gain["value"]);
        Assert.IsType<long>(((Dictionary<string, object?>)((Dictionary<string, object?>)node["properties"]!)["Count"]!)["value"]);
    }

    /// <summary>
    ///     Validates that the tree has the same schema as the JSON: the restored diagram and its JSON are identical.
    /// </summary>
    [Fact]
    public void Diagram_ToTreeFromTree_RoundTripsLikeJson()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();

        // Act
        var loaded = Diagram.FromTree(source.ToTree(), SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(TestModel.Dump(source), TestModel.Dump(loaded));
        Assert.Equal(source.ToJson(), loaded.ToJson());
        Assert.False(loaded.History.CanUndo);
    }

    /// <summary>
    ///     Validates that the tree is a copy: changing it does not change the diagram.
    /// </summary>
    [Fact]
    public void Diagram_ToTree_IsACopy()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();
        var before = diagram.ToJson();

        // Act
        var tree = diagram.ToTree();
        tree["pages"] = new List<object?>();

        // Assert
        Assert.Equal(before, diagram.ToJson());
    }

    /// <summary>
    ///     Validates that FromTree accepts other number types, dictionary flavors and list types like a foreign serializer produces.
    /// </summary>
    [Fact]
    public void Diagram_FromTree_ToleratesForeignNumbersMapsAndLists()
    {
        // Arrange: hand-built tree using int, double-for-int, a SortedList, a read-only dictionary and an array
        var tree = new Dictionary<string, object?>
        {
            ["formatVersion"] = 1,
            ["pages"] = new object?[]
            {
                new SortedList
                {
                    ["name"] = "Main",
                    ["nodes"] = new List<object?>
                    {
                        new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>
                        {
                            ["id"] = "n1",
                            ["typeKey"] = "acme.Producer",
                            ["name"] = "A",
                            ["bounds"] = new Dictionary<string, object>
                            {
                                ["x"] = 10,
                                ["y"] = 20L,
                                ["width"] = 90.0f,
                                ["height"] = 60m,
                            },
                            ["properties"] = new Dictionary<string, object?>
                            {
                                ["Count"] = new Dictionary<string, object?> { ["kind"] = "int", ["value"] = 4.0 },
                                ["Gain"] = new Dictionary<string, object?> { ["kind"] = "double", ["value"] = (ushort)3 },
                            },
                        }),
                    },
                },
            },
        };

        // Act
        var diagram = Diagram.FromTree(tree, TestModel.CreateTypes());

        // Assert
        var node = Assert.Single(diagram.Pages[0].Nodes);
        Assert.Equal(new Rect(10, 20, 90, 60), node.Bounds);
        Assert.Equal(4, node.Properties.Get<int>("Count"));
        Assert.Equal(3.0, node.Properties.Get<double>("Gain"));
    }

    /// <summary>
    ///     Validates that a plain sequence of key/value pairs (what serializer table types enumerate) and JsonElement values are accepted.
    /// </summary>
    [Fact]
    public void Diagram_FromTree_PairSequencesAndJsonElements_AreAccepted()
    {
        // Arrange: the diagram as a JsonElement tree, with the root handed over as a bare pair sequence
        var source = SerializationFixtures.CreateRichDiagram();
        using var document = JsonDocument.Parse(source.ToJson());
        var root = document.RootElement.EnumerateObject().Select(p => new KeyValuePair<string, object?>(p.Name, p.Value.Clone())).ToList();

        // Act
        var loaded = Diagram.FromTree(root, SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(TestModel.Dump(source), TestModel.Dump(loaded));
    }

    /// <summary>
    ///     Validates that unusable trees are reported as <see cref="SnapshotException"/>, never as JSON errors.
    /// </summary>
    [Fact]
    public void Diagram_FromTree_InvalidTree_ThrowsSnapshotException()
    {
        // Arrange
        var types = TestModel.CreateTypes();
        var deep = new Dictionary<string, object?>();
        var cursor = deep;
        for (var i = 0; i < 70; i++)
        {
            var next = new Dictionary<string, object?>();
            cursor["x"] = next;
            cursor = next;
        }

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Diagram.FromTree(null!, types));
        Assert.Throws<ArgumentNullException>(() => Diagram.FromTree(new Dictionary<string, object?>(), null!));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(new Dictionary<string, object?> { ["pages"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, types));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(new Dictionary<string, object?> { ["pages"] = double.NaN }, types));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(new Dictionary<string, object?> { ["pages"] = new SortedList { [1] = "x" } }, types));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(new Dictionary<string, object?> { ["pages"] = "not a list" }, types));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(new Dictionary<string, object?> { ["formatVersion"] = 99L }, types));
        Assert.Throws<SnapshotException>(() => Diagram.FromTree(deep, types));
    }

    /// <summary>
    ///     Validates that the empty tree (an empty diagram) loads and that null values and unsigned numbers are written.
    /// </summary>
    [Fact]
    public void Diagram_FromTree_MinimalTrees_Load()
    {
        // Arrange / Act
        var empty = Diagram.FromTree(new Dictionary<string, object?> { ["formatVersion"] = 1UL, ["pages"] = Array.Empty<object>(), ["extra"] = null }, TestModel.CreateTypes());

        // Assert
        Assert.Empty(empty.Pages);
    }
}
