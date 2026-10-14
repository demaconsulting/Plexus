using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="Diagram.MoveToPage"/>.
/// </summary>
public class MoveToPageTests
{
    /// <summary>
    ///     Creates two pages; page one holds A -> B -> C, a group of A and B, and an annotation.
    /// </summary>
    /// <returns>The diagram, both pages and the elements.</returns>
    private static (Diagram Diagram, DiagramPage One, DiagramPage Two, Node A, Node B, Node C, Wire AB, Wire BC, Annotation Note) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");
        var a = one.AddNode("acme.Producer", 0, 0, "A");
        var b = one.AddNode("acme.Gain", 150, 0, "B");
        var c = one.AddNode("acme.Consumer", 300, 0, "C");
        var ab = one.Connect(a.Port("value"), b.Port("in"), [new Point(120, 70), new Point(120, 50)]);
        var bc = one.Connect(b.Port("out"), c.Port("in"), [new Point(270, 70), new Point(270, 50)]);
        var note = one.AddAnnotation("note", new Rect(0, 150, 100, 40));
        return (diagram, one, two, a, b, c, ab, bc, note);
    }

    /// <summary>
    ///     Validates that moving a node removes wires to nodes left behind and that undo restores them.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_NodeWithOutsideWires_RemovesWiresAndUndoRestores()
    {
        // Arrange
        var (diagram, one, two, _, b, _, ab, bc, _) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([b], two));
        Assert.Equal(new[] { b }, two.Nodes);
        Assert.Empty(two.Wires);
        Assert.Empty(one.Wires);
        Assert.Same(two, b.Page);
        Assert.Null(ab.Page);
        Assert.Null(bc.Page);
        Assert.Equal("Move to page", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that wires among moved nodes move too and keep their routes and ids.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_NodesTogether_CarryWiresWithRoutes()
    {
        // Arrange
        var (diagram, one, two, a, b, _, ab, bc, _) = Create();
        var route = ab.Route.ToArray();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([a, b], two));
        Assert.Equal(new[] { ab }, two.Wires);
        Assert.Same(two, ab.Page);
        Assert.Equal(route, ab.Route);
        Assert.Null(bc.Page);
        Assert.Single(one.Nodes);
        Assert.Same(ab, diagram.FindElement(ab.Id));
    }

    /// <summary>
    ///     Validates that moving a group brings all members and internal wires, and group structure survives.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_Group_BringsMembersAndStructure()
    {
        // Arrange
        var (diagram, one, two, a, b, _, ab, _, _) = Create();
        var group = one.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([group], two));
        Assert.Equal(new[] { group }, two.Groups);
        Assert.Equal(new DiagramElement[] { a, b }, group.Members);
        Assert.Same(two, a.Page);
        Assert.Same(group, a.Parent);
        Assert.Same(two, ab.Page);
    }

    /// <summary>
    ///     Validates that a member moved out of a group that stays behind is released from that group.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_MemberOfStayingGroup_LeavesGroupAndUndoRestoresMembership()
    {
        // Arrange
        var (diagram, one, two, a, b, _, _, _, _) = Create();
        var group = one.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([b], two));
        Assert.Equal(new DiagramElement[] { a }, group.Members);
        Assert.Null(b.Parent);
        diagram.History.Undo();
        Assert.Equal(new DiagramElement[] { a, b }, group.Members);
    }

    /// <summary>
    ///     Validates that annotations move between pages unchanged.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_Annotation_MovesWithBounds()
    {
        // Arrange
        var (diagram, _, two, _, _, _, _, _, note) = Create();
        var bounds = note.Bounds;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([note], two));
        Assert.Equal(bounds, note.Bounds);
        Assert.Same(two, note.Page);
    }

    /// <summary>
    ///     Validates that elements already on the target page and wires in the list are ignored.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_ElementsAlreadyOnTargetOrWires_AreIgnored()
    {
        // Arrange
        var (diagram, one, _, a, _, _, ab, _, _) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        diagram.MoveToPage([a], one);
        diagram.MoveToPage([ab], diagram.Pages[1]);
        diagram.MoveToPage([], diagram.Pages[1]);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates argument validation of MoveToPage.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_InvalidArguments_Throw()
    {
        // Arrange
        var (diagram, one, _, a, _, _, _, _, _) = Create();
        var foreignPage = TestModel.CreateDiagram().Pages.Add("Foreign");
        var foreignNode = foreignPage.AddNode("acme.Producer", 0, 0);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => diagram.MoveToPage(null!, one));
        Assert.Throws<ArgumentNullException>(() => diagram.MoveToPage([a], null!));
        Assert.Throws<ArgumentException>(() => diagram.MoveToPage([a], foreignPage));
        Assert.Throws<ArgumentException>(() => diagram.MoveToPage([foreignNode], diagram.Pages[1]));
    }

    /// <summary>
    ///     Validates that elements from several source pages can be moved to one target in a single entry.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_FromSeveralPages_IsOneEntryAndRoundTrips()
    {
        // Arrange
        var (diagram, _, two, a, _, _, _, _, _) = Create();
        var three = diagram.Pages.Add("Three");
        var other = three.AddNode("acme.Producer", 0, 0, "Other");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.MoveToPage([a, other], two));
        Assert.Equal(2, two.Nodes.Count);
    }

    /// <summary>
    ///     Validates that node names stay unique and findable after a move.
    /// </summary>
    [Fact]
    public void Diagram_MoveToPage_KeepsNamesAndIdsFindable()
    {
        // Arrange
        var (diagram, _, two, _, b, _, _, _, _) = Create();

        // Act
        diagram.MoveToPage([b], two);

        // Assert
        Assert.Same(b, diagram.FindNode("B"));
        Assert.Same(b, diagram.FindElement(b.Id));
        Assert.Throws<ArgumentException>(() => two.AddNode("acme.Producer", 0, 0, "B"));
    }
}
