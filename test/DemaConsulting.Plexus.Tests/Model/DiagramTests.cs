using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the construction, shared history and settings of <see cref="Diagram"/>.
/// </summary>
public class DiagramTests
{
    /// <summary>
    ///     Validates that a null node-type registry is rejected.
    /// </summary>
    [Fact]
    public void Diagram_Constructor_NullTypes_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: no registry means no node can ever be created
        Assert.Throws<ArgumentNullException>(() => new Diagram(null!));
    }

    /// <summary>
    ///     Validates that a new diagram exposes the registry it was created with, has no pages and no history.
    /// </summary>
    [Fact]
    public void Diagram_Constructor_Registry_IsExposedAndDiagramStartsEmpty()
    {
        // Arrange
        var types = TestModel.CreateTypes();

        // Act
        var diagram = new Diagram(types);

        // Assert
        Assert.Same(types, diagram.Types);
        Assert.Empty(diagram.Pages);
        Assert.False(diagram.History.CanUndo);
        Assert.False(diagram.History.CanRedo);
    }

    /// <summary>
    ///     Validates that edits on different pages are recorded in one shared history and undone in reverse order.
    /// </summary>
    [Fact]
    public void Diagram_History_EditsOnSeveralPages_ShareOneUndoStack()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var one = diagram.Pages.Add("One");
        var two = diagram.Pages.Add("Two");

        // Act
        one.AddNode("acme.Producer", 0, 0);
        two.AddNode("acme.Producer", 0, 0);

        // Assert: both page creations and both node additions are in the same history
        Assert.Equal(4, diagram.History.UndoCount);

        // Act: undo reaches the most recent edit first, whatever page it was on
        diagram.History.Undo();

        // Assert
        Assert.Empty(two.Nodes);
        Assert.Single(one.Nodes);
    }

    /// <summary>
    ///     Validates that a new diagram starts with the documented default settings.
    /// </summary>
    [Fact]
    public void Diagram_Settings_Default_AreClassicThemeOrthogonalRouterAndUniqueNames()
    {
        // Arrange / Act
        var diagram = TestModel.CreateDiagram();

        // Assert
        Assert.Same(DiagramTheme.Classic, diagram.Theme);
        Assert.IsType<OrthogonalRouter>(diagram.Router);
        Assert.Same(ConnectionRules.Default, diagram.ConnectionRules);
        Assert.True(diagram.RequireUniqueNodeNames);
    }

    /// <summary>
    ///     Validates that changing settings is configuration: it is applied but never recorded in the undo history.
    /// </summary>
    [Fact]
    public void Diagram_Settings_Assigned_AreAppliedButNotUndoable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var theme = new DiagramTheme();
        var router = new SimpleRouter();

        // Act
        diagram.Theme = theme;
        diagram.Router = router;
        diagram.ConnectionRules = ConnectionRules.SameType;
        diagram.RequireUniqueNodeNames = false;

        // Assert
        Assert.Same(theme, diagram.Theme);
        Assert.Same(router, diagram.Router);
        Assert.Same(ConnectionRules.SameType, diagram.ConnectionRules);
        Assert.False(diagram.RequireUniqueNodeNames);
        Assert.False(diagram.History.CanUndo);
    }
}
