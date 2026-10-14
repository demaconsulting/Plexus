using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="Group"/>: creation, nesting, bounds, moving, collapse and ungrouping.
/// </summary>
public class GroupTests
{
    /// <summary>
    ///     Creates a page with three nodes (A, B, C) and an annotation.
    /// </summary>
    /// <returns>The diagram, page and elements.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node A, Node B, Node C, Annotation Note) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (
            diagram,
            page,
            page.AddNode("acme.Producer", 0, 0, "A"),
            page.AddNode("acme.Gain", 150, 0, "B"),
            page.AddNode("acme.Consumer", 300, 0, "C"),
            page.AddAnnotation("note", new Rect(0, 150, 100, 40)));
    }

    /// <summary>
    ///     Validates that CreateGroup links members and group, preserves member order and is undoable.
    /// </summary>
    [Fact]
    public void DiagramPage_CreateGroup_LinksMembersAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, _, note) = Create();
        Group? group = null;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => group = page.CreateGroup([b, a, note], "Pair"));
        Assert.Equal("Pair", group!.Title);
        Assert.Equal(new DiagramElement[] { b, a, note }, group.Members);
        Assert.Same(group, a.Parent);
        Assert.Same(group, note.Parent);
        Assert.Contains(group, page.Groups);
        Assert.Contains(group, page.Elements);
        Assert.Same(group, diagram.FindElement(group.Id));
        Assert.False(group.IsCollapsed);
    }

    /// <summary>
    ///     Validates that group bounds fit the members' visual extents plus padding and title bar, and follow member moves.
    /// </summary>
    [Fact]
    public void Group_Bounds_ExpandedFitsMemberExtentsWithPaddingAndTitle()
    {
        // Arrange: A at (0,0) and B at (150,0), padding 30 and title 24
        var (diagram, page, a, b, _, _) = Create();
        var theme = diagram.Theme;

        // Act
        var group = page.CreateGroup([a, b], "G");

        // Assert: union of the node extents inflated by padding, extra title bar on top
        var union = NodeGeometry.VisualBounds(a, theme).Union(NodeGeometry.VisualBounds(b, theme));
        Assert.Equal(
            new Rect(union.Left - 30, union.Top - 30 - 24, union.Width + 60, union.Height + 60 + 24),
            group.Bounds);

        // Act: moving a member changes the fitted bounds
        b.MoveTo(150, 100);

        // Assert
        union = NodeGeometry.VisualBounds(a, theme).Union(NodeGeometry.VisualBounds(b, theme));
        Assert.Equal(
            new Rect(union.Left - 30, union.Top - 30 - 24, union.Width + 60, union.Height + 60 + 24),
            group.Bounds);
    }

    /// <summary>
    ///     Validates that the frame encloses the name above, summary lines below and port labels beside a node.
    /// </summary>
    [Fact]
    public void Group_Bounds_EnclosesNameSummaryAndPortLabels()
    {
        // Arrange: the Producer shows two summary lines and an output port
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        var layout = NodeLayout.Compute(a, diagram.Theme, new ApproximateTextMeasurer());

        // Act
        var frame = group.Bounds;

        // Assert: every text and port rectangle drawn for the node lies inside the frame's body
        var body = new Rect(frame.Left, frame.Top + diagram.Theme.GroupTitleHeight, frame.Width, frame.Height - diagram.Theme.GroupTitleHeight);
        Assert.NotEmpty(layout.SummaryLines);
        Assert.All(
            [layout.Name.Bounds, .. layout.SummaryLines.Select(l => l.Bounds), .. layout.Ports.Select(p => p.Label.Bounds), .. layout.Ports.Select(p => p.Triangle)],
            r => Assert.True(body.Contains(r), $"{r} is outside {body}"));
        Assert.True(body.Contains(layout.Ports.Single().Anchor));
    }

    /// <summary>
    ///     Validates argument and state validation of CreateGroup.
    /// </summary>
    [Fact]
    public void DiagramPage_CreateGroup_InvalidInput_Throws()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var foreign = diagram.Pages.Add("Other").AddNode("acme.Producer", 0, 0);
        var inner = page.CreateGroup([a], "Inner");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.CreateGroup(null!, "G"));
        Assert.Throws<ArgumentException>(() => page.CreateGroup([], "G"));
        Assert.Throws<ArgumentException>(() => page.CreateGroup([foreign], "G"));
        Assert.Throws<InvalidOperationException>(() => page.CreateGroup([a, b], "Mixed parents"));
        Assert.NotNull(inner);
    }

    /// <summary>
    ///     Validates that wires in the member list are ignored when creating a group.
    /// </summary>
    [Fact]
    public void DiagramPage_CreateGroup_WiresInMemberList_AreIgnored()
    {
        // Arrange
        var (_, page, a, b, _, _) = Create();
        var wire = page.Connect(a.Port("value"), b.Port("in"));

        // Act
        var group = page.CreateGroup([a, b, wire], "G");

        // Assert
        Assert.Equal(2, group.Members.Count);
        Assert.Null(wire.Parent);
    }

    /// <summary>
    ///     Validates nesting: grouping members that share a parent creates a child group in the parent at the first member's slot.
    /// </summary>
    [Fact]
    public void DiagramPage_CreateGroup_MembersOfSameGroup_NestsInsideThatGroup()
    {
        // Arrange: outer group of A, B, C
        var (diagram, page, a, b, c, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        Group? inner = null;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => inner = page.CreateGroup([b, c], "Inner"));
        Assert.Equal(new DiagramElement[] { a, inner! }, outer.Members);
        Assert.Same(outer, inner!.Parent);
        Assert.Equal(new DiagramElement[] { b, c }, inner.Members);
    }

    /// <summary>
    ///     Validates that nested groups report all descendants and that AddMember moves elements between groups.
    /// </summary>
    [Fact]
    public void Group_AddMemberAndRemoveMember_MoveElementsBetweenGroupsUndoably()
    {
        // Arrange
        var (diagram, page, a, b, c, _) = Create();
        var first = page.CreateGroup([a], "First");
        var second = page.CreateGroup([b], "Second");

        // Act / Assert: add C to the first group, then move A from first to second
        TestModel.AssertRoundTrip(diagram, () => first.AddMember(c));
        TestModel.AssertRoundTrip(diagram, () => second.AddMember(a));
        Assert.Equal(new DiagramElement[] { c }, first.Members);
        Assert.Equal(new DiagramElement[] { b, a }, second.Members);
        TestModel.AssertRoundTrip(diagram, () => second.RemoveMember(a));
        Assert.Null(a.Parent);
    }

    /// <summary>
    ///     Validates the cycle and validity checks of AddMember and RemoveMember.
    /// </summary>
    [Fact]
    public void Group_AddMemberAndRemoveMember_InvalidOperations_Throw()
    {
        // Arrange
        var (_, page, a, b, _, _) = Create();
        var outer = page.CreateGroup([a], "Outer");
        var inner = page.CreateGroup([a], "Inner");
        var wire = page.Connect(a.Port("value"), b.Port("in"));

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => inner.AddMember(outer));
        Assert.Throws<InvalidOperationException>(() => outer.AddMember(outer));
        Assert.Throws<ArgumentException>(() => outer.AddMember(wire));
        Assert.Throws<ArgumentException>(() => outer.RemoveMember(b));
        Assert.Throws<ArgumentNullException>(() => outer.AddMember(null!));
    }

    /// <summary>
    ///     Validates that AllMembers enumerates nested descendants depth first.
    /// </summary>
    [Fact]
    public void Group_AllMembers_IncludesNestedGroupsAndTheirMembers()
    {
        // Arrange
        var (_, page, a, b, c, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");

        // Act
        var all = outer.AllMembers().ToList();

        // Assert
        Assert.Equal(new DiagramElement[] { a, inner, b, c }, all);
    }

    /// <summary>
    ///     Validates that moving a group moves all (nested) members and the collapsed position, and that undo restores them.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_Group_MovesAllMembersAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, c, note) = Create();
        var outer = page.CreateGroup([a, b, c, note], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");
        var outerBefore = outer.Bounds;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Move([outer], 15, 30));
        Assert.Equal(new Point(15, 30), a.Position);
        Assert.Equal(new Point(165, 30), b.Position);
        Assert.Equal(new Point(315, 30), c.Position);
        Assert.Equal(new Rect(15, 180, 100, 40), note.Bounds);
        Assert.Equal(outerBefore.Offset(15, 30), outer.Bounds);
        Assert.NotNull(inner);
    }

    /// <summary>
    ///     Validates that a wire between two nodes of a moved group is translated while a wire to an outside node is re-routed.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_Group_TranslatesInternalWiresAndReroutesCrossingWires()
    {
        // Arrange: A -> B inside the group, B -> C crossing out of it
        var (_, page, a, b, c, _) = Create();
        var inside = page.Connect(a.Port("value"), b.Port("in"), [new Point(120, 100)]);
        var crossing = page.Connect(b.Port("out"), c.Port("in"), [new Point(260, 100)]);
        var group = page.CreateGroup([a, b], "G");

        // Act
        page.Move([group], 0, 60);

        // Assert: internal wire shifted, crossing wire replaced by the router's route
        Assert.Equal(new[] { new Point(120, 160) }, inside.Route);
        Assert.NotEqual(new[] { new Point(260, 100) }, crossing.Route);
    }

    /// <summary>
    ///     Validates collapse and expand: the flag, the pinned position and exact undo.
    /// </summary>
    [Fact]
    public void Group_IsCollapsed_SetTrueAndFalse_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");
        var expanded = group.Bounds;

        // Act / Assert: collapse pins the box at the expanded top-left with the theme's collapsed size
        TestModel.AssertRoundTrip(diagram, () => group.IsCollapsed = true);
        Assert.Equal(new Rect(expanded.Position, new Size(120, 45)), group.Bounds);
        Assert.Equal(expanded.Position, group.CollapsedPosition);
        Assert.Equal("Collapse group", diagram.History.UndoDescription);

        // Act / Assert: expanding restores the fitted bounds
        TestModel.AssertRoundTrip(diagram, () => group.IsCollapsed = false);
        Assert.Equal(expanded, group.Bounds);
        Assert.Equal("Expand group", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that setting IsCollapsed to its current value records nothing.
    /// </summary>
    [Fact]
    public void Group_IsCollapsed_SameValue_RecordsNothing()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        var entries = diagram.History.UndoCount;

        // Act
        group.IsCollapsed = false;

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that moving a collapsed group moves its box and members and expanding afterwards keeps them together.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_CollapsedGroup_MovesBoxAndMembers()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");
        group.IsCollapsed = true;
        var collapsedBefore = group.Bounds;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Move([group], 45, 60));
        Assert.Equal(collapsedBefore.Offset(45, 60), group.Bounds);
        Assert.Equal(new Point(45, 60), a.Position);
        group.IsCollapsed = false;
        var union = NodeGeometry.VisualBounds(a, diagram.Theme).Union(NodeGeometry.VisualBounds(b, diagram.Theme));
        Assert.Equal(union.Position.X - 30, group.Bounds.X);
        Assert.Equal(union.Position.Y - 54, group.Bounds.Y);
    }

    /// <summary>
    ///     Validates that collapsing marks members as hidden but keeps them in the group.
    /// </summary>
    [Fact]
    public void Group_IsCollapsed_HidesMembers()
    {
        // Arrange
        var (_, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");

        // Act
        group.IsCollapsed = true;

        // Assert
        Assert.True(a.IsHiddenByCollapse);
        Assert.Equal(2, group.Members.Count);
    }

    /// <summary>
    ///     Validates that the group title can be changed undoably.
    /// </summary>
    [Fact]
    public void Group_Title_Set_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "Old");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => group.Title = "New");
        Assert.Throws<ArgumentNullException>(() => group.Title = null!);
    }

    /// <summary>
    ///     Validates that Ungroup removes the frame, keeps members and restores everything on undo.
    /// </summary>
    [Fact]
    public void DiagramPage_Ungroup_RemovesFrameKeepsMembersAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Ungroup(group));
        Assert.DoesNotContain(group, page.Groups);
        Assert.Null(a.Parent);
        Assert.Contains(a, page.Nodes);
        Assert.Null(diagram.FindElement(group.Id));
        Assert.Equal("Ungroup", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that ungrouping a nested group puts its members into the parent group at the same slot.
    /// </summary>
    [Fact]
    public void DiagramPage_Ungroup_NestedGroup_MembersTakeFramePlaceInParent()
    {
        // Arrange
        var (diagram, page, a, b, c, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Ungroup(inner));
        Assert.Equal(new DiagramElement[] { a, b, c }, outer.Members);
        Assert.Same(outer, b.Parent);
    }

    /// <summary>
    ///     Validates Ungroup argument validation.
    /// </summary>
    [Fact]
    public void DiagramPage_Ungroup_InvalidArguments_Throw()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var otherPageGroup = diagram.Pages.Add("Other").CreateGroup([diagram.Pages[1].AddNode("acme.Producer", 0, 0)], "X");
        page.CreateGroup([a], "G");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.Ungroup(null!));
        Assert.Throws<ArgumentException>(() => page.Ungroup(otherPageGroup));
    }

    /// <summary>
    ///     Validates that the member list is observable and read-only and reports additions and removals.
    /// </summary>
    [Fact]
    public void Group_Members_AddAndRemove_RaiseCollectionChangedAndKeepOrder()
    {
        // Arrange
        var (diagram, page, a, b, c, _) = Create();
        var group = page.CreateGroup([a], "G");
        var actions = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)group.Members).CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        group.AddMember(b);
        group.AddMember(c);
        group.RemoveMember(b);
        diagram.History.Undo();

        // Assert
        Assert.Equal(new DiagramElement[] { a, b, c }, group.Members);
        Assert.Equal(
            [
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Remove,
                NotifyCollectionChangedAction.Add,
            ],
            actions);
        Assert.IsType<ReadOnlyObservableCollection<DiagramElement>>(group.Members);
    }

    /// <summary>
    ///     Validates that adding an element that already belongs to the group records nothing.
    /// </summary>
    [Fact]
    public void Group_AddMember_AlreadyMemberOfSameGroup_RecordsNothing()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        var entries = diagram.History.UndoCount;

        // Act
        group.AddMember(a);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Single(group.Members);
    }

    /// <summary>
    ///     Validates that the bounds of an outer group contain the bounds of a nested group and the other members.
    /// </summary>
    [Fact]
    public void Group_Bounds_NestedGroup_OuterContainsInnerAndSiblings()
    {
        // Arrange
        var (diagram, page, a, b, c, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");

        // Act
        var outerBounds = outer.Bounds;

        // Assert
        Assert.True(outerBounds.Contains(inner.Bounds));
        Assert.True(outerBounds.Contains(NodeGeometry.VisualBounds(a, diagram.Theme)));
        Assert.True(outerBounds.Width > inner.Bounds.Width);
    }

    /// <summary>
    ///     Validates that a group without members is a fixed-size box at its remembered position.
    /// </summary>
    [Fact]
    public void Group_Bounds_EmptyGroup_FixedBoxAtCollapsedPosition()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        var position = group.CollapsedPosition;

        // Act
        group.RemoveMember(a);

        // Assert
        Assert.Empty(group.Members);
        Assert.Equal(new Rect(position, new Size(diagram.Theme.CollapsedGroupWidth, diagram.Theme.CollapsedGroupHeight)), group.Bounds);
        Assert.Empty(group.AllMembers());
    }

    /// <summary>
    ///     Validates that expanded bounds follow the members when a member joins or leaves the group.
    /// </summary>
    [Fact]
    public void Group_Bounds_MemberJoinsOrLeaves_BoundsFollowMembers()
    {
        // Arrange
        var (_, page, a, _, c, _) = Create();
        var group = page.CreateGroup([a], "G");
        var narrow = group.Bounds;

        // Act
        group.AddMember(c);
        var wide = group.Bounds;
        group.RemoveMember(c);

        // Assert
        Assert.True(wide.Width > narrow.Width);
        Assert.Equal(narrow, group.Bounds);
    }

    /// <summary>
    ///     Validates that elements of a collapsed group are not visible while the group itself stays visible.
    /// </summary>
    [Fact]
    public void Group_IsCollapsed_NestedMember_IsNotVisibleUntilExpanded()
    {
        // Arrange
        var (_, page, a, b, _, note) = Create();
        var group = page.CreateGroup([a, b, note], "G");

        // Act
        group.IsCollapsed = true;

        // Assert
        Assert.All(group.Members, m => Assert.False(m.IsVisible));
        Assert.True(group.IsVisible);

        // Act
        group.IsCollapsed = false;

        // Assert
        Assert.All(group.Members, m => Assert.True(m.IsVisible));
    }

    /// <summary>
    ///     Validates that assigning the current title records nothing.
    /// </summary>
    [Fact]
    public void Group_Title_SameValue_RecordsNothing()
    {
        // Arrange
        var (diagram, page, a, _, _, _) = Create();
        var group = page.CreateGroup([a], "Same");
        var entries = diagram.History.UndoCount;

        // Act
        group.Title = "Same";

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that a removed group rejects title, collapse and membership changes until undo restores it.
    /// </summary>
    [Fact]
    public void Group_Setters_RemovedGroup_ThrowInvalidOperationExceptionUntilUndo()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        group.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => group.Title = "X");
        Assert.Throws<InvalidOperationException>(() => group.IsCollapsed = true);
        Assert.Throws<InvalidOperationException>(() => group.AddMember(b));
        Assert.Throws<InvalidOperationException>(() => group.RemoveMember(a));
        diagram.History.Undo();
        group.Title = "X";
        Assert.Equal("X", group.Title);
    }

    /// <summary>
    ///     Validates that title, collapse and membership changes raise PropertyChanged on the group, also on undo.
    /// </summary>
    [Fact]
    public void Group_PropertyChanged_TitleCollapseAndMembers_RaisedForSetAndUndo()
    {
        // Arrange
        var (diagram, page, a, b, _, _) = Create();
        var group = page.CreateGroup([a], "G");
        var raised = new List<string?>();
        group.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        group.Title = "New";
        group.IsCollapsed = true;
        group.IsCollapsed = false;
        group.AddMember(b);

        // Assert
        Assert.Contains(nameof(Group.Title), raised);
        Assert.Contains(nameof(Group.IsCollapsed), raised);
        Assert.Contains(nameof(Group.Members), raised);
        Assert.Contains(nameof(Group.Bounds), raised);

        // Act
        raised.Clear();
        diagram.History.Undo();

        // Assert
        Assert.Contains(nameof(Group.Members), raised);
        Assert.Contains(nameof(Group.Bounds), raised);
    }

    /// <summary>
    ///     Validates that a membership change in a nested group is reported as a bounds change of every ancestor group.
    /// </summary>
    [Fact]
    public void Group_PropertyChanged_MemberJoinsNestedGroup_ReportsBoundsOnAncestors()
    {
        // Arrange
        var (_, page, a, b, c, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");
        var outerRaised = new List<string?>();
        var innerRaised = new List<string?>();
        outer.PropertyChanged += (_, e) => outerRaised.Add(e.PropertyName);
        inner.PropertyChanged += (_, e) => innerRaised.Add(e.PropertyName);

        // Act
        inner.AddMember(a);

        // Assert
        Assert.Contains(nameof(Group.Bounds), innerRaised);
        Assert.Contains(nameof(Group.Bounds), outerRaised);
    }
}
