using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the Node unit's per-node ports: ports computed by a <see cref="NodeType.PortProvider"/> and recomputed
///     when a node's properties or data change.
/// </summary>
public class NodePortsTests
{
    /// <summary>
    ///     Creates a page with a bus at (300, 0) and four producers feeding nothing yet.
    /// </summary>
    /// <returns>The diagram, page, bus and producers.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Bus, Node[] Producers) Create()
    {
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var producers = Enumerable.Range(0, 4).Select(i => page.AddNode("acme.Producer", 0, i * 150, $"P{i}")).ToArray();
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        return (diagram, page, bus, producers);
    }

    /// <summary>
    ///     Validates that a new node of a type with a port provider gets the ports the provider returns.
    /// </summary>
    [Fact]
    public void Node_Ports_PortProvider_NewNodeUsesProviderPorts()
    {
        // Arrange / Act
        var (_, _, bus, _) = Create();

        // Assert: the provider's definitions (with descriptions) were used, not the declared defaults
        Assert.Equal(["in0", "in1", "out"], bus.Ports.Select(p => p.Name));
        Assert.Equal("Channel 0", bus.Input("in0").Description);
        Assert.Equal([0, 1], bus.Inputs.Select(p => p.Index));
    }

    /// <summary>
    ///     Validates that a node of a type without a port provider keeps its declared ports when its properties change.
    /// </summary>
    [Fact]
    public void Node_Ports_NoPortProvider_KeepsTypePorts()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0);
        var port = producer.Output("value");
        var entries = diagram.History.UndoCount;

        // Act
        producer.Properties["Gain"] = 3.0;
        producer.Data = "payload";

        // Assert: same port object, definition from the type, one entry per change
        Assert.Same(port, Assert.Single(producer.Ports));
        Assert.Same(producer.Type.Outputs[0], port.Definition);
        Assert.Equal(entries + 2, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that adding ports keeps the existing port objects and their wires.
    /// </summary>
    [Fact]
    public void Node_Ports_CountIncreased_AddsPortsKeepingExistingInstancesAndWires()
    {
        // Arrange
        var (_, page, bus, producers) = Create();
        var in0 = bus.Input("in0");
        var in1 = bus.Input("in1");
        var w0 = page.Connect(producers[0].Output("value"), in0);
        var w1 = page.Connect(producers[1].Output("value"), in1);

        // Act
        bus.Properties["Channels"] = 4;

        // Assert: two new inputs, the old ones untouched and still wired
        Assert.Equal(["in0", "in1", "in2", "in3"], bus.Inputs.Select(p => p.Name));
        Assert.Same(in0, bus.Inputs[0]);
        Assert.Same(in1, bus.Inputs[1]);
        Assert.Equal([w0, w1], page.Wires);
        Assert.Same(in0, w0.Target);
    }

    /// <summary>
    ///     Validates that removing ports removes the wires attached to them.
    /// </summary>
    [Fact]
    public void Node_Ports_CountDecreased_RemovesPortsAndTheirWires()
    {
        // Arrange
        var (_, page, bus, producers) = Create();
        bus.Properties["Channels"] = 4;
        var wires = producers.Select((p, i) => page.Connect(p.Output("value"), bus.Input($"in{i}"))).ToArray();
        var in3 = bus.Input("in3");

        // Act
        bus.Properties["Channels"] = 2;

        // Assert: the wires of in2 and in3 are gone with their ports; in0 and in1 keep theirs
        Assert.Equal(["in0", "in1"], bus.Inputs.Select(p => p.Name));
        Assert.Equal([wires[0], wires[1]], page.Wires);
        Assert.Null(wires[3].Page);
        Assert.False(in3.IsOnNode);
    }

    /// <summary>
    ///     Validates that ports are identified by name, so a port that moves to another row keeps its object and wires.
    /// </summary>
    [Fact]
    public void Node_Ports_RenamedByName_ReattachesWiresByName()
    {
        // Arrange: a router with outputs x, y and a wire on y
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var router = page.AddNode("acme.Router", 0, 0, "R");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var y = router.Output("y");
        var wire = page.Connect(y, consumer.Input("in"));

        // Act: x disappears, y moves to the first row, z is new
        router.Data = new RouterData("y,z");

        // Assert: y kept its object, its wire and moved up; the route ends at the new anchor
        Assert.Equal(["y", "z"], router.Outputs.Select(p => p.Name));
        Assert.Same(y, router.Outputs[0]);
        Assert.Equal(0, y.Index);
        Assert.Same(y, wire.Source);
        Assert.Equal(page.ComputeRoute(wire.Source, wire.Target, wire), wire.Route);
    }

    /// <summary>
    ///     Validates that a wire on a port whose type changed is removed when the connection rules refuse it.
    /// </summary>
    [Fact]
    public void Node_Ports_SurvivingPortRetyped_RemovesWiresTheRulesRefuse()
    {
        // Arrange: matching types are required
        var (diagram, page, bus, producers) = Create();
        diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);
        var in0 = bus.Input("in0");
        var wire = page.Connect(producers[0].Output("value"), in0);

        // Act / Assert: one entry; undo and redo restore the wire and the port type exactly
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Kind"] = "i32");

        // Assert: after redo the port is the same object with the new type; its f32 wire is gone
        Assert.Same(in0, bus.Inputs[0]);
        Assert.Equal("i32", in0.TypeKey);
        Assert.Empty(page.Wires);
        Assert.Null(wire.Page);
    }

    /// <summary>
    ///     Validates that a wire on a port whose fan-in was turned off is removed when the connection rules refuse it.
    /// </summary>
    [Fact]
    public void Node_Ports_SurvivingPortFanInRemoved_RemovesWiresTheRulesRefuse()
    {
        // Arrange: a collector input that allows fan-in, fed by two producers
        var (diagram, page, _, producers) = Create();
        var collector = page.AddNode("acme.Collector", 600, 0, "C");
        collector.Properties["FanIn"] = true;
        var input = collector.Input("in");
        page.Connect(producers[0].Output("value"), input);
        page.Connect(producers[1].Output("value"), input);

        // Act / Assert: turning fan-in off is one exact undo entry
        TestModel.AssertRoundTrip(diagram, () => collector.Properties.Reset("FanIn"));

        // Assert: the same port object, now single-wire, keeps exactly one of its wires
        Assert.Same(input, collector.Inputs[0]);
        Assert.False(input.AllowsMultipleWires);
        Assert.Single(page.Wires);
    }

    /// <summary>
    ///     Validates that a wire on a retyped port stays when the connection rules still allow it.
    /// </summary>
    [Fact]
    public void Node_Ports_SurvivingPortRetyped_KeepsWiresStillAllowed()
    {
        // Arrange: the default rules do not compare types
        var (_, page, bus, producers) = Create();
        var wire = page.Connect(producers[0].Output("value"), bus.Input("in0"));

        // Act
        bus.Properties["Kind"] = "i32";

        // Assert
        Assert.Equal("i32", bus.Input("in0").TypeKey);
        Assert.Same(wire, Assert.Single(page.Wires));
    }

    /// <summary>
    ///     Validates that a node grows to fit more ports and that its wires are re-routed to the moved anchors.
    /// </summary>
    [Fact]
    public void Node_Ports_MorePortsThanHeight_GrowsNodeAndReroutesWires()
    {
        // Arrange
        var (diagram, page, bus, producers) = Create();
        var wire = page.Connect(producers[0].Output("value"), bus.Input("in0"));
        var before = bus.Bounds;

        // Act
        bus.Properties["Channels"] = 8;

        // Assert: taller by exactly what eight rows need, same position and width; the route is fresh
        Assert.Equal(NodeGeometry.RequiredHeight(8, diagram.Theme), bus.Bounds.Height);
        Assert.Equal(before.Position, bus.Bounds.Position);
        Assert.Equal(before.Width, bus.Bounds.Width);
        Assert.Equal(page.ComputeRoute(wire.Source, wire.Target, wire), wire.Route);
    }

    /// <summary>
    ///     Validates that a node shrinks to fit fewer ports, but never below its type's default height.
    /// </summary>
    [Fact]
    public void Node_Ports_FewerPorts_ShrinksToFitNotBelowDefaultHeight()
    {
        // Arrange: a tall eight-channel bus with a wire on in0
        var (diagram, page, bus, producers) = Create();
        var floor = NodeGeometry.DefaultNodeSize(bus.Type, diagram.Theme).Height;
        bus.Properties["Channels"] = 8;
        var wire = page.Connect(producers[0].Output("value"), bus.Input("in0"));
        var tall = bus.Bounds;

        // Act: three channels still need more than the default height; then a single channel does not
        bus.Properties["Channels"] = 3;
        var three = bus.Bounds;
        bus.Properties["Channels"] = 1;

        // Assert: each step fits the ports but never goes below the default; position and width are kept
        Assert.Equal(Math.Max(floor, NodeGeometry.RequiredHeight(3, diagram.Theme)), three.Height);
        Assert.True(three.Height < tall.Height);
        Assert.Equal(Math.Max(floor, NodeGeometry.RequiredHeight(1, diagram.Theme)), bus.Bounds.Height);
        Assert.Equal(floor, bus.Bounds.Height);
        Assert.Equal(tall.Position, bus.Bounds.Position);
        Assert.Equal(tall.Width, bus.Bounds.Width);
        Assert.Single(bus.Inputs);
        Assert.Equal(page.ComputeRoute(wire.Source, wire.Target, wire), wire.Route);
    }

    /// <summary>
    ///     Validates that a node of a type with a declared size shrinks back no further than the declared height.
    /// </summary>
    [Fact]
    public void Node_Ports_DeclaredSize_ShrinksNotBelowDeclaredHeight()
    {
        // Arrange: a router (declared 120 x 60) grown by five outputs
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var router = page.AddNode("acme.Router", 40, 20, "R");
        router.Data = new RouterData("a,b,c,d,e");
        var grown = router.Bounds;

        // Act
        router.Data = new RouterData("x");

        // Assert: the height fits one output but stays at least the declared height; the width stays declared
        Assert.Equal(NodeGeometry.RequiredHeight(5, diagram.Theme), grown.Height);
        Assert.Equal(Math.Max(60, NodeGeometry.RequiredHeight(1, diagram.Theme)), router.Bounds.Height);
        Assert.Equal(120, router.Bounds.Width);
        Assert.Equal(new Point(40, 20), router.Bounds.Position);
    }

    /// <summary>
    ///     Validates that a recomputation that keeps the number of ports leaves a host-set size alone, taller or shorter.
    /// </summary>
    [Fact]
    public void Node_Ports_SameCount_KeepsHandSetSize()
    {
        // Arrange
        var (_, _, bus, _) = Create();
        var taller = bus.Bounds with { Height = bus.Bounds.Height + 60 };
        var shorter = bus.Bounds with { Height = 30 };

        // Act / Assert: retyping the ports keeps a taller size
        bus.Bounds = taller;
        bus.Properties["Kind"] = "i32";
        Assert.Equal(taller, bus.Bounds);

        // Act / Assert: and keeps a shorter one too (the count did not change, so nothing is refitted)
        bus.Bounds = shorter;
        bus.Properties["Kind"] = "f32";
        Assert.Equal(shorter, bus.Bounds);
    }

    /// <summary>
    ///     Validates that undo and redo of a shrinking port change restore the exact previous and new bounds.
    /// </summary>
    [Fact]
    public void Node_Ports_Shrink_UndoRedoRestoreExactBounds()
    {
        // Arrange: an eight-channel bus with wires on its first two inputs
        var (diagram, page, bus, producers) = Create();
        bus.Properties["Channels"] = 8;
        page.Connect(producers[0].Output("value"), bus.Input("in0"));
        page.Connect(producers[1].Output("value"), bus.Input("in1"));
        var tall = bus.Bounds;

        // Act / Assert: one exact entry (the dump covers bounds and routes)
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Channels"] = 2);
        var shrunk = bus.Bounds;

        // Assert: undo restores the tall bounds exactly; redo the shrunk ones
        Assert.True(shrunk.Height < tall.Height);
        Assert.True(diagram.History.Undo());
        Assert.Equal(tall, bus.Bounds);
        Assert.True(diagram.History.Redo());
        Assert.Equal(shrunk, bus.Bounds);
    }

    /// <summary>
    ///     Validates that a port-changing property edit is one undo entry and that undo and redo restore everything exactly.
    /// </summary>
    [Fact]
    public void Node_Ports_PropertyChange_IsOneUndoEntryAndRoundTripsExactly()
    {
        // Arrange: four wired inputs
        var (diagram, page, bus, producers) = Create();
        bus.Properties["Channels"] = 4;
        foreach (var (producer, i) in producers.Select((p, i) => (p, i)))
        {
            page.Connect(producer.Output("value"), bus.Input($"in{i}"));
        }

        var ports = bus.Ports.ToArray();

        // Act / Assert: removes two wired ports, then grows the node; each is one exact entry
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Channels"] = 2);
        Assert.True(diagram.History.Undo());
        Assert.Equal(ports, bus.Ports);
        Assert.All(ports.Zip(bus.Ports), pair => Assert.Same(pair.First, pair.Second));
        TestModel.AssertRoundTrip(diagram, () => bus.Properties["Channels"] = 9);
    }

    /// <summary>
    ///     Validates that a change of the node's data recomputes its ports in one undo entry.
    /// </summary>
    [Fact]
    public void Node_Ports_DataChange_RecomputesPorts()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var router = page.AddNode("acme.Router", 0, 0, "R");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        page.Connect(router.Output("x"), consumer.Input("in"));

        // Act / Assert: x and its wire disappear; undo brings both back
        TestModel.AssertRoundTrip(diagram, () => router.Data = new RouterData("a,b,c"));
        Assert.Equal(["a", "b", "c"], router.Outputs.Select(p => p.Name));
        Assert.Empty(page.Wires);
    }

    /// <summary>
    ///     Validates that a provider failure rolls the triggering change back and records nothing.
    /// </summary>
    [Fact]
    public void Node_Ports_ProviderDuplicateNames_ThrowsAndRevertsAssignment()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var router = page.AddNode("acme.Router", 0, 0, "R");
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act
        Assert.Throws<ArgumentException>(() => router.Data = new RouterData("a,a"));

        // Assert
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that hosts are told about removed wires before the node reports its new ports.
    /// </summary>
    [Fact]
    public void Node_Ports_Change_ReportsWireRemovalsBeforePortsChanged()
    {
        // Arrange
        var (diagram, page, bus, producers) = Create();
        var wire = page.Connect(producers[1].Output("value"), bus.Input("in1"));
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);

        // Act
        bus.Properties["Channels"] = 1;

        // Assert: property change, wire removal, then the ports change of the node
        var removal = events.FindIndex(e => e.Kind == DiagramChangeKind.ElementRemoved && ReferenceEquals(e.Element, wire));
        var portsChanged = events.FindIndex(e => e.Kind == DiagramChangeKind.ElementChanged && ReferenceEquals(e.Element, bus) && e.PropertyName == "Ports");
        var propertyChanged = events.FindIndex(e => ReferenceEquals(e.Element, bus) && e.PropertyName == "Properties");
        Assert.True(propertyChanged >= 0 && propertyChanged < removal, "the property is reported first");
        Assert.True(removal < portsChanged, "the wire removal is reported before the ports change");
    }

    /// <summary>
    ///     Validates that an edit that leaves the ports as they are reports no ports change and records one entry.
    /// </summary>
    [Fact]
    public void Node_Ports_UnchangedPorts_RaisesNoPortsChangeAndOneEntry()
    {
        // Arrange
        var (diagram, _, bus, _) = Create();
        var definitions = bus.Ports.Select(p => p.Definition).ToArray();
        var portEvents = 0;
        bus.PropertyChanged += (_, e) => portEvents += e.PropertyName == "Ports" ? 1 : 0;
        var entries = diagram.History.UndoCount;

        // Act
        bus.Properties["Level"] = 2.0;

        // Assert: same definitions, no ports notification, one plain entry
        Assert.Equal(definitions, bus.Ports.Select(p => p.Definition));
        Assert.Equal(0, portEvents);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        Assert.Equal("Set Level", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that a port provider that returns null makes the edit fail.
    /// </summary>
    [Fact]
    public void Node_Ports_ProviderReturnsNull_ThrowsInvalidOperation()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("acme.Broken")
        {
            Properties = { new PropertyDefinition("Mode", 0) },
            PortProvider = node => node.Properties.Get<int>("Mode") == 0 ? new PortSet([], []) : null!,
        });
        var page = new Diagram(types).Pages.Add("Main");
        var node = page.AddNode("acme.Broken", 0, 0);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => node.Properties["Mode"] = 1);
        Assert.Contains("acme.Broken", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, node.Properties.Get<int>("Mode"));
    }

    /// <summary>
    ///     Validates that a node in a group reports the group's bounds as changed when its ports change.
    /// </summary>
    [Fact]
    public void Node_Ports_NodeInGroup_GroupFrameFollowsNewPorts()
    {
        // Arrange
        var (_, page, bus, _) = Create();
        var group = page.CreateGroup([bus], "G");
        var before = group.Bounds;
        var changes = 0;
        group.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(Group.Bounds) ? 1 : 0;

        // Act
        bus.Properties["Channels"] = 8;

        // Assert
        Assert.True(changes > 0);
        Assert.True(group.Bounds.Height > before.Height);
        Assert.True(group.Bounds.Contains(new Rect(bus.Bounds.X, bus.Bounds.Y, bus.Bounds.Width, bus.Bounds.Height)));
    }

    /// <summary>
    ///     Creates a page with a switch node (one input <c>in</c>, one output <c>out</c> while its <c>On</c> property is true,
    ///     no ports at all otherwise) wired to a producer and a consumer.
    /// </summary>
    /// <returns>The diagram, page and switch.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Switch) CreateSwitch()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Switch")
        {
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            Properties = { new PropertyDefinition("On", true) },
            PortProvider = node => node.Properties.Get<bool>("On")
                ? new PortSet([new PortDefinition("in", "f32")], [new PortDefinition("out", "f32")])
                : new PortSet([], []),
        });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var node = page.AddNode("acme.Switch", 200, 0, "S");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");
        page.Connect(producer.Output("value"), node.Input("in"));
        page.Connect(node.Output("out"), consumer.Input("in"));
        return (diagram, page, node);
    }

    /// <summary>
    ///     Validates that an empty port set leaves the node without ports, removes their wires and keeps the default height.
    /// </summary>
    [Fact]
    public void Node_Ports_ProviderReturnsEmptySet_RemovesEveryPortAndWire()
    {
        // Arrange: a wired switch with a hand-set taller size
        var (diagram, page, node) = CreateSwitch();
        var floor = NodeGeometry.DefaultNodeSize(node.Type, diagram.Theme).Height;
        node.Bounds = node.Bounds with { Height = floor + 90 };

        // Act / Assert: one exact entry
        TestModel.AssertRoundTrip(diagram, () => node.Properties["On"] = false);

        // Assert: no ports, no wires, and the height fits zero rows, which is the type's default height
        Assert.Empty(node.Ports);
        Assert.Empty(page.Wires);
        Assert.Equal(floor, node.Bounds.Height);
    }

    /// <summary>
    ///     Validates that when an input stops accepting several wires, the wires are re-checked in page order and the wire
    ///     latest in page order survives, each removal being reported before the ports change.
    /// </summary>
    [Fact]
    public void Node_Ports_FanInRemoved_KeepsWireLatestInPageOrder()
    {
        // Arrange: a fan-in collector fed by three wires, connected in page order w0, w1, w2
        var (diagram, page, _, producers) = Create();
        var collector = page.AddNode("acme.Collector", 600, 0, "C");
        collector.Properties["FanIn"] = true;
        var wires = producers.Take(3).Select(p => page.Connect(p.Output("value"), collector.Input("in"))).ToArray();
        var events = new List<string>();
        diagram.Changed += (_, e) =>
        {
            if (e.Kind == DiagramChangeKind.ElementRemoved && e.Element is Wire wire)
            {
                events.Add("removed " + Array.IndexOf(wires, wire));
            }
            else if (e.PropertyName == "Ports")
            {
                events.Add("ports");
            }
        };

        // Act
        collector.Properties.Reset("FanIn");

        // Assert: the last wire in page order survives; the others are removed in page order before the ports change
        Assert.Same(wires[2], Assert.Single(page.Wires));
        Assert.Equal(["removed 0", "removed 1", "ports"], events);
    }

    /// <summary>
    ///     Validates that an exception from the port provider reverts the assignment and propagates unchanged.
    /// </summary>
    [Fact]
    public void Node_Ports_ProviderThrows_RevertsAssignmentAndPropagates()
    {
        // Arrange: a provider that fails for one value
        var failure = new TimeoutException("provider failed");
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Fragile")
        {
            Inputs = { new PortDefinition("in", "f32") },
            Properties = { new PropertyDefinition("Mode", 0) },
            PortProvider = node => node.Properties.Get<int>("Mode") == 1
                ? throw failure
                : new PortSet([new PortDefinition("in", "f32")], []),
        });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Fragile", 0, 0);
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act
        var thrown = Assert.Throws<TimeoutException>(() => node.Properties["Mode"] = 1);

        // Assert: the provider's own exception; model and history unchanged
        Assert.Same(failure, thrown);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that a connection rule that throws while wires are re-checked rolls back every step.
    /// </summary>
    [Fact]
    public void Node_Ports_RuleThrowsDuringRevalidation_RollsBackEverything()
    {
        // Arrange: a rule that fails only while a wire is re-checked, and a wire on a port that will be retyped
        var (diagram, page, bus, producers) = Create();
        page.Connect(producers[0].Output("value"), bus.Input("in0"));
        diagram.ConnectionRules = (from, _) => from.Node.Page?.WireBeingReconnected is not null
            ? throw new InvalidOperationException("rule failed")
            : ConnectionCheck.Allowed;
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;
        var portEvents = 0;
        bus.PropertyChanged += (_, e) => portEvents += e.PropertyName == "Ports" ? 1 : 0;

        // Act
        Assert.Throws<InvalidOperationException>(() => bus.Properties["Kind"] = "i32");

        // Assert: everything rolled back, nothing recorded, no ports change reported
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Equal(0, portEvents);
    }

    /// <summary>
    ///     Validates that a wire refused after a port was retyped is already removed when the ports change is reported.
    /// </summary>
    [Fact]
    public void Node_Ports_RetypedPortRefusedWire_RemovedBeforePortsChanged()
    {
        // Arrange: matching types required, and an f32 wire on in0
        var (diagram, page, bus, producers) = Create();
        diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);
        var wire = page.Connect(producers[0].Output("value"), bus.Input("in0"));
        bool? wireGoneWhenReported = null;
        bus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Ports")
            {
                wireGoneWhenReported = wire.Page is null && !page.Wires.Contains(wire);
            }
        };

        // Act
        bus.Properties["Kind"] = "i32";

        // Assert
        Assert.True(wireGoneWhenReported);
    }

    /// <summary>
    ///     Validates that the ports change is reported once, after the node was resized and its wires re-routed.
    /// </summary>
    [Fact]
    public void Node_Ports_Resize_BoundsAndRoutesSettledWhenPortsChanged()
    {
        // Arrange: a wire on in0, and a handler recording what it sees when the ports change is reported
        var (_, page, bus, producers) = Create();
        var wire = page.Connect(producers[0].Output("value"), bus.Input("in0"));
        var seen = new List<(Rect Bounds, Point[] Route)>();
        bus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Ports")
            {
                seen.Add((bus.Bounds, wire.Route.ToArray()));
            }
        };

        // Act
        bus.Properties["Channels"] = 8;

        // Assert: exactly one report, and at that time bounds and route already had their final values
        var (bounds, route) = Assert.Single(seen);
        Assert.Equal(bus.Bounds, bounds);
        Assert.Equal(wire.Route, route);
    }

    /// <summary>
    ///     Validates that undo and redo each report the ports change once, after wires, bounds and routes are restored.
    /// </summary>
    [Fact]
    public void Node_Ports_Undo_ReportsPortsChangeOnceAfterRestore()
    {
        // Arrange: four wired inputs, then a shrink to two (which removes two wires and resizes)
        var (diagram, page, bus, producers) = Create();
        bus.Properties["Channels"] = 4;
        foreach (var (producer, i) in producers.Select((p, i) => (p, i)))
        {
            page.Connect(producer.Output("value"), bus.Input($"in{i}"));
        }

        bus.Properties["Channels"] = 2;
        var seen = new List<string>();
        bus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Ports")
            {
                seen.Add(TestModel.Dump(diagram));
            }
        };

        // Act / Assert: undo reports once, when wires, bounds, routes and the property are all restored
        Assert.True(diagram.History.Undo());
        var atUndo = Assert.Single(seen);
        Assert.Equal(TestModel.Dump(diagram), atUndo);
        Assert.Equal(4, page.Wires.Count);

        // Act / Assert: redo reports once, when the wires are removed and the node is resized again
        seen.Clear();
        Assert.True(diagram.History.Redo());
        var atRedo = Assert.Single(seen);
        Assert.Equal(TestModel.Dump(diagram), atRedo);
        Assert.Equal(2, page.Wires.Count);
    }
}
