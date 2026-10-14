namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for <see cref="SceneBuilder"/>: primitives for nodes, wires and annotations, highlights, culling and custom visuals.
/// </summary>
public class SceneBuilderTests
{
    /// <summary>
    ///     Validates that an empty page yields only the background grid and empty content bounds.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_EmptyPage_ContainsOnlyGrid()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();

        // Act
        var scene = SceneBuilder.Build(page);

        // Assert
        var grid = Assert.IsType<GridPrimitive>(Assert.Single(scene.Primitives));
        Assert.Equal(DiagramTheme.Classic.GridSize, grid.Spacing);
        Assert.Equal(DiagramTheme.Classic.GridDotColor, grid.Color);
        Assert.True(scene.Bounds.IsEmpty);
        Assert.Same(page, scene.Page);
    }

    /// <summary>
    ///     Validates that the grid is omitted when ShowGrid is false and sits at the back otherwise.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ShowGrid_ControlsGridPrimitive()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        page.AddNode("acme.Producer", 30, 60, "P");

        // Act
        var withGrid = SceneBuilder.Build(page);
        var withoutGrid = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: grid first and covering the content; absent when disabled
        var grid = Assert.IsType<GridPrimitive>(withGrid.Primitives[0]);
        Assert.True(grid.Rect.Contains(withGrid.Bounds));
        Assert.Empty(withoutGrid.Primitives.OfType<GridPrimitive>());
    }

    /// <summary>
    ///     Validates the primitives the standard visual emits for a node: box, title, name, summary, port triangle and label.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StandardNode_EmitsBoxTitleNameSummaryAndPorts()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 30, 60, "P");
        var summaryCount = node.Type.GetSummaryLines(node).Count;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: one box, one triangle, texts = title + name + summary lines + one port label
        Assert.All(scene.Primitives, p => Assert.Same(node, p.Element));
        var box = Assert.Single(scene.Primitives.OfType<RectPrimitive>());
        Assert.Equal(node.Bounds, box.Rect);
        Assert.Equal(DiagramTheme.Classic.NodeFill, box.Fill);
        Assert.Single(scene.Primitives.OfType<PathPrimitive>());
        var texts = scene.Primitives.OfType<TextPrimitive>().Select(t => t.Text).ToList();
        Assert.Equal(3 + summaryCount, texts.Count);
        Assert.Contains("PRODUCER", texts);
        Assert.Contains("P", texts);
        Assert.Contains("value", texts);
        Assert.Contains("Format: F32", texts);
    }

    /// <summary>
    ///     Validates that port triangles take the color of their port type and point right.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_PortTriangle_UsesPortTypeColor()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Iconic", 30, 60, "I");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: both ports are i32 (blue) and each triangle ends in a tip on its right edge
        var triangles = scene.Primitives.OfType<PathPrimitive>().ToList();
        Assert.Equal(2, triangles.Count);
        Assert.All(triangles, t => Assert.Equal(Color.Parse("#1E90FF"), t.Fill));
        Assert.All(triangles, t => Assert.Equal(PathCommandKind.Close, t.Commands[^1].Kind));
        Assert.True(scene.TryGetNodeLayout(node, out var layout));
        foreach (var port in layout.Ports)
        {
            var tip = triangles.Select(t => t.Commands[1].End).Single(p => Math.Abs(p.X - port.Triangle.Right) < 1e-9);
            Assert.Equal(port.Anchor.Y, tip.Y, 6);
        }
    }

    /// <summary>
    ///     Validates that the node icon is emitted into the layout's icon rectangle.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeWithIcon_EmitsIconPrimitive()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Iconic", 30, 60, "I");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var icon = Assert.Single(scene.Primitives.OfType<IconPrimitive>());
        Assert.Same(node.Type.Icon, icon.Icon);
        Assert.Equal(TryLayout(scene, node)!.IconBounds, icon.Rect);
        Assert.Same(node, icon.Element);
    }

    /// <summary>
    ///     Validates that a straight wire is a stroked two-point path from the source anchor to the target anchor.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StraightWire_IsStrokedPathBetweenAnchors()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: the stroked line runs from the source anchor to just inside the arrowhead at the target anchor
        var path = StrokedPath(scene, wire);
        Assert.Equal(diagram.Theme.WireColor, path.Stroke);
        Assert.Equal(diagram.Theme.WireThickness, path.StrokeThickness);
        Assert.Null(path.Fill);
        Assert.Equal(2, path.Commands.Count);
        Assert.Equal(PathCommandKind.MoveTo, path.Commands[0].Kind);
        Assert.Equal(producer.Port("value").Anchor, path.Commands[0].End);
        Assert.Equal(PathCommandKind.LineTo, path.Commands[1].Kind);
        var target = consumer.Port("in").Anchor;
        Assert.Equal(new Point(target.X - (diagram.Theme.WireArrowLength * 0.75), target.Y), path.Commands[1].End);
    }

    /// <summary>
    ///     Validates that wires end in a filled arrowhead whose tip is the target anchor and which points along the last segment.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Wire_EndsInFilledArrowheadAtTheTarget()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var theme = diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var arrow = SceneTestModel.For<PathPrimitive>(scene, wire).Single(p => p.Fill is not null);
        var tip = consumer.Port("in").Anchor;
        Assert.Equal(theme.WireColor, arrow.Fill);
        Assert.Null(arrow.Stroke);
        Assert.Equal(PathCommandKind.Close, arrow.Commands[^1].Kind);
        Assert.Equal(tip, arrow.Commands[0].End);
        Assert.Equal(tip.X - theme.WireArrowLength, arrow.Commands[1].End.X, 6);
        Assert.Equal(tip.X - theme.WireArrowLength, arrow.Commands[2].End.X, 6);
        Assert.Equal(theme.WireArrowWidth, Math.Abs(arrow.Commands[1].End.Y - arrow.Commands[2].End.Y), 6);
    }

    /// <summary>
    ///     Validates that the arrowhead can be switched off in the theme, leaving the plain line to the anchor.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_WireArrowheadsDisabled_DrawsOnlyTheLine()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var theme = page.Diagram.Theme with { WireArrowheads = false };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Theme = theme });

        // Assert
        var path = Assert.Single(SceneTestModel.For<PathPrimitive>(scene, wire));
        Assert.Equal(consumer.Port("in").Anchor, path.Commands[^1].End);
    }

    /// <summary>
    ///     Validates that bends in a wire are drawn as rounded corners using cubic curves.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_WireWithBends_RoundsCornersWithCubics()
    {
        // Arrange: a wire with two right-angle bends
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 100, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(150, 45), new Point(150, 145)]);

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: MoveTo, then line + cubic per corner, then the final line
        var path = StrokedPath(scene, wire);
        Assert.Equal(2, path.Commands.Count(c => c.Kind == PathCommandKind.CubicTo));
        Assert.Equal(6, path.Commands.Count);
        var radius = diagram.Theme.WireCornerRadius;

        // The first curve starts one radius before the first bend and ends one radius after it
        Assert.Equal(new Point(150 - radius, 45), path.Commands[1].End);
        Assert.Equal(new Point(150, 45 + radius), path.Commands[2].End);
        var target = consumer.Port("in").Anchor;
        Assert.Equal(new Point(target.X - (diagram.Theme.WireArrowLength * 0.75), target.Y), path.Commands[^1].End);
    }

    /// <summary>
    ///     Validates that wires are drawn beneath nodes and nodes follow the page's z-order.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DrawingOrder_WiresUnderNodesInZOrder()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var primitives = scene.Primitives.ToList();
        var wireIndex = primitives.FindIndex(p => ReferenceEquals(p.Element, wire));
        var producerIndex = primitives.FindIndex(p => ReferenceEquals(p.Element, producer));
        var consumerIndex = primitives.FindIndex(p => ReferenceEquals(p.Element, consumer));
        Assert.True(wireIndex < producerIndex);
        Assert.True(producerIndex < consumerIndex);
    }

    /// <summary>
    ///     Validates that a selected node gets an outline in the theme's selection color and an unselected one does not.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SelectedNode_AddsSelectionOutline()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var selected = page.AddNode("acme.Producer", 0, 0, "P");
        var other = page.AddNode("acme.Consumer", 300, 0, "C");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [selected] });

        // Assert
        var outline = SceneTestModel.For<RectPrimitive>(scene, selected).Single(r => r.Fill is null);
        Assert.Equal(diagram.Theme.SelectionColor, outline.Stroke);
        Assert.True(outline.Rect.Contains(selected.Bounds));
        Assert.DoesNotContain(SceneTestModel.For<RectPrimitive>(scene, other), r => r.Fill is null);
    }

    /// <summary>
    ///     Validates that a selected wire is underlaid with a wider selection-colored stroke while staying on top in its own color.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SelectedWire_AddsWiderUnderlay()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [wire] });

        // Assert: underlay first (selection color, thicker), wire stroke last (wire color)
        var paths = SceneTestModel.For<PathPrimitive>(scene, wire).Where(p => p.Fill is null).ToList();
        Assert.Equal(2, paths.Count);
        Assert.Equal(diagram.Theme.SelectionColor, paths[0].Stroke);
        Assert.True(paths[0].StrokeThickness > paths[1].StrokeThickness);
        Assert.Equal(diagram.Theme.WireColor, paths[1].Stroke);
    }

    /// <summary>
    ///     Validates that hover uses the hover color and that selection takes precedence over hover.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Hover_UsesHoverColorUnlessSelected()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 0, 0, "P");

        // Act
        var hovered = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Hover = node });
        var both = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Hover = node, Selection = [node] });

        // Assert
        Assert.Equal(diagram.Theme.HoverColor, SceneTestModel.For<RectPrimitive>(hovered, node).Single(r => r.Fill is null).Stroke);
        Assert.Equal(diagram.Theme.SelectionColor, SceneTestModel.For<RectPrimitive>(both, node).Single(r => r.Fill is null).Stroke);
    }

    /// <summary>
    ///     Validates that elements outside the culling bounds are left out and the grid covers the visible area.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_VisibleBounds_CullsElementsOutsideAndSizesGrid()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var near = page.AddNode("acme.Producer", 0, 0, "Near");
        var far = page.AddNode("acme.Consumer", 2000, 2000, "Far");
        var wire = page.Connect(near.Port("value"), far.Port("in"));
        var visible = new Rect(-50, -50, 300, 300);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Bounds = visible });

        // Assert: the far node is gone, the near node stays, the long wire still crosses the area
        Assert.True(scene.TryGetNodeLayout(near, out _));
        Assert.False(scene.TryGetNodeLayout(far, out _));
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, far));
        Assert.Contains(scene.Primitives, p => ReferenceEquals(p.Element, near));
        Assert.True(scene.TryGetWirePoints(wire, out _));
        Assert.Equal(visible, scene.Primitives.OfType<GridPrimitive>().Single().Rect);
    }

    /// <summary>
    ///     Validates that a wire entirely outside the culling bounds is left out.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_VisibleBounds_CullsWiresOutside()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 1000, 1000, "A");
        var b = page.AddNode("acme.Consumer", 1300, 1000, "B");
        var wire = page.Connect(a.Port("value"), b.Port("in"));

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Bounds = new Rect(0, 0, 200, 200), ShowGrid = false });

        // Assert
        Assert.Empty(scene.Primitives);
        Assert.False(scene.TryGetWirePoints(wire, out _));
    }

    /// <summary>
    ///     Validates that an annotation is drawn with the theme fill, an explicit fill overrides it, and text is wrapped.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Annotation_UsesFillAndWrapsText()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var plain = page.AddAnnotation("alpha beta gamma delta epsilon zeta eta theta", new Rect(0, 300, 90, 200));
        var tinted = page.AddAnnotation("hi", new Rect(200, 300, 90, 60));
        tinted.Fill = Color.Parse("#AABBCC");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var plainBox = SceneTestModel.For<RectPrimitive>(scene, plain).Single();
        Assert.Equal(diagram.Theme.AnnotationFill, plainBox.Fill);
        Assert.Equal(diagram.Theme.AnnotationBorder, plainBox.Stroke);
        Assert.Equal(Color.Parse("#AABBCC"), SceneTestModel.For<RectPrimitive>(scene, tinted).Single().Fill);
        var lines = SceneTestModel.For<TextPrimitive>(scene, plain);
        Assert.True(lines.Count > 1);
        Assert.Equal("alpha beta gamma delta epsilon zeta eta theta", string.Join(' ', lines.Select(l => l.Text)));
        Assert.All(lines, l => Assert.True(plainBox.Rect.Contains(l.Rect)));
        Assert.Equal(diagram.Theme.AnnotationText, lines[0].Style.Color);
    }

    /// <summary>
    ///     Validates that text lines that do not fit an annotation's height are dropped.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_Annotation_TooSmallForText_DropsLines()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var note = page.AddAnnotation("one two three", new Rect(0, 0, 60, 14));

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Empty(SceneTestModel.For<TextPrimitive>(scene, note));
        Assert.Single(SceneTestModel.For<RectPrimitive>(scene, note));
    }

    /// <summary>
    ///     Validates that a selected annotation shows an outline and a resize handle in its corner.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SelectedAnnotation_ShowsResizeHandle()
    {
        // Arrange
        var (diagram, page) = SceneTestModel.CreatePage();
        var note = page.AddAnnotation("note", new Rect(0, 0, 100, 50));

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [note] });

        // Assert
        var handle = SceneTestModel.For<RectPrimitive>(scene, note).Single(r => r.Fill == diagram.Theme.SelectionColor);
        Assert.Equal(note.Bounds.Right, handle.Rect.Right);
        Assert.Equal(note.Bounds.Bottom, handle.Rect.Bottom);
        Assert.Equal(diagram.Theme.ResizeHandleSize, handle.Rect.Width);
    }

    /// <summary>
    ///     Validates that a theme in the options replaces the diagram's theme for appearance.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ThemeOverride_ChangesColors()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var red = Color.Parse("#FF0000");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Theme = new DiagramTheme { NodeFill = red } });

        // Assert
        Assert.Equal(red, SceneTestModel.For<RectPrimitive>(scene, node).Single().Fill);
        Assert.Equal(red, scene.Theme.NodeFill);
    }

    /// <summary>
    ///     Validates that a custom text measurer changes label geometry but not port anchors.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_TextMeasurerOption_IsUsedForLayout()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Producer", 0, 0, "Name");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, TextMeasurer = new FixedTextMeasurer() });

        // Assert: "Name" is four characters of ten units each
        var name = SceneTestModel.For<TextPrimitive>(scene, node).Single(t => t.Text == "Name");
        Assert.Equal(40, name.Rect.Width);
    }

    /// <summary>
    ///     Validates that a node type's custom visual replaces the standard template and its primitives are attributed to the node.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CustomVisual_ReplacesStandardTemplate()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Custom", 30, 60, "X");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: exactly the visual's two primitives, attributed to the node
        Assert.Equal(2, scene.Primitives.Count);
        Assert.All(scene.Primitives, p => Assert.Same(node, p.Element));
        Assert.Equal(Color.Parse("#123456"), Assert.IsType<RectPrimitive>(scene.Primitives[0]).Fill);
        Assert.Equal("custom", Assert.IsType<TextPrimitive>(scene.Primitives[1]).Text);

        // Hit testing keeps using the node layout, so the port is still reachable
        var anchor = node.Port("in").Anchor;
        Assert.Equal(HitKind.Port, scene.HitTest(anchor).Kind);
    }

    /// <summary>
    ///     Validates that a visual extending the standard one keeps the standard primitives and adds its own.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_DecoratingVisual_KeepsStandardPrimitivesAndAddsOwn()
    {
        // Arrange
        var (_, page) = SceneTestModel.CreatePage();
        var node = page.AddNode("acme.Decorated", 30, 60, "D");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var rects = SceneTestModel.For<RectPrimitive>(scene, node);
        Assert.Equal(2, rects.Count);
        Assert.Equal(DiagramTheme.Classic.NodeFill, rects[0].Fill);
        Assert.Equal(Color.Parse("#FF00FF"), rects[1].Stroke);
        Assert.Contains(SceneTestModel.For<TextPrimitive>(scene, node), t => t.Text == node.Type.Title);
    }

    /// <summary>
    ///     Validates that building with a null page throws.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NullPage_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => SceneBuilder.Build(null!));
    }

    /// <summary>
    ///     Validates that rounded paths shrink the corner radius at short segments and leave straight runs alone.
    /// </summary>
    [Fact]
    public void WirePaths_Rounded_ShrinksRadiusOnShortSegmentsAndSkipsStraightRuns()
    {
        // Arrange: a corner with a 4 unit leg, then a collinear point
        Point[] points = [new Point(0, 0), new Point(100, 0), new Point(100, 4), new Point(100, 50)];

        // Act
        var commands = WirePaths.Rounded(points, 6);

        // Assert: the first corner radius is limited to half of the 4 unit leg; the collinear point adds a plain line
        Assert.Equal(PathCommandKind.MoveTo, commands[0].Kind);
        Assert.Equal(new Point(98, 0), commands[1].End);
        Assert.Equal(PathCommandKind.CubicTo, commands[2].Kind);
        Assert.Equal(new Point(100, 2), commands[2].End);
        Assert.Equal(new Point(100, 50), commands[^1].End);
        Assert.Empty(WirePaths.Rounded([], 6));
    }

    /// <summary>
    ///     Validates that a title wider than the box wraps onto several lines that stay inside the box.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_LongTitle_WrapsIntoLinesInsideTheBox()
    {
        // Arrange: with 10 units per character a 90-wide box fits about eight characters per line
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Long") { Title = "WRAP THIS TITLE" });
        var page = new Diagram(types).Pages.Add("P");
        var node = page.AddNode("acme.Long", 0, 0, "L");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { TextMeasurer = new FixedTextMeasurer(), ShowGrid = false });

        // Assert: the header text style is the only white text; the lines carry the whole title and never grow the font
        var titleLines = SceneTestModel.For<TextPrimitive>(scene, node).Where(t => t.Style.Color == Color.White).ToList();
        Assert.True(titleLines.Count >= 2);
        Assert.All(titleLines, line => Assert.True(line.Rect.Width <= node.Bounds.Width));
        Assert.All(titleLines, line => Assert.True(line.Style.FontSize <= scene.Theme.HeaderFontSize));
        Assert.Equal("WRAP THIS TITLE", string.Join(" ", titleLines.Select(t => t.Text)));
    }

    /// <summary>
    ///     Validates that building a scene changes neither the model nor the undo history, even with selection, hover and a collapsed group.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_WithCollapsedGroupAndHighlights_DoesNotModifyModelOrHistory()
    {
        // Arrange: A -> B -> C with B collapsed (its wires get transient display routes)
        var (diagram, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 200, 0, "B");
        var c = page.AddNode("acme.Consumer", 400, 0, "C");
        var ab = page.Connect(a.Port("value"), b.Port("in"));
        page.Connect(b.Port("out"), c.Port("in"));
        page.CreateGroup([b], "Grp").IsCollapsed = true;
        var before = TestModel.Dump(diagram);
        var undoCount = diagram.History.UndoCount;
        var routeBefore = ab.Route.ToArray();

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { Selection = [a, ab], Hover = c });

        // Assert: the scene shows the wire to the proxy port, but the stored model and history are untouched
        Assert.True(scene.TryGetWirePoints(ab, out var shown));
        Assert.NotEqual(ab.GetPoints(), shown);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoCount, diagram.History.UndoCount);
        Assert.Equal(routeBefore, ab.Route);
    }

    /// <summary>
    ///     Gets the layout of a node from a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The layout, or null when the node was not built.</returns>
    private static NodeLayout? TryLayout(DiagramScene scene, Node node) =>
        scene.TryGetNodeLayout(node, out var layout) ? layout : null;

    /// <summary>
    ///     Gets the stroked (unfilled) path of a wire, which excludes its arrowhead.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="wire">The wire.</param>
    /// <returns>The wire's line path.</returns>
    private static PathPrimitive StrokedPath(DiagramScene scene, Wire wire) =>
        Assert.Single(SceneTestModel.For<PathPrimitive>(scene, wire), p => p.Fill is null);
}
