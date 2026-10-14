namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     System, subsystem and model level tests for dynamic wire styling, which use the public API only: a styler
///     assigned to the diagram decides how each wire is drawn, follows the model and the host, and never undoes.
/// </summary>
public class PlexusWireStylingTests
{
    /// <summary>
    ///     Proves end to end that wires are styled by what they connect to (type and node property), that the style
    ///     follows model edits, host state after a refresh, and that a failing styler leaves the theme look.
    /// </summary>
    [Fact]
    public void Plexus_WireStyling_StylerFollowsConnectionsAndRefreshes()
    {
        // Arrange: one f32 wire from a producer that has an Enabled property
        var (diagram, page, producer, wire) = CreateWiredPage();
        var simulatedLevel = 0.0;
        diagram.WireStyler = WireStylers.Combine(
            WireStylers.ByPortType(),
            WireStylers.BySourceNodeProperty("Enabled", enabled => enabled is false ? new WireStyle { Dash = [6, 4] } : null),
            _ => simulatedLevel > 0.5 ? new WireStyle { Thickness = 5 } : null);

        // Act / Assert: colored by its f32 port type, solid and theme-thick
        var scene = Build(page);
        Assert.Equal(Color.Parse("#FF8C00"), Stroke(scene, wire).Stroke);
        Assert.Equal(diagram.Theme.WireThickness, Stroke(scene, wire).StrokeThickness);
        Assert.Equal(1, FigureCount(Stroke(scene, wire)));

        // A model edit on the connected node changes the style on the next build
        producer.Properties["Enabled"] = false;
        Assert.True(FigureCount(Stroke(Build(page), wire)) > 3);

        // Host state outside the diagram is picked up after RefreshAppearance, which editors observe through Changed
        var notified = 0;
        diagram.Changed += (_, _) => notified++;
        simulatedLevel = 1.0;
        diagram.RefreshAppearance();
        Assert.Equal(1, notified);
        Assert.Equal(5, Stroke(Build(page), wire).StrokeThickness);

        // A failing styler never breaks rendering: the theme look is used
        diagram.WireStyler = _ => throw new InvalidOperationException("host failure");
        Assert.Equal(diagram.Theme.WireColor, Stroke(Build(page), wire).Stroke);
    }

    /// <summary>
    ///     Proves at subsystem level that the scene styles each wire individually from its connections and uses the
    ///     theme for every member and every wire the styler does not decide.
    /// </summary>
    [Fact]
    public void Scene_WireStyling_StyleFollowsConnectionsAndFallsBackToTheme()
    {
        // Arrange: two wires; only the second is styled, and only with a color
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var f32 = page.Connect(page.AddNode("acme.Producer", 0, 0, "P").Port("value"), page.AddNode("acme.Consumer", 300, 0, "C").Port("in"));
        var gain = page.AddNode("acme.Gain", 0, 200, "G");
        var i32 = page.Connect(gain.Port("out"), page.AddNode("acme.Consumer", 300, 200, "D").Port("in"));
        diagram.WireStyler = wire => ReferenceEquals(wire, i32) ? new WireStyle { Color = Color.Parse("#00FF00") } : null;

        // Act
        var scene = Build(page);

        // Assert: the styled wire has its color but the theme's thickness; the other wire is entirely theme
        Assert.Equal(Color.Parse("#00FF00"), Stroke(scene, i32).Stroke);
        Assert.Equal(diagram.Theme.WireThickness, Stroke(scene, i32).StrokeThickness);
        Assert.Equal(diagram.Theme.WireColor, Stroke(scene, f32).Stroke);
        Assert.Equal(1, FigureCount(Stroke(scene, f32)));
    }

    /// <summary>
    ///     Proves at model level that assigning the styler and refreshing notify observers without touching the history.
    /// </summary>
    [Fact]
    public void Model_WireStyler_AssignAndRefresh_NotifyWithoutUndoEntries()
    {
        // Arrange
        var (diagram, _, _, _) = CreateWiredPage();
        var undoCount = diagram.History.UndoCount;
        var settingsChanges = 0;
        diagram.Changed += (_, e) => settingsChanges += e.Kind == DiagramChangeKind.SettingsChanged ? 1 : 0;

        // Act
        diagram.WireStyler = _ => new WireStyle { Thickness = 3 };
        diagram.RefreshAppearance();

        // Assert
        Assert.Equal(2, settingsChanges);
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Creates a diagram with a producer wired to a consumer.
    /// </summary>
    /// <returns>The diagram, its page, the producer and the wire.</returns>
    private static (Diagram Diagram, DiagramPage Page, Node Producer, Wire Wire) CreateWiredPage()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var producer = page.AddNode("acme.Producer", 30, 30, "P");
        var consumer = page.AddNode("acme.Consumer", 400, 30, "C");
        return (diagram, page, producer, page.Connect(producer.Port("value"), consumer.Port("in")));
    }

    /// <summary>
    ///     Builds a scene without the grid.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The scene.</returns>
    private static DiagramScene Build(DiagramPage page) => SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

    /// <summary>
    ///     Finds the stroked line of a wire.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="wire">The wire.</param>
    /// <returns>The wire's stroked path.</returns>
    private static PathPrimitive Stroke(DiagramScene scene, Wire wire) =>
        scene.Primitives.OfType<PathPrimitive>().Last(p => ReferenceEquals(p.Element, wire) && p.Fill is null);

    /// <summary>
    ///     Counts the figures of a path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The number of move-to commands.</returns>
    private static int FigureCount(PathPrimitive path) => path.Commands.Count(c => c.Kind == PathCommandKind.MoveTo);
}
