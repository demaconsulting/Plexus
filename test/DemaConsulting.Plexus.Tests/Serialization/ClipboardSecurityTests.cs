using System.Text;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Tests that <see cref="PlexusClipboard.Paste"/> treats clipboard text as untrusted: every rejection
///     is a failed result (never an exception) and leaves the diagram and history untouched.
/// </summary>
public class ClipboardSecurityTests
{
    /// <summary>
    ///     A valid node entry used as a building block.
    /// </summary>
    private const string ValidNode = """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":90,"height":90}}""";

    /// <summary>
    ///     A valid second node with an input port.
    /// </summary>
    private const string ValidConsumer = """{"id":"n2","typeKey":"acme.Consumer","name":"C","bounds":{"x":200,"y":0,"width":90,"height":90}}""";

    /// <summary>
    ///     Wraps member text into a clipboard document.
    /// </summary>
    /// <param name="members">Members after the format marker, for example <c>"nodes":[...]</c>.</param>
    /// <returns>The document.</returns>
    private static string Document(string members) =>
        "{\"format\":\"plexus.clipboard\",\"version\":1" + (members.Length > 0 ? "," + members : string.Empty) + "}";

    /// <summary>
    ///     Builds a valid annotation entry.
    /// </summary>
    /// <param name="index">A number making the id unique.</param>
    /// <returns>The JSON text.</returns>
    private static string AnnotationJson(int index) =>
        "{\"id\":\"a" + index + "\",\"text\":\"\",\"bounds\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1}}";

