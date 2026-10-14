using System.Text;
using System.Text.Json;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Subsystem tests for Serialization: snapshots, JSON, clipboard and duplicate working together over one model.
/// </summary>
public class SerializationTests
{
    /// <summary>
    ///     Validates that a rich diagram written to JSON loads back to an identical model with a clean history.
    /// </summary>
    [Fact]
    public void Serialization_DiagramJson_RichDiagram_RoundTripsExactly()
    {
        // Arrange
        var original = SerializationFixtures.CreateRichDiagram();

        // Act
        var restored = Diagram.FromJson(original.ToJson(), SerializationFixtures.CreateTypes());

        // Assert
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(restored));
        Assert.Equal(0, restored.History.UndoCount);
        Assert.Equal(0, restored.History.RedoCount);
    }

    /// <summary>
    ///     Validates that loading a snapshot is faithful: fan-in wires into an input that does not allow it are restored, not validated away.
    /// </summary>
    [Fact]
    public void Serialization_FromJson_FanInIntoStrictInput_RestoresEveryWire()
    {
        // Arrange: a diagram saved by a host that allowed fan-in on the merger
        var original = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: true));
        var page = original.Pages.Add("Main");
        var first = page.AddNode("acme.Producer", 0, 0, "A");
        var second = page.AddNode("acme.Producer", 0, 150, "B");
        var merger = page.AddNode("acme.Merger", 300, 60, "M");
        page.Connect(first.Port("value"), merger.Port("in"));
        page.Connect(second.Port("value"), merger.Port("in"));

        // Act: load it with a registry whose merger does not allow fan-in
        var restored = Diagram.FromJson(original.ToJson(), SerializationFixtures.CreateTypesWithMerger(allowsFanIn: false));

        // Assert
        var restoredMerger = restored.Pages[0].Nodes.Single(n => n.Name == "M");
        Assert.Equal(2, restoredMerger.Port("in").Wires.Count);
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(restored));
    }

    /// <summary>
    ///     Validates that snapshot and clipboard JSON are versioned, carry their format markers and contain no type names.
    /// </summary>
    [Fact]
    public void Serialization_Json_IsVersionedAndContainsNoTypeNames()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();
        var page = diagram.Pages[0];

        // Act
        var snapshotJson = diagram.ToJson(indented: false);
        var clipboardJson = PlexusClipboard.Copy(page.Elements);

        // Assert
        using var snapshot = JsonDocument.Parse(snapshotJson);
        Assert.Equal(DiagramSnapshot.CurrentFormatVersion, snapshot.RootElement.GetProperty("formatVersion").GetInt32());
        using var clipboard = JsonDocument.Parse(clipboardJson);
        Assert.Equal(PlexusClipboard.FormatId, clipboard.RootElement.GetProperty("format").GetString());
        Assert.Equal(PlexusClipboard.FormatVersion, clipboard.RootElement.GetProperty("version").GetInt32());
        foreach (var text in new[] { snapshotJson, clipboardJson })
        {
            Assert.DoesNotContain("$type", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DemaConsulting", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that copying a page and pasting it into another diagram reproduces the content as one undoable step.
    /// </summary>
    [Fact]
    public void Serialization_CopyPaste_AcrossDiagrams_ReproducesContentAsOneUndoStep()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var clipboard = PlexusClipboard.Copy(source.Pages[0].Elements);
        var targetDiagram = SerializationFixtures.CreateDiagram();
        var target = targetDiagram.Pages.Add("Target");
        var undoBefore = targetDiagram.History.UndoCount;

        // Act
        var result = PlexusClipboard.Paste(target, clipboard, new Point(0, 0));

        // Assert: same number of nodes, wires, annotations and groups, all with new ids
        Assert.True(result.Success, result.Error);
        Assert.Equal(source.Pages[0].Nodes.Count, target.Nodes.Count);
        Assert.Equal(source.Pages[0].Wires.Count, target.Wires.Count);
        Assert.Equal(source.Pages[0].Annotations.Count, target.Annotations.Count);
        Assert.Equal(source.Pages[0].Groups.Count, target.Groups.Count);
        Assert.Empty(source.Pages[0].Elements.Select(e => e.Id).Intersect(target.Elements.Select(e => e.Id)));

        // One undo entry removes everything again
        Assert.Equal(undoBefore + 1, targetDiagram.History.UndoCount);
        targetDiagram.History.Undo();
        Assert.Empty(target.Elements);
    }

    /// <summary>
    ///     Validates that duplicating elements and copy-then-paste at the default offset create equivalent copies.
    /// </summary>
    [Fact]
    public void Serialization_Duplicate_AndCopyPaste_ShareTheCloneBehavior()
    {
        // Arrange: two identical diagrams, each with two connected nodes selected
        var (duplicatePage, duplicateSelection) = CreateConnectedPair();
        var (pastePage, pasteSelection) = CreateConnectedPair();
        var step = duplicatePage.Diagram.Theme.GridSize;

        // Act
        duplicatePage.Duplicate(duplicateSelection, step, step);
        var result = PlexusClipboard.Paste(pastePage, PlexusClipboard.Copy(pasteSelection));

        // Assert: same names, positions and routes, in the same order
        Assert.True(result.Success, result.Error);
        Assert.Equal(Describe(duplicatePage), Describe(pastePage));
    }

    /// <summary>
    ///     Validates that every kind of invalid clipboard content fails the paste without exception and leaves the diagram and its history untouched.
    /// </summary>
    [Fact]
    public void Serialization_InvalidClipboardContent_FailsWithoutChangingTheDiagram()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateRichDiagram();
        var page = diagram.Pages[0];
        var before = TestModel.Dump(diagram);
        var undoCount = diagram.History.UndoCount;
        var unknownType = """{"format":"plexus.clipboard","version":1,"nodes":[{"id":"n","typeKey":"no.Such","name":"X","bounds":{"x":0,"y":0,"width":1,"height":1}}]}""";
        var badGeometry = """{"format":"plexus.clipboard","version":1,"nodes":[{"id":"n","typeKey":"acme.Producer","name":"X","bounds":{"x":1e308,"y":0,"width":1,"height":1}}]}""";
        var danglingWire = """{"format":"plexus.clipboard","version":1,"wires":[{"id":"w","sourceNodeId":"a","sourcePort":"value","targetNodeId":"b","targetPort":"in"}]}""";

        // Act / Assert
        foreach (var json in new[] { "not json", "{}", unknownType, badGeometry, danglingWire })
        {
            var result = PlexusClipboard.Paste(page, json);
            Assert.False(result.Success, json);
            Assert.Empty(result.Elements);
        }

        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that hostile size, depth and element-count payloads are rejected as failed results, not exceptions.
    /// </summary>
    [Fact]
    public void Serialization_HostileClipboardLimits_AreRejectedWithoutExceptions()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var undoBefore = diagram.History.UndoCount;
        var oversize = new string(' ', PlexusClipboard.MaxPayloadBytes) + "{}";
        var tooDeep = "{\"format\":\"plexus.clipboard\",\"version\":1,\"x\":" + new string('[', 500) + new string(']', 500) + "}";
        var many = new StringBuilder("{\"format\":\"plexus.clipboard\",\"version\":1,\"annotations\":[");
        for (var i = 0; i <= PlexusClipboard.MaxElements; i++)
        {
            many.Append(i == 0 ? string.Empty : ",")
                .Append("{\"id\":\"a").Append(i).Append("\",\"text\":\"\",\"bounds\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1}}");
        }

        many.Append("]}");

        // Act / Assert
        foreach (var json in new[] { oversize, tooDeep, many.ToString() })
        {
            var result = PlexusClipboard.Paste(page, json);
            Assert.False(result.Success);
            Assert.False(string.IsNullOrEmpty(result.Error));
        }

        Assert.Empty(page.Elements);
        Assert.Equal(undoBefore, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that unknown node types are all reported, by both loading a snapshot and pasting.
    /// </summary>
    [Fact]
    public void Serialization_MissingNodeTypes_AreAllReportedByBothEntryPoints()
    {
        // Arrange: a document that uses two types the loading registry does not know
        var source = SerializationFixtures.CreateDiagram();
        var sourcePage = source.Pages.Add("P");
        sourcePage.AddNode("acme.Sensor", 0, 0);
        sourcePage.AddNode("acme.Custom", 200, 0);
        var snapshot = source.ToSnapshot();
        var clipboard = PlexusClipboard.Copy(sourcePage.Elements);
        var poorTypes = TestModel.CreateTypes();

        // Act
        var exception = Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(snapshot, poorTypes));
        var targetPage = new Diagram(poorTypes).Pages.Add("T");
        var paste = PlexusClipboard.Paste(targetPage, clipboard);

        // Assert
        string[] expected = ["acme.Custom", "acme.Sensor"];
        Assert.Equal(expected, exception.MissingNodeTypes.Order().ToArray());
        Assert.False(paste.Success);
        Assert.Equal(expected, paste.MissingNodeTypes.Order().ToArray());
        Assert.Empty(targetPage.Elements);
    }

    /// <summary>
    ///     Validates that members that look like type names are ignored when reading snapshot JSON, clipboard text and node payloads.
    /// </summary>
    [Fact]
    public void Serialization_TypeNameMembers_AreIgnoredWhenReading()
    {
        // Arrange: a diagram with a typed payload, whose JSON is peppered with type-name members at three levels
        var original = SerializationFixtures.CreateDiagram();
        var page = original.Pages.Add("P");
        page.AddNode("acme.Sensor", 0, 0, "S").Data = new SensorData("x", 2);
        var evil = "\"$type\":\"System.Diagnostics.Process, System\",";
        var json = original.ToJson(indented: false)
            .Replace("\"formatVersion\"", evil + "\"formatVersion\"", StringComparison.Ordinal)
            .Replace("\"Label\"", evil + "\"Label\"", StringComparison.Ordinal);
        var clipboard = PlexusClipboard.Copy(page.Elements)
            .Replace("\"format\"", evil + "\"format\"", StringComparison.Ordinal)
            .Replace("\"Label\"", evil + "\"Label\"", StringComparison.Ordinal);
        Assert.Contains("$type", json, StringComparison.Ordinal);

        // Act
        var restored = Diagram.FromJson(json, SerializationFixtures.CreateTypes());
        var target = SerializationFixtures.CreateDiagram().Pages.Add("T");
        var paste = PlexusClipboard.Paste(target, clipboard);

        // Assert: ordinary nodes with ordinary payloads, nothing else was instantiated
        Assert.Equal(new SensorData("x", 2), restored.FindNode("S")!.Data);
        Assert.True(paste.Success, paste.Error);
        Assert.Empty(paste.Warnings);
        Assert.Equal(new SensorData("x", 2), Assert.IsType<Node>(Assert.Single(paste.Elements)).Data);
    }

    /// <summary>
    ///     Validates that invalid snapshot text or content is reported with the documented exception types and never produces a diagram.
    /// </summary>
    [Fact]
    public void Serialization_InvalidSnapshots_ThrowTheDocumentedExceptions()
    {
        // Arrange: a valid one-node snapshot, then a wire that refers to a node that does not exist
        var types = SerializationFixtures.CreateTypes();
        var source = SerializationFixtures.CreateDiagram();
        source.Pages.Add("P").AddNode("acme.Producer", 0, 0, "A");
        var valid = source.ToSnapshot();
        var dangling = new WireSnapshot { Id = "w", SourceNodeId = "missing", SourcePort = "value", TargetNodeId = "gone", TargetPort = "in" };
        var broken = new DiagramSnapshot { Pages = [valid.Pages[0] with { Wires = [dangling] }] };

        // Act / Assert
        Assert.ThrowsAny<JsonException>(() => Diagram.FromJson("{", types));
        Assert.Throws<SnapshotException>(() => Diagram.FromJson("""{"formatVersion":99,"pages":[]}""", types));
        Assert.Throws<SnapshotException>(() => Diagram.FromSnapshot(broken, types));
        Assert.NotNull(Diagram.FromSnapshot(valid, types));
    }

    /// <summary>
    ///     Validates that a payload whose node type declares no way to serialize it is left out of snapshots and clipboard text.
    /// </summary>
    [Fact]
    public void Serialization_OpaquePayload_IsOmittedFromSnapshotAndClipboard()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Opaque", 0, 0, "O");
        node.Data = new object();

        // Act
        var snapshot = diagram.ToSnapshot();
        var clipboard = PlexusClipboard.Copy([node]);
        var paste = PlexusClipboard.Paste(page, clipboard);

        // Assert
        Assert.Null(snapshot.Pages[0].Nodes[0].Data);
        Assert.DoesNotContain("\"data\"", clipboard, StringComparison.Ordinal);
        Assert.True(paste.Success, paste.Error);
        Assert.Null(Assert.IsType<Node>(paste.Elements[0]).Data);
    }

    /// <summary>
    ///     Creates a page with two connected nodes and returns the page and the nodes as a selection.
    /// </summary>
    /// <returns>The page and its two nodes.</returns>
    private static (DiagramPage Page, DiagramElement[] Selection) CreateConnectedPair()
    {
        var page = SerializationFixtures.CreateDiagram().Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 45, "Source");
        var consumer = page.AddNode("acme.Consumer", 330, 45, "Sink");
        page.Connect(producer.Port("value"), consumer.Port("in"));
        return (page, [producer, consumer]);
    }

    /// <summary>
    ///     Describes the content of a page without ids: names, bounds and routes.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The description.</returns>
    private static string Describe(DiagramPage page)
    {
        var nodes = page.Nodes.Select(n => $"{n.Name}@{TestModel.Format(n.Bounds)}");
        var routes = page.Wires.Select(w => string.Join(';', w.Route.Select(p => TestModel.Format(p.X) + "," + TestModel.Format(p.Y))));
        return string.Join('|', nodes) + "#" + string.Join('|', routes);
    }
}
