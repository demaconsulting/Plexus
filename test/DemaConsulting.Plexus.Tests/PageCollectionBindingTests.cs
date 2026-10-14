using System.Collections;
using System.Collections.Specialized;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for binding <see cref="PageCollection"/> to UI item controls (non-generic read-only
///     <see cref="IList"/>) and for the "fully detached before notification" ordering of removal events.
/// </summary>
public class PageCollectionBindingTests
{
    /// <summary>
    ///     Validates that the collection is a read-only non-generic list that reflects the pages.
    /// </summary>
    [Fact]
    public void PageCollection_AsIList_ReflectsPagesAndIsReadOnly()
    {
        // Arrange: a diagram with two pages
        var diagram = TestModel.CreateDiagram();
        var first = diagram.Pages.Add("First");
        var second = diagram.Pages.Add("Second");

        // Act: view the collection through the non-generic interfaces an items control uses
        IList list = diagram.Pages;
        ICollection collection = diagram.Pages;
        var copy = new object[2];
        collection.CopyTo(copy, 0);

        // Assert
        Assert.Multiple(
            () => Assert.True(list.IsReadOnly),
            () => Assert.True(list.IsFixedSize),
            () => Assert.False(collection.IsSynchronized),
            () => Assert.NotNull(collection.SyncRoot),
            () => Assert.Equal(2, list.Count),
            () => Assert.Same(first, list[0]),
            () => Assert.Same(second, list[1]),
            () => Assert.Equal(1, list.IndexOf(second)),
            () => Assert.Equal(-1, list.IndexOf("not a page")),
            () => Assert.True(list.Contains(first)),
            () => Assert.False(list.Contains(null)),
            () => Assert.Equal<object[]>([first, second], copy),
            () => Assert.True(diagram.Pages.Contains(second)));
    }

    /// <summary>
    ///     Validates that every non-generic mutator throws NotSupportedException and changes nothing.
    /// </summary>
    [Fact]
    public void PageCollection_AsIList_Mutators_ThrowNotSupported()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Only");
        IList list = diagram.Pages;

        // Act / Assert: the undoable PageCollection API is the only way to change the list
        Assert.Multiple(
            () => Assert.Throws<NotSupportedException>(() => list.Add(page)),
            () => Assert.Throws<NotSupportedException>(() => list.Insert(0, page)),
            () => Assert.Throws<NotSupportedException>(() => list.Remove(page)),
            () => Assert.Throws<NotSupportedException>(() => list.RemoveAt(0)),
            () => Assert.Throws<NotSupportedException>(() => list.Clear()),
            () => Assert.Throws<NotSupportedException>(() => list[0] = page),
            () => Assert.Single(diagram.Pages));
    }

    /// <summary>
    ///     Validates that CollectionChanged reports adds, moves and removes with this collection as the sender, so a bound control stays in sync.
    /// </summary>
    [Fact]
    public void PageCollection_CollectionChanged_ReportsAddMoveRemoveWithListInSync()
    {
        // Arrange: a listener that records the action and the list contents at notification time
        var diagram = TestModel.CreateDiagram();
        var seen = new List<(NotifyCollectionChangedAction Action, string Names, object? Sender)>();
        diagram.Pages.CollectionChanged += (sender, e) =>
            seen.Add((e.Action, string.Join(',', diagram.Pages.Select(p => p.Name)), sender));

        // Act
        var a = diagram.Pages.Add("A");
        diagram.Pages.Add("B");
        diagram.Pages.Move(a, 1);
        diagram.Pages.Remove(a);

        // Assert: the list already holds the new state whenever the event is raised
        Assert.Equal(
            [
                (NotifyCollectionChangedAction.Add, "A", (object?)diagram.Pages),
                (NotifyCollectionChangedAction.Add, "A,B", diagram.Pages),
                (NotifyCollectionChangedAction.Move, "B,A", diagram.Pages),
                (NotifyCollectionChangedAction.Remove, "B", diagram.Pages),
            ],
            seen);
    }

    /// <summary>
    ///     Validates that a removed page is detached (IsAttached false, elements unregistered) before CollectionChanged and Diagram.Changed fire.
    /// </summary>
    [Fact]
    public void PageCollection_Remove_PageIsDetachedBeforeNotifications()
    {
        // Arrange: a page with a node, and observers capturing the page state when they are called
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var attachedInCollectionEvent = true;
        var attachedInChangedEvent = true;
        DiagramElement? foundInCollectionEvent = node;
        diagram.Pages.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                attachedInCollectionEvent = page.IsAttached;
                foundInCollectionEvent = diagram.FindElement(node.Id);
            }
        };
        diagram.Changed += (_, e) =>
        {
            if (e.Kind == DiagramChangeKind.PageRemoved)
            {
                attachedInChangedEvent = e.Page!.IsAttached;
            }
        };

        // Act
        diagram.Pages.Remove(page);

        // Assert
        Assert.Multiple(
            () => Assert.False(attachedInCollectionEvent),
            () => Assert.False(attachedInChangedEvent),
            () => Assert.Null(foundInCollectionEvent));
    }

    /// <summary>
    ///     Validates that undoing a page removal re-attaches the page before the add notifications fire.
    /// </summary>
    [Fact]
    public void PageCollection_UndoRemove_PageIsAttachedBeforeNotifications()
    {
        // Arrange: a removed page and observers capturing its state on re-add
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        diagram.Pages.Remove(page);
        var attachedInCollectionEvent = false;
        DiagramElement? foundInCollectionEvent = null;
        diagram.Pages.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                attachedInCollectionEvent = page.IsAttached;
                foundInCollectionEvent = diagram.FindElement(node.Id);
            }
        };

        // Act
        diagram.History.Undo();

        // Assert
        Assert.Multiple(
            () => Assert.True(attachedInCollectionEvent),
            () => Assert.Same(node, foundInCollectionEvent));
    }

    /// <summary>
    ///     Validates that a removed element is detached (no page, not findable) before the page's list event and Diagram.Changed fire.
    /// </summary>
    [Fact]
    public void DiagramPage_Remove_ElementIsDetachedBeforeNotifications()
    {
        // Arrange: a node with observers on the node list and on Diagram.Changed
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var pageInListEvent = new List<DiagramPage?> { node.Page };
        var foundInListEvent = new List<DiagramElement?> { node };
        var pageInChangedEvent = new List<DiagramPage?> { node.Page };
        ((INotifyCollectionChanged)page.Nodes).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                pageInListEvent.Add(node.Page);
                foundInListEvent.Add(diagram.FindElement(node.Id));
            }
        };
        diagram.Changed += (_, e) =>
        {
            if (e.Kind == DiagramChangeKind.ElementRemoved)
            {
                pageInChangedEvent.Add(e.Element!.Page);
            }
        };

        // Act
        page.Remove(node);

        // Assert: the entries recorded during the removal show a detached element
        Assert.Multiple(
            () => Assert.Null(pageInListEvent[^1]),
            () => Assert.Null(foundInListEvent[^1]),
            () => Assert.Null(pageInChangedEvent[^1]));
    }

    /// <summary>
    ///     Validates that an element restored by undo is fully attached (page set, findable) before the add notifications fire.
    /// </summary>
    [Fact]
    public void DiagramPage_UndoRemove_ElementIsAttachedBeforeNotifications()
    {
        // Arrange: a removed node and an observer capturing its state on re-add
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        page.Remove(node);
        DiagramPage? pageInListEvent = null;
        DiagramElement? foundInListEvent = null;
        ((INotifyCollectionChanged)page.Nodes).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                pageInListEvent = node.Page;
                foundInListEvent = diagram.FindElement(node.Id);
            }
        };

        // Act
        diagram.History.Undo();

        // Assert
        Assert.Multiple(
            () => Assert.Same(page, pageInListEvent),
            () => Assert.Same(node, foundInListEvent));
    }

    /// <summary>
    ///     Validates that a member leaving a group has no parent yet when the Members list event fires.
    /// </summary>
    [Fact]
    public void Group_RemoveMember_ParentIsClearedBeforeMembersEvent()
    {
        // Arrange: a group with one member and an observer on the member list
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var group = page.CreateGroup([node], "G");
        Group? parentInEvent = group;
        ((INotifyCollectionChanged)group.Members).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                parentInEvent = node.Parent;
            }
        };

        // Act
        group.RemoveMember(node);

        // Assert
        Assert.Null(parentInEvent);
    }

    /// <summary>
    ///     Validates that a drained removal still leaves the diagram consistent after undo (the reordering did not change the model).
    /// </summary>
    [Fact]
    public void DiagramPage_RemoveThenUndo_RestoresExactState()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var consumer = page.AddNode("acme.Consumer", 300, 0, "C");
        page.Connect(producer.Port("value"), consumer.Port("in"));
        page.CreateGroup([producer], "G");
        page.AddAnnotation("note", new Rect(0, 100, 50, 20));

        // Act / Assert: removal of a node (with its wire) and of the whole page both round-trip
        TestModel.AssertRoundTrip(diagram, () => page.Remove(producer));
        TestModel.AssertRoundTrip(diagram, () => diagram.Pages.Remove(page));
    }
}
