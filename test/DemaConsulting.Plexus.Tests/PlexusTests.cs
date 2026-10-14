using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     System-level tests that exercise the Plexus core model end to end through its public API.
/// </summary>
public class PlexusTests
{
    /// <summary>
    ///     Proves the "hello diagram" works with zero configuration: build, connect, edit and query.
    /// </summary>
    [Fact]
    public void Plexus_HelloDiagram_DefaultConfiguration_BuildsConnectedDiagram()
    {
        // Arrange: a registry with two node types and nothing else configured
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
        types.Add(new NodeType("acme.Producer") { Outputs = { new PortDefinition("value", "f32") } });
        types.Add(new NodeType("acme.Consumer") { Inputs = { new PortDefinition("in", "f32") } });
        var diagram = new Diagram(types);

        // Act: the ten-line scenario
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 30);
        var consumer = page.AddNode("acme.Consumer", 300, 90);
        var wire = producer.Port("value").ConnectTo(consumer.Port("in"));

        // Assert: auto names, wire ends, routing and history all work by default
        Assert.Equal("Producer1", producer.Name);
        Assert.Equal("Consumer1", consumer.Name);
        Assert.Same(producer, wire.Source.Node);
        Assert.Same(consumer, wire.Target.Node);
        Assert.NotEmpty(wire.Route);
        Assert.True(diagram.History.CanUndo);
        Assert.True(diagram.History.IsDirty);
    }

    /// <summary>
    ///     Proves that a long, varied editing session can be fully undone to an empty diagram and fully redone.
    /// </summary>
    [Fact]
    public void Plexus_EditingSession_UndoAllThenRedoAll_RestoresExactStates()
    {
        // Arrange: an empty diagram
        var diagram = TestModel.CreateDiagram();
        var empty = TestModel.Dump(diagram);

        // Act: touch every kind of mutation
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");
        var a = one.AddNode("acme.Producer", 0, 0, "A");
        var b = one.AddNode("acme.Gain", 150, 0, "B");
        var c = one.AddNode("acme.Consumer", 300, 0, "C");
        var ab = a.Port("value").ConnectTo(b.Port("in"));
        var bc = b.Port("out").ConnectTo(c.Port("in"));
        ab.SetRoute([new Point(120, 100)]);
        a.Properties["Gain"] = 3.5;
        a.Properties["extra"] = "x";
        a.Data = "payload";
        c.Name = "C2";
        var note = one.AddAnnotation("hello", new Rect(0, 200, 120, 40));
        note.Text = "changed";
        var group = one.CreateGroup([a, b], "G");
        group.IsCollapsed = true;
        one.Move([group], 15, 15);
        group.IsCollapsed = false;
        diagram.MoveToPage([c, note], two);
        one.Ungroup(group);
        one.Remove(a);
        two.Name = "Second";
        diagram.Pages.Move(two, 0);
        var final = TestModel.Dump(diagram);
        Assert.NotNull(bc);

        // Assert: undo everything returns to the empty diagram; redo everything returns to the final state
        var steps = 0;
        while (diagram.History.Undo())
        {
            steps++;
        }

        Assert.True(steps > 10);
        Assert.Equal(empty, TestModel.Dump(diagram));
        while (diagram.History.Redo())
        {
            // Keep stepping until the stack is exhausted
        }

        Assert.Equal(final, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Proves that a transaction wrapping a whole editing gesture undoes as a single step.
    /// </summary>
    [Fact]
    public void Plexus_Transaction_WholeGesture_UndoesAsOneStep()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var before = TestModel.Dump(diagram);

        // Act: build a small sub-diagram inside one transaction
        using (diagram.History.BeginTransaction("Paste"))
        {
            var a = page.AddNode("acme.Producer", 0, 0);
            var b = page.AddNode("acme.Consumer", 200, 0);
            a.Port("value").ConnectTo(b.Port("in"));
            page.CreateGroup([a, b], "Pasted");
        }

        // Assert: one entry with the transaction's description; undo removes everything
        Assert.Equal("Paste", diagram.History.UndoDescription);
        Assert.True(diagram.History.Undo());
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.True(diagram.History.Redo());
        Assert.Equal(4, page.Elements.Count);
    }

    /// <summary>
    ///     Proves that history can be suspended while loading content and cleared afterwards.
    /// </summary>
    [Fact]
    public void Plexus_LoadingWithSuspendedHistory_LeavesCleanEmptyHistory()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();

        // Act: load content without recording, then reset history
        using (diagram.History.Suspend())
        {
            var page = diagram.Pages.Add("Loaded");
            page.AddNode("acme.Producer", 0, 0);
        }

        diagram.History.Clear();

        // Assert
        Assert.Single(diagram.Pages);
        Assert.False(diagram.History.CanUndo);
        Assert.False(diagram.History.IsDirty);
    }

    /// <summary>
    ///     Proves that a host can declare node types and port types, that invalid declarations are rejected and that
    ///     registered types are frozen.
    /// </summary>
    [Fact]
    public void Plexus_NodeTypes_DeclareAndRegister_RejectsInvalidDeclarationsAndFreezesTypes()
    {
        // Arrange: a registry with one port type and a fully declared node type
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
        var amp = new NodeType("acme.Amplifier")
        {
            Title = "AMPLIFIER",
            Icon = IconSource.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'/>"),
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            Properties = { new PropertyDefinition("Stages", 4) },
        };

        // Act: register the type
        types.Add(amp);

        // Assert: the type is retrievable by key with its declaration
        Assert.True(types.Contains("acme.Amplifier"));
        Assert.Same(amp, types.Get("acme.Amplifier"));
        Assert.Equal("AMPLIFIER", amp.Title);
        Assert.NotNull(amp.Icon);
        Assert.Equal(Color.Parse("#FF8C00"), types.PortTypes.Get("f32").Color);

        // Assert: duplicate keys, duplicate port names and duplicate property names are rejected
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.Amplifier")));
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.TwoIn")
        {
            Inputs = { new PortDefinition("a", "f32"), new PortDefinition("a", "f32") },
        }));
        Assert.Throws<ArgumentException>(() => types.Add(new NodeType("acme.TwoProps")
        {
            Properties = { new PropertyDefinition("P", 1), new PropertyDefinition("P", 2) },
        }));
        Assert.Equal(1, types.Count);

        // Assert: a registered type can no longer be modified
        Assert.Throws<NotSupportedException>(() => amp.Inputs.Add(new PortDefinition("extra", "f32")));
    }

    /// <summary>
    ///     Proves that node properties start at their declared defaults, convert or reject assigned values by kind,
    ///     and can be reset.
    /// </summary>
    [Fact]
    public void Plexus_Properties_TypedValues_DefaultsValidationAndReset()
    {
        // Arrange: a node whose type declares number, boolean, color and choice properties
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);

        // Assert: declared defaults are in place
        Assert.Equal(1.0, node.Properties.Get<double>("Gain"));
        Assert.True(node.Properties.Get<bool>("Enabled"));
        Assert.Equal("A", node.Properties.Get<string>("Mode"));
        Assert.Equal(Color.Parse("#112233"), node.Properties.Get<Color>("Tint"));

        // Act: assign values of the declared kinds
        node.Properties["Gain"] = 3.5;
        node.Properties["Count"] = 7;
        node.Properties["Mode"] = "B";
        node.Properties["Tint"] = Color.Red;

        // Assert: values are stored and incompatible assignments are rejected
        Assert.Equal(3.5, node.Properties.Get<double>("Gain"));
        Assert.Equal(7, node.Properties.Get<int>("Count"));
        Assert.Equal("B", node.Properties.Get<string>("Mode"));
        Assert.Equal(Color.Red, node.Properties.Get<Color>("Tint"));
        Assert.Throws<ArgumentException>(() => node.Properties["Enabled"] = "yes");
        Assert.Throws<ArgumentException>(() => node.Properties["Mode"] = "C");

        // Act: reset one property
        node.Properties.Reset("Gain");

        // Assert: the default is restored and the other values are untouched
        Assert.Equal(1.0, node.Properties.Get<double>("Gain"));
        Assert.Equal(7, node.Properties.Get<int>("Count"));
    }

    /// <summary>
    ///     Proves that pages can be added, inserted, renamed, reordered and removed, and that removal is undoable.
    /// </summary>
    [Fact]
    public void Plexus_Pages_AddInsertRenameMoveRemove_KeepsOrderAndUndoesRemoval()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();

        // Act: build up and reorder the page list
        var b = diagram.Pages.Add("B");
        var c = diagram.Pages.Add("C");
        var a = diagram.Pages.Insert(0, "A");
        c.Name = "Gamma";
        diagram.Pages.Move(c, 0);

        // Assert: the order follows the operations
        Assert.Equal(["Gamma", "A", "B"], diagram.Pages.Select(p => p.Name));

        // Act: remove a page
        Assert.True(diagram.Pages.Remove(a));

        // Assert: it is gone, and undo brings it back at the same place
        Assert.Equal(["Gamma", "B"], diagram.Pages.Select(p => p.Name));
        Assert.True(diagram.History.Undo());
        Assert.Equal(["Gamma", "A", "B"], diagram.Pages.Select(p => p.Name));
        Assert.Same(b, diagram.Pages[2]);
    }

    /// <summary>
    ///     Proves that nodes and annotations are added with unique generated or explicit names and ids and can be
    ///     found from the diagram.
    /// </summary>
    [Fact]
    public void Plexus_Elements_AddNodesAndAnnotations_UniqueNamesAndIdsAreFindable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");

        // Act
        var first = page.AddNode("acme.Gain", 0, 0);
        var second = page.AddNode("acme.Gain", 100, 0);
        var named = page.AddNode("acme.Gain", 200, 0, "Stage");
        var note = page.AddAnnotation("Remember", new Rect(0, 100, 120, 40));

        // Assert: generated names are unique, explicit names are kept and ids are distinct
        Assert.Equal(["Gain1", "Gain2", "Stage"], new[] { first.Name, second.Name, named.Name });
        Assert.Equal(4, new[] { first.Id, second.Id, named.Id, note.Id }.Distinct().Count());
        Assert.Equal(new Rect(0, 100, 120, 40), note.Bounds);
        Assert.Equal("Remember", note.Text);

        // Assert: elements are found again from the diagram, and invalid additions are rejected
        Assert.Same(named, diagram.FindNode("Stage"));
        Assert.Same(note, diagram.FindElement(note.Id));
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Gain", 0, 0, "Stage"));
        Assert.Throws<ArgumentException>(() => page.AddNode("acme.Unknown", 0, 0));
    }

    /// <summary>
    ///     Proves that renaming goes through validation and a host hook that can correct or cancel the change, and
    ///     that an applied rename is undoable.
    /// </summary>
    [Fact]
    public void Plexus_Rename_HostHookCorrectsOrCancels_AppliedRenameIsUndoable()
    {
        // Arrange: a host hook that replaces spaces and cancels long names
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Gain", 0, 0, "Original");
        page.AddNode("acme.Gain", 100, 0, "Taken");
        diagram.NodeRenaming += (_, e) =>
        {
            if (e.ProposedName.Length > 12)
            {
                e.Cancel = true;
                e.Reason = "Too long.";
            }
            else
            {
                e.NewName = e.ProposedName.Replace(' ', '_');
            }
        };

        // Act
        var corrected = diagram.TryRename(node, "My amp", out var correctedReason);
        var cancelled = diagram.TryRename(node, "A very long name indeed", out var cancelReason);
        var duplicate = diagram.TryRename(node, "Taken", out var duplicateReason);

        // Assert: the hook's correction is applied; cancellation and duplicates are refused with a reason
        Assert.True(corrected);
        Assert.Null(correctedReason);
        Assert.Equal("My_amp", node.Name);
        Assert.False(cancelled);
        Assert.Equal("Too long.", cancelReason);
        Assert.False(duplicate);
        Assert.False(string.IsNullOrEmpty(duplicateReason));
        Assert.Equal("My_amp", node.Name);

        // Assert: the applied rename is undoable
        Assert.True(diagram.History.Undo());
        Assert.Equal("Original", node.Name);
    }

    /// <summary>
    ///     Proves that wires can be connected from port objects or "Node.port" paths, are ordered output to input,
    ///     are routed by default and have an editable route.
    /// </summary>
    [Fact]
    public void Plexus_Wires_ConnectByObjectsAndPaths_AreOrderedRoutedAndEditable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var first = page.AddNode("acme.Consumer", 300, 120, "C1");
        var second = page.AddNode("acme.Consumer", 300, 300, "C2");

        // Act: connect by objects and, with the ends reversed, by paths
        var byObjects = producer.Port("value").ConnectTo(first.Port("in"));
        var byPaths = page.Connect("C2.in", "P.value");

        // Assert: both wires run from the output to the input, with an automatic route
        Assert.Same(producer.Port("value"), byObjects.Source);
        Assert.Same(first.Port("in"), byObjects.Target);
        Assert.Same(producer.Port("value"), byPaths.Source);
        Assert.Same(second.Port("in"), byPaths.Target);
        Assert.NotEmpty(byObjects.Route);

        // Act: edit the route manually, then recompute it
        var manual = new[] { new Point(150, 10), new Point(150, 200) };
        byObjects.SetRoute(manual);
        var edited = byObjects.Route.ToArray();
        byObjects.Reroute();

        // Assert: the manual route was stored, and rerouting replaced it with the router's route
        Assert.Equal(manual, edited);
        Assert.NotEqual(manual, byObjects.Route);
        Assert.Equal(byObjects.Source.Anchor, byObjects.GetPoints()[0]);
    }

    /// <summary>
    ///     Proves that structural rules and host rules deny connections with a reason, both when asking beforehand
    ///     and when connecting.
    /// </summary>
    [Fact]
    public void Plexus_ConnectionRules_StructuralAndHostRules_DenyWithReasonBeforeAndOnConnect()
    {
        // Arrange: type-compatibility and single-input rules
        var diagram = TestModel.CreateDiagram();
        diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.SameType, ConnectionRules.SingleInputWire);
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var other = page.AddNode("acme.Producer", 0, 150, "Q");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var intConsumer = page.AddNode("acme.IntConsumer", 300, 150, "I");
        page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act: ask without connecting
        var typeMismatch = page.CanConnect(producer.Port("value"), intConsumer.Port("in"));
        var secondInputWire = page.CanConnect(other.Port("value"), consumer.Port("in"));
        var outputToOutput = page.CanConnect(producer.Port("value"), other.Port("value"));
        var allowed = page.CanConnect(other.Port("value"), page.AddNode("acme.Consumer", 300, 300, "D").Port("in"));

        // Assert: denials carry reasons and the model is unchanged
        Assert.False(typeMismatch.IsAllowed);
        Assert.False(string.IsNullOrEmpty(typeMismatch.Reason));
        Assert.False(secondInputWire.IsAllowed);
        Assert.False(string.IsNullOrEmpty(secondInputWire.Reason));
        Assert.False(outputToOutput.IsAllowed);
        Assert.True(allowed.IsAllowed);
        Assert.Single(page.Wires);

        // Act / Assert: connecting a denied pair throws with the same reason
        var ex = Assert.Throws<InvalidOperationException>(() => page.Connect(producer.Port("value"), intConsumer.Port("in")));
        Assert.Equal(typeMismatch.Reason, ex.Message);
        Assert.Single(page.Wires);
    }

    /// <summary>
    ///     Proves that groups nest, collapse, move their members and dissolve without removing the members.
    /// </summary>
    [Fact]
    public void Plexus_Groups_NestCollapseMoveAndUngroup_MembersFollowTheGroup()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 150, 0, "B");
        var c = page.AddNode("acme.Consumer", 300, 0, "C");

        // Act: nest an inner group in an outer group
        var inner = page.CreateGroup([a, b], "Inner");
        var outer = page.CreateGroup([inner, c], "Outer");

        // Assert: hierarchy
        Assert.Same(inner, a.Parent);
        Assert.Same(outer, inner.Parent);
        Assert.Equal(4, outer.AllMembers().Count());

        // Act: collapse the inner group
        inner.IsCollapsed = true;

        // Assert: its members are hidden, the other member is not
        Assert.False(a.IsVisible);
        Assert.False(b.IsVisible);
        Assert.True(c.IsVisible);

        // Act: expand and move the outer group
        inner.IsCollapsed = false;
        var before = a.Bounds;
        page.Move([outer], 15, 30);

        // Assert: every member moved by the same offset
        Assert.Equal(before.Offset(15, 30), a.Bounds);
        Assert.Equal(new Point(315, 30), c.Bounds.Position);

        // Act: dissolve the outer group
        page.Ungroup(outer);

        // Assert: the frame is gone, the members remain and take its place
        Assert.DoesNotContain(outer, page.Elements);
        Assert.Contains(c, page.Elements);
        Assert.Null(c.Parent);
        Assert.Null(inner.Parent);
        Assert.Same(inner, a.Parent);
    }

    /// <summary>
    ///     Proves that moving keeps wires attached, removing deletes attached wires, and moving between pages drops
    ///     wires that would span pages.
    /// </summary>
    [Fact]
    public void Plexus_Editing_MoveRemoveAndMoveToPage_KeepsWiresConsistent()
    {
        // Arrange: a chain A -> B -> C on one page and an empty second page
        var diagram = TestModel.CreateDiagram();
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");
        var a = one.AddNode("acme.Producer", 0, 0, "A");
        var b = one.AddNode("acme.Gain", 200, 0, "B");
        var c = one.AddNode("acme.Consumer", 400, 0, "C");
        var ab = a.Port("value").ConnectTo(b.Port("in"));
        var bc = b.Port("out").ConnectTo(c.Port("in"));

        // Act: move B; the wires stay attached to it
        one.Move([b], 0, 120);

        // Assert
        Assert.Equal(b.Port("in").Anchor, ab.GetPoints()[^1]);
        Assert.Equal(b.Port("out").Anchor, bc.GetPoints()[0]);

        // Act: move C to the other page
        diagram.MoveToPage([c], two);

        // Assert: C changed page and the wire to it was removed
        Assert.Same(two, c.Page);
        Assert.DoesNotContain(bc, one.Wires);
        Assert.Contains(ab, one.Wires);

        // Act: remove B
        one.Remove(b);

        // Assert: its remaining wire is removed with it
        Assert.Empty(one.Wires);
        Assert.Single(one.Nodes);
    }

    /// <summary>
    ///     Proves that the diagram raises Changed for edits, undo and redo, and that the page list is observable.
    /// </summary>
    [Fact]
    public void Plexus_ChangeNotification_EditUndoRedo_RaisesChangedForEveryChange()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var kinds = new List<DiagramChangeKind>();
        var pageListChanges = 0;
        diagram.Changed += (_, e) => kinds.Add(e.Kind);
        ((System.Collections.Specialized.INotifyCollectionChanged)diagram.Pages).CollectionChanged += (_, _) => pageListChanges++;

        // Act: add a page and a node, then undo and redo the node
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0);
        diagram.History.Undo();
        diagram.History.Redo();
        node.Properties["Gain"] = 2.0;

        // Assert: every step was reported, including undo and redo
        Assert.Equal(
            [
                DiagramChangeKind.PageAdded,
                DiagramChangeKind.ElementAdded,
                DiagramChangeKind.ElementRemoved,
                DiagramChangeKind.ElementAdded,
                DiagramChangeKind.ElementChanged,
            ],
            kinds);
        Assert.Equal(1, pageListChanges);
    }

    /// <summary>
    ///     Proves that the geometry and color value types work together in the model: parsed colors, rectangles
    ///     and points describe and position elements.
    /// </summary>
    [Fact]
    public void Plexus_Geometry_ColorsRectsAndPoints_DescribeAndPositionElements()
    {
        // Arrange / Act: parse colors from both hexadecimal forms and build a rectangle from two corners
        var opaque = Color.Parse("#FF8C00");
        var translucent = Color.Parse("#80FF8C00");
        var rect = Rect.FromPoints(new Point(300, 200), new Point(100, 50));
        var diagram = TestModel.CreateDiagram();
        var note = diagram.Pages.Add("Main").AddAnnotation("Area", rect);

        // Assert: colors parse and format back, rectangles normalize and relate to points
        Assert.Equal(new Color(255, 140, 0), opaque);
        Assert.Equal(128, translucent.A);
        Assert.Equal(opaque, Color.Parse(opaque.ToString()));
        Assert.Equal(new Rect(100, 50, 200, 150), note.Bounds);
        Assert.True(note.Bounds.Contains(new Point(150, 100)));
        Assert.False(note.Bounds.Contains(new Point(50, 100)));
        Assert.Equal(new Point(200, 125), note.Bounds.Center);
        Assert.Throws<FormatException>(() => Color.Parse("orange"));
    }

    /// <summary>
    ///     Proves that the default router routes around nodes and that a host router is used for new wires.
    /// </summary>
    [Fact]
    public void Plexus_Routing_DefaultAvoidsNodesAndCustomRouterIsUsed()
    {
        // Arrange: producer and consumer on one row with a node in between
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var blocker = page.AddNode("acme.Gain", 150, 0, "B");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");

        // Act: connect with the default router
        var routed = page.Connect(producer.Port("value"), consumer.Port("in"));
        var points = routed.GetPoints();

        // Assert: only horizontal and vertical segments, none passing through the blocking node
        for (var i = 1; i < points.Count; i++)
        {
            Assert.True(Math.Abs(points[i].X - points[i - 1].X) < 1e-9 || Math.Abs(points[i].Y - points[i - 1].Y) < 1e-9);
            Assert.False(SegmentCrosses(points[i - 1], points[i], blocker.Bounds));
        }

        // Act: switch to a host router and connect another wire
        var other = page.AddNode("acme.Consumer", 300, 200, "D");
        diagram.Router = new FixedRouter(new Point(220, 220));
        var custom = page.Connect(producer.Port("value"), other.Port("in"));

        // Assert: the host router's bend points are the wire's route
        Assert.Equal([new Point(220, 220)], custom.Route);
        Assert.IsType<OrthogonalRouter>(new Diagram(TestModel.CreateTypes()).Router);
    }

    /// <summary>
    ///     Proves that a scene is built from a page without any UI framework and contains the drawing primitives of
    ///     the grid, nodes and wires, and bounds that cover the content.
    /// </summary>
    [Fact]
    public void Plexus_Scene_BuildPage_ContainsGridNodesAndWires()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 30, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 90, "C");
        var wire = producer.Port("value").ConnectTo(consumer.Port("in"));

        // Act
        var withGrid = SceneBuilder.Build(page);
        var withoutGrid = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: grid, node text and wire path primitives are present and attributed to their elements
        Assert.Contains(withGrid.Primitives, p => p is GridPrimitive);
        Assert.DoesNotContain(withoutGrid.Primitives, p => p is GridPrimitive);
        Assert.Contains(withoutGrid.Primitives, p => p is TextPrimitive t && ReferenceEquals(t.Element, producer));
        Assert.Contains(withoutGrid.Primitives, p => p is PathPrimitive && ReferenceEquals(p.Element, wire));
        Assert.True(withoutGrid.Bounds.Contains(producer.Bounds));
        Assert.True(withoutGrid.Bounds.Contains(consumer.Bounds));
        Assert.True(withoutGrid.TryGetNodeLayout(producer, out _));
    }

    /// <summary>
    ///     Proves that theme, text measurement and node visuals can be customized and are honored by the scene.
    /// </summary>
    [Fact]
    public void Plexus_SceneCustomization_ThemeMeasurerAndVisual_AreHonored()
    {
        // Arrange: a custom node type with its own visual and a page using it
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Badge") { Visual = new BadgeVisual() });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var plain = page.AddNode("acme.Consumer", 0, 0, "Plain");
        var badge = page.AddNode("acme.Badge", 200, 0, "Badge");
        var theme = DiagramTheme.Classic with { NodeFill = Color.Parse("#335577"), CanvasBackground = Color.Parse("#101010") };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Theme = theme, TextMeasurer = new WideMeasurer() });
        var defaultScene = SceneBuilder.Build(page);

        // Assert: the theme color is used, the custom visual's primitive is present, and the measurer changes layout
        Assert.Same(theme, scene.Theme);
        Assert.Contains(scene.Primitives, p => p is RectPrimitive r && ReferenceEquals(r.Element, plain) && r.Fill == theme.NodeFill);
        Assert.Contains(scene.Primitives, p => p is TextPrimitive t && t.Text == "badge-marker" && ReferenceEquals(t.Element, badge));
        Assert.DoesNotContain(defaultScene.Primitives, p => p is TextPrimitive t && t.Text == "badge-marker" && !ReferenceEquals(t.Element, badge));
        Assert.True(scene.TryGetNodeLayout(plain, out var wide));
        Assert.True(defaultScene.TryGetNodeLayout(plain, out var normal));
        Assert.NotEqual(normal.Name.Bounds, wide.Name.Bounds);
    }

    /// <summary>
    ///     Proves that hit testing identifies nodes, ports, wires and empty canvas in model terms.
    /// </summary>
    [Fact]
    public void Plexus_HitTest_NodePortWireAndCanvas_AreIdentified()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 60, "P");
        var consumer = page.AddNode("acme.Consumer", 330, 60, "C");
        var wire = producer.Port("value").ConnectTo(consumer.Port("in"));
        var scene = SceneBuilder.Build(page);
        var points = wire.GetPoints();
        var middle = new Point((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);

        // Act
        var onNode = scene.HitTest(new Point(75, 105));
        var onPort = scene.HitTest(consumer.Port("in").Anchor);
        var onWire = scene.HitTest(middle);
        var onCanvas = scene.HitTest(new Point(1000, 1000));

        // Assert
        Assert.Equal(HitKind.Node, onNode.Kind);
        Assert.Same(producer, onNode.Element);
        Assert.Equal(HitKind.Port, onPort.Kind);
        Assert.Same(consumer.Port("in"), onPort.Port);
        Assert.Equal(HitKind.Wire, onWire.Kind);
        Assert.Same(wire, onWire.Element);
        Assert.Equal(HitKind.Canvas, onCanvas.Kind);
        Assert.False(onCanvas.IsHit);
    }

    /// <summary>
    ///     Proves that a diagram can be captured as JSON and restored with the same content and an empty, clean
    ///     history.
    /// </summary>
    [Fact]
    public void Plexus_Snapshot_ToJsonFromJson_RestoresContentWithCleanHistory()
    {
        // Arrange: a diagram with several element kinds
        var types = TestModel.CreateTypes();
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 300, 90, "B");
        a.Properties["Gain"] = 2.5;
        a.Port("value").ConnectTo(b.Port("in"));
        page.AddAnnotation("note", new Rect(0, 200, 100, 40));
        page.CreateGroup([a, b], "G");
        var expected = TestModel.Dump(diagram);

        // Act
        var json = diagram.ToJson();
        var restored = Diagram.FromJson(json, types);

        // Assert: identical content, no undo entries and not dirty
        Assert.Equal(expected, TestModel.Dump(restored));
        Assert.False(restored.History.CanUndo);
        Assert.False(restored.History.IsDirty);
        Assert.Throws<SnapshotException>(() => Diagram.FromJson("{\"format\":\"nonsense\"}", types));
    }

    /// <summary>
    ///     Proves that duplicating a selection clones nodes, wires and groups at an offset as one undo step.
    /// </summary>
    [Fact]
    public void Plexus_Duplicate_NodesAndWire_CloneAtOffsetAsOneUndoStep()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 300, 0, "B");
        a.Port("value").ConnectTo(b.Port("in"));
        var before = TestModel.Dump(diagram);

        // Act
        var copies = page.Duplicate([a, b], 30, 40);

        // Assert: two new nodes at the offset and their wire, with new ids and names
        Assert.Equal(3, copies.Count);
        Assert.Equal(2, page.Wires.Count);
        var copy = Assert.IsType<Node>(copies[0]);
        Assert.Equal(new Point(30, 40), copy.Bounds.Position);
        Assert.NotEqual(a.Id, copy.Id);
        Assert.NotEqual(a.Name, copy.Name);

        // Assert: a single undo removes the whole duplicate
        Assert.True(diagram.History.Undo());
        Assert.Equal(before, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Proves that a selection can be copied to text and pasted into another diagram, atomically, with
    ///     missing node types reported.
    /// </summary>
    [Fact]
    public void Plexus_Clipboard_CopyPasteAcrossDiagrams_RecreatesElementsAtomically()
    {
        // Arrange: a source selection with an internal wire and a wire leaving the selection
        var source = TestModel.CreateDiagram();
        var sourcePage = source.Pages.Add("Main");
        var a = sourcePage.AddNode("acme.Producer", 0, 0, "A");
        var g = sourcePage.AddNode("acme.Gain", 200, 0, "G");
        var c = sourcePage.AddNode("acme.Consumer", 400, 0, "C");
        a.Properties["Gain"] = 4.0;
        a.Port("value").ConnectTo(g.Port("in"));
        g.Port("out").ConnectTo(c.Port("in"));
        var target = TestModel.CreateDiagram();
        var targetPage = target.Pages.Add("Target");

        // Act: copy A and G, paste at a position
        var text = PlexusClipboard.Copy([a, g]);
        var result = PlexusClipboard.Paste(targetPage, text, new Point(100, 100));

        // Assert: two nodes and only the internal wire arrive, with their properties
        Assert.True(result.Success);
        Assert.Equal(2, targetPage.Nodes.Count);
        Assert.Single(targetPage.Wires);
        Assert.Equal(4.0, targetPage.Nodes[0].Properties.Get<double>("Gain"));
        Assert.Equal(new Point(100, 100), targetPage.Nodes[0].Bounds.Position);

        // Act: paste into a diagram that does not know the node types
        var foreign = new Diagram(new NodeTypeRegistry());
        var foreignPage = foreign.Pages.Add("Foreign");
        var undoBeforePaste = foreign.History.UndoDescription;
        var failed = PlexusClipboard.Paste(foreignPage, text);

        // Assert: nothing is pasted and the missing types are listed
        Assert.False(failed.Success);
        Assert.Contains("acme.Producer", failed.MissingNodeTypes);
        Assert.Contains("acme.Gain", failed.MissingNodeTypes);
        Assert.Empty(foreignPage.Elements);
        Assert.Equal(undoBeforePaste, foreign.History.UndoDescription);
    }

    /// <summary>
    ///     Proves that hostile or malformed clipboard text is rejected with an error and leaves the target page and
    ///     its history untouched.
    /// </summary>
    [Fact]
    public void Plexus_ClipboardSafety_HostileAndMalformedText_IsRejectedWithoutChange()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "A");
        var valid = PlexusClipboard.Copy([node]);
        var undoBefore = diagram.History.CanUndo;
        var oversize = "{\"format\":\"" + PlexusClipboard.FormatId + "\",\"pad\":\""
                       + new string('x', PlexusClipboard.MaxPayloadBytes + 1) + "\"}";
        var tooDeep = new string('[', 200) + new string(']', 200);
        var hostile = new[] { "not json at all", "{\"format\":\"something.else\"}", oversize, tooDeep };

        // Act
        var results = hostile.Select(text => PlexusClipboard.Paste(page, text)).ToList();

        // Assert: each payload is rejected with an error message and the page keeps only the original node
        Assert.All(results, r =>
        {
            Assert.False(r.Success);
            Assert.False(string.IsNullOrEmpty(r.Error));
            Assert.Empty(r.Elements);
        });
        Assert.Single(page.Elements);
        Assert.Equal(undoBefore, diagram.History.CanUndo);
        Assert.Equal("Main", page.Name);

        // Assert: the valid payload still pastes afterwards
        Assert.True(PlexusClipboard.Paste(page, valid).Success);
    }

    /// <summary>
    ///     Proves that wires of different nets are routed on separate tracks, a branching net is marked with a junction dot,
    ///     and a selected wire offers handles whose edits (reconnect, jog) are undoable and keep the wire orthogonal.
    /// </summary>
    [Fact]
    public void Plexus_WireNetsAndEditing_SeparateNetsJunctionsAndHandles_WorkThroughThePublicApi()
    {
        // Arrange: two producers feeding a mixer and a consumer, so one net branches and a second net runs alongside
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var p1 = page.AddNode("acme.Producer", 0, 0, "P1");
        var p2 = page.AddNode("acme.Producer", 0, 300, "P2");
        var mixer = page.AddNode("acme.Mixer", 450, 120, "M");
        var consumer = page.AddNode("acme.Consumer", 450, 420, "C");
        var otherConsumer = page.AddNode("acme.Consumer", 450, 600, "C2");
        var a = page.Connect(p1.Port("value"), mixer.Port("a"));
        var b = page.Connect(p2.Port("value"), mixer.Port("b"));
        var branch = page.Connect(p2.Port("value"), consumer.Port("in"));

        // Assert: different nets do not share a segment, the branching net shows a dot
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [a] });
        Assert.False(SharesSegment(a.GetPoints(), b.GetPoints()));
        Assert.Contains(scene.Primitives.OfType<RectPrimitive>(), r => r.Fill is not null && r.Stroke is null && (ReferenceEquals(r.Element, b) || ReferenceEquals(r.Element, branch)));

        // Act: a selected wire reports its handles, and the edits made through them are undoable
        var bendHit = scene.HitTest(a.Route[0]);
        var jogged = a.AddJog(1, new Point((a.GetPoints()[1].X + a.GetPoints()[2].X) / 2, (a.GetPoints()[1].Y + a.GetPoints()[2].Y) / 2));
        var routeWithJog = a.Route.ToArray();
        a.Reconnect(null, otherConsumer.Port("in"));
        var reconnected = a.Target;
        diagram.History.Undo();
        var afterFirstUndo = (a.Target, a.Route.ToArray());
        diagram.History.Undo();

        // Assert: bend handle found, jog added, reconnect kept the wire and moved only its target, undo steps back one edit at a time
        Assert.Multiple(
            () => Assert.Equal(HitKind.WireBend, bendHit.Kind),
            () => Assert.True(jogged),
            () => Assert.Same(otherConsumer.Port("in"), reconnected),
            () => Assert.Same(mixer.Port("a"), afterFirstUndo.Target),
            () => Assert.Equal(routeWithJog, afterFirstUndo.Item2),
            () => Assert.Equal(4, routeWithJog.Length - a.Route.Count),
            () => Assert.Contains(branch, page.Wires));
    }

    /// <summary>
    ///     Determines whether two polylines have a collinear stretch in common.
    /// </summary>
    /// <param name="first">The first polyline.</param>
    /// <param name="second">The second polyline.</param>
    /// <returns>True if some segment of one runs along some segment of the other for a positive length.</returns>
    private static bool SharesSegment(IReadOnlyList<Point> first, IReadOnlyList<Point> second)
    {
        for (var i = 0; i + 1 < first.Count; i++)
        {
            for (var j = 0; j + 1 < second.Count; j++)
            {
                var vertical = Math.Abs(first[i].X - first[i + 1].X) < 1e-6 && Math.Abs(second[j].X - second[j + 1].X) < 1e-6 && Math.Abs(first[i].X - second[j].X) < 1e-6;
                var horizontal = Math.Abs(first[i].Y - first[i + 1].Y) < 1e-6 && Math.Abs(second[j].Y - second[j + 1].Y) < 1e-6 && Math.Abs(first[i].Y - second[j].Y) < 1e-6;
                var overlap = vertical
                    ? Math.Min(Math.Max(first[i].Y, first[i + 1].Y), Math.Max(second[j].Y, second[j + 1].Y)) - Math.Max(Math.Min(first[i].Y, first[i + 1].Y), Math.Min(second[j].Y, second[j + 1].Y))
                    : Math.Min(Math.Max(first[i].X, first[i + 1].X), Math.Max(second[j].X, second[j + 1].X)) - Math.Max(Math.Min(first[i].X, first[i + 1].X), Math.Min(second[j].X, second[j + 1].X));
                if ((vertical || horizontal) && overlap > 1e-6)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Proves that a node whose ports come from a port provider updates its ports and wires with a property change, as one undo step.
    /// </summary>
    [Fact]
    public void Plexus_DynamicPorts_PropertyChange_UpdatesPortsWiresAndUndoesInOneStep()
    {
        // Arrange: a host type with a "Channels" property deciding its inputs, fed on its last channel
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("acme.Source") { Outputs = { new PortDefinition("out", "f32") } });
        types.Add(new NodeType("acme.Bus")
        {
            Inputs = { new PortDefinition("in0", "f32"), new PortDefinition("in1", "f32") },
            Properties = { new PropertyDefinition("Channels", 2) },
            PortProvider = node => new PortSet(
                Enumerable.Range(0, node.Properties.Get<int>("Channels")).Select(i => new PortDefinition($"in{i}", "f32")),
                []),
        });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        page.AddNode("acme.Source", 0, 0, "S1");
        page.AddNode("acme.Source", 0, 150, "S2");
        var bus = page.AddNode("acme.Bus", 300, 0, "Bus");
        bus.Properties["Channels"] = 4;
        var first = page.Connect("S1.out", "Bus.in0");
        var last = page.Connect("S2.out", "Bus.in3");
        var in0 = bus.Input("in0");
        var entries = diagram.History.UndoCount;
        var height = bus.Bounds.Height;

        // Act: drop to two channels
        bus.Properties["Channels"] = 2;

        // Assert: in3 and its wire are gone, in0 kept its object and wire, one undo entry
        Assert.Equal(["in0", "in1"], bus.Inputs.Select(p => p.Name));
        Assert.Same(in0, bus.Inputs[0]);
        Assert.Equal([first], page.Wires);
        Assert.Equal(height, bus.Bounds.Height);
        Assert.Equal(entries + 1, diagram.History.UndoCount);

        // Act / Assert: undo restores the four ports and both wires exactly
        Assert.True(diagram.History.Undo());
        Assert.Equal(4, bus.Inputs.Count);
        Assert.Equal([first, last], page.Wires);
        Assert.Same(bus.Input("in3"), last.Target);
    }

    /// <summary>
    ///     Proves that saving, loading and pasting restore wires that attach to ports computed by a port provider.
    /// </summary>
    [Fact]
    public void Plexus_DynamicPorts_SaveLoadAndPaste_RestoreWiresOnDynamicPorts()
    {
        // Arrange
        var original = Serialization.SerializationFixtures.CreateDynamicPortDiagram();

        // Act: save and load, then paste everything into a fresh diagram
        var loaded = Diagram.FromJson(original.ToJson(), TestModel.CreateDynamicTypes());
        var target = TestModel.CreateDynamicDiagram().Pages.Add("Target");
        var result = PlexusClipboard.Paste(target, PlexusClipboard.Copy(original.Pages[0].Elements));

        // Assert: the loaded diagram equals the original, and the pasted wires use the computed ports
        Assert.Equal(TestModel.Dump(original), TestModel.Dump(loaded));
        Assert.True(result.Success, result.Error);
        Assert.Equal(["P.value -> Bus.in2", "R.z -> C.in"], target.Wires.Select(w => $"{w.Source} -> {w.Target}"));
    }

    /// <summary>
    ///     Proves that port, node type and port type descriptions provide the text hosts show in tooltips, including live state.
    /// </summary>
    [Fact]
    public void Plexus_Descriptions_PortsAndNodeTypes_ProvideToolTipText()
    {
        // Arrange
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")) { DisplayName = "Float" });
        types.Add(new NodeType("acme.Gauge")
        {
            Description = "Shows a value.",
            Inputs = { new PortDefinition("in", "f32") { Description = "The value to show." } },
            Properties = { new PropertyDefinition("Value", 0.0) },
            ToolTip = node => $"Value = {node.Properties.Get<double>("Value")}",
        });
        types.Add(new NodeType("acme.Plain") { Description = "Does nothing." });
        var page = new Diagram(types).Pages.Add("Main");
        var gauge = page.AddNode("acme.Gauge", 0, 0);
        var plain = page.AddNode("acme.Plain", 200, 0);

        // Act
        gauge.Properties["Value"] = 2.5;

        // Assert: live tooltip text, description fallback, port description and type display name
        Assert.Equal("Value = 2.5", gauge.Type.GetToolTip(gauge));
        Assert.Equal("Does nothing.", plain.Type.GetToolTip(plain));
        Assert.Equal("The value to show.", gauge.Input("in").Description);
        Assert.Equal("Float", types.PortTypes.Get("f32").DisplayName);
    }

    /// <summary>
    ///     Determines whether a straight segment passes through the interior of a rectangle.
    /// </summary>
    /// <param name="a">The segment start.</param>
    /// <param name="b">The segment end.</param>
    /// <param name="rect">The rectangle.</param>
    /// <returns>True if the segment enters the rectangle's interior.</returns>
    private static bool SegmentCrosses(Point a, Point b, Rect rect)
    {
        var box = Rect.FromPoints(a, b);
        return box.Left < rect.Right && box.Right > rect.Left && box.Top < rect.Bottom && box.Bottom > rect.Top;
    }

    /// <summary>
    ///     A host router that always returns the same bend points.
    /// </summary>
    private sealed class FixedRouter(params Point[] bends) : IWireRouter
    {
        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request) => bends;
    }

    /// <summary>
    ///     A node visual that draws a marker text inside the node box.
    /// </summary>
    private sealed class BadgeVisual : INodeVisual
    {
        /// <inheritdoc />
        public void Build(NodeVisualContext context) =>
            context.Add(new TextPrimitive("badge-marker", context.Layout.Box, context.Theme.HeaderTextStyle, context.Node));
    }

    /// <summary>
    ///     A text measurer that makes every character 20 units wide.
    /// </summary>
    private sealed class WideMeasurer : ITextMeasurer
    {
        /// <inheritdoc />
        public Size Measure(string text, TextStyle style) => new(text.Length * 20, 14);
    }
}
