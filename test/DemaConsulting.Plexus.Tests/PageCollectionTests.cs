using System.Collections.Specialized;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="PageCollection"/> and <see cref="DiagramPage"/> naming and lifetime.
/// </summary>
public class PageCollectionTests
{
    /// <summary>
    ///     Validates that Add appends a page and Insert places it at the requested index, both undoable.
    /// </summary>
    [Fact]
    public void PageCollection_AddAndInsert_PlacePagesAndRoundTrip()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        DiagramPage? first = null;
        DiagramPage? inserted = null;

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => first = diagram.Pages.Add("First"));
        TestModel.AssertRoundTrip(diagram, () => inserted = diagram.Pages.Insert(0, "Zero"));
        Assert.Equal(new[] { inserted, first }, diagram.Pages);
        Assert.True(first!.IsAttached);
        Assert.Same(diagram, first.Diagram);
        Assert.Equal(0, diagram.Pages.IndexOf(inserted!));
        Assert.Equal("Add page", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates argument validation of Add and Insert.
    /// </summary>
    [Fact]
    public void PageCollection_AddAndInsert_InvalidArguments_Throw()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => diagram.Pages.Add(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => diagram.Pages.Insert(1, "X"));
        Assert.Throws<ArgumentOutOfRangeException>(() => diagram.Pages.Insert(-1, "X"));
    }

    /// <summary>
    ///     Validates that removing a page detaches its content and undo restores the same page object and content.
    /// </summary>
    [Fact]
    public void PageCollection_Remove_DetachesContentAndUndoRestoresIt()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var keep = diagram.Pages.Add("Keep");
        var page = diagram.Pages.Add("Doomed");
        var node = page.AddNode("acme.Producer", 0, 0, "Gone");
        var wire = page.Connect(node.Port("value"), page.AddNode("acme.Consumer", 200, 0).Port("in"));

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => Assert.True(diagram.Pages.Remove(page)));
        Assert.Equal(new[] { keep }, diagram.Pages);
        Assert.False(page.IsAttached);
        Assert.Null(diagram.FindElement(node.Id));
        Assert.Null(diagram.FindNode("Gone"));
        diagram.History.Undo();
        Assert.True(page.IsAttached);
        Assert.Same(node, diagram.FindElement(node.Id));
        Assert.Same(wire, diagram.FindElement(wire.Id));
        Assert.Same(node, diagram.FindNode("Gone"));
    }

    /// <summary>
    ///     Validates that Remove returns false for a page that is not in the collection.
    /// </summary>
    [Fact]
    public void PageCollection_Remove_PageNotInDiagram_ReturnsFalse()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var foreign = TestModel.CreateDiagram().Pages.Add("Foreign");

        // Act / Assert
        Assert.False(diagram.Pages.Remove(foreign));
        Assert.Throws<ArgumentNullException>(() => diagram.Pages.Remove(null!));
    }

    /// <summary>
    ///     Validates that a removed page cannot be modified until restored.
    /// </summary>
    [Fact]
    public void DiagramPage_Mutation_WhenRemoved_ThrowsInvalidOperationException()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("P");
        diagram.Pages.Remove(page);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Producer", 0, 0));
        Assert.Throws<InvalidOperationException>(() => page.AddAnnotation("x", new Rect(0, 0, 1, 1)));
        Assert.Throws<InvalidOperationException>(() => page.Name = "Other");
        Assert.Throws<InvalidOperationException>(() => page.Move([], 1, 1));
    }

    /// <summary>
    ///     Validates that Move reorders pages undoably and validates its arguments.
    /// </summary>
    [Fact]
    public void PageCollection_Move_ReordersPagesAndRoundTrips()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var a = diagram.Pages.Add("A");
        var b = diagram.Pages.Add("B");
        var c = diagram.Pages.Add("C");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => diagram.Pages.Move(a, 2));
        Assert.Equal(new[] { b, c, a }, diagram.Pages);
        var entries = diagram.History.UndoCount;
        diagram.Pages.Move(a, 2);
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => diagram.Pages.Move(a, 3));
        Assert.Throws<ArgumentException>(() => diagram.Pages.Move(TestModel.CreateDiagram().Pages.Add("Z"), 0));
        Assert.Throws<ArgumentNullException>(() => diagram.Pages.Move(null!, 0));
    }

    /// <summary>
    ///     Validates that renaming a page is undoable, validated and no-op for the same name.
    /// </summary>
    [Fact]
    public void DiagramPage_Name_Set_UndoRedoRoundTripAndValidation()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Old");

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Name = "New");
        var entries = diagram.History.UndoCount;
        page.Name = "New";
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<ArgumentException>(() => page.Name = "");
    }

    /// <summary>
    ///     Validates that the collection raises INotifyCollectionChanged with itself as the sender.
    /// </summary>
    [Fact]
    public void PageCollection_CollectionChanged_RaisedForAddRemoveMove()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var actions = new List<NotifyCollectionChangedAction>();
        object? sender = null;
        diagram.Pages.CollectionChanged += (s, e) =>
        {
            sender = s;
            actions.Add(e.Action);
        };

        // Act
        var a = diagram.Pages.Add("A");
        var b = diagram.Pages.Add("B");
        diagram.Pages.Move(a, 1);
        diagram.Pages.Remove(b);

        // Assert
        Assert.Equal(
            new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Remove },
            actions);
        Assert.Same(diagram.Pages, sender);
    }

    /// <summary>
    ///     Validates that the content lists of a page raise collection notifications.
    /// </summary>
    [Fact]
    public void DiagramPage_ContentLists_AreObservable()
    {
        // Arrange
        var page = TestModel.CreateDiagram().Pages.Add("P");
        var nodeEvents = 0;
        var elementEvents = 0;
        ((INotifyCollectionChanged)page.Nodes).CollectionChanged += (_, _) => nodeEvents++;
        ((INotifyCollectionChanged)page.Elements).CollectionChanged += (_, _) => elementEvents++;

        // Act
        page.AddNode("acme.Producer", 0, 0);

        // Assert
        Assert.Equal(1, nodeEvents);
        Assert.Equal(1, elementEvents);
    }

    /// <summary>
    ///     Validates the host data payload: replace semantics and undo.
    /// </summary>
    [Fact]
    public void Node_Data_Set_UndoRedoRoundTrip()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("P").AddNode("acme.Producer", 0, 0);

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => node.Data = new { Value = 1 });
        TestModel.AssertRoundTrip(diagram, () => node.Data = "text");
        TestModel.AssertRoundTrip(diagram, () => node.Data = null);
        Assert.Null(node.Data);
    }

    /// <summary>
    ///     Validates that assigning an equal data object records nothing.
    /// </summary>
    [Fact]
    public void Node_Data_SetEqualValue_RecordsNothing()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("P").AddNode("acme.Producer", 0, 0);
        node.Data = "same";
        var entries = diagram.History.UndoCount;

        // Act
        node.Data = "same";

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates annotation text and fill edits are undoable.
    /// </summary>
    [Fact]
    public void Annotation_TextAndFill_Set_UndoRedoRoundTrip()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var note = diagram.Pages.Add("P").AddAnnotation("old", new Rect(0, 0, 10, 10));

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => note.Text = "new");
        TestModel.AssertRoundTrip(diagram, () => note.Fill = Color.Red);
        TestModel.AssertRoundTrip(diagram, () => note.Fill = null);
        Assert.Throws<ArgumentNullException>(() => note.Text = null!);
    }
}
