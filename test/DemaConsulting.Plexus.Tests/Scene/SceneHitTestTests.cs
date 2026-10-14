namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="DiagramScene.HitTest"/>: every <see cref="HitKind"/>, topmost-first ordering and tolerance.
/// </summary>
public class SceneHitTestTests
{
    /// <summary>
    ///     Validates that empty canvas reports the Canvas kind with no element.
    /// </summary>
    [Fact]
    public void Scene_HitTest_EmptyCanvas_ReturnsCanvas()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        page.AddNode("acme.Producer", 30, 60, "P");
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(new Point(1000, 1000));

        // Assert
        Assert.Equal(HitKind.Canvas, hit.Kind);
        Assert.Null(hit.Element);
        Assert.False(hit.IsHit);
        Assert.Equal(-1, hit.SegmentIndex);
    }

    /// <summary>
    ///     Validates that the node body reports the Node kind.
    /// </summary>
    [Fact]
    public void Scene_HitTest_NodeBody_ReturnsNode()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 30, 60, "P");
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(new Point(75, 105));

        // Assert
        Assert.Equal(HitKind.Node, hit.Kind);
        Assert.Same(node, hit.Element);
        Assert.Same(node, hit.Node);
        Assert.Null(hit.Port);
    }

    /// <summary>
    ///     Validates that the name drawn above a node reports NodeName.
    /// </summary>
    [Fact]
    public void Scene_HitTest_NodeName_ReturnsNodeName()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 30, 60, "P");
        var scene = SceneBuilder.Build(page);
        Assert.True(scene.TryGetNodeLayout(node, out var layout));

        // Act
        var hit = scene.HitTest(layout.Name.Bounds.Center);

        // Assert
        Assert.Equal(HitKind.NodeName, hit.Kind);
        Assert.Same(node, hit.Node);
    }

    /// <summary>
    ///     Validates that a port anchor reports the Port kind with the model port, for inputs and outputs.
    /// </summary>
    [Fact]
    public void Scene_HitTest_PortAnchor_ReturnsPort()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Gain", 30, 60, "G");
        var scene = SceneBuilder.Build(page);

        // Act
        var input = scene.HitTest(node.Port("in").Anchor);
        var output = scene.HitTest(node.Port("out").Anchor);

        // Assert
        Assert.Equal(HitKind.Port, input.Kind);
        Assert.Same(node.Port("in"), input.Port);
        Assert.Equal(HitKind.Port, output.Kind);
        Assert.Same(node.Port("out"), output.Port);
        Assert.Same(node, output.Node);
    }

    /// <summary>
    ///     Validates that a wire reports the Wire kind with the index of the segment that was hit.
    /// </summary>
    [Fact]
    public void Scene_HitTest_WireSegments_ReturnWireWithSegmentIndex()
    {
        // Arrange: wire points (source anchor) (150,45) (150,145) (target anchor)
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 100, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, 45), new Point(150, 145)]);
        var scene = SceneBuilder.Build(page);

        // Act
        var points = wire.GetPoints();
        var first = scene.HitTest(new Point((points[0].X + 150) / 2, 45));
        var second = scene.HitTest(new Point(150, 95));
        var third = scene.HitTest(new Point((150 + points[3].X) / 2, 145));

        // Assert
        Assert.Equal((HitKind.Wire, 0), (first.Kind, first.SegmentIndex));
        Assert.Equal((HitKind.Wire, 1), (second.Kind, second.SegmentIndex));
        Assert.Equal((HitKind.Wire, 2), (third.Kind, third.SegmentIndex));
        Assert.Same(wire, first.Element);

        // The segment index counts the full polyline, so the helper resolves it to the right end points
        Assert.Equal((points[0], points[1]), first.GetWireSegment());
        Assert.Equal((points[2], points[3]), third.GetWireSegment());
        Assert.Equal((points[1], points[2]), second.GetWireSegment(points));
        Assert.Null(HitResult.Canvas.GetWireSegment());
    }

    /// <summary>
    ///     Validates that wire hits honor the tolerance: near points hit, distant points miss.
    /// </summary>
    [Fact]
    public void Scene_HitTest_WireTolerance_NearHitsFarMisses()
    {
        // Arrange: a straight horizontal wire at y = 45
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var scene = SceneBuilder.Build(page);

        // Act / Assert: wire half thickness 1 plus default tolerance 3 reaches 4 units
        Assert.Equal(HitKind.Wire, scene.HitTest(new Point(200, 48)).Kind);
        Assert.Equal(HitKind.Canvas, scene.HitTest(new Point(200, 55)).Kind);
        Assert.Equal(HitKind.Wire, scene.HitTest(new Point(200, 55), 12).Kind);
    }

    /// <summary>
    ///     Validates that a node drawn over a wire wins the hit test.
    /// </summary>
    [Fact]
    public void Scene_HitTest_NodeOverWire_ReturnsNode()
    {
        // Arrange: the wire runs straight through the middle node's box
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");
        var middle = page.AddNode("acme.Gain", 150, 0, "M");
        page.Connect(producer.Port("value"), consumer.Port("in"), []);
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(new Point(195, 45));

        // Assert
        Assert.Equal(HitKind.Node, hit.Kind);
        Assert.Same(middle, hit.Element);
    }

    /// <summary>
    ///     Validates that of two overlapping nodes the later one in z-order is hit.
    /// </summary>
    [Fact]
    public void Scene_HitTest_OverlappingNodes_ReturnsTopmost()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        page.AddNode("acme.Consumer", 500, 500, "Lower");
        var upper = page.AddNode("acme.Consumer", 530, 520, "Upper");
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(new Point(560, 550));

        // Assert
        Assert.Same(upper, hit.Element);
    }

    /// <summary>
    ///     Validates that an annotation reports Annotation, and its corner handle only when the annotation is selected.
    /// </summary>
    [Fact]
    public void Scene_HitTest_Annotation_ReturnsAnnotationAndHandleWhenSelected()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var note = page.AddAnnotation("note", new Rect(30, 300, 120, 60));
        var corner = new Point(note.Bounds.Right - 3, note.Bounds.Bottom - 3);

        // Act
        var plain = SceneBuilder.Build(page);
        var selected = SceneBuilder.Build(page, new SceneOptions { Selection = [note] });

        // Assert
        Assert.Equal(HitKind.Annotation, plain.HitTest(new Point(60, 320)).Kind);
        Assert.Equal(HitKind.Annotation, plain.HitTest(corner).Kind);
        Assert.Equal(HitKind.AnnotationResizeHandle, selected.HitTest(corner).Kind);
        Assert.Equal(HitKind.Annotation, selected.HitTest(new Point(60, 320)).Kind);
        Assert.Same(note, selected.HitTest(corner).Element);
    }

    /// <summary>
    ///     Validates the group hits: border, title bar, toggle, and a transparent interior.
    /// </summary>
    [Fact]
    public void Scene_HitTest_ExpandedGroup_ReturnsBorderHeaderToggleAndTransparentInterior()
    {
        // Arrange: frame (285,361)-(405,490), title bar 24 high, toggle at the left of the bar
        var (_, page) = SceneTestModel.CreatePage();
        var member = page.AddNode("acme.Gain", 300, 400, "G");
        var group = page.CreateGroup([member], "Grp");
        var theme = page.Diagram.Theme;
        var frame = group.Bounds;
        var scene = SceneBuilder.Build(page);

        // Act
        var border = scene.HitTest(new Point(frame.Left + 1, frame.Top + 80));
        var header = scene.HitTest(new Point(frame.Right - 10, frame.Top + (theme.GroupTitleHeight / 2)));
        var toggle = scene.HitTest(new Point(frame.Left + 6 + (theme.GroupToggleSize / 2), frame.Top + (theme.GroupTitleHeight / 2)));
        var interior = scene.HitTest(new Point(frame.Left + 7, frame.Top + 50));

        // Assert
        Assert.Equal(HitKind.Group, border.Kind);
        Assert.Same(group, border.Element);
        Assert.Equal(HitKind.GroupHeader, header.Kind);
        Assert.Equal(HitKind.GroupCollapseToggle, toggle.Kind);
        Assert.Same(group, toggle.Element);
        Assert.Equal(HitKind.Canvas, interior.Kind);
    }

    /// <summary>
    ///     Validates that a node inside an expanded group is hit before the group.
    /// </summary>
    [Fact]
    public void Scene_HitTest_NodeInsideGroup_ReturnsNode()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var member = page.AddNode("acme.Gain", 300, 400, "G");
        page.CreateGroup([member], "Grp");
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(member.Bounds.Center);

        // Assert
        Assert.Same(member, hit.Element);
        Assert.Equal(HitKind.Node, hit.Kind);
    }

    /// <summary>
    ///     Validates that a collapsed group reports CollapsedGroup over its box and GroupCollapseToggle over its toggle.
    /// </summary>
    [Fact]
    public void Scene_HitTest_CollapsedGroup_ReturnsCollapsedGroupAndToggle()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var member = page.AddNode("acme.Gain", 300, 400, "G");
        var group = page.CreateGroup([member], "Grp");
        group.IsCollapsed = true;
        var theme = page.Diagram.Theme;
        var box = group.Bounds;
        var scene = SceneBuilder.Build(page);

        // Act
        var body = scene.HitTest(new Point(box.Right - 10, box.Bottom - 5));
        var toggle = scene.HitTest(new Point(box.Left + 6 + (theme.GroupToggleSize / 2), box.Top + (theme.GroupTitleHeight / 2)));
        var hiddenMember = scene.HitTest(member.Bounds.Center);

        // Assert
        Assert.Equal(HitKind.CollapsedGroup, body.Kind);
        Assert.Same(group, body.Element);
        Assert.Equal(HitKind.GroupCollapseToggle, toggle.Kind);
        Assert.NotSame(member, hiddenMember.Element);
    }

    /// <summary>
    ///     Validates that wires hidden inside a collapsed group cannot be hit.
    /// </summary>
    [Fact]
    public void Scene_HitTest_HiddenWire_IsNotHit()
    {
        // Arrange: A -> B both inside the collapsed group, wire runs across the gap between them
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Consumer", 600, 0, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var group = page.CreateGroup([a, b], "Grp");
        group.IsCollapsed = true;
        var scene = SceneBuilder.Build(page);

        // Act
        var hit = scene.HitTest(new Point(300, 45));

        // Assert
        Assert.NotSame(wire, hit.Element);
        Assert.Equal(HitKind.Canvas, hit.Kind);
    }

    /// <summary>
    ///     Validates that hit testing uses the same geometry that was drawn, by hitting the center of every rectangle primitive of a node.
    /// </summary>
    [Fact]
    public void Scene_HitTest_UsesRenderedGeometry()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Iconic", 30, 60, "I");
        var scene = SceneBuilder.Build(page);
        var box = SceneTestModel.For<RectPrimitive>(scene, node).Single().Rect;
        var icon = SceneTestModel.For<IconPrimitive>(scene, node).Single();

        // Act
        var onBox = scene.HitTest(box.Center);
        var onIcon = scene.HitTest(icon.Rect.Center);

        // Assert: the drawn box and its icon are both "the node"
        Assert.Same(node, onBox.Element);
        Assert.Same(node, onIcon.Element);
        Assert.Equal(HitKind.Node, onIcon.Kind);
    }

    /// <summary>
    ///     Validates that the content bounds cover node boxes and their labels.
    /// </summary>
    [Fact]
    public void Scene_Bounds_CoverNodeLayouts()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 30, 60, "A");
        var b = page.AddNode("acme.Consumer", 500, 400, "B");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(scene.TryGetNodeLayout(a, out var layoutA));
        Assert.True(scene.TryGetNodeLayout(b, out var layoutB));
        Assert.True(scene.Bounds.Contains(layoutA.Bounds));
        Assert.True(scene.Bounds.Contains(layoutB.Bounds));
    }

    /// <summary>
    ///     Validates that the displayed wire points are the stored route plus both anchors.
    /// </summary>
    [Fact]
    public void Scene_TryGetWirePoints_VisibleWire_ReturnsModelPoints()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 100, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.Equal(wire.GetPoints(), points);
    }

    /// <summary>
    ///     Validates that hit testing stays quick and correct on a page of a thousand nodes with thousands of queries.
    /// </summary>
    [Fact]
    public void Scene_HitTest_ThousandNodes_ManyQueriesAreQuickAndCorrect()
    {
        // Arrange: a 40 x 25 lattice of nodes at pitch 150
        var (_, page) = SceneTestModel.CreatePage();
        var nodes = new List<Node>();
        for (var i = 0; i < 1000; i++)
        {
            nodes.Add(page.AddNode("acme.Gain", i % 40 * 150, i / 40 * 150, null));
        }

        var scene = SceneBuilder.Build(page);

        // Act: hit the body center of every node, five times over
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var wrong = 0;
        for (var pass = 0; pass < 5; pass++)
        {
            foreach (var node in nodes)
            {
                var hit = scene.HitTest(node.Bounds.Center);
                if (!ReferenceEquals(hit.Element, node))
                {
                    wrong++;
                }
            }
        }

        watch.Stop();

        // Assert: generous bound (5000 queries; typically a few milliseconds in total)
        Assert.Equal(0, wrong);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"5000 hit tests took {watch.ElapsedMilliseconds} ms");
    }
}
