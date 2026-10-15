namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Subsystem tests for Scene: theme, layout, text measurement, scene building and hit testing working together.
/// </summary>
public class SceneTests
{
    /// <summary>
    ///     Validates that a page becomes a list of primitives of the closed set of kinds, with the grid first, and no painter or font is needed.
    /// </summary>
    [Fact]
    public void Scene_BuildPage_ProducesPrimitivesOfTheClosedKindsWithGridFirst()
    {
        // Arrange: an iconic node, a wire and an annotation cover every primitive kind
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Iconic", 0, 0, "A");
        var b = page.AddNode("acme.Iconic", 400, 0, "B");
        page.Connect(a.Port("out"), b.Port("in"));
        page.AddAnnotation("note", new Rect(0, 300, 200, 60));

        // Act
        var scene = SceneBuilder.Build(page);

        // Assert
        Assert.IsType<GridPrimitive>(scene.Primitives[0]);
        Assert.All(scene.Primitives, p => Assert.True(p is RectPrimitive or PathPrimitive or TextPrimitive or IconPrimitive or GridPrimitive));
        Assert.Contains(scene.Primitives, p => p is RectPrimitive);
        Assert.Contains(scene.Primitives, p => p is PathPrimitive);
        Assert.Contains(scene.Primitives, p => p is TextPrimitive);
        Assert.Contains(scene.Primitives, p => p is IconPrimitive);
    }

    /// <summary>
    ///     Validates the drawing order: grid, group frame, wire, then the elements in z-order.
    /// </summary>
    [Fact]
    public void Scene_DrawingOrder_GridGroupFramesWiresThenElements()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 400, 0, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var group = page.CreateGroup([a, b], "Grp");
        var note = page.AddAnnotation("note", new Rect(0, 300, 200, 60));

        // Act
        var scene = SceneBuilder.Build(page);

