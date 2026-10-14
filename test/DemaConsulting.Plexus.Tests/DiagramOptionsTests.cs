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

    /// <summary>
    ///     Validates that the initialize callback runs once for a diagram created with the options, after the configuration is installed.
    /// </summary>
    [Fact]
    public void Diagram_Constructor_WithInitialize_InvokesItOnceWithTheConfiguredDiagram()
    {
        // Arrange
        var calls = new List<(Diagram Diagram, bool ThemeInstalled)>();
        var options = CreateOptions() with { Initialize = d => calls.Add((d, Math.Abs(d.Theme.GridSize - 20) < 0.5)) };

        // Act
        var diagram = new Diagram(TestModel.CreateTypes(), options);

        // Assert
        var call = Assert.Single(calls);
        Assert.Same(diagram, call.Diagram);
        Assert.True(call.ThemeInstalled);
        Assert.Equal(0, diagram.Version);
    }

    /// <summary>
    ///     Validates that every loader runs the callback after the whole document is loaded, so a rename rule attached there applies to the loaded diagram.
    /// </summary>
    [Fact]
    public void Diagram_Loaders_WithInitialize_InvokeItAfterTheLoadAndAttachPerInstanceHandlers()
    {
        // Arrange: a callback that records what the diagram holds and attaches a rename veto
        var source = TestModel.CreateDiagram();
        source.Pages.Add("Main").AddNode("acme.Producer", 0, 0, "Original");
        var seen = new List<(int Pages, int Nodes, bool CanUndo)>();
        var options = new DiagramOptions
        {
            Initialize = d =>
            {
                seen.Add((d.Pages.Count, d.Pages.SelectMany(p => p.Nodes).Count(), d.History.CanUndo));
                d.NodeRenaming += (_, e) =>
                {
                    e.Cancel = true;
                    e.Reason = "Renaming is disabled.";
                };
            },
        };

        // Act
        var loaded = new[]
        {
            Diagram.FromJson(source.ToJson(), TestModel.CreateTypes(), options),
            Diagram.FromSnapshot(source.ToSnapshot(), TestModel.CreateTypes(), options),
            Diagram.FromTree(source.ToTree(), TestModel.CreateTypes(), options),
        };

        // Assert: one call per load, after the content exists and with a clean history; the handler is live on each diagram
        Assert.Equal(3, seen.Count);
        Assert.All(seen, s => Assert.Equal((1, 1, false), s));
        Assert.All(loaded, d =>
        {
            Assert.False(d.TryRename(d.FindNode("Original")!, "Other", out var reason));
            Assert.Equal("Renaming is disabled.", reason);
        });
    }

    /// <summary>
    ///     Validates that Apply invokes the callback last, on every call, and that a null callback does nothing.
    /// </summary>
    [Fact]
    public void Diagram_Apply_WithInitialize_InvokesItAfterTheSettings()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var themeAtCall = new List<double>();
        var options = new DiagramOptions { Theme = DiagramTheme.Classic with { GridSize = 45 }, Initialize = d => themeAtCall.Add(d.Theme.GridSize) };

        // Act
        diagram.Apply(options);
        diagram.Apply(options);
        diagram.Apply(new DiagramOptions());

        // Assert
        Assert.Equal([45.0, 45.0], themeAtCall);
    }
}
