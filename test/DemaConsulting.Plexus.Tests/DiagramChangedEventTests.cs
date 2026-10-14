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
}
