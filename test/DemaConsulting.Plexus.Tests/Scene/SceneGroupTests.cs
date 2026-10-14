namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Unit tests for how <see cref="SceneBuilder"/> draws groups: expanded frames, collapsed boxes, proxy ports and hidden members.
/// </summary>
public class SceneGroupTests
{
    /// <summary>
    ///     Creates the A -> B -> C chain used by the group tests.
    /// </summary>
    /// <returns>The page, the three nodes and the two wires.</returns>
    private static (DiagramPage Page, Node A, Node B, Node C, Wire AB, Wire BC) CreateChain()
    {
        var (_, page) = SceneTestModel.CreatePage();
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 200, 0, "B");
        var c = page.AddNode("acme.Consumer", 400, 0, "C");
        var ab = page.Connect(a.Port("value"), b.Port("in"));
        var bc = page.Connect(b.Port("out"), c.Port("in"));
        return (page, a, b, c, ab, bc);
    }

    /// <summary>
    ///     Validates that an expanded group draws a translucent frame, a title bar with title and a minus toggle behind its members.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ExpandedGroup_DrawsFrameTitleBarAndToggleBehindMembers()
    {
        // Arrange
        var (page, a, b, _, _, _) = CreateChain();
        var group = page.CreateGroup([a, b], "Grp");
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: frame and title bar use the group colors; the title text is present
        var rects = SceneTestModel.For<RectPrimitive>(scene, group);
        var frame = rects.First(r => r.Fill == theme.GroupFill);
        Assert.Equal(group.Bounds, frame.Rect);
        Assert.Contains(rects, r => r.Fill == theme.GroupTitleFill);
        Assert.Contains(SceneTestModel.For<TextPrimitive>(scene, group), t => t.Text == "Grp");

        // The toggle is a small box plus a single minus bar (two path commands)
        var minus = Assert.Single(SceneTestModel.For<PathPrimitive>(scene, group));
        Assert.Equal(2, minus.Commands.Count);

        // The frame is drawn before any member
        var primitives = scene.Primitives.ToList();
        Assert.True(primitives.IndexOf(frame) < primitives.FindIndex(p => ReferenceEquals(p.Element, a)));
    }

    /// <summary>
    ///     Validates that nested expanded groups draw the outer frame before the inner one.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NestedExpandedGroups_DrawOuterFrameFirst()
    {
        // Arrange: the inner group is created first, so it precedes the outer one in the page's z-order
        var (page, a, b, _, _, _) = CreateChain();
        var inner = page.CreateGroup([b], "Inner");
        var outer = page.CreateGroup([inner, a], "Outer");

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        var primitives = scene.Primitives.ToList();
        var outerIndex = primitives.FindIndex(p => ReferenceEquals(p.Element, outer));
        var innerIndex = primitives.FindIndex(p => ReferenceEquals(p.Element, inner));
        Assert.True(outerIndex < innerIndex);
    }

    /// <summary>
    ///     Validates that a selected group gets an outline in the selection color.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_SelectedGroup_AddsSelectionOutline()
    {
        // Arrange
        var (page, a, _, _, _, _) = CreateChain();
        var group = page.CreateGroup([a], "Grp");

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [group] });

        // Assert
        Assert.Contains(
            SceneTestModel.For<RectPrimitive>(scene, group),
            r => r.Fill is null && r.Stroke == page.Diagram.Theme.SelectionColor);
    }

    /// <summary>
    ///     Validates that a collapsed group hides its members, draws a summary box and keeps the stored routes untouched.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_HidesMembersAndDrawsSummaryBox()
    {
        // Arrange
        var (page, _, b, _, ab, bc) = CreateChain();
        var group = page.CreateGroup([b], "Grp");
        var abRoute = ab.Route.ToArray();
        var bcRoute = bc.Route.ToArray();
        group.IsCollapsed = true;
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: nothing is drawn for B; the group is a node-colored box at its collapsed position
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, b));
        Assert.False(scene.TryGetNodeLayout(b, out _));
        var box = SceneTestModel.For<RectPrimitive>(scene, group).Single(r => r.Fill == theme.NodeFill);
        Assert.Equal(group.CollapsedPosition, box.Rect.Position);
        Assert.Equal(theme.CollapsedGroupWidth, box.Rect.Width);
        Assert.Contains(SceneTestModel.For<TextPrimitive>(scene, group), t => t.Text == "Grp");

        // The plus glyph has a horizontal and a vertical bar (four path commands)
        Assert.Contains(SceneTestModel.For<PathPrimitive>(scene, group), p => p.Commands.Count == 4 && p.Fill is null);

        // Display routes are transient: stored routes are unchanged
        Assert.Equal(abRoute, ab.Route);
        Assert.Equal(bcRoute, bc.Route);
    }

    /// <summary>
    ///     Validates that wires crossing a collapsed group's boundary end at proxy ports labeled with the inner node and port.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_CrossingWiresAttachToProxyPorts()
    {
        // Arrange: B alone is collapsed, so A->B enters it and B->C leaves it
        var (page, _, b, _, ab, bc) = CreateChain();
        var group = page.CreateGroup([b], "Grp");
        group.IsCollapsed = true;
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: proxy labels name the inner ports
        var labels = SceneTestModel.For<TextPrimitive>(scene, group).Select(t => t.Text).ToList();
        Assert.Contains("B.in", labels);
        Assert.Contains("B.out", labels);

        // The incoming wire ends at the outer end of the proxy's label left of the box and the outgoing one starts right of it, on the same row, centered on the box
        var box = SceneTestModel.For<RectPrimitive>(scene, group).Single(r => r.Fill == theme.NodeFill).Rect;
        var rowY = box.Top + (box.Height / 2);
        Assert.True(scene.TryGetWirePoints(ab, out var abPoints));
        Assert.Equal(new Point(box.Left - NodeGeometry.PortReach("B.in", theme), rowY), abPoints[^1]);
        Assert.Equal(ab.Source.Anchor, abPoints[0]);
        Assert.True(scene.TryGetWirePoints(bc, out var bcPoints));
        Assert.Equal(new Point(box.Right + NodeGeometry.PortReach("B.out", theme), rowY), bcPoints[0]);
        Assert.Equal(bc.Target.Anchor, bcPoints[^1]);

        // Both display routes are orthogonal
        Assert.True(RouteAssertions.IsOrthogonal(abPoints));
        Assert.True(RouteAssertions.IsOrthogonal(bcPoints));

        // Proxy triangles are colored by port type
        var triangles = SceneTestModel.For<PathPrimitive>(scene, group).Where(p => p.Fill is not null).ToList();
        Assert.Equal(2, triangles.Count);
        Assert.All(triangles, t => Assert.Equal(Color.Parse("#FF8C00"), t.Fill));
    }

    /// <summary>
    ///     Builds a page where a collapsed group's output wire runs to the mixer input below the one another net's wire enters.
    /// </summary>
    /// <returns>The page, the wire of the other net and the wire leaving the collapsed group.</returns>
    private static (DiagramPage Page, Wire Other, Wire Collapsed) CreateCollapsedNeighbor()
    {
        // The amplifier's wire is created first and so is already routed when the group is collapsed
        var (_, page) = SceneTestModel.CreatePage();
        var amp = page.AddNode("acme.Gain", 500, 150, "Amp");
        var mixer = page.AddNode("acme.Mixer", 700, 345, "Mix");
        var pad = page.AddNode("acme.Gain", 100, 615, "Pad");
        var other = page.Connect(amp.Port("out"), mixer.Port("a"));
        var collapsed = page.Connect(pad.Port("out"), mixer.Port("b"));
        page.CreateGroup([pad], "PadGroup").IsCollapsed = true;
        return (page, other, collapsed);
    }

    /// <summary>
    ///     A router for tests that records the requests it receives and returns no bend points.
    /// </summary>
    private sealed class RecordingRouter : IWireRouter
    {
        /// <summary>
        ///     Gets the requests received so far.
        /// </summary>
        public List<RouteRequest> Requests { get; } = [];

        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request)
        {
            Requests.Add(request);
            return [];
        }
    }

    /// <summary>
    ///     Validates that the display route of a wire attached to a collapsed group is requested with the segments of the other wires and a net of its own, so the router can keep nets apart.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWire_DisplayRequestCarriesOtherNetsSegments()
    {
        // Arrange: one visible wire with a stored route, one wire leaving a collapsed group
        var router = new RecordingRouter();
        var (_, page) = SceneTestModel.CreatePage();
        page.Diagram.Router = router;
        var amp = page.AddNode("acme.Gain", 500, 150, "Amp");
        var mixer = page.AddNode("acme.Mixer", 700, 345, "Mix");
        var pad = page.AddNode("acme.Gain", 100, 615, "Pad");
        var start = amp.Port("out").Anchor;
        var end = mixer.Port("a").Anchor;
        var other = page.Connect(amp.Port("out"), mixer.Port("a"), [new Point(650, start.Y), new Point(650, end.Y)]);
        var collapsed = page.Connect(pad.Port("out"), mixer.Port("b"));
        page.CreateGroup([pad], "PadGroup").IsCollapsed = true;
        router.Requests.Clear();

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: the request has a net, and the visible wire's three segments belong to another net
        var request = Assert.Single(router.Requests);
        Assert.Multiple(
            () => Assert.True(request.Net >= 0),
            () => Assert.Equal(3, request.ExistingSegments.Count),
            () => Assert.Contains(request.ExistingSegments, s => s.Start == new Point(650, start.Y) && s.End == new Point(650, end.Y)),
            () => Assert.All(request.ExistingSegments, s => Assert.NotEqual(request.Net, s.Net)),
            () => Assert.True(scene.TryGetWirePoints(collapsed, out _)),
            () => Assert.True(scene.TryGetWirePoints(other, out _)));
    }

    /// <summary>
    ///     Validates that two display routes out of the same collapsed output are one net, so they may share a trunk, and that a later display route also keeps clear of an earlier one of another net.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWires_ShareNetBySourcePort()
    {
        // Arrange: Pad.out feeds two mixer inputs, Other.out a third; all three cross the collapsed boundary
        var router = new RecordingRouter();
        var (_, page) = SceneTestModel.CreatePage();
        page.Diagram.Router = router;
        var pad = page.AddNode("acme.Gain", 100, 615, "Pad");
        var side = page.AddNode("acme.Gain", 100, 400, "Side");
        var mixer = page.AddNode("acme.Mixer", 700, 345, "Mix");
        page.Connect(pad.Port("out"), mixer.Port("a"));
        page.Connect(pad.Port("out"), mixer.Port("b"));
        page.Connect(side.Port("out"), mixer.Port("c"));
        page.CreateGroup([pad, side], "Both").IsCollapsed = true;
        router.Requests.Clear();

        // Act
        SceneTestModel.BuildNoGrid(page);

        // Assert: requests arrive in page order; the second shares the first's net and sees its segment, the third is another net and sees both
        Assert.Equal(3, router.Requests.Count);
        var (first, second, third) = (router.Requests[0], router.Requests[1], router.Requests[2]);
        Assert.Multiple(
            () => Assert.Empty(first.ExistingSegments),
            () => Assert.Equal(first.Net, second.Net),
            () => Assert.NotEmpty(second.ExistingSegments),
            () => Assert.All(second.ExistingSegments, s => Assert.Equal(first.Net, s.Net)),
            () => Assert.NotEqual(first.Net, third.Net),
            () => Assert.Equal(second.ExistingSegments.Count + 1, third.ExistingSegments.Count));
    }

    /// <summary>
    ///     Validates that wires into an input that allows fan-in are one net, so they may share the final approach, while wires into a plain input from different sources stay different nets.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_FanInIntoOptedInInput_RoutesAsOneNet()
    {
        // Arrange: two sources, a merging sink and a plain sink fed by a third source
        var router = new RecordingRouter();
        var (_, page) = SceneTestModel.CreatePage();
        page.Diagram.Router = router;
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Producer", 0, 300, "B");
        var c = page.AddNode("acme.Producer", 0, 600, "C");
        var merger = page.AddNode("acme.Merger", 600, 150, "M");
        var plain = page.AddNode("acme.Consumer", 600, 450, "P");
        page.Connect(a.Port("value"), merger.Port("in"), null);
        page.Connect(c.Port("value"), plain.Port("in"), null);
        router.Requests.Clear();

        // Act: route the second wire into the merger, then another wire from B to the plain sink
        page.Connect(b.Port("value"), merger.Port("in"));
        var intoMerger = router.Requests[0];
        page.Connect(b.Port("value"), page.AddNode("acme.Consumer", 600, 750, "Q").Port("in"));
        var elsewhere = router.Requests[1];

        // Assert: the merger wire shares the first wire's net and sees its segments as its own; the other wire shares only B's net
        Assert.Multiple(
            () => Assert.NotEmpty(intoMerger.ExistingSegments),
            () => Assert.Contains(intoMerger.ExistingSegments, s => s.Net == intoMerger.Net),
            () => Assert.Contains(elsewhere.ExistingSegments, s => s.Net != elsewhere.Net));
    }

    /// <summary>
    ///     Validates that display routes are deterministic: building the same page twice gives the same route and leaves the stored route alone.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWire_DisplayRouteIsDeterministic()
    {
        // Arrange
        var (page, _, collapsed) = CreateCollapsedNeighbor();
        var stored = collapsed.Route.ToList();

        // Act
        var first = SceneTestModel.BuildNoGrid(page);
        var second = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(first.TryGetWirePoints(collapsed, out var firstPoints));
        Assert.True(second.TryGetWirePoints(collapsed, out var secondPoints));
        Assert.Equal(firstPoints, secondPoints);
        Assert.Equal(stored, collapsed.Route);
    }

    /// <summary>
    ///     Validates that a wire entirely inside a collapsed group is hidden and does not produce a proxy.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_WireInsideIsHidden()
    {
        // Arrange: A and B collapsed together, only B->C crosses the boundary
        var (page, a, b, _, ab, bc) = CreateChain();
        var group = page.CreateGroup([a, b], "Grp");
        group.IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.False(scene.TryGetWirePoints(ab, out _));
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, ab));
        Assert.True(scene.TryGetWirePoints(bc, out _));
        var labels = SceneTestModel.For<TextPrimitive>(scene, group).Select(t => t.Text).ToList();
        Assert.Contains("B.out", labels);
        Assert.DoesNotContain("B.in", labels);
        Assert.DoesNotContain("A.value", labels);
    }

    /// <summary>
    ///     Validates that several wires through the same inner port share one proxy.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_SharedInnerPortHasOneProxy()
    {
        // Arrange: B.out feeds two consumers outside the group
        var (page, _, b, _, _, _) = CreateChain();
        var d = page.AddNode("acme.Consumer", 400, 200, "D");
        page.Connect(b.Port("out"), d.Port("in"));
        var group = page.CreateGroup([b], "Grp");
        group.IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.Single(SceneTestModel.For<TextPrimitive>(scene, group), t => t.Text == "B.out");
    }

    /// <summary>
    ///     Validates that the box grows taller than the group's size when many proxy ports need room.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroup_BoxGrowsToFitProxyPorts()
    {
        // Arrange: a mixer with four inputs fed from outside
        var (_, page) = SceneTestModel.CreatePage();
        var mixer = page.AddNode("acme.Mixer", 300, 0, "M");
        foreach (var input in mixer.Inputs)
        {
            var producer = page.AddNode("acme.Producer", 0, mixer.Wires.Count * 120, null);
            page.Connect(producer.Port("value"), input);
        }

        var group = page.CreateGroup([mixer], "Grp");
        group.IsCollapsed = true;
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: four rows of proxies plus the title bar exceed the default collapsed height
        var box = SceneTestModel.For<RectPrimitive>(scene, group).Single(r => r.Fill == theme.NodeFill).Rect;
        Assert.True(box.Height > theme.CollapsedGroupHeight);
        Assert.True(box.Height >= theme.GroupTitleHeight + (4 * theme.PortPitch));
    }

    /// <summary>
    ///     Validates that with nested collapsed groups only the outermost one is drawn.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_NestedCollapsedGroups_OutermostWins()
    {
        // Arrange
        var (page, a, b, _, ab, bc) = CreateChain();
        var inner = page.CreateGroup([b], "Inner");
        var outer = page.CreateGroup([inner, a], "Outer");
        inner.IsCollapsed = true;
        outer.IsCollapsed = true;
        var theme = page.Diagram.Theme;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert: one summary box for the outer group, nothing for the inner group or members
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, inner));
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, a) || ReferenceEquals(p.Element, b));
        var box = SceneTestModel.For<RectPrimitive>(scene, outer).Single(r => r.Fill == theme.NodeFill).Rect;

        // A->B is internal to the outer group; B->C crosses its boundary, so it leaves from the outer box
        Assert.False(scene.TryGetWirePoints(ab, out _));
        Assert.True(scene.TryGetWirePoints(bc, out var points));
        Assert.Equal(box.Right + NodeGeometry.PortReach("B.out", theme), points[0].X);
        Assert.Contains(SceneTestModel.For<TextPrimitive>(scene, outer), t => t.Text == "B.out");
    }

    /// <summary>
    ///     Validates that an expanded group inside a collapsed one is hidden with it.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ExpandedGroupInsideCollapsedGroup_IsHidden()
    {
        // Arrange
        var (page, _, b, _, _, _) = CreateChain();
        var inner = page.CreateGroup([b], "Inner");
        var outer = page.CreateGroup([inner], "Outer");
        outer.IsCollapsed = true;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.False(inner.IsCollapsed);
        Assert.DoesNotContain(scene.Primitives, p => ReferenceEquals(p.Element, inner));
        Assert.Contains(scene.Primitives, p => ReferenceEquals(p.Element, outer));
    }

    /// <summary>
    ///     Validates that expanding a collapsed group restores the member drawing.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_ExpandAgain_RestoresMembers()
    {
        // Arrange
        var (page, _, b, _, _, _) = CreateChain();
        var group = page.CreateGroup([b], "Grp");
        group.IsCollapsed = true;
        group.IsCollapsed = false;

        // Act
        var scene = SceneTestModel.BuildNoGrid(page);

        // Assert
        Assert.True(scene.TryGetNodeLayout(b, out _));
        Assert.Contains(SceneTestModel.For<RectPrimitive>(scene, group), r => r.Fill == page.Diagram.Theme.GroupFill);
    }
}
