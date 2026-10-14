using DemaConsulting.Plexus.Routing;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="DiagramOptions"/>: one configuration object for new, loaded and existing diagrams.
/// </summary>
public class DiagramOptionsTests
{
    /// <summary>
    ///     Builds options with a distinctive value for every member.
    /// </summary>
    /// <returns>The options.</returns>
    private static DiagramOptions CreateOptions() => new()
    {
        Theme = DiagramTheme.Classic with { GridSize = 20 },
        Router = new SimpleRouter(),
        ConnectionRules = ConnectionRules.SameType,
        WireStyler = _ => null,
        NodeStyler = _ => null,
        RequireUniqueNodeNames = false,
    };

    /// <summary>
    ///     Validates that the constructor installs every member without raising change notifications.
    /// </summary>
    [Fact]
    public void Diagram_Constructor_WithOptions_InstallsEverythingSilently()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var diagram = new Diagram(TestModel.CreateTypes(), options);

        // Assert
        Assert.Same(options.Theme, diagram.Theme);
        Assert.Same(options.Router, diagram.Router);
        Assert.Same(options.ConnectionRules, diagram.ConnectionRules);
        Assert.Same(options.WireStyler, diagram.WireStyler);
        Assert.Same(options.NodeStyler, diagram.NodeStyler);
        Assert.False(diagram.RequireUniqueNodeNames);
        Assert.Equal(0, diagram.Version);
    }

    /// <summary>
    ///     Validates that null options and empty options keep every default.
    /// </summary>
    [Fact]
    public void Diagram_Constructor_NullOrEmptyOptions_KeepsDefaults()
    {
        // Arrange / Act
        var plain = new Diagram(TestModel.CreateTypes(), null);
        var empty = new Diagram(TestModel.CreateTypes(), new DiagramOptions());

        // Assert
        Assert.Same(DiagramTheme.Classic, plain.Theme);
        Assert.Same(DiagramTheme.Classic, empty.Theme);
        Assert.IsType<OrthogonalRouter>(empty.Router);
        Assert.Same(ConnectionRules.Default, empty.ConnectionRules);
        Assert.Null(empty.WireStyler);
        Assert.Null(empty.NodeStyler);
        Assert.True(empty.RequireUniqueNodeNames);
        Assert.Throws<ArgumentNullException>(() => new Diagram(null!, new DiagramOptions()));
    }

    /// <summary>
    ///     Validates that every loader installs the options on the loaded diagram.
    /// </summary>
    [Fact]
    public void Diagram_Loaders_WithOptions_InstallThemOnTheLoadedDiagram()
    {
        // Arrange
        var options = CreateOptions();
        var source = TestModel.CreateDiagram();
        source.Pages.Add("Main").AddNode("acme.Producer", 0, 0);

        // Act
        var fromJson = Diagram.FromJson(source.ToJson(), TestModel.CreateTypes(), options);
        var fromSnapshot = Diagram.FromSnapshot(source.ToSnapshot(), TestModel.CreateTypes(), options);
        var fromTree = Diagram.FromTree(source.ToTree(), TestModel.CreateTypes(), options);

        // Assert
        Assert.All(new[] { fromJson, fromSnapshot, fromTree }, loaded =>
        {
            Assert.Same(options.Theme, loaded.Theme);
            Assert.Same(options.WireStyler, loaded.WireStyler);
            Assert.False(loaded.RequireUniqueNodeNames);
            Assert.False(loaded.History.CanUndo);
            Assert.Single(loaded.Pages);
        });
    }

    /// <summary>
    ///     Validates that Apply replaces only the members that are set, raises SettingsChanged per member and is not undoable.
    /// </summary>
    [Fact]
    public void Diagram_Apply_ReplacesOnlySetMembers_NotUndoable()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var router = diagram.Router;
        var kinds = new List<(DiagramChangeKind Kind, string? Name)>();
        diagram.Changed += (_, e) => kinds.Add((e.Kind, e.PropertyName));
        var options = new DiagramOptions { Theme = DiagramTheme.Classic with { GridSize = 30 }, RequireUniqueNodeNames = false };

        // Act
        diagram.Apply(options);

        // Assert
        Assert.Equal(30, diagram.Theme.GridSize);
        Assert.False(diagram.RequireUniqueNodeNames);
        Assert.Same(router, diagram.Router);
        Assert.Equal([(DiagramChangeKind.SettingsChanged, "Theme"), (DiagramChangeKind.SettingsChanged, "RequireUniqueNodeNames")], kinds);
        Assert.False(diagram.History.CanUndo);
        Assert.Throws<ArgumentNullException>(() => diagram.Apply(null!));
    }

    /// <summary>
    ///     Validates that Apply installs every member of a full set of options.
    /// </summary>
    [Fact]
    public void Diagram_Apply_FullOptions_InstallsEveryMember()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var options = CreateOptions();

        // Act
        diagram.Apply(options);

        // Assert
        Assert.Same(options.Router, diagram.Router);
        Assert.Same(options.ConnectionRules, diagram.ConnectionRules);
        Assert.Same(options.WireStyler, diagram.WireStyler);
        Assert.Same(options.NodeStyler, diagram.NodeStyler);
        Assert.Same(options.Theme, diagram.Theme);
    }
}
