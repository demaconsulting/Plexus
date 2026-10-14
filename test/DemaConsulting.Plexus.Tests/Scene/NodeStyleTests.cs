namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     A node visual for tests that draws one rectangle from the style the context resolved, like a host's own visual would.
/// </summary>
internal sealed class StyleAwareNodeVisual : INodeVisual
{
    /// <inheritdoc />
    public void Build(NodeVisualContext context)
    {
        context.Add(new RectPrimitive(context.Layout.Box, context.Fill, context.Border, context.BorderThickness, 0));
        context.Add(new TextPrimitive("styled", context.Layout.Box, context.HeaderTextStyle));
    }
}

/// <summary>
///     Unit tests for dynamic node styling: <see cref="NodeStyle"/>, <see cref="Diagram.NodeStyler"/>,
///     <see cref="NodeStylers"/>, the way <see cref="StandardNodeVisual"/> applies a style and how custom visuals can read it.
/// </summary>
public class NodeStyleTests
{
    /// <summary>
    ///     Gets a distinctive color for each style member.
    /// </summary>
    private static readonly Color Warm = Color.Parse("#FFAA33");

    /// <summary>
    ///     Validates that a diagram without a styler draws nodes from the theme and has no styler.
    /// </summary>
    [Fact]
    public void Diagram_NodeStyler_Default_IsNullAndNodesUseTheme()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var box = Box(scene, node);
        Assert.Null(diagram.NodeStyler);
        Assert.Multiple(
            () => Assert.Equal(diagram.Theme.NodeFill, box.Fill),
            () => Assert.Equal(diagram.Theme.NodeBorder, box.Stroke),
            () => Assert.Equal(diagram.Theme.NodeBorderThickness, box.StrokeThickness),
            () => Assert.Equal(diagram.Theme.NodeHeaderText, Title(scene, node).Style.Color),
            () => Assert.Null(Assert.Single(SceneTestModel.For<IconPrimitive>(scene, node)).Tint),
            () => Assert.Single(SceneTestModel.For<RectPrimitive>(scene, node)));
    }

    /// <summary>
    ///     Validates that assigning the styler raises a settings change named after it, without an undo entry.
    /// </summary>
    [Fact]
    public void Diagram_NodeStyler_Assign_RaisesChangedWithoutHistory()
    {
        // Arrange
        var (diagram, _, _) = CreateNodePage();
        var undoCount = diagram.History.UndoCount;
        var events = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => events.Add(e);

        // Act
        diagram.NodeStyler = _ => null;
        diagram.RefreshAppearance();

        // Assert
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(DiagramChangeKind.SettingsChanged, e.Kind));
        Assert.Equal(nameof(Diagram.NodeStyler), events[0].PropertyName);
        Assert.Equal("Appearance", events[1].PropertyName);
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that the style's fill, border, border thickness and title color reach the box and the title.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStyle_FillBorderThicknessAndHeaderText_AreApplied()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle
        {
            Fill = Warm,
            Border = Color.Parse("#00FF00"),
            BorderThickness = 3,
            HeaderText = Color.Parse("#0000FF"),
        };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var box = Box(scene, node);
        Assert.Multiple(
            () => Assert.Equal(Warm, box.Fill),
            () => Assert.Equal(Color.Parse("#00FF00"), box.Stroke),
            () => Assert.Equal(3, box.StrokeThickness),
            () => Assert.Equal(Color.Parse("#0000FF"), Title(scene, node).Style.Color),
            () => Assert.Equal(diagram.Theme.NodeCornerRadius, box.CornerRadius));
    }

    /// <summary>
    ///     Validates that members a style leaves null fall back to the theme, and that a null style changes nothing.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStyle_NullMembersAndNullStyle_FallBackToTheme()
    {
        // Arrange: only the fill is styled
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle { Fill = Warm };

        // Act
        var styled = Box(SceneTestModel.BuildNoGrid(page), node);
        diagram.NodeStyler = _ => null;
        var plain = Box(SceneTestModel.BuildNoGrid(page), node);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Warm, styled.Fill),
            () => Assert.Equal(diagram.Theme.NodeBorder, styled.Stroke),
            () => Assert.Equal(diagram.Theme.NodeBorderThickness, styled.StrokeThickness),
            () => Assert.Equal(diagram.Theme.NodeFill, plain.Fill));
    }

    /// <summary>
    ///     Validates that the icon tint and the opacity reach the icon primitive.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStyle_IconTintAndOpacity_ReachTheIcon()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle { IconTint = Warm, Opacity = 0.5 };

        // Act
        var icon = Assert.Single(SceneTestModel.For<IconPrimitive>(SceneTestModel.BuildNoGrid(page), node));

        // Assert
        Assert.Equal(Warm, icon.Tint);
        Assert.Equal(0.5, icon.Opacity);
    }

    /// <summary>
    ///     Validates that the opacity scales the alpha of the box, texts and port triangles but not the selection outline.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStyle_Opacity_FadesEverythingTheNodeDraws()
    {
        // Arrange: a node with ports, selected, at half opacity
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle { Opacity = 0.5 };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [node] });

        // Assert
        var rects = SceneTestModel.For<RectPrimitive>(scene, node);
        var selection = rects.Single(r => r.Fill is null);
        var texts = SceneTestModel.For<TextPrimitive>(scene, node);
        var triangles = SceneTestModel.For<PathPrimitive>(scene, node).Where(p => p.Fill is not null).ToList();
        Assert.Multiple(
            () => Assert.Equal(128, rects.Single(r => r.Fill is not null).Fill!.Value.A),
            () => Assert.Equal(128, rects.Single(r => r.Fill is not null).Stroke!.Value.A),
            () => Assert.Equal(diagram.Theme.SelectionColor, selection.Stroke),
            () => Assert.NotEmpty(texts),
            () => Assert.All(texts, t => Assert.Equal(128, t.Style.Color.A)),
            () => Assert.NotEmpty(triangles),
            () => Assert.All(triangles, p => Assert.Equal(128, p.Fill!.Value.A)));
    }

    /// <summary>
    ///     Validates that a glow adds fading rings below the box, extending beyond it, and that no glow adds nothing.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStyle_Glow_AddsFadingRingsBelowTheBox()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle { Glow = Color.Parse("#00FF00") };

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: six unfilled rings, outermost weakest, all before the box in drawing order and outside it
        var rects = SceneTestModel.For<RectPrimitive>(scene, node);
        var rings = rects.Where(r => r.Fill is null).ToList();
        var box = Box(scene, node);
        Assert.Multiple(
            () => Assert.Equal(6, rings.Count),
            () => Assert.All(rings, r => Assert.True(rects.IndexOf(r) < rects.IndexOf(box))),
            () => Assert.True(rings[0].Stroke!.Value.A > rings[^1].Stroke!.Value.A),
            () => Assert.All(rings, r => Assert.True(r.Rect.Width > box.Rect.Width)),
            () => Assert.True(scene.Bounds.Width >= rings[^1].Rect.Width));
    }

    /// <summary>
    ///     Validates that a failing styler never breaks the scene and draws the node from the theme.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NodeStylerThrows_FallsBackToTheme()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => throw new InvalidOperationException("host failure");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Equal(diagram.Theme.NodeFill, Box(scene, node).Fill);
    }

    /// <summary>
    ///     Validates that a property edit on the node changes the style on the next build, and so does undoing it.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_PropertyChangeOnNode_RestylesNode()
    {
        // Arrange: the fill follows the Enabled property
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = NodeStylers.ByProperty("Enabled", enabled => enabled is false ? new NodeStyle { Fill = Warm } : null);
        var before = Box(SceneTestModel.BuildNoGrid(page), node).Fill;

        // Act
        node.Properties["Enabled"] = false;
        var after = Box(SceneTestModel.BuildNoGrid(page), node).Fill;
        diagram.History.Undo();
        var undone = Box(SceneTestModel.BuildNoGrid(page), node).Fill;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(diagram.Theme.NodeFill, before),
            () => Assert.Equal(Warm, after),
            () => Assert.Equal(diagram.Theme.NodeFill, undone));
    }

    /// <summary>
    ///     Validates that the selection outline is still drawn, last, over a styled node.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_StyledSelectedNode_DrawsSelectionOutlineOnTop()
    {
        // Arrange
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = _ => new NodeStyle { Fill = Warm, Glow = Warm };

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [node] });

        // Assert
        var last = SceneTestModel.For<RectPrimitive>(scene, node)[^1];
        Assert.Equal(diagram.Theme.SelectionColor, last.Stroke);
        Assert.Null(last.Fill);
    }

    /// <summary>
    ///     Validates that a custom visual reads the resolved style and the theme fallbacks from its context.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CustomVisual_ReadsTheResolvedStyleFromTheContext()
    {
        // Arrange: a type with a style-aware visual, styled only with a fill and opacity
        var diagram = SceneTestModel.CreateDiagram();
        diagram.Types.Add(new NodeType("acme.StyleAware") { Visual = new StyleAwareNodeVisual(), Inputs = { new PortDefinition("in", "f32") } });
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.StyleAware", 0, 0, "S");
        var style = new NodeStyle { Fill = Warm, Opacity = 0.5 };
        diagram.NodeStyler = _ => style;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: the context exposes the style itself, the faded fill and the theme's fallbacks (faded too)
        var box = SceneTestModel.For<RectPrimitive>(scene, node).Single();
        var title = SceneTestModel.For<TextPrimitive>(scene, node).Single(t => t.Text == "styled");
        Assert.Multiple(
            () => Assert.Equal(Warm with { A = 128 }, box.Fill),
            () => Assert.Equal(diagram.Theme.NodeBorder with { A = (byte)Math.Round(diagram.Theme.NodeBorder.A * 0.5) }, box.Stroke),
            () => Assert.Equal(diagram.Theme.NodeBorderThickness, box.StrokeThickness),
            () => Assert.Equal(diagram.Theme.NodeHeaderText with { A = 128 }, title.Style.Color));
    }

    /// <summary>
    ///     Validates that a node without a styler or with a null style exposes the empty style, and the opacity helper leaves colors alone.
    /// </summary>
    [Fact]
    public void NodeVisualContext_Style_WithoutStyler_IsNoneAndFadedIsIdentity()
    {
        // Arrange
        var (_, page, node) = CreateNodePage();
        NodeVisualContext? captured = null;
        var visualType = new NodeType("acme.Probe") { Visual = new ProbeVisual(c => captured = c) };
        page.Diagram.Types.Add(visualType);
        page.AddNode("acme.Probe", 0, 100, "Probe");

        // Act
        SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.NotNull(captured);
        Assert.Same(NodeStyle.None, captured.Style);
        Assert.Equal(Warm, captured.Faded(Warm));
        Assert.Equal(captured.Theme.HeaderTextStyle, captured.HeaderTextStyle);
        Assert.Throws<ArgumentNullException>(() => captured.Faded(null!));
        Assert.NotNull(node);
    }

    /// <summary>
    ///     Validates that the style rejects invalid thickness and opacity and combines with overrides winning where set.
    /// </summary>
    [Fact]
    public void NodeStyle_ValidationAndWithOverrides_BehaveAsDocumented()
    {
        // Arrange
        var baseStyle = new NodeStyle { Fill = Warm, Border = Color.Red, BorderThickness = 2, Opacity = 0.8 };
        var overrides = new NodeStyle { Fill = Color.Blue, IconTint = Color.White, Glow = Color.Green, HeaderText = Color.Black };

        // Act
        var combined = baseStyle.WithOverrides(overrides);

        // Assert
        Assert.Multiple(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { BorderThickness = 0 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { BorderThickness = -1 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { BorderThickness = double.NaN }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { Opacity = 1.01 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { Opacity = -0.1 }),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new NodeStyle { Opacity = double.PositiveInfinity }),
            () => Assert.Equal(0, new NodeStyle { Opacity = 0 }.Opacity),
            () => Assert.Equal(Color.Blue, combined.Fill),
            () => Assert.Equal(Color.Red, combined.Border),
            () => Assert.Equal(2, combined.BorderThickness),
            () => Assert.Equal(Color.White, combined.IconTint),
            () => Assert.Equal(Color.Green, combined.Glow),
            () => Assert.Equal(Color.Black, combined.HeaderText),
            () => Assert.Equal(0.8, combined.Opacity),
            () => Assert.Same(baseStyle, baseStyle.WithOverrides(null)));
    }

    /// <summary>
    ///     Validates that the icon primitive validates its opacity and defaults to an opaque icon without a tint.
    /// </summary>
    [Fact]
    public void IconPrimitive_TintAndOpacity_DefaultAndValidate()
    {
        // Arrange
        var icon = IconSource.FromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"/>");
        var rect = new Rect(0, 0, 10, 10);

        // Act
        var plain = new IconPrimitive(icon, rect);
        var tinted = new IconPrimitive(icon, rect, null, Warm, 0.25);

        // Assert
        Assert.Multiple(
            () => Assert.Null(plain.Tint),
            () => Assert.Equal(1, plain.Opacity),
            () => Assert.Equal(Warm, tinted.Tint),
            () => Assert.Equal(0.25, tinted.Opacity),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new IconPrimitive(icon, rect, null, null, 1.5)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new IconPrimitive(icon, rect, null, null, double.NaN)));
    }

    /// <summary>
    ///     Validates that the property styler maps the value (or null for a missing property) and the condition styler matches by predicate.
    /// </summary>
    [Fact]
    public void NodeStylers_ByPropertyAndWhen_MapNodes()
    {
        // Arrange
        var (_, page, producer) = CreateNodePage();
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        var byProperty = NodeStylers.ByProperty("Mode", mode => mode switch { "B" => new NodeStyle { Fill = Warm }, null => new NodeStyle { Fill = Color.Red }, _ => null });
        var byName = NodeStylers.When(node => node.Name == "C", new NodeStyle { Fill = Color.Blue });

        // Act / Assert
        Assert.Multiple(
            () => Assert.Null(byProperty(producer)),
            () => Assert.Equal(Color.Red, byProperty(consumer)!.Fill),
            () => Assert.Equal(Color.Blue, byName(consumer)!.Fill),
            () => Assert.Null(byName(producer)));
        producer.Properties["Mode"] = "B";
        Assert.Equal(Warm, byProperty(producer)!.Fill);
    }

    /// <summary>
    ///     Validates that a combined styler lets later stylers override only the members they set, and returns null when nothing styled.
    /// </summary>
    [Fact]
    public void NodeStylers_Combine_LaterStylersOverrideEarlierNonNullMembers()
    {
        // Arrange
        var (_, _, node) = CreateNodePage();
        var combined = NodeStylers.Combine(
            _ => new NodeStyle { Fill = Warm, Border = Color.Red },
            _ => null,
            _ => new NodeStyle { Fill = Color.Blue, Glow = Color.Green });

        // Act
        var style = combined(node)!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Color.Blue, style.Fill),
            () => Assert.Equal(Color.Red, style.Border),
            () => Assert.Equal(Color.Green, style.Glow),
            () => Assert.Null(NodeStylers.Combine(_ => null, _ => null)(node)),
            () => Assert.Null(NodeStylers.Combine()(node)));
    }

    /// <summary>
    ///     Validates that the helpers reject null arguments.
    /// </summary>
    [Fact]
    public void NodeStylers_NullArguments_Throw() =>
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.ByProperty(null!, _ => null)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.ByProperty("p", null!)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.When(null!, NodeStyle.None)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.When(_ => true, null!)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.Combine(null!)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.Combine(_ => null, null!)),
            () => Assert.Throws<ArgumentNullException>(() => NodeStylers.PerPage(null!)));

    /// <summary>
    ///     Validates that the per-page factory runs once per page and version, not once per node, and again after a change.
    /// </summary>
    [Fact]
    public void NodeStylers_PerPage_BuildsOncePerPageAndVersion()
    {
        // Arrange: two nodes on one page and a factory that counts its calls
        var (diagram, page, _) = CreateNodePage();
        page.AddNode("acme.Consumer", 300, 0, "C");
        var factoryCalls = 0;
        var level = 1;
        diagram.NodeStyler = NodeStylers.PerPage(p =>
        {
            factoryCalls++;
            var captured = level;
            return node => new NodeStyle { BorderThickness = captured };
        });

        // Act: one scene build styles both nodes from one factory call; a second build reuses it
        var first = SceneTestModel.BuildNoGrid(page);
        var second = SceneTestModel.BuildNoGrid(page);
        var callsAfterTwoBuilds = factoryCalls;
        level = 5;
        diagram.RefreshAppearance();
        var third = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, callsAfterTwoBuilds),
            () => Assert.All(page.Nodes, n => Assert.Equal(1, Box(first, n).StrokeThickness)),
            () => Assert.All(page.Nodes, n => Assert.Equal(1, Box(second, n).StrokeThickness)),
            () => Assert.All(page.Nodes, n => Assert.Equal(5, Box(third, n).StrokeThickness)),
            () => Assert.Equal(2, factoryCalls));
    }

    /// <summary>
    ///     Validates that a property edit, undo and redo each invalidate the per-page styler, so lamps could never be stale.
    /// </summary>
    [Fact]
    public void NodeStylers_PerPage_FollowsPropertyEditsUndoAndRedo()
    {
        // Arrange: the style depends on the first node's Enabled property, read once per page and version
        var (diagram, page, node) = CreateNodePage();
        diagram.NodeStyler = NodeStylers.PerPage(p =>
        {
            var enabled = p.Nodes[0].Properties.Get<bool>("Enabled");
            return n => new NodeStyle { Fill = enabled ? Warm : Color.Blue };
        });

        // Act
        var initial = Box(SceneTestModel.BuildNoGrid(page), node).Fill;
        node.Properties["Enabled"] = false;
        var edited = Box(SceneTestModel.BuildNoGrid(page), node).Fill;
        diagram.History.Undo();
        var undone = Box(SceneTestModel.BuildNoGrid(page), node).Fill;
        diagram.History.Redo();
        var redone = Box(SceneTestModel.BuildNoGrid(page), node).Fill;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Warm, initial),
            () => Assert.Equal(Color.Blue, edited),
            () => Assert.Equal(Warm, undone),
            () => Assert.Equal(Color.Blue, redone));
    }

    /// <summary>
    ///     Validates that each page gets its own styler, that a failing factory falls back and is retried, that a null styler styles nothing and that a detached node is not styled.
    /// </summary>
    [Fact]
    public void NodeStylers_PerPage_KeepsPagesApartAndHandlesFailuresAndDetachedNodes()
    {
        // Arrange: a node on each of two pages, styled by the page name's length
        var diagram = TestModel.CreateDiagram();
        var shortPage = diagram.Pages.Add("Ab");
        var longPage = diagram.Pages.Add("Abcdef");
        var shortNode = shortPage.AddNode("acme.Gain", 0, 0, "A");
        var longNode = longPage.AddNode("acme.Gain", 0, 0, "A2");
        var perPage = NodeStylers.PerPage(page => node => new NodeStyle { BorderThickness = page.Name.Length });

        // Act / Assert: per-page thickness
        Assert.Equal(2, perPage(shortNode)!.BorderThickness);
        Assert.Equal(6, perPage(longNode)!.BorderThickness);

        // A failing factory throws to the caller (the diagram absorbs it) and is retried; a null styler styles nothing for the version
        var attempts = 0;
        var flaky = NodeStylers.PerPage(page => ++attempts == 1 ? throw new InvalidOperationException("host failure") : null!);
        Assert.Throws<InvalidOperationException>(() => flaky(shortNode));
        Assert.Null(flaky(shortNode));
        Assert.Null(flaky(shortNode));
        Assert.Equal(2, attempts);

        // A node that is not on a page is not styled
        var detached = shortPage.AddNode("acme.Gain", 10, 10, "Gone");
        shortPage.Remove(detached);
        Assert.Null(perPage(detached));
    }

    /// <summary>
    ///     Creates a diagram with one page holding an iconic node that has an Enabled and a Mode property.
    /// </summary>
    /// <returns>The diagram, page and node.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Node) CreateNodePage()
    {
        var diagram = SceneTestModel.CreateDiagram();
        diagram.Types.Add(new NodeType("acme.Lamp")
        {
            Title = "LAMP",
            Icon = IconSource.FromSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"),
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            Properties =
            {
                new PropertyDefinition("Enabled", true),
                new PropertyDefinition("Mode", "A") { Choices = ["A", "B"] },
            },
        });
        var page = diagram.Pages.Add("Main");
        return (diagram, page, page.AddNode("acme.Lamp", 20, 20, "L"));
    }

    /// <summary>
    ///     Gets the filled box rectangle of a node.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The rectangle.</returns>
    private static RectPrimitive Box(DiagramScene scene, Node node) =>
        SceneTestModel.For<RectPrimitive>(scene, node).Single(r => r.Fill is not null);

    /// <summary>
    ///     Gets the title text of a node.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The title primitive.</returns>
    private static TextPrimitive Title(DiagramScene scene, Node node) =>
        SceneTestModel.For<TextPrimitive>(scene, node).Single(t => t.Text == node.Type.Title);

    /// <summary>
    ///     A node visual that hands its context to a callback, for tests that inspect what visuals are given.
    /// </summary>
    private sealed class ProbeVisual : INodeVisual
    {
        /// <summary>
        ///     The callback that receives each context.
        /// </summary>
        private readonly Action<NodeVisualContext> _probe;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ProbeVisual"/> class.
        /// </summary>
        /// <param name="probe">The callback.</param>
        public ProbeVisual(Action<NodeVisualContext> probe) => _probe = probe;

        /// <inheritdoc />
        public void Build(NodeVisualContext context) => _probe(context);
    }
}
