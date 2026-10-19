using System.Collections.Specialized;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Subsystem tests for the Model subsystem: the diagram root, pages and elements working together through the public API.
/// </summary>
public class ModelTests
{
    private static readonly string[] ExpectedFinalOrder = ["B", "A2"];

    private static readonly string[] OriginalOrder = ["A", "B"];

    private static readonly string[] RenamedOnce = ["A>ALPHA"];

    private static readonly string[] RenamedTwice = ["A>ALPHA", "ALPHA>A"];
    /// <summary>
    ///     Validates that pages can be added, inserted, renamed, reordered and removed, and that every step is undoable.
    /// </summary>
    [Fact]
    public void Model_Pages_AddInsertRenameMoveRemove_KeepsOrderAndUndoes()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var a = diagram.Pages.Add("A");
        var b = diagram.Pages.Add("B");

        // Act: insert between, rename, reorder and remove
        var c = diagram.Pages.Insert(1, "C");
        a.Name = "A2";
        diagram.Pages.Move(b, 0);
        diagram.Pages.Remove(c);
        var finalOrder = diagram.Pages.Select(p => p.Name).ToArray();

        // Assert: the final order, then a full undo and redo of the four steps
        Assert.Equal(ExpectedFinalOrder, finalOrder);
        for (var i = 0; i < 4; i++)
        {
            Assert.True(diagram.History.Undo());
        }

        Assert.Equal(OriginalOrder, diagram.Pages.Select(p => p.Name));
        for (var i = 0; i < 4; i++)
        {
            Assert.True(diagram.History.Redo());
        }

        Assert.Equal(finalOrder, diagram.Pages.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a removed page keeps its content, rejects edits, and is fully restored by undo.
    /// </summary>
    [Fact]
    public void Model_Pages_RemovedPage_RejectsEditsUntilUndo()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0);

        // Act
        diagram.Pages.Remove(page);

        // Assert: detached, content not findable, edits rejected
        Assert.False(page.IsAttached);
        Assert.Null(diagram.FindElement(node.Id));
        Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Producer", 10, 10));

        // Act: undo the removal
        diagram.History.Undo();