        // Assert
        var primitives = scene.Primitives.ToList();
        var grid = primitives.FindIndex(p => p is GridPrimitive);
        var frame = primitives.FindIndex(p => ReferenceEquals(p.Element, group));
        var wireLine = primitives.FindIndex(p => ReferenceEquals(p.Element, wire));
        var nodeA = primitives.FindIndex(p => ReferenceEquals(p.Element, a));
        var nodeB = primitives.FindIndex(p => ReferenceEquals(p.Element, b));
        var annotation = primitives.FindIndex(p => ReferenceEquals(p.Element, note));
        Assert.Equal(0, grid);
        Assert.True(grid < frame);
        Assert.True(frame < wireLine);
        Assert.True(wireLine < nodeA);
        Assert.True(nodeA < nodeB);
        Assert.True(nodeB < annotation);
    }

    /// <summary>
    ///     Validates that hit testing agrees with the drawn primitives for nodes, ports, wires and annotations.
    /// </summary>
    [Fact]
    public void Scene_HitTest_AgreesWithDrawnGeometry()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 60, "A");
        var b = page.AddNode("acme.Consumer", 300, 60, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var note = page.AddAnnotation("note", new Rect(0, 300, 200, 60));
        var scene = SceneBuilder.Build(page);

        // Act / Assert: the center of every drawn box is hit as its element
        Assert.Same(a, scene.HitTest(SceneTestModel.For<RectPrimitive>(scene, a).Single().Rect.Center).Element);
        Assert.Same(b, scene.HitTest(SceneTestModel.For<RectPrimitive>(scene, b).Single().Rect.Center).Element);
        Assert.Same(note, scene.HitTest(SceneTestModel.For<RectPrimitive>(scene, note)[0].Rect.Center).Element);

        // The middle of the drawn wire line is hit as the wire
        var points = wire.GetPoints();
        var middle = new Point((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
        Assert.Same(wire, scene.HitTest(middle).Element);

        // The port anchors reported by the layout are hit as ports
        Assert.True(scene.TryGetNodeLayout(a, out var layout));
        var hit = scene.HitTest(layout.Ports[0].HitBounds.Center);
        Assert.Equal(HitKind.Port, hit.Kind);
        Assert.Same(a.Port("value"), hit.Port);
    }

    /// <summary>
    ///     Validates that port anchors and displayed wire points do not depend on the text measurer.
    /// </summary>
    [Fact]
    public void Scene_PortAnchors_AreIdenticalForAnyTextMeasurer()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Mixer", 400, 100, "B");
        var wire = page.Connect(a.Port("value"), b.Port("a"));

        // Act
        var approximate = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new ApproximateTextMeasurer() });
        var fixedWidth = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new FixedTextMeasurer() });

        // Assert: identical wire geometry, and layout anchors equal the model anchors under both measurers
        Assert.True(approximate.TryGetWirePoints(wire, out var first));
        Assert.True(fixedWidth.TryGetWirePoints(wire, out var second));
        Assert.Equal(first, second);
        foreach (var scene in new[] { approximate, fixedWidth })
        {
            Assert.True(scene.TryGetNodeLayout(b, out var layout));
            Assert.All(layout.Ports, p => Assert.Equal(p.Port.Anchor, p.Anchor));
        }
    }

    /// <summary>
    ///     Validates that a customized theme changes both the drawn wire and the hit-test reach consistently.
    /// </summary>
    [Fact]
    public void Scene_Theme_WireThickness_DrivesDrawingAndHitTesting()
    {
        // Arrange: two ports on one row give a straight wire; the probe point lies 4 units off the line
        var (diagram, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 60, "A");
        var b = page.AddNode("acme.Consumer", 300, 60, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var points = wire.GetPoints();
        var probe = new Point((points[0].X + points[1].X) / 2, points[0].Y + 4);

        // Act
        var thin = SceneBuilder.Build(page);
        diagram.Theme = DiagramTheme.Classic with { WireThickness = 10, WireColor = Color.Parse("#FF0000") };
        var thick = SceneBuilder.Build(page);

        // Assert: the thick theme is drawn thick in red and its wire is reachable from the probe, the thin one is not
        var line = Assert.Single(SceneTestModel.For<PathPrimitive>(thick, wire), p => p.Fill is null);
        Assert.Equal(10, line.StrokeThickness);
        Assert.Equal(Color.Parse("#FF0000"), line.Stroke);
        Assert.NotSame(wire, thin.HitTest(probe, 0.5).Element);
        Assert.Same(wire, thick.HitTest(probe, 0.5).Element);
    }

    /// <summary>
    ///     Validates that visible bounds leave out elements outside them from primitives, layouts and hit targets.
    /// </summary>
    [Fact]
    public void Scene_VisibleBounds_OmitOutsideElementsEverywhere()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var near = page.AddNode("acme.Producer", 0, 0, "Near");
        var far = page.AddNode("acme.Consumer", 5000, 5000, "Far");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Bounds = new Rect(-100, -100, 600, 600), ShowGrid = false });

        // Assert
        Assert.True(scene.TryGetNodeLayout(near, out _));
        Assert.False(scene.TryGetNodeLayout(far, out _));
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, far));
        Assert.Equal(HitKind.Canvas, scene.HitTest(far.Bounds.Center).Kind);
        Assert.Empty(scene.Query(far.Bounds));
    }

    /// <summary>
    ///     Validates that expanded and collapsed groups are drawn, published and hit-tested consistently, and that members disappear while collapsed.
    /// </summary>
    [Fact]
    public void Scene_Groups_ExpandedAndCollapsed_AreDrawnPublishedAndHitConsistently()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 400, 0, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var group = page.CreateGroup([b], "Grp");

        // Act: expanded
        var expanded = SceneBuilder.Build(page);
        Assert.True(expanded.TryGetGroupLayout(group, out var expandedLayout));
        var expandedToggle = expanded.HitTest(expandedLayout.CollapseToggle.Center);

        // Act: collapsed
        group.IsCollapsed = true;
        var collapsed = SceneBuilder.Build(page);
        Assert.True(collapsed.TryGetGroupLayout(group, out var collapsedLayout));
        var collapsedBody = collapsed.HitTest(collapsedLayout.Frame.Center);

        // Assert
        Assert.False(expandedLayout.IsCollapsed);
        Assert.Equal(HitKind.GroupCollapseToggle, expandedToggle.Kind);
        Assert.True(expanded.TryGetNodeLayout(b, out _));
        Assert.True(collapsedLayout.IsCollapsed);
        Assert.Equal(HitKind.CollapsedGroup, collapsedBody.Kind);
        Assert.False(collapsed.TryGetNodeLayout(b, out _));

        // The wire into the collapsed group ends at the published proxy port anchor
        Assert.True(collapsed.TryGetWirePoints(wire, out var shown));
        Assert.Equal(collapsedLayout.InputPorts.Single().Anchor, shown[^1]);
    }

    /// <summary>
    ///     Validates that building, querying and hit testing never change the model or its history.
    /// </summary>
    [Fact]
    public void Scene_BuildAndQuery_DoNotModifyTheModel()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 400, 0, "B");
        page.Connect(a.Port("value"), b.Port("in"));
        page.CreateGroup([b], "Grp").IsCollapsed = true;
        var before = TestModel.Dump(diagram);
        var undoCount = diagram.History.UndoCount;

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Selection = [a], Hover = b });
        _ = scene.HitTest(a.Bounds.Center);
        _ = scene.Query(new Rect(-1000, -1000, 5000, 5000));

        // Assert
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that a custom text measurer and a custom node visual both take part in the same scene as the standard parts.
    /// </summary>
    [Fact]
    public void Scene_CustomMeasurerAndVisual_ArePluggable()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var custom = page.AddNode("acme.Custom", 0, 0, "Cu");
        var standard = page.AddNode("acme.Producer", 300, 0, "St");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new FixedTextMeasurer(), ShowGrid = false });

        // Assert: the custom visual replaced the template of its node only, and the measurer sized the name (10 per character)
        Assert.Contains(SceneTestModel.For<TextPrimitive>(scene, custom), t => t.Text == "custom");
        Assert.DoesNotContain(SceneTestModel.For<TextPrimitive>(scene, standard), t => t.Text == "custom");
        Assert.True(scene.TryGetNodeLayout(custom, out var layout));
        Assert.Equal(20, layout.Name.Bounds.Width);

        // Hit testing keeps using the layout, so the custom node is hit like any other
        Assert.Same(custom, scene.HitTest(layout.Box.Center).Element);
    }

    /// <summary>
    ///     Validates that a theme assigned to a diagram is the one the scene is built with, and that a scene option overrides it.
    /// </summary>
    [Fact]
    public void Scene_Theme_DiagramThemeIsUsedUnlessTheOptionOverridesIt()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        diagram.Theme = DiagramTheme.Classic with { GridSize = 30 };
        var overrideTheme = DiagramTheme.Classic with { GridSize = 45 };

        // Act
        var fromDiagram = SceneBuilder.Build(page);
        var fromOption = SceneBuilder.Build(page, new SceneOptions { Theme = overrideTheme });

        // Assert
        Assert.Same(diagram.Theme, fromDiagram.Theme);
        Assert.Same(overrideTheme, fromOption.Theme);
        Assert.Equal(30, Assert.IsType<GridPrimitive>(fromDiagram.Primitives[0]).Spacing);
        Assert.Equal(45, Assert.IsType<GridPrimitive>(fromOption.Primitives[0]).Spacing);
    }

    /// <summary>
    ///     Validates that selecting an annotation draws its resize handle and makes exactly that handle hittable.
    /// </summary>
    [Fact]
    public void Scene_SelectedAnnotation_ShowsResizeHandleThatIsHittable()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var note = page.AddAnnotation("note", new Rect(100, 100, 200, 80));
        var theme = page.Diagram.Theme;
        var handleCenter = new Point(note.Bounds.Right - (theme.ResizeHandleSize / 2), note.Bounds.Bottom - (theme.ResizeHandleSize / 2));

        // Act
        var plain = SceneTestModel.BuildNoGrid(page);
        var selected = SceneBuilder.Build(page, new SceneOptions { Selection = [note], ShowGrid = false });

        // Assert: the handle exists only in the selected scene, drawn and hit-tested alike
        Assert.DoesNotContain(SceneTestModel.For<RectPrimitive>(plain, note), r => r.Fill == theme.SelectionColor);
        Assert.Contains(SceneTestModel.For<RectPrimitive>(selected, note), r => r.Fill == theme.SelectionColor);
        Assert.Equal(HitKind.Annotation, plain.HitTest(handleCenter).Kind);
        Assert.Equal(HitKind.AnnotationResizeHandle, selected.HitTest(handleCenter).Kind);
    }

    /// <summary>
    ///     Validates that a region query returns exactly the drawn primitives that intersect the region, in drawing order.
    /// </summary>
    [Fact]
    public void Scene_Query_ReturnsTheDrawnPrimitivesIntersectingTheRegionInOrder()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 400, 0, "B");
        page.Connect(a.Port("value"), b.Port("in"));
        page.AddNode("acme.Gain", 2000, 2000, "Far");
        var scene = SceneBuilder.Build(page);
        var region = new Rect(-20, -20, 500, 200);

        // Act
        var found = scene.Query(region);

        // Assert
        Assert.Equal(scene.Primitives.Where(p => p.Bounds.Intersects(region)).ToList(), found);
        Assert.DoesNotContain(found, p => p.Element is Node { Name: "Far" });
    }

    /// <summary>
    ///     Validates that the scene exposes the node layouts, displayed wire points and content bounds it was built from.
    /// </summary>
    [Fact]
    public void Scene_Lookups_ExposeLayoutsWirePointsAndContentBounds()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 400, 100, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var otherPage = page.Diagram.Pages.Add("Other");
        var stranger = otherPage.AddNode("acme.Gain", 0, 0, "S");

        // Act
        var scene = SceneBuilder.Build(page);

        // Assert
        Assert.True(scene.TryGetNodeLayout(a, out var layoutA));
        Assert.True(scene.TryGetNodeLayout(b, out var layoutB));
        Assert.False(scene.TryGetNodeLayout(stranger, out _));
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.Equal(wire.GetPoints(), points);
        Assert.True(scene.Bounds.Contains(layoutA.Bounds));
        Assert.True(scene.Bounds.Contains(layoutB.Bounds));
        Assert.Same(page, scene.Page);
    }

    /// <summary>
    ///     Validates that null arguments of the scene entry points are rejected with argument-null exceptions.
    /// </summary>
    [Fact]
    public void Scene_NullArguments_AreRejected()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        page.AddNode("acme.Producer", 0, 0, "A");
        var scene = SceneBuilder.Build(page);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SceneBuilder.Build(null!));
        Assert.Throws<ArgumentNullException>(() => NodeLayout.Compute(null!, DiagramTheme.Classic, new ApproximateTextMeasurer()));
        Assert.Throws<ArgumentNullException>(() => new ApproximateTextMeasurer().Measure(null!, DiagramTheme.Classic.HeaderTextStyle));
        Assert.Throws<ArgumentNullException>(() => scene.TryGetNodeLayout(null!, out _));
        Assert.Throws<ArgumentNullException>(() => scene.TryGetWirePoints(null!, out _));
    }

    /// <summary>
    ///     Validates that a scene of a thousand nodes with wires is built and every node is hit at its center.
    /// </summary>
    [Fact]
    public void Scene_LargePage_BuildAndHitTest_EveryNodeIsHit()
    {
        // Arrange: 40 x 25 gain nodes, each wired to its right neighbor
        var (_, page) = SceneTestModel.CreatePage();
        var nodes = new List<Node>();
        for (var i = 0; i < 1000; i++)
        {
            nodes.Add(page.AddNode("acme.Gain", i % 40 * 200, i / 40 * 150, null));
        }

        for (var i = 0; i < 1000; i++)
        {
            if (i % 40 != 39)
            {
                page.Connect(nodes[i].Port("out"), nodes[i + 1].Port("in"));
            }
        }

        // Act
        var scene = SceneBuilder.Build(page);
        var hits = nodes.Count(n => ReferenceEquals(scene.HitTest(n.Bounds.Center).Element, n));

        // Assert
        Assert.Equal(1000, hits);
    }

    /// <summary>
    ///     Validates that the scene and hit testing show a port added by a port provider after a property change.
    /// </summary>
    [Fact]
    public void Scene_DynamicPorts_PropertyChange_SceneAndHitTestShowNewPort()
    {
        // Arrange
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var bus = page.AddNode("acme.Bus", 60, 60, "B");

        // Act: a third channel appears
        bus.Properties["Channels"] = 3;
        var scene = SceneBuilder.Build(page);
        var in2 = bus.Input("in2");
        var hit = scene.HitTest(NodeGeometry.PortEdge(bus.Bounds, PortDirection.Input, in2.Index, bus.Inputs.Count, diagram.Theme) - new Point(3, 0));

        // Assert: the new port is hit at its row
        Assert.Equal(HitKind.Port, hit.Kind);
        Assert.Same(in2, hit.Port);
    }

    /// <summary>
    ///     Validates that a node's visual extent and routing obstacles follow its current ports when a port provider adds
    ///     ports.
    /// </summary>
    [Fact]
    public void Scene_DynamicPorts_ExtentAndObstacles_FollowCurrentPorts()
    {
        // Arrange: a bus with two channels
        var diagram = TestModel.CreateDynamicDiagram();
        var page = diagram.Pages.Add("Main");
        var bus = page.AddNode("acme.Bus", 60, 60, "B");
        var extentBefore = NodeGeometry.VisualBounds(bus, diagram.Theme);
        var obstaclesBefore = NodeGeometry.RoutingObstacles(bus.Bounds, bus, diagram.Theme);

        // Act: eight channels
        bus.Properties["Channels"] = 8;
        var extent = NodeGeometry.VisualBounds(bus, diagram.Theme);
        var obstacles = NodeGeometry.RoutingObstacles(bus.Bounds, bus, diagram.Theme);
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: both grew and cover every current port row, as does the drawn layout
        Assert.True(extent.Height > extentBefore.Height);
        Assert.True(obstacles.Max(o => o.Bottom) > obstaclesBefore.Max(o => o.Bottom));
        Assert.True(scene.TryGetNodeLayout(bus, out var layout));
        Assert.Equal(9, layout.Ports.Count);
        Assert.All(bus.Ports, port =>
        {
            Assert.InRange(port.Anchor.Y, extent.Top, extent.Bottom);
            Assert.Contains(obstacles, o => port.Anchor.Y >= o.Top && port.Anchor.Y <= o.Bottom);
        });
    }

    /// <summary>
    ///     Validates that a scene theme with other port metrics changes nothing about where ports and wire ends are:
    ///     the drawn wire ends at the drawn port anchors and the ports are hit there.
    /// </summary>
    [Fact]
    public void Scene_ThemeOverride_PortsStayAtWireEnds()
    {
        // Arrange: two connected nodes in a classic diagram, drawn with a theme whose port metrics differ
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 60, "A");
        var b = page.AddNode("acme.Mixer", 400, 100, "B");
        var wire = page.Connect(a.Port("value"), b.Port("b"));
        var display = DiagramTheme.Classic with { PortPitch = 40, PortLabelGap = 25, PortTriangleLength = 16, PortTriangleHeight = 16 };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Theme = display, ShowGrid = false });

        // Assert: wire ends equal the drawn anchors, which equal the model anchors, and the ports are hit there
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.True(scene.TryGetNodeLayout(a, out var source));
        Assert.True(scene.TryGetNodeLayout(b, out var target));
        var start = source.Ports.Single(p => p.Port == wire.Source);
        var end = target.Ports.Single(p => p.Port == wire.Target);
        Assert.Equal(start.Anchor, points[0]);
        Assert.Equal(end.Anchor, points[^1]);
        Assert.Equal(wire.Source.Anchor, start.Anchor);
        Assert.Equal(wire.Target.Anchor, end.Anchor);
        Assert.Same(wire.Source, scene.HitTest(start.HitBounds.Center).Port);
        Assert.Same(wire.Target, scene.HitTest(end.HitBounds.Center).Port);
        Assert.Equal(end.Anchor.Y, end.Triangle.Center.Y, 9);
    }

    /// <summary>
    ///     Validates that the deterministic visual extent of each node contains every port label the scene draws for it.
    /// </summary>
    [Fact]
    public void Scene_NodeVisualExtent_ContainsEveryDrawnPortLabel()
    {
        // Arrange: a mixer squeezed so short that its port rows stick out of the box, and a producer
        var (diagram, page) = SceneTestModel.CreatePage();
        var mixer = page.AddNode("acme.Mixer", 300, 60, "M");
        mixer.Bounds = mixer.Bounds with { Height = 20 };
        page.AddNode("acme.Producer", 0, 60, "P");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert
        Assert.All(page.Nodes, node =>
        {
            var extent = NodeGeometry.VisualBounds(node, diagram.Theme);
            Assert.True(scene.TryGetNodeLayout(node, out var layout));
            Assert.All(layout.Ports, port =>
            {
                var label = port.Label.Bounds;
                Assert.True(
                    label.Top >= extent.Top - 1e-9 && label.Bottom <= extent.Bottom + 1e-9
                        && label.Left >= extent.Left - 1e-9 && label.Right <= extent.Right + 1e-9,
                    $"{port.Port} label {label} outside {extent}");
            });
        });
    }

    /// <summary>
    ///     Validates that the display route of a wire attached to a collapsed group avoids the frame of an unrelated
    ///     group, unless the theme switches group frames off as obstacles.
    /// </summary>
    [Fact]
    public void Scene_GroupObstacles_CollapsedGroupWire_AvoidsUnrelatedFrameUnlessSwitchedOff()
    {
        foreach (var obstacles in new[] { true, false })
        {
            // Arrange: producer (inside a collapsed group) and consumer on one row, an unrelated group frame between them
            var diagram = TestModel.CreateDiagram();
            diagram.Theme = DiagramTheme.Classic with { GroupsAreObstacles = obstacles };
            var page = diagram.Pages.Add("Main");
            var producer = page.AddNode("acme.Producer", 0, 0, "P");
            var consumer = page.AddNode("acme.Consumer", 800, 0, "C");
            var group = page.CreateGroup(
                [page.AddNode("acme.Gain", 350, -250, "Above"), page.AddNode("acme.Gain", 350, 250, "Below")],
                "G");
            var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
            page.CreateGroup([producer], "Pad").IsCollapsed = true;
            var frame = group.Bounds.Inflate(diagram.Theme.ObstacleMargin);

            // Act
            var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

            // Assert
            Assert.True(scene.TryGetWirePoints(wire, out var points));
            Assert.True(RouteAssertions.IsOrthogonal(points));
            Assert.Equal(!obstacles, RouteAssertions.CrossesAny(points, [obstacles ? frame : group.Bounds]));
        }
    }
}
