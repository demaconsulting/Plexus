using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for removing elements: wires follow nodes, groups remove frame only or with members, and undo restores everything.
/// </summary>
public class DiagramPageRemoveTests
{
    /// <summary>
    ///     Creates a page with a chain A -> B -> C (manual routes), an annotation and one extra wire D.
    /// </summary>
    /// <returns>The diagram, page, nodes, wires and annotation.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node A, Node B, Node C, Wire AB, Wire BC, Annotation Note) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0, "A");
        var b = page.AddNode("acme.Gain", 150, 0, "B");
        var c = page.AddNode("acme.Consumer", 300, 0, "C");
        var ab = page.Connect(a.Port("value"), b.Port("in"), [new Point(120, 70), new Point(120, 50)]);
        var bc = page.Connect(b.Port("out"), c.Port("in"), [new Point(270, 70), new Point(270, 50)]);
        var note = page.AddAnnotation("note", new Rect(0, 150, 100, 40));
        return (diagram, page, a, b, c, ab, bc, note);
    }

    /// <summary>
    ///     Validates that removing a node removes its wires and that undo restores ids, order and routes exactly.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_Node_RemovesAttachedWiresAndRoundTrips()
    {
        // Arrange
        var (diagram, page, _, b, _, ab, bc, _) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove(b));
        Assert.DoesNotContain(b, page.Nodes);
        Assert.Empty(page.Wires);
        Assert.Null(b.Page);
        Assert.Null(ab.Page);
        Assert.Null(bc.Page);
        Assert.Null(diagram.FindElement(b.Id));
        Assert.Equal("Remove node", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that undoing a removal re-attaches the same objects at the same positions.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_Node_UndoRestoresSameObjectsAtSamePositions()
    {
        // Arrange
        var (diagram, page, a, b, c, ab, bc, note) = Create();
        var zBefore = page.Elements.ToList();

        // Act
        page.Remove(b);
        diagram.History.Undo();

        // Assert
        Assert.Equal(new[] { a, b, c }, page.Nodes);
        Assert.Equal(new[] { ab, bc }, page.Wires);
        Assert.Equal(zBefore, page.Elements);
        Assert.Same(page, b.Page);
        Assert.Same(b, diagram.FindElement(b.Id));
        Assert.Equal(note, page.Annotations.Single());
    }

    /// <summary>
    ///     Validates that DiagramElement.Remove is a shortcut for DiagramPage.Remove.
    /// </summary>
    [Fact]
    public void DiagramElement_Remove_RemovesFromPage()
    {
        // Arrange
        var (diagram, page, _, _, _, _, _, note) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, note.Remove);
        Assert.Empty(page.Annotations);
        Assert.Throws<InvalidOperationException>(() => note.Remove());
    }

    /// <summary>
    ///     Validates that removing several elements is one undo entry.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_Multiple_IsSingleEntryAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, _, c, _, _, note) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove([c, a, note]));
        Assert.Single(page.Nodes);
        Assert.Equal("Remove 3 elements", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that removing nothing records nothing and that foreign or null elements are rejected.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_EmptyOrInvalid_NoEntryOrThrows()
    {
        // Arrange
        var (diagram, page, _, _, _, _, _, _) = Create();
        var foreign = diagram.Pages.Add("Other").AddNode("acme.Producer", 0, 0);
        var entries = diagram.History.UndoCount;

        // Act
        page.Remove(Array.Empty<DiagramElement>());

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<ArgumentNullException>(() => page.Remove((DiagramElement)null!));
        Assert.Throws<ArgumentNullException>(() => page.Remove((IEnumerable<DiagramElement>)null!));
        Assert.Throws<ArgumentException>(() => page.Remove(foreign));
        Assert.Throws<ArgumentException>(() => page.RemoveWithMembers(foreign));
    }

    /// <summary>
    ///     Validates that removing a group removes only the frame and keeps members, wires and positions.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_Group_RemovesFrameOnlyAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, _, _, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove(group));
        Assert.Contains(a, page.Nodes);
        Assert.Null(a.Parent);
        Assert.Equal(2, page.Wires.Count);
    }

    /// <summary>
    ///     Validates that RemoveWithMembers deletes a group's members and their wires.
    /// </summary>
    [Fact]
    public void DiagramPage_RemoveWithMembers_Group_RemovesMembersWiresAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, c, _, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.RemoveWithMembers(group));
        Assert.Equal(new[] { c }, page.Nodes);
        Assert.Empty(page.Wires);
        Assert.Empty(page.Groups);
    }

    /// <summary>
    ///     Validates that RemoveWithMembers handles nested groups and restores nesting on undo.
    /// </summary>
    [Fact]
    public void DiagramPage_RemoveWithMembers_NestedGroups_RemovesEverythingAndRoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, c, _, _, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        page.CreateGroup([b, c], "Inner");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.RemoveWithMembers(outer));
        Assert.Empty(page.Nodes);
        Assert.Empty(page.Groups);
        Assert.Empty(page.Wires);
    }

    /// <summary>
    ///     Validates that removing a node that is a group member shrinks the group and undo restores the member slot.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_GroupMember_UpdatesGroupAndRestoresSlotOnUndo()
    {
        // Arrange
        var (diagram, page, a, b, c, _, _, _) = Create();
        var group = page.CreateGroup([a, b, c], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove(b));
        Assert.Equal(new DiagramElement[] { a, c }, group.Members);
        diagram.History.Undo();
        Assert.Equal(new DiagramElement[] { a, b, c }, group.Members);
    }

    /// <summary>
    ///     Validates that removing a nested group frame inside an outer group releases its members into the outer group.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_NestedGroupFrame_MembersFlowToParent()
    {
        // Arrange
        var (diagram, page, a, b, c, _, _, _) = Create();
        var outer = page.CreateGroup([a, b, c], "Outer");
        var inner = page.CreateGroup([b, c], "Inner");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove(inner));
        Assert.Equal(new DiagramElement[] { a, b, c }, outer.Members);
    }

    /// <summary>
    ///     Validates that removing a group frame together with some of its members works in one operation.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_GroupAndOneMemberTogether_RoundTrips()
    {
        // Arrange
        var (diagram, page, a, b, _, _, _, _) = Create();
        var group = page.CreateGroup([a, b], "G");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Remove([group, a]));
        Assert.DoesNotContain(a, page.Nodes);
        Assert.Contains(b, page.Nodes);
        Assert.Null(b.Parent);
    }

    /// <summary>
    ///     Validates that a removed element cannot be mutated until the removal is undone.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_Element_MutationRejectedUntilUndo()
    {
        // Arrange
        var (diagram, page, _, _, _, _, _, note) = Create();
        page.Remove(note);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => note.Text = "x");
        diagram.History.Undo();
        note.Text = "ok";
        Assert.Equal("ok", note.Text);
    }
}
