using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for moving and resizing elements, including the translate-versus-reroute wire rule.
/// </summary>
public class DiagramPageMoveTests
{
    /// <summary>
    ///     Creates a page with a producer at (0, 0) and a consumer at (300, 0) joined by a wire with a manual route.
    /// </summary>
    /// <returns>The diagram, page, nodes and wire.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Consumer, Wire Wire) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 0, 0);
        var consumer = page.AddNode("acme.Consumer", 300, 0);
        var wire = page.Connect(producer.Port("value"), consumer.Port("in"), [new Point(200, 60), new Point(200, 10)]);
        return (diagram, page, producer, consumer, wire);
    }

    /// <summary>
    ///     Validates that Move shifts nodes and annotations by the delta and round-trips through undo.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_NodesAndAnnotations_ShiftsBoundsAndRoundTrips()
    {
        // Arrange
        var (diagram, page, producer, _, _) = Create();
        var note = page.AddAnnotation("n", new Rect(0, 200, 100, 40));

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => page.Move([producer, note], 15, -5));
        Assert.Equal(new Point(15, -5), producer.Position);
        Assert.Equal(new Rect(15, 195, 100, 40), note.Bounds);
        Assert.Equal("Move", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that a zero delta or an empty set records nothing.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_ZeroDeltaOrEmptySet_RecordsNothing()
    {
        // Arrange
        var (diagram, page, producer, _, _) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        page.Move([producer], 0, 0);
        page.Move([], 10, 10);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates argument validation of Move.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_InvalidArguments_Throw()
    {
        // Arrange
        var (diagram, page, producer, _, _) = Create();
        var foreign = diagram.Pages.Add("Other").AddNode("acme.Producer", 0, 0);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => page.Move(null!, 1, 1));
        Assert.Throws<ArgumentException>(() => page.Move([producer], double.NaN, 1));
        Assert.Throws<ArgumentException>(() => page.Move([foreign], 1, 1));
    }

    /// <summary>
    ///     Validates the translate rule: when both end nodes move by the same delta the manual route is translated.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_BothEndpointsMoved_TranslatesRoute()
    {
        // Arrange
        var (diagram, page, producer, consumer, wire) = Create();

        // Act
        TestModel.AssertRoundTrip(diagram, () => page.Move([producer, consumer], 30, 45));

        // Assert: manual shape preserved, shifted by the delta
        Assert.Equal(new[] { new Point(230, 105), new Point(230, 55) }, wire.Route);
    }

    /// <summary>
    ///     Validates the reroute rule: when only one end node moves the router replaces the manual route.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_OneEndpointMoved_ReroutesWire()
    {
        // Arrange
        var (diagram, page, _, consumer, wire) = Create();

        // Act
        TestModel.AssertRoundTrip(diagram, () => page.Move([consumer], 0, 150));

        // Assert: the router replaced the manual route with a Z whose vertical run lies between the two anchors
        Assert.Equal(2, wire.Route.Count);
        Assert.Equal(wire.Route[0].X, wire.Route[1].X);
        Assert.InRange(wire.Route[0].X, wire.Source.Anchor.X, wire.Target.Anchor.X);
        Assert.Equal(45, wire.Route[0].Y);
        Assert.Equal(195, wire.Route[1].Y);
    }

    /// <summary>
    ///     Validates that wires unrelated to the moved nodes keep their routes.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_UnrelatedNode_LeavesWireUntouched()
    {
        // Arrange
        var (diagram, page, _, _, wire) = Create();
        var bystander = page.AddNode("acme.Gain", 0, 300);
        var before = wire.Route.ToArray();

        // Act
        TestModel.AssertRoundTrip(diagram, () => page.Move([bystander], 10, 10));

        // Assert
        Assert.Equal(before, wire.Route);
    }

    /// <summary>
    ///     Validates that wires passed to Move are ignored because they follow their nodes.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_WireInSet_IsIgnored()
    {
        // Arrange
        var (diagram, page, _, _, wire) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        page.Move([wire], 50, 50);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Equal(new Point(200, 60), wire.Route[0]);
    }

    /// <summary>
    ///     Validates that Node.MoveTo and the Bounds setter move through the same pipeline and re-route attached wires.
    /// </summary>
    [Fact]
    public void Node_MoveToAndBounds_MoveNodeAndRerouteWires()
    {
        // Arrange
        var (diagram, _, _, consumer, wire) = Create();

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => consumer.MoveTo(300, 150));
        Assert.Equal(new Point(300, 150), consumer.Position);
        Assert.InRange(wire.Route[0].X, wire.Source.Anchor.X, wire.Target.Anchor.X);
        Assert.Equal(45, wire.Route[0].Y);
        TestModel.AssertRoundTrip(diagram, () => consumer.Bounds = new Rect(300, 150, 120, 90));
        Assert.Equal(new Size(120, 90), consumer.Bounds.Size);
        Assert.Equal("Resize", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that setting identical bounds records nothing and invalid bounds are rejected.
    /// </summary>
    [Fact]
    public void Node_Bounds_SameOrInvalid_NoEntryOrThrows()
    {
        // Arrange
        var (diagram, _, producer, _, _) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        var current = producer.Bounds;
        producer.Bounds = new Rect(current.Position, current.Size);

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
        Assert.Throws<ArgumentException>(() => producer.Bounds = new Rect(0, 0, -1, 10));
    }

    /// <summary>
    ///     Validates that resizing an annotation is undoable.
    /// </summary>
    [Fact]
    public void Annotation_Bounds_Set_UndoRedoRoundTrip()
    {
        // Arrange
        var (diagram, page, _, _, _) = Create();
        var note = page.AddAnnotation("n", new Rect(0, 200, 100, 40));

        // Act / Assert
        TestModel.AssertRoundTrip(diagram, () => note.Bounds = new Rect(0, 200, 180, 90));
        Assert.Equal("Resize", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that many moves inside a transaction compact to one history entry that undoes completely, wires included.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_ManyMovesInTransaction_CreateOneEntry()
    {
        // Arrange
        var (diagram, page, _, consumer, wire) = Create();
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act: a drag of 20 small steps
        using (diagram.History.BeginTransaction("Drag"))
        {
            for (var i = 0; i < 20; i++)
            {
                page.Move([consumer], 0, 5);
            }
        }

        // Assert: one entry; the wire follows; undo/redo restore exactly
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        Assert.Equal(new Point(300, 100), consumer.Position);
        var after = TestModel.Dump(diagram);
        Assert.NotEqual(before, after);
        Assert.True(diagram.History.Undo());
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(new[] { new Point(200, 60), new Point(200, 10) }, wire.Route);
        Assert.True(diagram.History.Redo());
        Assert.Equal(after, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Validates that moves outside a transaction each create their own entry.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_SeparateCalls_CreateSeparateEntries()
    {
        // Arrange
        var (diagram, page, producer, _, _) = Create();
        var entries = diagram.History.UndoCount;

        // Act
        page.Move([producer], 5, 0);
        page.Move([producer], 5, 0);

        // Assert
        Assert.Equal(entries + 2, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that wire-route edits and moves merge correctly when combined inside one transaction.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_MergedWithRouteEditInTransaction_RestoresOriginalOnUndo()
    {
        // Arrange
        var (diagram, page, producer, consumer, wire) = Create();
        var before = TestModel.Dump(diagram);
        var entries = diagram.History.UndoCount;

        // Act
        using (diagram.History.BeginTransaction("Edit"))
        {
            page.Move([producer, consumer], 15, 15);
            wire.SetRoute([new Point(1, 2)]);
            page.Move([producer], 0, 30);
        }

        // Assert
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        diagram.History.Undo();
        Assert.Equal(before, TestModel.Dump(diagram));
    }

    /// <summary>
    ///     Validates that moving a node raises PropertyChanged for Bounds and a Changed event.
    /// </summary>
    [Fact]
    public void DiagramPage_Move_RaisesBoundsChangedNotifications()
    {
        // Arrange
        var (diagram, page, producer, _, _) = Create();
        var props = new List<string?>();
        var changes = new List<DiagramChangedEventArgs>();
        producer.PropertyChanged += (_, e) => props.Add(e.PropertyName);
        diagram.Changed += (_, e) => changes.Add(e);

        // Act
        page.Move([producer], 5, 5);

        // Assert
        Assert.Contains("Bounds", props);
        Assert.Contains(changes, c => c.Kind == DiagramChangeKind.ElementChanged && ReferenceEquals(c.Element, producer) && c.PropertyName == "Bounds");
    }
}
