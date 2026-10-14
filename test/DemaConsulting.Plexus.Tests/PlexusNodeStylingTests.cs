namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     System, subsystem and model level tests for dynamic node styling, which use the public API only: a styler
///     assigned to the diagram decides how each node is drawn, follows the model and the host, and never undoes.
/// </summary>
public class PlexusNodeStylingTests
{
    /// <summary>
    ///     Proves end to end that nodes are styled by their properties and connections, that the style follows model
    ///     edits and host state after a refresh, and that a failing styler leaves the theme look.
    /// </summary>
    [Fact]
    public void Plexus_NodeStyling_StylerFollowsPropertiesAndRefreshes()
    {
        // Arrange: a producer wired to a consumer; the producer glows while enabled, the consumer fades with a host variable
        var (diagram, page, producer, consumer) = CreateWiredPage();
        var hostFade = 1.0;
        diagram.NodeStyler = NodeStylers.Combine(
            NodeStylers.ByProperty("Enabled", enabled => enabled is true ? new NodeStyle { Fill = Color.Parse("#336699"), Glow = Color.Parse("#00FF00") } : null),
            node => node.Inputs.Any(input => input.Wires.Count > 0) ? new NodeStyle { Border = Color.Parse("#FF0000"), Opacity = hostFade } : null);

        // Act / Assert: the enabled producer is blue with a halo; the connected consumer has a red border
        var scene = Build(page);
        Assert.Equal(Color.Parse("#336699"), Box(scene, producer).Fill);
        Assert.Contains(SceneTestModel.For<RectPrimitive>(scene, producer), r => r.Fill is null);
        Assert.Equal(Color.Parse("#FF0000"), Box(scene, consumer).Stroke);
        Assert.Equal(diagram.Theme.NodeFill, Box(scene, consumer).Fill!.Value with { A = 255 });

        // A model edit on the node changes the style on the next build, and undo restores it
        producer.Properties["Enabled"] = false;
        Assert.Equal(diagram.Theme.NodeFill, Box(Build(page), producer).Fill);
        diagram.History.Undo();
        Assert.Equal(Color.Parse("#336699"), Box(Build(page), producer).Fill);

        // Host state outside the diagram is picked up after RefreshAppearance, which editors observe through Changed
        var notified = 0;
        diagram.Changed += (_, _) => notified++;
        hostFade = 0.25;
        diagram.RefreshAppearance();
        Assert.Equal(1, notified);
        Assert.Equal(64, Box(Build(page), consumer).Fill!.Value.A);

        // A failing styler never breaks rendering: the theme look is used
        diagram.NodeStyler = _ => throw new InvalidOperationException("host failure");
        Assert.Equal(diagram.Theme.NodeFill, Box(Build(page), producer).Fill);
    }

    /// <summary>
    ///     Proves at subsystem level that the scene styles each node individually and uses the theme for every member
    ///     and every node the styler does not decide, and keeps the selection outline on top.
    /// </summary>
    [Fact]
    public void Scene_NodeStyling_StyleFollowsNodesAndFallsBackToTheme()
    {
        // Arrange: two nodes; only the second is styled, and only with a fill
        var (diagram, page, first, second) = CreateWiredPage();
        diagram.NodeStyler = node => ReferenceEquals(node, second) ? new NodeStyle { Fill = Color.Parse("#00FF00") } : null;

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false, Selection = [second] });

        // Assert: the styled node has its fill but the theme's border; the other node is entirely theme; selection stays last
        Assert.Equal(Color.Parse("#00FF00"), Box(scene, second).Fill);
        Assert.Equal(diagram.Theme.NodeBorder, Box(scene, second).Stroke);
        Assert.Equal(diagram.Theme.NodeFill, Box(scene, first).Fill);
        Assert.Equal(diagram.Theme.SelectionColor, SceneTestModel.For<RectPrimitive>(scene, second)[^1].Stroke);
    }

    /// <summary>
    ///     Proves at model level that assigning the styler notifies observers without touching the history.
    /// </summary>
    [Fact]
    public void Model_NodeStyler_Assign_NotifiesWithoutUndoEntries()
    {
        // Arrange
        var (diagram, _, _, _) = CreateWiredPage();
        var undoCount = diagram.History.UndoCount;
        var settingsChanges = 0;
        diagram.Changed += (_, e) => settingsChanges += e.Kind == DiagramChangeKind.SettingsChanged ? 1 : 0;

        // Act
        diagram.NodeStyler = _ => new NodeStyle { Opacity = 0.5 };

        // Assert
        Assert.Equal(1, settingsChanges);
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Creates a diagram with a producer wired to a consumer.
    /// </summary>
    /// <returns>The diagram, its page, the producer and the consumer.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Node Consumer) CreateWiredPage()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 30, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 30, "C");
        page.Connect(producer.Port("value"), consumer.Port("in"));
        return (diagram, page, producer, consumer);
    }

    /// <summary>
    ///     Builds a scene without the grid.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The scene.</returns>
    private static DiagramScene Build(DiagramPage page) => SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

    /// <summary>
    ///     Finds the filled box of a node.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The node's box rectangle.</returns>
    private static RectPrimitive Box(DiagramScene scene, Node node) =>
        SceneTestModel.For<RectPrimitive>(scene, node).Single(r => r.Fill is not null);
}
