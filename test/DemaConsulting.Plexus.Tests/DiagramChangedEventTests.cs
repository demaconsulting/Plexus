using System.Collections.Specialized;
using System.ComponentModel;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="Diagram.Changed"/>: it must fire for every applied change, including undo and redo.
/// </summary>
public class DiagramChangedEventTests
{
    /// <summary>
    ///     Subscribes to a diagram's Changed event and collects the notifications.
    /// </summary>
    /// <param name="diagram">The diagram to observe.</param>
    /// <returns>The live list of received notifications.</returns>
    private static List<DiagramChangedEventArgs> Observe(Diagram diagram)
    {
        var list = new List<DiagramChangedEventArgs>();
        diagram.Changed += (_, e) => list.Add(e);
        return list;
    }

    /// <summary>
    ///     Validates that adding a page raises PageAdded and removing it raises PageRemoved, including via undo and redo.
    /// </summary>
    [Fact]
    public void Diagram_Changed_PageAddRemoveUndoRedo_RaisesPageEvents()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var events = Observe(diagram);

        // Act
        var page = diagram.Pages.Add("P");
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal(
            new[] { DiagramChangeKind.PageAdded, DiagramChangeKind.PageRemoved, DiagramChangeKind.PageAdded },
            events.Select(e => e.Kind));
        Assert.All(events, e => Assert.Same(page, e.Page));
    }

    /// <summary>
    ///     Validates that page rename and reorder raise their kinds.
    /// </summary>
    [Fact]
    public void Diagram_Changed_PageRenameAndMove_RaiseDedicatedKinds()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var a = diagram.Pages.Add("A");
        diagram.Pages.Add("B");
        var events = Observe(diagram);

        // Act
        a.Name = "A2";
        diagram.Pages.Move(a, 1);

        // Assert
        Assert.Equal(new[] { DiagramChangeKind.PageRenamed, DiagramChangeKind.PageMoved }, events.Select(e => e.Kind));
        Assert.Equal("Name", events[0].PropertyName);
    }

    /// <summary>
    ///     Validates that adding and removing elements raises ElementAdded and ElementRemoved with the element.
    /// </summary>
    [Fact]
    public void Diagram_Changed_AddAndRemoveElement_RaisesElementEvents()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var events = Observe(diagram);

        // Act
        var node = page.AddNode("acme.Producer", 0, 0);
        page.Remove(node);

        // Assert
        Assert.Equal(new[] { DiagramChangeKind.ElementAdded, DiagramChangeKind.ElementRemoved }, events.Select(e => e.Kind));
        Assert.All(events, e =>
        {
            Assert.Same(node, e.Element);
            Assert.Same(page, e.Page);
        });
    }

    /// <summary>
    ///     Validates that undo and redo of an element property change raise ElementChanged.
    /// </summary>
    [Fact]
    public void Diagram_Changed_UndoRedoOfPropertyChange_RaisesElementChanged()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var note = diagram.Pages.Add("P").AddAnnotation("a", new Rect(0, 0, 10, 10));
        var events = Observe(diagram);

        // Act
        note.Text = "b";
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal(3, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(DiagramChangeKind.ElementChanged, e.Kind);
            Assert.Equal("Text", e.PropertyName);
            Assert.Same(note, e.Element);
        });
    }

    /// <summary>
    ///     Validates that renaming a node raises ElementChanged for Name.
    /// </summary>
    [Fact]
    public void Diagram_Changed_NodeRename_RaisesNameChange()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("P").AddNode("acme.Producer", 0, 0);
        var events = Observe(diagram);

        // Act
        node.Name = "Renamed";

        // Assert
        var change = Assert.Single(events);
        Assert.Equal("Name", change.PropertyName);
    }

    /// <summary>
    ///     Validates that setting theme, router, rules and uniqueness raise SettingsChanged with the setting name.
    /// </summary>
    [Fact]
    public void Diagram_Changed_SettingsChange_RaisesSettingsChanged()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var events = Observe(diagram);

        // Act
        diagram.Theme = new Scene.DiagramTheme();
        diagram.Router = new Routing.SimpleRouter();
        diagram.ConnectionRules = ConnectionRules.SameType;
        diagram.RequireUniqueNodeNames = false;

        // Assert
        Assert.All(events, e => Assert.Equal(DiagramChangeKind.SettingsChanged, e.Kind));
        Assert.Equal(new[] { "Theme", "Router", "ConnectionRules", "RequireUniqueNodeNames" }, events.Select(e => e.PropertyName));
        Assert.Throws<ArgumentNullException>(() => diagram.Router = null!);
    }

    /// <summary>
    ///     Validates that a multi-step operation (move to page) raises events for every step and undo raises events too.
    /// </summary>
    [Fact]
    public void Diagram_Changed_MoveToPageAndUndo_RaisesRemovalAndAdditionEvents()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");
        var node = one.AddNode("acme.Producer", 0, 0);
        var events = Observe(diagram);

        // Act
        diagram.MoveToPage([node], two);
        var afterMove = events.Count;
        diagram.History.Undo();

        // Assert
        Assert.Equal(2, afterMove);
        Assert.Equal(DiagramChangeKind.ElementRemoved, events[0].Kind);
        Assert.Equal(DiagramChangeKind.ElementAdded, events[1].Kind);
        Assert.Equal(4, events.Count);
    }

    /// <summary>
    ///     Validates that node and group property notifications reach PropertyChanged subscribers.
    /// </summary>
    [Fact]
    public void DiagramElement_PropertyChanged_RaisedForGroupAndWireChanges()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("P");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 200, 0);
        var wire = page.Connect(a.Port("value"), b.Port("in"));
        var group = page.CreateGroup([a], "G");
        var wireProps = new List<string?>();
        var groupProps = new List<string?>();
        wire.PropertyChanged += (_, e) => wireProps.Add(e.PropertyName);
        group.PropertyChanged += (_, e) => groupProps.Add(e.PropertyName);

        // Act
        wire.SetRoute([new Point(100, 100)]);
        group.IsCollapsed = true;
        group.Title = "H";

        // Assert
        Assert.Contains("Route", wireProps);
        Assert.Contains("IsCollapsed", groupProps);
        Assert.Contains("Bounds", groupProps);
        Assert.Contains("Title", groupProps);
    }

    /// <summary>
    ///     Creates a diagram with one page holding a producer, a gain and a consumer wired in a chain.
    /// </summary>
    /// <returns>The diagram, the page and the gain node in the middle.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Gain) CreateChain()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0, "P");
        var gain = page.AddNode("acme.Gain", 200, 0, "G");
        var consumer = page.AddNode("acme.Consumer", 400, 0, "C");
        page.Connect(producer.Output("value"), gain.Input("in"));
        page.Connect(gain.Output("out"), consumer.Input("in"));
        return (diagram, page, gain);
    }

    /// <summary>
    ///     Validates that a throwing Changed handler does not stop an add: the node is added and recorded, then the exception is rethrown.
    /// </summary>
    [Fact]
    public void Diagram_Changed_HandlerThrowsOnAdd_NodeIsAddedRecordedAndExceptionRethrown()
    {
        // Arrange: a handler that always throws the same exception instance
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var failure = new InvalidOperationException("handler failed");
        EventHandler<DiagramChangedEventArgs> handler = (_, _) => throw failure;
        diagram.Changed += handler;
        var entries = diagram.History.UndoCount;

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Producer", 0, 0, "P"));

        // Assert: the very handler exception reached the caller after the node was added and recorded
        Assert.Same(failure, thrown);
        var node = Assert.Single(page.Nodes);
        Assert.Same(node, diagram.FindNode("P"));
        Assert.Equal(entries + 1, diagram.History.UndoCount);

        // Assert: the entry is a complete add (undo removes the node again)
        diagram.Changed -= handler;
        Assert.True(diagram.History.Undo());
        Assert.Empty(page.Nodes);
    }

    /// <summary>
    ///     Validates that a throwing Changed handler during a multi-step removal leaves one complete undo entry.
    /// </summary>
    [Fact]
    public void Diagram_Changed_HandlerThrowsDuringMultiStepOperation_IsOneCompleteEntry()
    {
        // Arrange: remove a node with two wires while a handler throws on every removal
        var (diagram, page, gain) = CreateChain();
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;
        var failure = new InvalidOperationException("handler failed");
        EventHandler<DiagramChangedEventArgs> handler = (_, e) =>
        {
            if (e.Kind == DiagramChangeKind.ElementRemoved)
            {
                throw failure;
            }
        };
        diagram.Changed += handler;

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() => page.Remove(gain));

        // Assert: the node and both wires are gone, as one entry
        Assert.Same(failure, thrown);
        Assert.Null(gain.Page);
        Assert.Empty(page.Wires);
        Assert.Equal(entries + 1, diagram.History.UndoCount);

        // Assert: one undo restores everything exactly
        diagram.Changed -= handler;
        Assert.True(diagram.History.Undo());
        Assert.Equal(before, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Validates that throwing handlers of every other change notification do not stop the change from being completed and recorded.
    /// </summary>
    [Fact]
    public void Diagram_PropertyChanged_HandlersThrow_ChangesAreRecorded()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var failure = new InvalidOperationException("handler failed");

        // Act / Assert: an element PropertyChanged handler; the move is applied and recorded
        PropertyChangedEventHandler elementHandler = (_, _) => throw failure;
        node.PropertyChanged += elementHandler;
        var entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => node.MoveTo(50, 60)));
        Assert.Equal(new Point(50, 60), node.Bounds.Position);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        node.PropertyChanged -= elementHandler;

        // Act / Assert: a property-bag PropertyChanged handler; the value is set and recorded
        PropertyChangedEventHandler bagHandler = (_, _) => throw failure;
        node.Properties.PropertyChanged += bagHandler;
        entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => node.Properties["Gain"] = 2.0));
        Assert.Equal(2.0, node.Properties["Gain"]);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        node.Properties.PropertyChanged -= bagHandler;

        // Act / Assert: a page content CollectionChanged handler; the node is added, findable and recorded
        NotifyCollectionChangedEventHandler nodesHandler = (_, _) => throw failure;
        ((INotifyCollectionChanged)page.Nodes).CollectionChanged += nodesHandler;
        entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Consumer", 200, 0, "C")));
        Assert.Equal(2, page.Nodes.Count);
        Assert.NotNull(diagram.FindNode("C"));
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        ((INotifyCollectionChanged)page.Nodes).CollectionChanged -= nodesHandler;

        // Act / Assert: a page-list CollectionChanged handler; the page is added, attached and recorded
        NotifyCollectionChangedEventHandler pagesHandler = (_, _) => throw failure;
        diagram.Pages.CollectionChanged += pagesHandler;
        entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => diagram.Pages.Add("Second")));
        Assert.Equal(2, diagram.Pages.Count);
        Assert.True(diagram.Pages[1].IsAttached);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        diagram.Pages.CollectionChanged -= pagesHandler;

        // Act / Assert: a NodeRenamed handler; the node is renamed and recorded
        EventHandler<NodeRenamedEventArgs> renamedHandler = (_, _) => throw failure;
        diagram.NodeRenamed += renamedHandler;
        entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => node.Name = "Feed"));
        Assert.Equal("Feed", node.Name);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        diagram.NodeRenamed -= renamedHandler;

        // Act / Assert: a page PropertyChanged handler; the page is renamed and recorded
        PropertyChangedEventHandler pageHandler = (_, _) => throw failure;
        page.PropertyChanged += pageHandler;
        entries = diagram.History.UndoCount;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => page.Name = "Renamed"));
        Assert.Equal("Renamed", page.Name);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        page.PropertyChanged -= pageHandler;
    }

    /// <summary>
    ///     Validates that undo and redo complete and move their entry when a Changed handler throws.
    /// </summary>
    [Fact]
    public void Diagram_Changed_HandlerThrowsOnUndoAndRedo_EntryMoves()
    {
        // Arrange: a recorded add, then a handler that throws on every notification
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var undoCount = diagram.History.UndoCount;
        var failure = new InvalidOperationException("handler failed");
        diagram.Changed += (_, _) => throw failure;

        // Act / Assert: the undo removes the node and moves its entry to the redo stack before the exception is reported
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => diagram.History.Undo()));
        Assert.Null(node.Page);
        Assert.Equal((undoCount - 1, 1), (diagram.History.UndoCount, diagram.History.RedoCount));

        // Act / Assert: the redo adds the node back and moves the entry back
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => diagram.History.Redo()));
        Assert.Same(page, node.Page);
        Assert.Equal((undoCount, 0), (diagram.History.UndoCount, diagram.History.RedoCount));
    }

    /// <summary>
    ///     Validates that a Changed handler exception inside a transaction rolls the whole transaction back and propagates.
    /// </summary>
    [Fact]
    public void Diagram_Changed_HandlerThrowsInsideTransact_TransactionIsRolledBack()
    {
        // Arrange: a handler that throws when a node is added
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;
        var failure = new InvalidOperationException("handler failed");
        diagram.Changed += (_, e) =>
        {
            if (e.Kind == DiagramChangeKind.ElementAdded)
            {
                throw failure;
            }
        };

        // Act: rename, then add (which throws after completing) inside one transaction
        var thrown = Assert.Throws<InvalidOperationException>(() => diagram.History.Transact("Edit", () =>
        {
            node.Name = "Feed";
            page.AddNode("acme.Consumer", 200, 0, "C");
        }));

        // Assert: the handler's exception propagated and the whole transaction was rolled back
        Assert.Same(failure, thrown);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.False(diagram.History.IsInTransaction);
    }

    /// <summary>
    ///     Validates that when several handlers of one change throw, the first exception is reported and the change completes.
    /// </summary>
    [Fact]
    public void Diagram_Changed_TwoHandlerFailures_FirstIsRethrown()
    {
        // Arrange: the element handler (raised first) and the diagram handler (raised second) both throw
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Producer", 0, 0, "P");
        var first = new InvalidOperationException("element handler failed");
        var second = new ArgumentException("diagram handler failed");
        node.PropertyChanged += (_, _) => throw first;
        diagram.Changed += (_, _) => throw second;
        var renamed = 0;
        diagram.NodeRenamed += (_, _) => renamed++;

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() => node.Name = "Feed");

        // Assert: the first exception is reported; the later notification still ran and the rename is complete
        Assert.Same(first, thrown);
        Assert.Equal(1, renamed);
        Assert.Equal("Feed", node.Name);
    }

    /// <summary>
    ///     Validates that when the change itself fails, its exception (not a handler's) propagates and nothing is recorded.
    /// </summary>
    [Fact]
    public void Diagram_Changed_OperationFailsAndHandlerThrows_OperationExceptionPropagates()
    {
        // Arrange: a node whose port provider fails for Mode 1, and a handler that throws on every notification
        var types = new NodeTypeRegistry();
        types.Add(new NodeType("acme.Fragile")
        {
            Properties = { new PropertyDefinition("Mode", 0) },
            PortProvider = node => node.Properties.Get<int>("Mode") == 0
                ? new PortSet([], [])
                : throw new NotSupportedException("mode not supported"),
        });
        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode("acme.Fragile", 0, 0);
        var entries = diagram.History.UndoCount;
        diagram.Changed += (_, _) => throw new InvalidOperationException("handler failed");

        // Act
        Assert.Throws<NotSupportedException>(() => node.Properties["Mode"] = 1);

        // Assert: the change was rolled back and nothing recorded
        Assert.Equal(0, node.Properties.Get<int>("Mode"));
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that the diagram version advances for every notification even when a handler throws.
    /// </summary>
    [Fact]
    public void Diagram_Changed_HandlerThrows_VersionStillAdvances()
    {
        // Arrange: a counting handler first, then a throwing one
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var notifications = 0;
        diagram.Changed += (_, _) => notifications++;
        diagram.Changed += (_, _) => throw new InvalidOperationException("handler failed");
        var version = diagram.Version;

        // Act
        Assert.Throws<InvalidOperationException>(() => page.AddNode("acme.Producer", 0, 0, "P"));

        // Assert: one version step per notification
        Assert.True(notifications > 0);
        Assert.Equal(version + notifications, diagram.Version);
    }
}