        // Assert: same page object with its content, editable again
        Assert.True(page.IsAttached);
        Assert.Same(node, diagram.FindElement(node.Id));
        page.AddNode("acme.Producer", 10, 10);
        Assert.Equal(2, page.Nodes.Count);
    }

    /// <summary>
    ///     Validates that nodes and annotations are added with generated or explicit unique names and ids and found again.
    /// </summary>
    [Fact]
    public void Model_Elements_AddNodesAndAnnotation_UseUniqueNamesAndAreFindable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var first = diagram.Pages.Add("First");
        var second = diagram.Pages.Add("Second");

        // Act
        var generated1 = first.AddNode("acme.Producer", 0, 0);
        var generated2 = second.AddNode("acme.Producer", 0, 0);
        var explicitNode = first.AddNode("acme.Consumer", 200, 0, "Custom", "custom-id");
        var note = second.AddAnnotation("Remember", new Rect(0, 100, 120, 40));

        // Assert: unique generated names across pages, explicit identity kept
        Assert.Equal("Producer1", generated1.Name);
        Assert.Equal("Producer2", generated2.Name);
        Assert.NotEqual(generated1.Id, generated2.Id);
        Assert.Same(explicitNode, diagram.FindNode("Custom"));
        Assert.Same(explicitNode, diagram.FindElement("custom-id"));
        Assert.Same(note, diagram.FindElement(note.Id));
        Assert.Equal("Remember", note.Text);

        // Assert: duplicate names and ids, and unknown type keys are rejected
        Assert.Throws<ArgumentException>(() => second.AddNode("acme.Consumer", 0, 0, "Custom"));
        Assert.Throws<ArgumentException>(() => second.AddNode("acme.Consumer", 0, 0, null, "custom-id"));
        Assert.Throws<ArgumentException>(() => first.AddNode("acme.Missing", 0, 0));
    }

    /// <summary>
    ///     Validates that the rename pipeline trims and validates, lets a host hook correct or cancel, and is undoable.
    /// </summary>
    [Fact]
    public void Model_Rename_ValidationHostHookAndUndo_AppliesCorrectionsAndRejects()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        page.AddNode("acme.Consumer", 200, 0, "B");
        var renamed = new List<string>();
        diagram.NodeRenamed += (_, e) => renamed.Add($"{e.OldName}>{e.NewName}");
        diagram.NodeRenaming += (_, e) =>
        {
            if (e.NewName == "forbidden")
            {
                e.Cancel = true;
                e.Reason = "Reserved name.";
            }
            else
            {
                e.NewName = e.NewName.ToUpperInvariant();
            }
        };

        // Act
        var corrected = diagram.TryRename(a, "  alpha ", out var correctedReason);
        var duplicate = diagram.TryRename(a, "B", out var duplicateReason);
        var cancelled = diagram.TryRename(a, "forbidden", out var cancelReason);
        var empty = diagram.TryRename(a, "   ", out var emptyReason);

        // Assert: the hook corrected the trimmed name; invalid and cancelled proposals leave it unchanged
        Assert.True(corrected);
        Assert.Null(correctedReason);
        Assert.Equal("ALPHA", a.Name);
        Assert.False(duplicate);
        Assert.NotNull(duplicateReason);
        Assert.False(cancelled);
        Assert.Equal("Reserved name.", cancelReason);
        Assert.False(empty);
        Assert.NotNull(emptyReason);
        Assert.Equal(RenamedOnce, renamed);

        // Act / Assert: undo restores the old name and reports it
        diagram.History.Undo();
        Assert.Equal("A", a.Name);
        Assert.Equal(RenamedTwice, renamed);
    }

    /// <summary>
    ///     Validates that wires are connected by port objects and by path, are routed, accept route edits and re-route.
    /// </summary>
    [Fact]
    public void Model_Wires_ConnectByObjectsAndPath_RouteAndEdit()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var gain = page.AddNode("acme.Gain", 300, 200, "G");
        var consumer = page.AddNode("acme.Consumer", 600, 0, "C");

        // Act: connect by path, and by objects with the ports reversed
        var viaPath = page.Connect("P.value", "G.in");
        var viaObjects = page.Connect(consumer.Port("in"), gain.Port("out"));

        // Assert: both wires run from the output to the input and are routed
        Assert.Same(producer.Port("value"), viaPath.Source);
        Assert.Same(gain.Port("in"), viaPath.Target);
        Assert.Same(gain.Port("out"), viaObjects.Source);
        Assert.Same(consumer.Port("in"), viaObjects.Target);
        Assert.NotEmpty(viaPath.Route);
        Assert.Same(gain.Port("out"), diagram.FindPort("G.out", PortDirection.Output));

        // Act: edit the route by hand, then re-route everything
        var manual = new[] { new Point(150, 40), new Point(150, 240) };
        viaPath.SetRoute(manual);
        Assert.Equal(manual, viaPath.Route);
        page.RerouteAll();

        // Assert: the router replaced the manual route, and undo brings it back
        Assert.NotEqual(manual, viaPath.Route);
        diagram.History.Undo();
        Assert.Equal(manual, viaPath.Route);
    }

    /// <summary>
    ///     Validates that structural and host connection rules deny with reasons before connecting and when connecting.
    /// </summary>
    [Fact]
    public void Model_ConnectionRules_StructureAndHostRule_DenyWithReasons()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var gain = page.AddNode("acme.Gain", 300, 0, "G");
        var intConsumer = page.AddNode("acme.IntConsumer", 600, 0, "I");
        diagram.ConnectionRules = ConnectionRules.SameType;

        // Act
        var outputToOutput = page.CanConnect(producer.Port("value"), gain.Port("out"));
        var sameNode = page.CanConnect(gain.Port("in"), gain.Port("out"));
        var typeMismatch = page.CanConnect(producer.Port("value"), intConsumer.Port("in"));
        var allowed = page.CanConnect(producer.Port("value"), gain.Port("in"));
        var denied = Assert.Throws<InvalidOperationException>(() => page.Connect(producer.Port("value"), intConsumer.Port("in")));
        page.Connect(producer.Port("value"), gain.Port("in"));
        var duplicate = page.CanConnect(producer.Port("value"), gain.Port("in"));

        // Assert: every denial carries a reason, querying changed nothing, and an allowed pair connects
        Assert.False(outputToOutput.IsAllowed);
        Assert.False(sameNode.IsAllowed);
        Assert.False(typeMismatch.IsAllowed);
        Assert.All(new[] { outputToOutput, sameNode, typeMismatch }, check => Assert.False(string.IsNullOrWhiteSpace(check.Reason)));
        Assert.Equal(typeMismatch.Reason, denied.Message);
        Assert.True(allowed.IsAllowed);
        Assert.False(duplicate.IsAllowed);
        Assert.Single(page.Wires);
    }

    /// <summary>
    ///     Validates that nested groups move their members together, collapse and expand, and can be dissolved, all undoably.
    /// </summary>
    [Fact]
    public void Model_Groups_NestCollapseMoveAndUngroup_MembersFollow()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 200, 0, "B");
        var c = page.AddNode("acme.Consumer", 400, 0, "C");
        var inner = page.CreateGroup([a, b], "Inner");
        var outer = page.CreateGroup([inner, c], "Outer");
        var origin = a.Bounds.Position;

        // Act: moving the outer group moves every nested member
        page.Move([outer], 10, 20);

        // Assert
        Assert.Same(inner, a.Parent);
        Assert.Same(outer, inner.Parent);
        Assert.Equal(new Point(origin.X + 10, origin.Y + 20), a.Bounds.Position);
        Assert.Equal(new[] { a, b, c }, outer.AllMembers().OfType<Node>().OrderBy(n => n.Name));

        // Act: collapse and expand the inner group
        inner.IsCollapsed = true;
        var hiddenWhileCollapsed = !a.IsVisible;
        inner.IsCollapsed = false;

        // Assert
        Assert.True(hiddenWhileCollapsed);
        Assert.True(a.IsVisible);

        // Act: dissolve the inner group
        page.Ungroup(inner);

        // Assert: members take the frame's place in the outer group, undo restores the nesting
        Assert.Same(outer, a.Parent);
        Assert.DoesNotContain(inner, page.Groups);
        diagram.History.Undo();
        Assert.Same(inner, a.Parent);
        Assert.Same(outer, inner.Parent);
    }

    /// <summary>
    ///     Validates that moving nodes translates wires between them, and removing a node removes its wires.
    /// </summary>
    [Fact]
    public void Model_Editing_MoveAndRemove_WiresFollowAndAreRemoved()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var gain = page.AddNode("acme.Gain", 300, 0, "G");
        var consumer = page.AddNode("acme.Consumer", 600, 0, "C");
        var first = page.Connect(producer.Port("value"), gain.Port("in"), [new Point(150, 50)]);
        var second = page.Connect(gain.Port("out"), consumer.Port("in"));

        // Act: move both ends of the first wire by the same delta
        page.Move([producer, gain], 10, 20);

        // Assert: the manual route was translated with the nodes
        Assert.Equal(new[] { new Point(160, 70) }, first.Route);

        // Act: remove the middle node
        page.Remove(gain);

        // Assert: its wires went with it, and one undo brings everything back
        Assert.Empty(page.Wires);
        diagram.History.Undo();
        Assert.Equal(new[] { first, second }, page.Wires);
        Assert.Same(gain, diagram.FindElement(gain.Id));
    }

    /// <summary>
    ///     Validates that moving elements to another page keeps wires between moved nodes and removes wires that would span pages.
    /// </summary>
    [Fact]
    public void Model_Editing_MoveToPage_CarriesInternalWiresAndDropsOutsideWires()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");
        var producer = one.AddNode("acme.Producer", 0, 0, "P");
        var gain = one.AddNode("acme.Gain", 300, 0, "G");
        var consumer = one.AddNode("acme.Consumer", 600, 0, "C");
        var inside = one.Connect(producer.Port("value"), gain.Port("in"));
        one.Connect(gain.Port("out"), consumer.Port("in"));

        // Act
        diagram.MoveToPage([producer, gain], two);

        // Assert: the nodes and their shared wire moved, the wire to the node left behind is gone
        Assert.Equal(new[] { consumer }, one.Nodes);
        Assert.Empty(one.Wires);
        Assert.Equal(new[] { producer, gain }, two.Nodes);
        Assert.Equal(new[] { inside }, two.Wires);
        Assert.Same(two, gain.Page);

        // Act / Assert: one undo restores both wires on the original page
        diagram.History.Undo();
        Assert.Equal(3, one.Nodes.Count);
        Assert.Equal(2, one.Wires.Count);
        Assert.Empty(two.Nodes);
    }

    /// <summary>
    ///     Validates that a mixed editing session can be undone step by step and redone, restoring the exact model state each time.
    /// </summary>
    [Fact]
    public void Model_UndoRedo_MixedEditingSession_RestoresEveryStateExactly()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var states = new List<string> { TestModel.Dump(diagram) };
        DiagramPage page = null!;
        Node producer = null!;
        Node consumer = null!;
        Group group = null!;
        var steps = new List<Action>
        {
            () => page = diagram.Pages.Add("Main"),
            () => producer = page.AddNode("acme.Producer", 0, 0, "P"),
            () => consumer = page.AddNode("acme.Consumer", 300, 100, "C"),
            () => page.Connect(producer.Port("value"), consumer.Port("in")),
            () => page.AddAnnotation("note", new Rect(0, 200, 100, 40)),
            () => group = page.CreateGroup([producer, consumer], "Pair"),
            () => page.Move([group], 40, 40),
            () => consumer.Name = "Sink",
            () => group.IsCollapsed = true,
            () => page.Remove(producer),
        };

        // Act: apply every step, remembering the state after each
        foreach (var step in steps)
        {
            step();
            states.Add(TestModel.Dump(diagram));
        }

        // Assert: undo walks back through the recorded states exactly
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            Assert.True(diagram.History.Undo());
            Assert.Equal(states[i], TestModel.Dump(diagram));
        }

        Assert.False(diagram.History.CanUndo);

        // Assert: redo replays the recorded states exactly
        for (var i = 1; i <= steps.Count; i++)
        {
            Assert.True(diagram.History.Redo());
            Assert.Equal(states[i], TestModel.Dump(diagram));
        }
    }

    /// <summary>
    ///     Validates that Changed reports each kind of change, including the effects of undo and redo.
    /// </summary>
    [Fact]
    public void Model_ChangeNotification_EditsUndoAndRedo_RaiseChangedForEveryChange()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var kinds = new List<(DiagramChangeKind Kind, string? Property)>();
        diagram.Changed += (_, e) => kinds.Add((e.Kind, e.PropertyName));

        // Act
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0);
        node.Name = "Renamed";
        diagram.Theme = new Scene.DiagramTheme();
        diagram.History.Undo();
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal(
            new (DiagramChangeKind, string?)[]
            {
                (DiagramChangeKind.PageAdded, null),
                (DiagramChangeKind.ElementAdded, null),
                (DiagramChangeKind.ElementChanged, "Name"),
                (DiagramChangeKind.SettingsChanged, "Theme"),
                (DiagramChangeKind.ElementChanged, "Name"),
                (DiagramChangeKind.ElementRemoved, null),
                (DiagramChangeKind.ElementAdded, null),
            },
            kinds);
    }

    /// <summary>
    ///     Validates that the observable page collection and a page's content lists report changes that keep them in sync.
    /// </summary>
    [Fact]
    public void Model_ChangeNotification_ObservableCollections_ReportAddMoveRemove()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var pageActions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)diagram.Pages).CollectionChanged += (_, e) => pageActions.Add(e.Action);
        var first = diagram.Pages.Add("First");
        diagram.Pages.Add("Second");
        var nodeActions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)first.Nodes).CollectionChanged += (_, e) => nodeActions.Add(e.Action);

        // Act
        diagram.Pages.Move(first, 1);
        var node = first.AddNode("acme.Producer", 0, 0);
        first.Remove(node);
        diagram.Pages.Remove(first);

        // Assert
        Assert.Equal(
            new[]
            {
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Move,
                NotifyCollectionChangedAction.Remove,
            },
            pageActions);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove }, nodeActions);
    }

    /// <summary>
    ///     Validates that property and data changes recompute per-node ports, keeping and dropping wires, as one undo entry each.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_PropertyAndDataChanges_RecomputePortsInOneUndoEntry()
    {
        // Arrange: wires on a bus input and on a router output
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        var router = page.AddNode("acme.Router", 0, 300, "R");
        var consumer = page.AddNode("acme.Consumer", 300, 300, "C");
        var kept = page.Connect("P.value", "Bus.in0");
        var dropped = page.Connect("R.y", "C.in");

        // Act / Assert: each change is one exact entry
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Channels"] = 6);
        TestModel.AssertRoundTrip(diagram, () => router.Data = new RouterData("x"));

        // Assert: the bus grew and kept its wire; the router lost y and its wire
        Assert.Equal(6, bus.Inputs.Count);
        Assert.Same(kept, Assert.Single(page.Wires));
        Assert.Null(dropped.Page);
        Assert.Same(producer, kept.Source.Node);
        Assert.Equal(["x"], router.Outputs.Select(p => p.Name));
        Assert.Empty(consumer.Wires);
    }

    /// <summary>
    ///     Validates that after a port is retyped only the wires the connection rules now refuse are removed.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_RetypedPort_RemovesOnlyRefusedWires()
    {
        // Arrange: matching types required; an f32 wire on the bus input and one on its output
        var diagram = TestModel.CreateDynamicDiagram();
        diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "P");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        page.AddNode("acme.Consumer", 600, 0, "C");
        var refused = page.Connect("P.value", "Bus.in0");
        var allowed = page.Connect("Bus.out", "C.in");

        // Act / Assert: retyping the inputs is one exact entry
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Kind"] = "i32");

        // Assert: the input wire is refused now and removed; the unchanged output keeps its wire
        Assert.Null(refused.Page);
        Assert.Same(allowed, Assert.Single(page.Wires));
    }

    /// <summary>
    ///     Validates that a change in the number of ports fits the node's height, keeps its position and width, and re-routes its wires.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_PortCountChange_FitsHeightAndReroutes()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Producer", 0, 0, "P");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        var wire = page.Connect("P.value", "Bus.in0");
        var before = bus.Bounds;

        // Act: more ports than the node has room for, then back to the original count
        bus.Properties["Channels"] = 10;
        var grown = bus.Bounds;
        var grownRoute = wire.Route.ToArray();
        bus.Properties["Channels"] = 2;

        // Assert: the node grew to fit, keeping position and width; the wire follows the moved anchor; back to the start
        Assert.True(grown.Height > before.Height);
        Assert.Equal(before.Position, grown.Position);
        Assert.Equal(before.Width, grown.Width);
        Assert.Equal(page.ComputeRoute(wire.Source, wire.Target, wire), wire.Route);
        Assert.NotEqual(grownRoute, wire.Route);
        Assert.Equal(before, bus.Bounds);
    }

    /// <summary>
    ///     Validates that a failing port provider rolls the whole edit back and records nothing.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_ProviderFails_RollsBackAndRecordsNothing()
    {
        // Arrange: a router whose new payload would produce a duplicate output name
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Router", 0, 0, "R");
        page.AddNode("acme.Consumer", 300, 0, "C");
        page.Connect("R.x", "C.in");
        var router = diagram.FindNode("R")!;
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act
        Assert.Throws<ArgumentException>(() => router.Data = new RouterData("z,z"));

        // Assert: payload, ports and wires as before; nothing recorded
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that the node's ports change is reported once, after its removed wires, new size and routes are in place.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_Notifications_ReportPortsChangeAfterModelSettles()
    {
        // Arrange: four wired inputs
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        bus.Properties["Channels"] = 4;
        for (var i = 0; i < 4; i++)
        {
            page.AddNode("acme.Producer", 0, i * 150.0, $"P{i}");
            page.Connect($"P{i}.value", $"Bus.in{i}");
        }

        var reports = new List<string>();
        diagram.Changed += (_, e) =>
        {
            if (ReferenceEquals(e.Element, bus) && e.PropertyName == "Ports")
            {
                reports.Add(TestModel.Dump(diagram));
            }
        };

        // Act / Assert: the edit, its undo and its redo each report once, with the model already in its final state
        bus.Properties["Channels"] = 1;
        Assert.Equal(TestModel.Dump(diagram), Assert.Single(reports));
        reports.Clear();
        diagram.History.Undo();
        Assert.Equal(TestModel.Dump(diagram), Assert.Single(reports));
        reports.Clear();
        diagram.History.Redo();
        Assert.Equal(TestModel.Dump(diagram), Assert.Single(reports));
    }

    /// <summary>
    ///     Validates that a port the host still holds after the provider removed it cannot be connected.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_RemovedPortHeldByHost_CannotBeConnected()
    {
        // Arrange: the host keeps a reference to in3, then the bus drops it
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        bus.Properties["Channels"] = 4;
        var held = bus.Input("in3");
        bus.Properties["Channels"] = 2;

        // Act
        var check = page.CanConnect(producer.Output("value"), held);

        // Assert: refused with a reason, and connecting throws without changing anything
        Assert.False(check.IsAllowed);
        Assert.False(string.IsNullOrEmpty(check.Reason));
        Assert.ThrowsAny<Exception>(() => page.Connect(producer.Output("value"), held));
        Assert.Empty(page.Wires);
    }

    /// <summary>
    ///     Validates that a new node of a provider type gets the provider's ports and a height that fits them.
    /// </summary>
    [Fact]
    public void Model_DynamicPorts_AddNode_UsesProviderPortsAndFitsHeight()
    {
        // Arrange: a router type whose default payload names five outputs (more than its declared height has room for)
        var types = TestModel.CreateDynamicTypes();
        types.Add(new NodeType("acme.WideRouter")
        {
            DataType = typeof(RouterData),
            CreateData = () => new RouterData("a,b,c,d,e"),
            DefaultSize = new Size(120, 60),
            Outputs = { new PortDefinition("x", "f32") },
            PortProvider = node => new PortSet(
                [],
                ((RouterData)node.Data!).Outputs.Split(',').Select(name => new PortDefinition(name, "f32"))),
        });
        var page = new Diagram(types).Pages.Add("Main");

        // Act
        var node = page.AddNode("acme.WideRouter", 0, 0, "W");

        // Assert: the provider's outputs, the declared width and a height taller than declared that holds them
        Assert.Equal(["a", "b", "c", "d", "e"], node.Outputs.Select(p => p.Name));
        Assert.Equal(120, node.Bounds.Width);
        Assert.True(node.Bounds.Height > 60);
        Assert.All(node.Outputs, port => Assert.True(port.Anchor.Y < node.Bounds.Bottom));
    }

    /// <summary>
    ///     Validates that change handlers which throw never leave the model and its history inconsistent across edits,
    ///     undo, redo and transactions.
    /// </summary>
    [Fact]
    public void Model_HandlerFailure_EditsUndoRedoAndTransactions_KeepModelAndHistoryConsistent()
    {
        // Arrange: a node, and a switchable handler that throws on every notification
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var failing = false;
        diagram.Changed += (_, _) =>
        {
            if (failing)
            {
                throw new InvalidOperationException("handler failed");
            }
        };
        var start = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act / Assert: an edit completes and is recorded
        failing = true;
        Assert.Throws<InvalidOperationException>(() => node.Properties["Gain"] = 3.0);
        Assert.Equal(3.0, node.Properties["Gain"]);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        var edited = TestModel.Dump(diagram);

        // Act / Assert: undo and redo complete and move the entry
        Assert.Throws<InvalidOperationException>(() => diagram.History.Undo());
        Assert.Equal(start, TestModel.Dump(diagram));
        Assert.Throws<InvalidOperationException>(() => diagram.History.Redo());
        Assert.Equal(edited, TestModel.Dump(diagram));

        // Act / Assert: inside a transaction the failure rolls the whole transaction back
        Assert.Throws<InvalidOperationException>(() => diagram.History.Transact("Edit", () =>
        {
            node.Name = "Feed";
            node.MoveTo(100, 100);
        }));
        Assert.Equal(edited, TestModel.Dump(diagram));
        Assert.Equal(entries + 1, diagram.History.UndoCount);

        // Assert: with the handler quiet again, the history still undoes to the start
        failing = false;
        Assert.True(diagram.History.Undo());
        Assert.Equal(start, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Validates that the model hands the frame of a visible group containing neither end node to the router as an
    ///     obstacle for the stored route, and leaves out the frame of a group that contains an end node.
    /// </summary>
    [Fact]
    public void Model_Groups_UnrelatedGroupFrame_IsAnObstacleForStoredRoutes()
    {
        // Arrange: producer and consumer on one row, an unrelated group framing nodes above and below between them
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 800, 0, "C");
        var group = page.CreateGroup(
            [page.AddNode("acme.Gain", 350, -250, "Above"), page.AddNode("acme.Gain", 350, 250, "Below")],
            "G");
        var frame = group.Bounds.Inflate(diagram.Theme.ObstacleMargin);
        var inner = page.AddNode("acme.Consumer", 380, 0, "Inner");
        group.AddMember(inner);

        // Act: one wire to the outside consumer, one to the consumer inside the group
        var outside = page.Connect(producer.Port("value"), consumer.Port("in"));
        var into = page.Connect(producer.Port("value"), inner.Port("in"));

        // Assert: the stored route of the unrelated wire avoids the frame; the wire into the group may enter it
        Assert.False(RouteAssertions.CrossesAny([outside.Source.Anchor, .. outside.Route, outside.Target.Anchor], [frame]));
        Assert.True(RouteAssertions.IsOrthogonal(into.GetPoints()));
        Assert.True(RouteAssertions.CrossesAny(into.GetPoints(), [frame]));
    }
}
