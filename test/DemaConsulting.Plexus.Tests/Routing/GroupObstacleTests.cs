using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for groups as routing obstacles: <see cref="DiagramTheme.GroupsAreObstacles"/>, the end-node exemption,
///     collapsed boxes, display routes of collapsed groups, re-routing and determinism.
/// </summary>
public class GroupObstacleTests
{
    /// <summary>
    ///     A router that records every request it receives and delegates to the default router.
    /// </summary>
    private sealed class CapturingRouter : IWireRouter
    {
        /// <summary>
        ///     Gets the requests received so far.
        /// </summary>
        public List<RouteRequest> Requests { get; } = [];

        /// <inheritdoc />
        public IReadOnlyList<Point> Route(RouteRequest request)
        {
            Requests.Add(request);
            return new OrthogonalRouter().Route(request);
        }
    }

    /// <summary>
    ///     Creates a page with a producer and a consumer on one row and a group above and below that row between them.
    /// </summary>
    /// <param name="theme">The theme, or null for the default.</param>
    /// <returns>The page, producer, consumer and the group; no wire exists yet.</returns>
    private static (DiagramPage Page, Node Producer, Node Consumer, Group Group) CreateFramedRow(DiagramTheme? theme = null)
    {
        var diagram = TestModel.CreateDiagram();
        if (theme is not null)
        {
            diagram.Theme = theme;
        }

        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 800, 0, "C");
        var above = page.AddNode("acme.Gain", 350, -250, "Above");
        var below = page.AddNode("acme.Gain", 350, 250, "Below");
        var group = page.CreateGroup([above, below], "G");
        return (page, producer, consumer, group);
    }

    /// <summary>
    ///     Gets the full polyline of a wire from anchor to anchor.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The points of the drawn wire.</returns>
    private static List<Point> Polyline(Wire wire) => [wire.Source.Anchor, .. wire.Route, wire.Target.Anchor];

    /// <summary>
    ///     Validates that a wire is routed around the frame of a group none of its end nodes belongs to.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_GroupBetweenPorts_RoutesAroundGroupFrame()
    {
        // Arrange: the group frame spans the free row between the producer and the consumer
        var (page, producer, consumer, group) = CreateFramedRow();
        var frame = group.Bounds.Inflate(page.Diagram.Theme.ObstacleMargin);

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert: the wire detours and never enters the inflated frame
        Assert.NotEmpty(wire.Route);
        Assert.True(RouteAssertions.IsOrthogonal(Polyline(wire)));
        Assert.False(RouteAssertions.CrossesAny(Polyline(wire), [frame]));
    }

    /// <summary>
    ///     Validates that the option switches the behavior off: the wire then runs through the empty middle of the frame.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_GroupsAreObstaclesOff_RoutesThroughGroupFrame()
    {
        // Arrange
        var (page, producer, consumer, group) = CreateFramedRow(DiagramTheme.Classic with { GroupsAreObstacles = false });

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert: nothing but the nodes blocks the row, so the wire crosses the group frame
        Assert.True(RouteAssertions.CrossesAny(Polyline(wire), [group.Bounds]));
    }

    /// <summary>
    ///     Validates that the option defaults to on.
    /// </summary>
    [Fact]
    public void DiagramTheme_GroupsAreObstacles_DefaultsToTrue()
    {
        Assert.True(DiagramTheme.Classic.GroupsAreObstacles);
        Assert.True(new DiagramTheme().GroupsAreObstacles);
    }

    /// <summary>
    ///     Validates which group frames reach the router: all of them for an unrelated wire, none for the group an end node is in.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_EndNodeInsideGroup_ThatGroupIsNotAnObstacle()
    {
        // Arrange: a second group holds the producer
        var (page, producer, consumer, group) = CreateFramedRow();
        var router = new CapturingRouter();
        page.Diagram.Router = router;
        var margin = page.Diagram.Theme.ObstacleMargin;
        var holder = page.CreateGroup([producer], "Holder");
        var unrelated = page.AddNode("acme.Consumer", 800, 600, "U");

        // Act: one wire that starts in Holder and one wire that has nothing to do with either group
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var inside = router.Requests[^1];
        var other = page.AddNode("acme.Producer", 0, 600, "O");
        page.Connect(other.Port("value"), unrelated.Port("in"));
        var outside = router.Requests[^1];

        // Assert: Holder is exempt for the first wire but G is not; the second wire sees both frames
        Assert.DoesNotContain(holder.Bounds.Inflate(margin), inside.Obstacles);
        Assert.Contains(group.Bounds.Inflate(margin), inside.Obstacles);
        Assert.Contains(holder.Bounds.Inflate(margin), outside.Obstacles);
        Assert.Contains(group.Bounds.Inflate(margin), outside.Obstacles);
    }

    /// <summary>
    ///     Validates that a wire entering a group is not forced around that group: it crosses the frame edge with a simple route.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_TargetInsideGroup_MayCrossItsFrame()
    {
        // Arrange: the consumer joins the framed group, so the frame spans the row up to the consumer
        var (page, producer, consumer, group) = CreateFramedRow();
        group.AddMember(consumer);

        // Act
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Assert: the wire runs straight in through the frame edge instead of circling the group
        var polyline = Polyline(wire);
        Assert.True(RouteAssertions.IsOrthogonal(polyline));
        Assert.True(wire.Route.Count <= 2);
        Assert.True(RouteAssertions.CrossesAny(polyline, [group.Bounds]));
    }

    /// <summary>
    ///     Validates the exemption at any nesting depth: an outer group holding an end node is exempt, an inner group holding neither is not.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_NestedGroups_ExemptionFollowsMembershipPerGroup()
    {
        // Arrange: Outer holds the producer and the framed group; the framed group holds neither end node
        var (page, producer, consumer, inner) = CreateFramedRow();
        var outer = page.CreateGroup([producer, inner], "Outer");
        var router = new CapturingRouter();
        page.Diagram.Router = router;
        var margin = page.Diagram.Theme.ObstacleMargin;

        // Act
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var request = router.Requests.Single();

        // Assert: the outer group contains the source (exempt), the inner group contains neither end (obstacle)
        Assert.DoesNotContain(outer.Bounds.Inflate(margin), request.Obstacles);
        Assert.Contains(inner.Bounds.Inflate(margin), request.Obstacles);
    }

    /// <summary>
    ///     Validates the exemption for a node nested two levels deep: every ancestor group is exempt.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_EndNodeNestedTwoLevelsDeep_AllAncestorsAreExempt()
    {
        // Arrange
        var (page, producer, consumer, _) = CreateFramedRow();
        var inner = page.CreateGroup([producer], "Inner");
        var outer = page.CreateGroup([inner], "Outer");
        var router = new CapturingRouter();
        page.Diagram.Router = router;
        var margin = page.Diagram.Theme.ObstacleMargin;

        // Act
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var request = router.Requests.Single();

        // Assert
        Assert.DoesNotContain(inner.Bounds.Inflate(margin), request.Obstacles);
        Assert.DoesNotContain(outer.Bounds.Inflate(margin), request.Obstacles);
    }

    /// <summary>
    ///     Creates a wire whose straight path is blocked only by the box of a collapsed group.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The wire and the collapsed box.</returns>
    private static (Wire Wire, Rect Box, double Margin) ConnectThroughCollapsedBox(DiagramTheme theme)
    {
        // A node in the middle is collapsed into a box; the wire's two nodes are placed so the box sits on their row
        var diagram = TestModel.CreateDiagram();
        diagram.Theme = theme;
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 800, 0, "C");
        var middle = page.AddNode("acme.Gain", 350, 0, "Middle");
        var collapsed = page.CreateGroup([middle], "Pad");
        collapsed.IsCollapsed = true;
        var box = collapsed.Bounds;
        var centerY = box.Top + (box.Height / 2);
        producer.Bounds = new Rect(0, centerY - (producer.Bounds.Height / 2), producer.Bounds.Width, producer.Bounds.Height);
        consumer.Bounds = new Rect(800, centerY - (consumer.Bounds.Height / 2), consumer.Bounds.Width, consumer.Bounds.Height);
        return (page.Connect(producer.Port("value"), consumer.Port("in")), box, theme.ObstacleMargin);
    }

    /// <summary>
    ///     Validates that the box of a collapsed group is an obstacle for wires that do not end in it.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_CollapsedGroupBetweenPorts_RoutesAroundCollapsedBox()
    {
        // Arrange / Act
        var (wire, box, margin) = ConnectThroughCollapsedBox(DiagramTheme.Classic);

        // Assert: the wire stays out of the inflated collapsed box
        Assert.NotEmpty(wire.Route);
        Assert.False(RouteAssertions.CrossesAny(Polyline(wire), [box.Inflate(margin)]));
    }

    /// <summary>
    ///     Validates that with the option off the collapsed box does not block the wire.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_CollapsedGroupBetweenPorts_GroupsAreObstaclesOff_CrossesBox()
    {
        // Arrange / Act
        var (wire, box, _) = ConnectThroughCollapsedBox(DiagramTheme.Classic with { GroupsAreObstacles = false });

        // Assert
        Assert.True(RouteAssertions.CrossesAny(Polyline(wire), [box]));
    }

    /// <summary>
    ///     Validates that a group inside a collapsed group is hidden and therefore does not block anything.
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_GroupInsideCollapsedGroup_IsNotAnObstacle()
    {
        // Arrange: the framed group G is nested in a collapsed group, so only the small collapsed box remains drawn
        var (page, producer, consumer, group) = CreateFramedRow();
        var outer = page.CreateGroup([group], "Outer");
        outer.IsCollapsed = true;
        var router = new CapturingRouter();
        page.Diagram.Router = router;
        var margin = page.Diagram.Theme.ObstacleMargin;

        // Act
        page.Connect(producer.Port("value"), consumer.Port("in"));
        var request = router.Requests.Single();

        // Assert: the hidden inner frame is not offered, only the collapsed outer box is
        Assert.DoesNotContain(group.Bounds.Inflate(margin), request.Obstacles);
        Assert.Contains(outer.Bounds.Inflate(margin), request.Obstacles);
    }

    /// <summary>
    ///     Validates that moving a group does not re-route unrelated wires, while RerouteAll does and is undoable.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_AfterGroupMoved_RoutesAroundGroup()
    {
        // Arrange: the wire is created first; the group frame is then moved onto its row
        var (page, producer, consumer, group) = CreateFramedRow();
        page.Move([group], 0, 900);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var straight = wire.Route.ToArray();
        page.Move([group], 0, -900);

        // Assert (moving): the wire was left alone and now crosses the frame
        Assert.Equal(straight, wire.Route);
        Assert.True(RouteAssertions.CrossesAny(Polyline(wire), [group.Bounds]));

        // Act
        page.RerouteAll();

        // Assert (re-routing): the wire avoids the frame; undo restores the previous route
        var frame = group.Bounds.Inflate(page.Diagram.Theme.ObstacleMargin);
        Assert.False(RouteAssertions.CrossesAny(Polyline(wire), [frame]));
        page.Diagram.History.Undo();
        Assert.Equal(straight, wire.Route);
    }

    /// <summary>
    ///     Validates that a wire whose end node is moved is re-routed around groups.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_EndNode_ReroutesAroundGroupFrame()
    {
        // Arrange: the consumer stays on the group's row; the wire starts from a producer far below, then the producer moves onto the row
        var (page, producer, consumer, group) = CreateFramedRow();
        page.Move([producer], 0, 900);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));

        // Act: only the source moves, so the wire is re-routed with the group frame in the way
        page.Move([producer], 0, -900);

        // Assert
        var frame = group.Bounds.Inflate(page.Diagram.Theme.ObstacleMargin);
        Assert.False(RouteAssertions.CrossesAny(Polyline(wire), [frame]));
    }

    /// <summary>
    ///     Validates that routing around groups is deterministic.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_WithGroups_IsDeterministic()
    {
        // Arrange
        var (page, producer, consumer, _) = CreateFramedRow();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        var first = wire.Route.ToArray();

        // Act
        page.RerouteAll();
        var second = wire.Route.ToArray();
        page.RerouteAll();

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first, wire.Route);
    }

    /// <summary>
    ///     Validates that the display route of a wire to a collapsed group avoids the frame of an unrelated expanded group.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWire_RoutesAroundUnrelatedGroupFrame()
    {
        // Arrange: the producer sits in a collapsed group at the left, the consumer on the same row at the right, G in between
        var (page, producer, consumer, group) = CreateFramedRow();
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        page.CreateGroup([producer], "Pad").IsCollapsed = true;
        var frame = group.Bounds.Inflate(page.Diagram.Theme.ObstacleMargin);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.True(RouteAssertions.IsOrthogonal(points));
        Assert.False(RouteAssertions.CrossesAny(points, [frame]));
    }

    /// <summary>
    ///     Validates that a display route may cross the frame of a group that contains the wire's far end.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWire_TargetInsideGroup_FrameIsNotAnObstacle()
    {
        // Arrange: the consumer sits in the framed group, the producer in a collapsed one
        var (page, producer, consumer, group) = CreateFramedRow();
        group.AddMember(consumer);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        page.CreateGroup([producer], "Pad").IsCollapsed = true;
        var frame = group.Bounds.Inflate(page.Diagram.Theme.ObstacleMargin);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: the wire reaches the consumer inside the frame, so it must cross the frame edge
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.True(RouteAssertions.CrossesAny(points, [frame]));
        Assert.True(RouteAssertions.IsOrthogonal(points));
    }

    /// <summary>
    ///     Validates that the option also switches display routes: with it off they run through the frame.
    /// </summary>
    [Fact]
    public void SceneBuilder_Build_CollapsedGroupWire_GroupsAreObstaclesOff_CrossesFrame()
    {
        // Arrange
        var (page, producer, consumer, group) = CreateFramedRow(DiagramTheme.Classic with { GroupsAreObstacles = false });
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"));
        page.CreateGroup([producer], "Pad").IsCollapsed = true;

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert
        Assert.True(scene.TryGetWirePoints(wire, out var points));
        Assert.True(RouteAssertions.CrossesAny(points, [group.Bounds]));
    }

    /// <summary>
    ///     Validates that a page without groups hands the router exactly the node obstacles (no allocation of extra frames).
    /// </summary>
    [Fact]
    public void DiagramPage_Connect_NoGroups_ObstaclesAreOnlyNodeBoxes()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 400, 0);
        var router = new CapturingRouter();
        diagram.Router = router;

        // Act
        page.Connect(a.Port("value"), b.Port("in"));

        // Assert
        Assert.Equal(page.BuildObstacles(null), router.Requests.Single().Obstacles);
    }
}
