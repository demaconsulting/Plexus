using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="DiagramScene.TryGetGroupLayout"/>: the published group geometry must be
///     exactly what the scene draws and hit-tests with.
/// </summary>
public class SceneGroupLayoutTests
{
    /// <summary>
    ///     Creates the A -> B -> C chain.
    /// </summary>
    /// <returns>The page and the three nodes.</returns>
    private static (DiagramPage Page, Node A, Node B, Node C) CreateChain()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 200, 0, "B");
        var c = page.AddNode("acme.Consumer", 400, 0, "C");
        page.Connect(a.Port("value"), b.Port("in"));
        page.Connect(b.Port("out"), c.Port("in"));
        return (page, a, b, c);
    }

    /// <summary>
    ///     Validates that an expanded group's layout matches the drawn frame and title bar.
    /// </summary>
    [Fact]
    public void DiagramScene_TryGetGroupLayout_ExpandedGroup_MatchesDrawnGeometry()
    {
        // Arrange
        var (page, a, b, _) = CreateChain();
        var group = page.CreateGroup([a, b], "Grp");
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        var found = scene.TryGetGroupLayout(group, out var layout);

        // Assert: frame, header and toggle equal the primitives
        Assert.True(found);
        var rects = SceneTestModel.For<RectPrimitive>(scene, group);
        Assert.Multiple(
            () => Assert.Same(group, layout!.Group),
            () => Assert.False(layout!.IsCollapsed),
            () => Assert.Equal(group.Bounds, layout!.Frame),
            () => Assert.Equal(rects.First(r => r.Fill == theme.GroupFill).Rect, layout!.Frame),
            () => Assert.Equal(rects.First(r => r.Fill == theme.GroupTitleFill).Rect, layout!.Header),
            () => Assert.Equal(theme.GroupTitleHeight, layout!.Header.Height),
            () => Assert.Contains(rects, r => r.Fill is null && r.Rect == layout!.CollapseToggle),
            () => Assert.Equal(theme.GroupToggleSize, layout!.CollapseToggle.Width),
            () => Assert.True(layout!.Header.Contains(layout.CollapseToggle.Center)),
            () => Assert.Empty(layout!.InputPorts),
            () => Assert.Empty(layout!.OutputPorts));
    }

    /// <summary>
    ///     Validates that hit testing the published toggle and header answers with the group parts.
    /// </summary>
    [Fact]
    public void DiagramScene_TryGetGroupLayout_ExpandedGroup_RectanglesAreWhatHitTestUses()
    {
        // Arrange
        var (page, a, b, _) = CreateChain();
        var group = page.CreateGroup([a, b], "Grp");
        var scene = SceneTestModel.BuildNoGrid(page);
        Assert.True(scene.TryGetGroupLayout(group, out var layout));

        // Act: a point in the middle of the toggle and one at the right end of the header
        var onToggle = scene.HitTest(layout.CollapseToggle.Center);
        var onHeader = scene.HitTest(new Point(layout.Header.Right - 10, layout.Header.Center.Y));

        // Assert
        Assert.Multiple(
            () => Assert.Equal(HitKind.GroupCollapseToggle, onToggle.Kind),
            () => Assert.Same(group, onToggle.Element),
            () => Assert.Equal(HitKind.GroupHeader, onHeader.Kind),
            () => Assert.Same(group, onHeader.Element));
    }

    /// <summary>
    ///     Validates that a collapsed group's layout publishes the drawn box, the toggle and its proxy ports.
    /// </summary>
    [Fact]
    public void DiagramScene_TryGetGroupLayout_CollapsedGroup_PublishesBoxToggleAndProxyPorts()
    {
        // Arrange: B collapsed alone, so A->B enters it and B->C leaves it
        var (page, _, b, _) = CreateChain();
        var group = page.CreateGroup([b], "Grp");
        group.IsCollapsed = true;
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);
        var found = scene.TryGetGroupLayout(group, out var layout);

        // Assert: the box is the drawn node-colored rectangle and the toggle is hit-testable
        Assert.True(found);
        var box = SceneTestModel.For<RectPrimitive>(scene, group).Single(r => r.Fill == theme.NodeFill).Rect;
        Assert.Multiple(
            () => Assert.True(layout!.IsCollapsed),
            () => Assert.Equal(box, layout!.Frame),
            () => Assert.Equal(HitKind.GroupCollapseToggle, scene.HitTest(layout!.CollapseToggle.Center).Kind),
            () => Assert.Equal(HitKind.CollapsedGroup, scene.HitTest(new Point(layout!.Frame.Center.X, layout.Frame.Bottom - 2)).Kind));

        // Each proxy names its inner port and sits where displayed wires attach
        var input = Assert.Single(layout!.InputPorts);
        var output = Assert.Single(layout.OutputPorts);
        var wire = page.Wires.First(w => ReferenceEquals(w.Target.Node, b));
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.Multiple(
            () => Assert.Same(b.Port("in"), input.Port),
            () => Assert.Equal("B.in", input.Label.Text),
            () => Assert.Equal(points[^1], input.Anchor),
            () => Assert.True(input.Triangle.Right <= box.Left + 0.001),
            () => Assert.Same(b.Port("out"), output.Port),
            () => Assert.Equal("B.out", output.Label.Text),
            () => Assert.True(output.Triangle.Left >= box.Right - 0.001));
    }

    /// <summary>
    ///     Validates that groups that are not drawn have no layout, and that a null group is rejected.
    /// </summary>
    [Fact]
    public void DiagramScene_TryGetGroupLayout_HiddenGroupOrOtherPage_ReturnsFalse()
    {
        // Arrange: the inner group is hidden inside a collapsed outer group
        var (page, a, b, _) = CreateChain();
        var inner = page.CreateGroup([a], "Inner");
        var outer = page.CreateGroup([inner, b], "Outer");
        outer.IsCollapsed = true;
        var scene = SceneTestModel.BuildNoGrid(page);

        // Act
        var innerFound = scene.TryGetGroupLayout(inner, out var innerLayout);
        var outerFound = scene.TryGetGroupLayout(outer, out _);

        // Assert
        Assert.Multiple(
            () => Assert.False(innerFound),
            () => Assert.Null(innerLayout),
            () => Assert.True(outerFound),
            () => Assert.Throws<ArgumentNullException>(() => scene.TryGetGroupLayout(null!, out _)));
    }
}