    /// <summary>
    ///     Asserts that a payload is rejected without exception, without mutation and without a history entry.
    /// </summary>
    /// <param name="json">The payload.</param>
    /// <returns>The failed result.</returns>
    private static PasteResult AssertRejected(string? json)
    {
        // Arrange: a target with some content so mutation would be visible
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        page.AddNode("acme.Producer", 0, 0);
        var before = TestModel.Dump(diagram);
        var undoBefore = diagram.History.UndoCount;

        // Act
        var result = PlexusClipboard.Paste(page, json!);

        // Assert
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Empty(result.Elements);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoBefore, diagram.History.UndoCount);
        return result;
    }

    /// <summary>
    ///     Validates that text that is not Plexus clipboard data is rejected.
    /// </summary>
    /// <param name="json">The payload.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("""{"format":"something.else","version":1}""")]
    [InlineData("""{"format":"plexus.clipboard","version":2}""")]
    [InlineData("""{"format":"plexus.clipboard"}""")]
    [InlineData("""{"version":1}""")]
    public void PlexusClipboard_Paste_NotPlexusData_IsRejected(string? json)
    {
        // Act / Assert
        AssertRejected(json);
    }

    /// <summary>
    ///     Validates that text over the 8 MiB limit is rejected.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_Oversize_IsRejected()
    {
        // Arrange: valid JSON padded beyond 8 MiB by a long unknown string member
        var padding = new string('x', PlexusClipboard.MaxPayloadBytes + 1);
        var json = Document("\"pad\":\"" + padding + "\"");

        // Act
        var result = AssertRejected(json);

        // Assert
        Assert.Contains("MiB", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that multi-byte text whose character count is under the limit but whose UTF-8 size is over it is rejected.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_OversizeInBytes_IsRejected()
    {
        // Arrange: 3 bytes per character, fewer than 8 Mi characters
        var padding = new string('€', (PlexusClipboard.MaxPayloadBytes / 3) + 10);
        var json = Document("\"pad\":\"" + padding + "\"");
        Assert.True(json.Length < PlexusClipboard.MaxPayloadBytes);
        Assert.True(Encoding.UTF8.GetByteCount(json) > PlexusClipboard.MaxPayloadBytes);

        // Act / Assert
        AssertRejected(json);
    }

    /// <summary>
    ///     Validates that JSON nested deeper than 64 levels is rejected, in unknown members and in node payloads.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_TooDeep_IsRejected()
    {
        // Arrange: depth 200 in an ignored member, and in a node payload
        var deep = new string('[', 200) + new string(']', 200);
        var deepData = ValidNode[..^1] + ",\"data\":" + deep + "}";

        // Act / Assert
        AssertRejected(Document("\"unknown\":" + deep));
        AssertRejected(Document("\"nodes\":[" + deepData + "]"));
    }

    /// <summary>
    ///     Validates that more than 10,000 elements are rejected.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_TooManyElements_IsRejected()
    {
        // Arrange
        var annotations = Enumerable.Range(0, PlexusClipboard.MaxElements + 1)
            .Select(AnnotationJson);
        var json = Document("\"annotations\":[" + string.Join(',', annotations) + "]");

        // Act
        var result = AssertRejected(json);

        // Assert
        Assert.Contains("at most", result.Error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that exactly the maximum number of elements is still accepted.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_MaximumElements_IsAccepted()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var annotations = Enumerable.Range(0, PlexusClipboard.MaxElements)
            .Select(AnnotationJson);

        // Act
        var result = PlexusClipboard.Paste(page, Document("\"annotations\":[" + string.Join(',', annotations) + "]"));

        // Assert
        Assert.True(result.Success, result.Error);
        Assert.Equal(PlexusClipboard.MaxElements, page.Annotations.Count);
    }

    /// <summary>
    ///     Validates that malformed or non-finite geometry is rejected.
    /// </summary>
    /// <param name="bounds">The bounds JSON.</param>
    [Theory]
    [InlineData("""{"x":0,"y":0,"width":-1,"height":10}""")]
    [InlineData("""{"x":0,"y":0,"width":10,"height":-0.5}""")]
    [InlineData("""{"x":NaN,"y":0,"width":10,"height":10}""")]
    [InlineData("""{"x":0,"y":"1","width":10,"height":10}""")]
    [InlineData("""{"x":1e308,"y":0,"width":10,"height":10}""")]
    [InlineData("""{"x":0,"y":0,"width":1e999,"height":10}""")]
    [InlineData("""{"x":0,"y":0,"width":10}""")]
    [InlineData("null")]
    public void PlexusClipboard_Paste_BadGeometry_IsRejected(string bounds)
    {
        // Arrange
        var node = """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":""" + bounds + "}";

        // Act / Assert
        AssertRejected(Document("\"nodes\":[" + node + "]"));
    }

    /// <summary>
    ///     Validates that bad wire routes are rejected.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_BadRoute_IsRejected()
    {
        // Arrange
        var wire = """{"id":"w","sourceNodeId":"n1","sourcePort":"value","targetNodeId":"n2","targetPort":"in","route":[{"x":1e12,"y":0}]}""";

        // Act / Assert
        AssertRejected(Document("\"nodes\":[" + ValidNode + "," + ValidConsumer + "],\"wires\":[" + wire + "]"));
    }

    /// <summary>
    ///     Validates that structurally inconsistent content (references, ports, ids, groups, z-order) is rejected.
    /// </summary>
    /// <param name="members">The document members.</param>
    [Theory]
    [MemberData(nameof(InconsistentDocuments))]
    public void PlexusClipboard_Paste_InconsistentContent_IsRejected(string members)
    {
        // Act / Assert
        AssertRejected(Document(members));
    }

    /// <summary>
    ///     Gets documents whose content is inconsistent in different ways.
    /// </summary>
    public static TheoryData<string> InconsistentDocuments
    {
        get
        {
            var nodes = "\"nodes\":[" + ValidNode + "," + ValidConsumer + "]";
            const string group = """{"id":"g1","title":"G","memberIds":["n1"],"isCollapsed":false,"collapsedPosition":{"x":0,"y":0}}""";
            return
            [
                // Wire references an unknown node, an unknown port, the wrong direction, or the same node twice
                nodes + ""","wires":[{"id":"w","sourceNodeId":"zzz","sourcePort":"value","targetNodeId":"n2","targetPort":"in"}]""",
                nodes + ""","wires":[{"id":"w","sourceNodeId":"n1","sourcePort":"nope","targetNodeId":"n2","targetPort":"in"}]""",
                nodes + ""","wires":[{"id":"w","sourceNodeId":"n2","sourcePort":"in","targetNodeId":"n1","targetPort":"value"}]""",
                nodes + ""","wires":[{"id":"w","sourceNodeId":"n1","sourcePort":"value","targetNodeId":"n1","targetPort":"value"}]""",

                // Duplicate wire between the same ports; duplicate and empty ids
                nodes + ""","wires":[{"id":"w1","sourceNodeId":"n1","sourcePort":"value","targetNodeId":"n2","targetPort":"in"},{"id":"w2","sourceNodeId":"n1","sourcePort":"value","targetNodeId":"n2","targetPort":"in"}]""",
                "\"nodes\":[" + ValidNode + "," + ValidNode + "]",
                "\"nodes\":[" + """{"id":"","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":1,"height":1}}""" + "]",
                "\"nodes\":[" + """{"id":"x","typeKey":"acme.Producer","name":"  ","bounds":{"x":0,"y":0,"width":1,"height":1}}""" + "]",
                "\"nodes\":[null]",

                // Group problems: unknown member, member in two groups, cycle, wire as member
                nodes + ",\"groups\":[" + group.Replace("\"n1\"", "\"ghost\"", StringComparison.Ordinal) + "]",
                nodes + ",\"groups\":[" + group + "," + group.Replace("\"g1\"", "\"g2\"", StringComparison.Ordinal) + "]",
                nodes + ",\"groups\":[" + group.Replace("\"n1\"", "\"g2\"", StringComparison.Ordinal) + "," + group.Replace("\"g1\"", "\"g2\"", StringComparison.Ordinal).Replace("\"n1\"", "\"g1\"", StringComparison.Ordinal) + "]",
                nodes + ",\"groups\":[" + group.Replace("\"n1\"", "\"g1\"", StringComparison.Ordinal) + "]",

                // Z-order that omits, repeats or invents elements
                nodes + ",\"zOrder\":[\"n1\"]",
                nodes + ",\"zOrder\":[\"n1\",\"n1\"]",
                nodes + ",\"zOrder\":[\"n1\",\"ghost\"]",

                // Bad annotation fill, bad property value
                "\"annotations\":[" + """{"id":"a","text":"","bounds":{"x":0,"y":0,"width":1,"height":1},"fill":"red"}""" + "]",
                "\"nodes\":[" + """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":1,"height":1},"properties":{"Count":{"kind":"int","value":"many"}}}""" + "]",
                "\"nodes\":[" + """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":1,"height":1},"properties":{"Count":{"kind":"int","value":1.5}}}""" + "]",
                "\"nodes\":[" + """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":1,"height":1},"properties":{"Count":null}}""" + "]",
                "\"nodes\":[" + """{"id":"n1","typeKey":"acme.Producer","name":"P","bounds":{"x":0,"y":0,"width":1,"height":1},"properties":{"Count":{"kind":"int"}}}""" + "]",
            ];
        }
    }

    /// <summary>
    ///     Validates that a group nested deeper than the allowed depth is rejected without recursion problems.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_DeeplyNestedGroups_AreRejected()
    {
        // Arrange: a chain of 500 groups, each containing the next
        var groups = Enumerable.Range(0, 500).Select(i =>
        {
            var members = i < 499 ? "\"g" + (i + 1) + "\"" : string.Empty;
            return "{\"id\":\"g" + i + "\",\"title\":\"G\",\"memberIds\":[" + members + "],\"isCollapsed\":false,\"collapsedPosition\":{\"x\":0,\"y\":0}}";
        });

        // Act / Assert
        AssertRejected(Document("\"groups\":[" + string.Join(',', groups) + "]"));
    }

    /// <summary>
    ///     Validates that type-name members in the JSON are ignored rather than used to create objects.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_TypeNameMembers_AreIgnored()
    {
        // Arrange: polymorphism-style members at every level
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var node = ValidNode.Replace("{\"id\"", "{\"$type\":\"System.Diagnostics.Process, System\",\"id\"", StringComparison.Ordinal);
        var json = "{\"$type\":\"System.Object\",\"format\":\"plexus.clipboard\",\"version\":1,\"nodes\":[" + node + "]}";

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert: pasted as an ordinary node
        Assert.True(result.Success, result.Error);
        Assert.Equal("acme.Producer", ((Node)result.Elements[0]).Type.Key);
    }

    /// <summary>
    ///     Validates that an unknown node type is a failure with the key listed, even in otherwise hostile content.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_UnknownTypeInHostileContent_ListsMissingKeys()
    {
        // Arrange
        var node = ValidNode.Replace("acme.Producer", "evil.Type<script>", StringComparison.Ordinal);

        // Act
        var result = AssertRejected(Document("\"nodes\":[" + node + "]"));

        // Assert
        Assert.Equal(["evil.Type<script>"], result.MissingNodeTypes);
    }

    /// <summary>
    ///     Validates that a failing custom serializer or data type cannot corrupt a paste: the node is pasted without data and a warning is issued.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_HostilePayload_BecomesWarning()
    {
        // Arrange: a custom-serializer node whose payload lacks the member the serializer needs
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var node = """{"id":"c","typeKey":"acme.Custom","name":"C","bounds":{"x":0,"y":0,"width":1,"height":1},"data":{"other":1}}""";

        // Act
        var result = PlexusClipboard.Paste(page, Document("\"nodes\":[" + node + "]"));

        // Assert
        Assert.True(result.Success, result.Error);
        Assert.Null(((Node)result.Elements[0]).Data);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that a node naming no node type (null, empty or white space) makes the paste fail without changes,
    ///     instead of being treated as an unknown type or escaping as an exception.
    /// </summary>
    /// <param name="typeKey">The JSON value of the type key.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    public void PlexusClipboard_Paste_NodeWithoutTypeKey_FailsAndChangesNothing(string typeKey)
    {
        // Arrange: a valid node followed by one without a usable type key
        var node = "{\"id\":\"x\",\"typeKey\":" + typeKey + ",\"name\":\"X\",\"bounds\":{\"x\":0,\"y\":0,\"width\":90,\"height\":90}}";

        // Act
        var result = AssertRejected(Document("\"nodes\":[" + ValidNode + "," + node + "]"));

        // Assert: the message names the missing key and no type is reported as missing
        Assert.Contains("no node type key", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.MissingNodeTypes);
    }

    /// <summary>
    ///     Validates that a payload serializer throwing an exception of an unexpected type cannot break a paste: the
    ///     node is pasted without data and a warning carries the serializer's message.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_SerializerThrowsUnexpectedException_BecomesWarning()
    {
        // Arrange: a node type whose serializer fails with a host-specific exception type
        var types = SerializationFixtures.CreateTypes();
        types.Add(new NodeType("acme.Fragile") { DataSerializer = new ThrowingDataSerializer() });
        var page = new Diagram(types).Pages.Add("P");
        var node = """{"id":"f","typeKey":"acme.Fragile","name":"F","bounds":{"x":0,"y":0,"width":1,"height":1},"data":{"a":1}}""";

        // Act
        var result = PlexusClipboard.Paste(page, Document("\"nodes\":[" + node + "]"));

        // Assert
        Assert.True(result.Success, result.Error);
        Assert.Null(((Node)result.Elements[0]).Data);
        Assert.Contains("serializer exploded", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }


    /// <summary>
    ///     Validates that content valid up to the last element still fails atomically (the failure is found before any mutation).
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_LateFailure_LeavesNothingBehind()
    {
        // Arrange: many valid nodes followed by one bad wire
        var many = string.Join(',', Enumerable.Range(0, 50).Select(i =>
            "{\"id\":\"n" + i + "\",\"typeKey\":\"acme.Producer\",\"name\":\"P" + i + "\",\"bounds\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1}}"));
        var wire = """{"id":"w","sourceNodeId":"n0","sourcePort":"value","targetNodeId":"missing","targetPort":"in"}""";

        // Act / Assert
        AssertRejected(Document("\"nodes\":[" + many + "],\"wires\":[" + wire + "]"));
    }
}
