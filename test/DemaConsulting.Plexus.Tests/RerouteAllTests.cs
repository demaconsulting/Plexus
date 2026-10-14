using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Routing;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="DiagramPage.RerouteAll"/>.
/// </summary>
public class RerouteAllTests
{
    /// <summary>
    ///     Validates that RerouteAll recomputes wires created before a blocking node existed, as one undo entry.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_RecomputesAllWiresAsOneUndoEntry()
    {
        // Arrange: wires are connected first, then a node is dropped on their straight path
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 600, 0);
        var c = page.AddNode("acme.Producer", 0, 300);
        var d = page.AddNode("acme.Consumer", 600, 300);
        var first = page.Connect(a.Port("value"), b.Port("in"));
        var second = page.Connect(c.Port("value"), d.Port("in"));
        page.AddNode("acme.Gain", 280, -20);
        page.AddNode("acme.Gain", 280, 280);
        var firstBefore = first.Route.ToArray();
        var secondBefore = second.Route.ToArray();
        var entries = diagram.History.UndoCount;

        // Act
        page.RerouteAll();

        // Assert: both wires changed in a single history entry, and undo restores the old routes
        Assert.NotEqual(firstBefore, first.Route);
        Assert.NotEqual(secondBefore, second.Route);
        Assert.Equal(entries + 1, diagram.History.UndoCount);
        diagram.History.Undo();
        Assert.Equal(firstBefore, first.Route);
        Assert.Equal(secondBefore, second.Route);
        diagram.History.Redo();
        Assert.NotEqual(firstBefore, first.Route);
    }

    /// <summary>
    ///     Validates that RerouteAll records nothing when no route changes and when there are no wires.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_NothingChanges_RecordsNoHistory()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        page.RerouteAll();
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 400, 0);
        page.Connect(a.Port("value"), b.Port("in"));
        var entries = diagram.History.UndoCount;

        // Act
        page.RerouteAll();

        // Assert
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that RerouteAll uses the diagram's current router and discards manual routes.
    /// </summary>
    [Fact]
    public void DiagramPage_RerouteAll_UsesCurrentRouter_DiscardingManualRoutes()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var a = page.AddNode("acme.Producer", 0, 0);
        var b = page.AddNode("acme.Consumer", 400, 100);
        var wire = page.Connect(a.Port("value"), b.Port("in"), [new Point(200, 30), new Point(200, 500)]);
        diagram.Router = new SimpleRouter();

        // Act
        page.RerouteAll();

        // Assert: the simple router's elbow replaced the manual detour
        Assert.DoesNotContain(new Point(200, 500), wire.Route);
        Assert.Equal(2, wire.Route.Count);
    }
}
