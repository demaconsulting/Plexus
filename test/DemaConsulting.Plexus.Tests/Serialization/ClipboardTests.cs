using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     Tests for <see cref="PlexusClipboard"/>: copy, paste, validation, naming and undo.
/// </summary>
public class ClipboardTests
{
    /// <summary>
    ///     Creates two connected nodes (with a manual route) on a page of a fresh diagram.
    /// </summary>
    /// <returns>The diagram, page, producer, gain node and wire.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Gain, Wire Wire) CreatePair()
    {
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 45);
        var gain = page.AddNode("acme.Gain", 240, 45, "Amplifier");
        var wire = page.Connect(producer.Port("value"), gain.Port("in"), [new Point(100, 20)]);
        return (diagram, page, producer, gain, wire);
    }

    /// <summary>
    ///     Validates that copy and paste within a page creates offset copies with new ids and unique names as one undo step.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_WithinPage_CreatesOffsetCopiesWithNewIdsAndNames()
    {
        // Arrange: two connected nodes copied to the clipboard
        var (diagram, page, producer, gain, wire) = CreatePair();
        var json = PlexusClipboard.Copy([producer, gain]);
        var undoBefore = diagram.History.UndoCount;

        // Act: paste without a position
        var result = PlexusClipboard.Paste(page, json);

        // Assert: success, nodes and the wire, offset by one grid step
        Assert.True(result.Success, result.Error);
        Assert.Null(result.Error);
        Assert.Equal(3, result.Elements.Count);
        var pastedProducer = result.Elements.OfType<Node>().Single(n => n.Type.Key == "acme.Producer");
        var pastedGain = result.Elements.OfType<Node>().Single(n => n.Type.Key == "acme.Gain");
        var pastedWire = result.Elements.OfType<Wire>().Single();
        var grid = diagram.Theme.GridSize;
        Assert.Equal(producer.Bounds.Offset(grid, grid), pastedProducer.Bounds);
        Assert.Equal(gain.Bounds.Offset(grid, grid), pastedGain.Bounds);
        Assert.Equal([new Point(100 + grid, 20 + grid)], pastedWire.Route);
        Assert.Same(pastedProducer, pastedWire.Source.Node);
        Assert.Same(pastedGain, pastedWire.Target.Node);

        // Assert: fresh ids and unique names, original untouched
        Assert.All(result.Elements, e => Assert.NotEqual(e.Id, producer.Id));
        Assert.DoesNotContain(result.Elements, e => e.Id == gain.Id || e.Id == wire.Id);
        Assert.Equal("Producer2", pastedProducer.Name);
        Assert.Equal("Amplifier1", pastedGain.Name);
        Assert.Equal("Producer1", producer.Name);
        Assert.All(result.Elements, e => Assert.Same(page, e.Page));
        Assert.All(result.Elements, e => Assert.Same(e, diagram.FindElement(e.Id)));

        // Assert: exactly one undo entry
        Assert.Equal(undoBefore + 1, diagram.History.UndoCount);
        Assert.Equal("Paste", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that paste and undo/redo restore the exact model state.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_UndoAndRedo_RestoreExactState()
    {
        // Arrange
        var (diagram, page, producer, gain, _) = CreatePair();
        var json = PlexusClipboard.Copy([producer, gain]);

        // Act / Assert: one entry, undone and redone twice with identical states
        TestModel.AssertRoundTrip(diagram, () => Assert.True(PlexusClipboard.Paste(page, json).Success));
    }

    /// <summary>
    ///     Validates that the pasted content's top-left corner lands on the requested position.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_AtPosition_PlacesTopLeftOfContent()
    {
        // Arrange
        var (_, page, producer, gain, _) = CreatePair();
        var json = PlexusClipboard.Copy([producer, gain]);

        // Act
        var result = PlexusClipboard.Paste(page, json, new Point(500, 300));

        // Assert: the left-most/top-most original (30, 45) moves to (500, 300); relative layout is kept
        var pastedProducer = result.Elements.OfType<Node>().Single(n => n.Type.Key == "acme.Producer");
        var pastedGain = result.Elements.OfType<Node>().Single(n => n.Type.Key == "acme.Gain");
        Assert.Equal(new Point(500, 300), pastedProducer.Position);
        Assert.Equal(new Point(710, 300), pastedGain.Position);
        Assert.Equal([new Point(570, 275)], result.Elements.OfType<Wire>().Single().Route);
    }

    /// <summary>
    ///     Validates that content can be pasted into a different page of the same diagram.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_AcrossPages_AddsToTargetPageOnly()
    {
        // Arrange
        var (diagram, page, producer, gain, _) = CreatePair();
        var other = diagram.Pages.Add("Other");
        var json = PlexusClipboard.Copy([producer, gain]);

        // Act
        var result = PlexusClipboard.Paste(other, json, new Point(0, 0));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, other.Nodes.Count);
        Assert.Single(other.Wires);
        Assert.Equal(2, page.Nodes.Count);
        Assert.All(result.Elements, e => Assert.Same(other, e.Page));
    }

    /// <summary>
    ///     Validates that content moves between two separate diagrams with separate but equivalent registries.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_AcrossDiagrams_RestoresPropertiesDataAndGroups()
    {
        // Arrange: the rich source diagram and a second "application" with its own registry
        var source = SerializationFixtures.CreateRichDiagram();
        var sourcePage = source.Pages[0];
        var json = PlexusClipboard.Copy(sourcePage.Elements);
        var target = SerializationFixtures.CreateDiagram();
        var targetPage = target.Pages.Add("Pasted");

        // Act: paste with the content's top-left corner at (30, 20)
        var result = PlexusClipboard.Paste(targetPage, json, new Point(30, 20));

        // Assert: everything arrived
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Equal(sourcePage.Elements.Count, result.Elements.Count);
        Assert.Equal(sourcePage.Nodes.Count, targetPage.Nodes.Count);
        Assert.Equal(sourcePage.Wires.Count, targetPage.Wires.Count);
        Assert.Equal(sourcePage.Groups.Count, targetPage.Groups.Count);

        // Assert: properties keep kind and value; names unchanged because the target was empty
        var original = sourcePage.Nodes.Single(n => n.Name == "Producer1");
        var copy = targetPage.Nodes.Single(n => n.Name == "Producer1");
        foreach (var (name, value) in original.Properties)
        {
            Assert.Equal(value, copy.Properties[name]);
            Assert.Equal(value.GetType(), copy.Properties[name].GetType());
        }

        // Assert: payloads of both mechanisms arrived
        Assert.Equal(new SensorData("front \"left\"", 3), targetPage.Nodes.Single(n => n.Name == "Sensor1").Data);
        Assert.Equal(new CustomData("hello ünïcode"), targetPage.Nodes.Single(n => n.Name == "Custom1").Data);

        // Assert: nested, collapsed groups were rebuilt
        var inner = targetPage.Groups.Single(g => g.Title == "Inner");
        var outer = targetPage.Groups.Single(g => g.Title == "Outer");
        Assert.True(inner.IsCollapsed);
        Assert.Same(outer, inner.Parent);
        Assert.Equal(2, inner.Members.Count);

        // Assert: the relative z-order of the non-wire elements is kept
        Assert.Equal(
            sourcePage.Elements.Where(e => e is not Wire).Select(e => e.GetType()),
            targetPage.Elements.Where(e => e is not Wire).Select(e => e.GetType()));
    }

    /// <summary>
    ///     Validates that selecting a group copies its members, nested groups and internal wires recursively.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Copy_Group_CopiesMembersRecursively()
    {
        // Arrange
        var source = SerializationFixtures.CreateRichDiagram();
        var outer = source.Pages[0].Groups.Single(g => g.Title == "Outer");
        var inner = source.Pages[0].Groups.Single(g => g.Title == "Inner");
        var target = SerializationFixtures.CreateDiagram();
        var page = target.Pages.Add("P");

        // Act
        var json = PlexusClipboard.Copy([outer]);
        var result = PlexusClipboard.Paste(page, json);

        // Assert: two groups, two nodes, one annotation and the wire between the grouped nodes
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, page.Groups.Count);
        Assert.Equal(2, page.Nodes.Count);
        Assert.Single(page.Annotations);
        Assert.Single(page.Wires);
        var pastedInner = page.Groups.Single(g => g.Title == "Inner");
        var pastedOuter = page.Groups.Single(g => g.Title == "Outer");
        Assert.Same(pastedOuter, pastedInner.Parent);
        Assert.True(pastedInner.IsCollapsed);
        var grid = target.Theme.GridSize;
        Assert.Equal(inner.CollapsedPosition.Offset(grid, grid), pastedInner.CollapsedPosition);
        Assert.Equal(2, pastedInner.Members.Count);
        Assert.Equal(2, pastedOuter.Members.Count);
    }

    /// <summary>
    ///     Validates that a wire is only copied when both of its nodes are copied.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Copy_WireWithOneEndCopied_IsExcluded()
    {
        // Arrange
        var (_, page, producer, _, wire) = CreatePair();

        // Act: the wire is selected but only one of its nodes is
        var json = PlexusClipboard.Copy([producer, wire]);
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Elements);
        Assert.IsType<Node>(result.Elements[0]);
        Assert.Single(page.Wires);
    }

    /// <summary>
    ///     Validates that wires whose both ends are copied travel even when the wire itself is not selected.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Copy_WireBetweenCopiedNodes_IsIncludedImplicitly()
    {
        // Arrange
        var (_, page, producer, gain, _) = CreatePair();

        // Act
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([producer, gain]));

        // Assert
        Assert.Single(result.Elements.OfType<Wire>());
    }

    /// <summary>
    ///     Validates that an empty selection produces valid clipboard text whose paste does nothing.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_EmptySelection_SucceedsWithoutHistoryEntry()
    {
        // Arrange
        var (diagram, page, _, _, wire) = CreatePair();
        var undoBefore = diagram.History.UndoCount;

        // Act: a lone wire has no nodes to ride along with
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([wire]));

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Elements);
        Assert.Equal(undoBefore, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that the clipboard text carries the format id and version.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Copy_WritesFormatIdAndVersion()
    {
        // Arrange
        var (_, _, producer, _, _) = CreatePair();

        // Act
        var json = PlexusClipboard.Copy([producer]);

        // Assert
        Assert.Equal("plexus.clipboard", PlexusClipboard.FormatId);
        Assert.Contains("\"format\":\"plexus.clipboard\"", json, StringComparison.Ordinal);
        Assert.Contains("\"version\":1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that unknown node types fail the paste atomically and list every missing key.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_MissingNodeTypes_FailsAtomicallyListingAllKeys()
    {
        // Arrange: a payload using three types of which the target knows only one
        var source = SerializationFixtures.CreateRichDiagram();
        var json = PlexusClipboard.Copy(source.Pages[0].Elements);
        var target = new Diagram(TestModel.CreateTypes());
        var page = target.Pages.Add("P");
        page.AddNode("acme.Producer", 0, 0);
        target.History.MarkClean();
        var before = TestModel.Dump(target);
        var undoBefore = target.History.UndoCount;

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert: failure names all missing keys, nothing changed, no history entry
        Assert.False(result.Success);
        Assert.Equal(["acme.Sensor", "acme.Custom"], result.MissingNodeTypes);
        Assert.Contains("acme.Sensor", result.Error, StringComparison.Ordinal);
        Assert.Contains("acme.Custom", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Elements);
        Assert.Equal(before, TestModel.Dump(target));
        Assert.Equal(undoBefore, target.History.UndoCount);
        Assert.False(target.History.IsDirty);
    }

    /// <summary>
    ///     Validates the numeric-suffix naming rule: MainSensor, MainSensor1, MainSensor2.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_RepeatedPaste_AppendsIncreasingNumericSuffix()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Opaque", 0, 0, "MainSensor");
        var json = PlexusClipboard.Copy([node]);

        // Act
        var first = PlexusClipboard.Paste(page, json);
        var second = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.Equal("MainSensor1", ((Node)first.Elements[0]).Name);
        Assert.Equal("MainSensor2", ((Node)second.Elements[0]).Name);
    }

    /// <summary>
    ///     Validates that names are kept when the diagram does not require unique names.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_UniqueNamesNotRequired_KeepsNames()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        diagram.RequireUniqueNodeNames = false;
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Opaque", 0, 0, "Same");

        // Act
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([node]));

        // Assert
        Assert.Equal("Same", ((Node)result.Elements[0]).Name);
    }

    /// <summary>
    ///     Validates that names are unique against the whole diagram, including other pages.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_NameUsedOnOtherPage_IsMadeUnique()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var a = diagram.Pages.Add("A");
        var b = diagram.Pages.Add("B");
        var node = a.AddNode("acme.Opaque", 0, 0, "Shared");

        // Act
        var result = PlexusClipboard.Paste(b, PlexusClipboard.Copy([node]));

        // Assert
        Assert.Equal("Shared1", ((Node)result.Elements[0]).Name);
    }

    /// <summary>
    ///     Validates that a property of a different kind in the target definition falls back to the default with a warning.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_PropertyKindMismatch_UsesDefaultAndWarns()
    {
        // Arrange: "Level" is a string in the source and an int in the target
        var source = new Diagram(WidgetTypes("text"));
        var node = source.Pages.Add("P").AddNode("acme.Widget", 0, 0);
        node.Properties["Level"] = "hello";
        var target = new Diagram(WidgetTypes(3));
        var page = target.Pages.Add("P");

        // Act
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([node]));

        // Assert
        Assert.True(result.Success);
        var pasted = (Node)result.Elements[0];
        Assert.Equal(3, pasted.Properties["Level"]);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Level", warning, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a choice value missing from the target's options falls back to the default with a warning.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_ChoiceNotInTargetOptions_UsesDefaultAndWarns()
    {
        // Arrange
        var source = new NodeTypeRegistry();
        source.Add(new NodeType("acme.Widget") { Properties = { new PropertyDefinition("Mode", "A") { Choices = ["A", "B"] } } });
        var sourceDiagram = new Diagram(source);
        var node = sourceDiagram.Pages.Add("P").AddNode("acme.Widget", 0, 0);
        node.Properties["Mode"] = "B";
        var target = new NodeTypeRegistry();
        target.Add(new NodeType("acme.Widget") { Properties = { new PropertyDefinition("Mode", "A") { Choices = ["A", "C"] } } });
        var page = new Diagram(target).Pages.Add("P");

        // Act
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([node]));

        // Assert
        Assert.True(result.Success);
        Assert.Equal("A", ((Node)result.Elements[0]).Properties["Mode"]);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that compatible numeric kinds convert without a warning (int into a double property).
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_CompatibleNumericKind_ConvertsWithoutWarning()
    {
        // Arrange: an int in the source, a double property in the target
        var source = new Diagram(WidgetTypes(1));
        var node = source.Pages.Add("P").AddNode("acme.Widget", 0, 0);
        node.Properties["Level"] = 5;
        var page = new Diagram(WidgetTypes(1.5)).Pages.Add("P");

        // Act
        var result = PlexusClipboard.Paste(page, PlexusClipboard.Copy([node]));

        // Assert
        Assert.Empty(result.Warnings);
        Assert.Equal(5.0, ((Node)result.Elements[0]).Properties["Level"]);
    }

    /// <summary>
    ///     Validates that wires rejected by the target's connection rules are skipped with a warning.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_WireDeniedByHostRule_IsSkippedWithWarning()
    {
        // Arrange
        var (diagram, page, producer, gain, _) = CreatePair();
        var json = PlexusClipboard.Copy([producer, gain]);
        diagram.ConnectionRules = (_, _) => ConnectionCheck.Denied("not here");

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Elements.Count);
        Assert.Single(page.Wires);
        Assert.Contains("not here", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that pasting wires that would put two wires into an input that does not allow fan-in skips the second with a warning.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_FanInIntoStrictInput_SkipsSecondWireWithWarning()
    {
        // Arrange: the source host allows fan-in on the merger, the target host does not
        var source = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: true));
        var sourcePage = source.Pages.Add("Main");
        var first = sourcePage.AddNode("acme.Producer", 0, 0, "A");
        var second = sourcePage.AddNode("acme.Producer", 0, 150, "B");
        var merger = sourcePage.AddNode("acme.Merger", 300, 60, "M");
        sourcePage.Connect(first.Port("value"), merger.Port("in"));
        sourcePage.Connect(second.Port("value"), merger.Port("in"));
        var json = PlexusClipboard.Copy([first, second, merger]);
        var target = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: false));
        var targetPage = target.Pages.Add("Main");

        // Act
        var result = PlexusClipboard.Paste(targetPage, json);

        // Assert: all nodes and one wire arrive; the refused wire is reported with the reason
        Assert.True(result.Success);
        Assert.Equal(3, targetPage.Nodes.Count);
        Assert.Single(targetPage.Wires);
        Assert.Contains("does not accept more than one", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that pasting into a host whose input allows fan-in keeps every wire without warnings.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_FanInIntoOptedInInput_KeepsAllWires()
    {
        // Arrange
        var diagram = new Diagram(SerializationFixtures.CreateTypesWithMerger(allowsFanIn: true));
        var page = diagram.Pages.Add("Main");
        var first = page.AddNode("acme.Producer", 0, 0, "A");
        var second = page.AddNode("acme.Producer", 0, 150, "B");
        var merger = page.AddNode("acme.Merger", 300, 60, "M");
        page.Connect(first.Port("value"), merger.Port("in"));
        page.Connect(second.Port("value"), merger.Port("in"));
        var json = PlexusClipboard.Copy([first, second, merger]);

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
        Assert.Equal(4, page.Wires.Count);
    }

    /// <summary>
    ///     Validates that payloads of types declaring no serializer are omitted from the clipboard text.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Copy_OpaqueData_IsOmitted()
    {
        // Arrange
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var node = page.AddNode("acme.Opaque", 0, 0);
        node.Data = "secret";

        // Act
        var json = PlexusClipboard.Copy([node]);
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);
        Assert.Null(((Node)result.Elements[0]).Data);
    }

    /// <summary>
    ///     Validates that a payload that cannot be read becomes a warning and the node is pasted without data.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_UnreadablePayload_WarnsAndPastesWithoutData()
    {
        // Arrange: corrupt the typed payload in the text
        var diagram = SerializationFixtures.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var sensor = page.AddNode("acme.Sensor", 0, 0);
        sensor.Data = new SensorData("s", 3);
        var json = PlexusClipboard.Copy([sensor]).Replace("\"Count\":3", "\"Count\":\"x\"", StringComparison.Ordinal);

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.True(result.Success);
        Assert.Null(((Node)result.Elements[0]).Data);
        Assert.Contains("data", Assert.Single(result.Warnings), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that pasting the same text twice yields distinct ids each time.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_Twice_GeneratesDistinctIds()
    {
        // Arrange
        var (_, page, producer, gain, _) = CreatePair();
        var json = PlexusClipboard.Copy([producer, gain]);

        // Act
        var first = PlexusClipboard.Paste(page, json);
        var second = PlexusClipboard.Paste(page, json);

        // Assert
        var ids = first.Elements.Concat(second.Elements).Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    /// <summary>
    ///     Validates argument checking of copy and paste.
    /// </summary>
    [Fact]
    public void PlexusClipboard_InvalidArguments_Throw()
    {
        // Arrange
        var (diagram, page, producer, _, _) = CreatePair();
        var other = diagram.Pages.Add("Other");
        var foreign = other.AddNode("acme.Opaque", 0, 0);
        var removed = page.AddNode("acme.Opaque", 5, 5);
        page.Remove(removed);
        var json = PlexusClipboard.Copy([producer]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PlexusClipboard.Copy(null!));
        Assert.Throws<ArgumentException>(() => PlexusClipboard.Copy([producer, foreign]));
        Assert.Throws<ArgumentException>(() => PlexusClipboard.Copy([removed]));
        Assert.Throws<ArgumentNullException>(() => PlexusClipboard.Paste(null!, json));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlexusClipboard.Paste(page, json, new Point(double.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlexusClipboard.Paste(page, json, new Point(0, 1e300)));
        diagram.Pages.Remove(other);
        Assert.Throws<InvalidOperationException>(() => PlexusClipboard.Paste(other, json));
    }

    /// <summary>
    ///     Creates a registry with one widget type whose "Level" property has the given default.
    /// </summary>
    /// <param name="levelDefault">The default value, which determines the property kind.</param>
    /// <returns>The registry.</returns>
    private static NodeTypeRegistry WidgetTypes(object levelDefault)
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("acme.Widget") { Properties = { new PropertyDefinition("Level", levelDefault) } });
        return types;
    }

    /// <summary>
    ///     Validates that pasting nodes whose ports come from a provider recreates the wires on those ports.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_DynamicPortNodes_RecreatesWiresOnDynamicPorts()
    {
        // Arrange: copy everything from a diagram with wires on computed ports
        var source = SerializationFixtures.CreateDynamicPortDiagram();
        var json = PlexusClipboard.Copy(source.Pages[0].Elements);
        var target = TestModel.CreateDynamicDiagram();
        var page = target.Pages.Add("Target");

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert: both wires exist on the recomputed ports, with the pasted bounds kept
        Assert.True(result.Success, result.Error);
        Assert.Equal(["Bus.in2", "C.in"], page.Wires.Select(w => w.Target.ToString()));
        Assert.Equal("R.z", page.Wires[1].Source.ToString());
        Assert.Equal(source.FindNode("Bus")!.Bounds.Size, page.Nodes.Single(n => n.Name == "Bus").Bounds.Size);
    }

    /// <summary>
    ///     Validates that a port provider failing during a paste makes the paste fail with the provider's message and
    ///     leaves the target page and history unchanged.
    /// </summary>
    [Fact]
    public void PlexusClipboard_Paste_ProviderThrows_FailsWithMessageAndChangesNothing()
    {
        // Arrange: copied content with a bus, pasted where the bus provider fails
        var json = PlexusClipboard.Copy(SerializationFixtures.CreateDynamicPortDiagram().Pages[0].Elements);
        var types = SerializationFixtures.CreateDynamicTypesWithBusProvider(_ => throw new HostFailureException("bus offline"));
        var target = new Diagram(types);
        var page = target.Pages.Add("Target");
        page.AddNode("acme.Producer", 0, 0);
        var before = TestModel.Dump(target);
        var undoBefore = target.History.UndoCount;

        // Act
        var result = PlexusClipboard.Paste(page, json);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("'Bus'", result.Error, StringComparison.Ordinal);
        Assert.Contains("bus offline", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Elements);
        Assert.Equal(before, TestModel.Dump(target));
        Assert.Equal(undoBefore, target.History.UndoCount);
    }
}
