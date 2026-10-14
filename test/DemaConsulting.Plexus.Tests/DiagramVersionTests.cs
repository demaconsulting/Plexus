namespace DemaConsulting.Plexus.Tests;

using DemaConsulting.Plexus.Geometry;
using DemaConsulting.Plexus.Scene;

/// <summary>
///     Tests of <see cref="Diagram.Version"/> (the cheap cache key) and <see cref="WireStylers.PerPage"/> (a styler built once per page and version).
/// </summary>
public class DiagramVersionTests
{
    /// <summary>
    ///     Validates that the version moves with edits, undo, redo and settings, but not with reads or scene builds.
    /// </summary>
    [Fact]
    public void Diagram_Version_AdvancesWithEveryChangeIncludingUndoRedoAndRefresh()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var afterPage = diagram.Version;

        // Act: reads and scene builds do not change the diagram
        _ = TestModel.Dump(diagram);
        _ = SceneBuilder.Build(page, new SceneOptions());
        var afterReads = diagram.Version;

        // Act: an edit, its undo and its redo, a settings change and a refresh each advance the version
        var node = page.AddNode("acme.Producer", 30, 30, "P");
        var afterAdd = diagram.Version;
        diagram.History.Undo();
        var afterUndo = diagram.Version;
        diagram.History.Redo();
        var afterRedo = diagram.Version;
        node.Properties["Enabled"] = false;
        var afterProperty = diagram.Version;
        diagram.Theme = DiagramTheme.Classic;
        var afterTheme = diagram.Version;
        diagram.RefreshAppearance();
        var afterRefresh = diagram.Version;

        // Assert
        Assert.Multiple(
            () => Assert.True(afterPage > 0, "adding a page is a change"),
            () => Assert.Equal(afterPage, afterReads),
            () => Assert.True(afterAdd > afterReads),
            () => Assert.True(afterUndo > afterAdd),
            () => Assert.True(afterRedo > afterUndo),
            () => Assert.True(afterProperty > afterRedo),
            () => Assert.True(afterTheme > afterProperty),
            () => Assert.True(afterRefresh > afterTheme));
    }

    /// <summary>
    ///     Validates that the version already has its new value when <see cref="Diagram.Changed"/> handlers run.
    /// </summary>
    [Fact]
    public void Diagram_Version_IsCurrentInsideChangedHandlers()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var before = diagram.Version;
        long seen = -1;
        diagram.Changed += (_, _) => seen = diagram.Version;

        // Act
        diagram.RefreshAppearance();

        // Assert
        Assert.Equal(before + 1, seen);
    }

    /// <summary>
    ///     Validates that the factory runs once per page and version, not once per wire, and again after any change.
    /// </summary>
    [Fact]
    public void WireStylers_PerPage_BuildsOncePerPageAndVersion()
    {
        // Arrange: two wires on one page, and a styler that counts its factory calls and colors by a captured per-page value
        var (diagram, page, wires) = CreatePageWithWires(2);
        var factoryCalls = 0;
        var level = 1;
        diagram.WireStyler = WireStylers.PerPage(p =>
        {
            factoryCalls++;
            var captured = level;
            return wire => new WireStyle { Thickness = captured };
        });

        // Act: a scene build styles both wires from one factory call; a second build without changes reuses it
        var first = Stroke(Build(page), wires[0]).StrokeThickness;
        var callsAfterFirst = factoryCalls;
        var second = Stroke(Build(page), wires[1]).StrokeThickness;
        var callsAfterSecond = factoryCalls;

        // Act: a change (here the host's state plus a refresh) builds a new styler at the next scene build
        level = 5;
        diagram.RefreshAppearance();
        var third = Stroke(Build(page), wires[0]).StrokeThickness;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, first),
            () => Assert.Equal(1, callsAfterFirst),
            () => Assert.Equal(1, second),
            () => Assert.Equal(1, callsAfterSecond),
            () => Assert.Equal(5, third),
            () => Assert.Equal(2, factoryCalls));
    }

    /// <summary>
    ///     Validates that undo and redo invalidate the per-page styler too, so a property edit is always reflected.
    /// </summary>
    [Fact]
    public void WireStylers_PerPage_FollowsPropertyEditsUndoAndRedo()
    {
        // Arrange: the style depends on the producer's Enabled property, read once per page and version
        var (diagram, page, wires) = CreatePageWithWires(1);
        var producer = wires[0].Source.Node;
        diagram.WireStyler = WireStylers.PerPage(p =>
        {
            var enabled = p.Nodes[0].Properties.Get<bool>("Enabled");
            return wire => new WireStyle { Thickness = enabled ? 4 : 2 };
        });

        // Act
        var initial = Stroke(Build(page), wires[0]).StrokeThickness;
        producer.Properties["Enabled"] = false;
        var edited = Stroke(Build(page), wires[0]).StrokeThickness;
        diagram.History.Undo();
        var undone = Stroke(Build(page), wires[0]).StrokeThickness;
        diagram.History.Redo();
        var redone = Stroke(Build(page), wires[0]).StrokeThickness;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(4, initial),
            () => Assert.Equal(2, edited),
            () => Assert.Equal(4, undone),
            () => Assert.Equal(2, redone));
    }

    /// <summary>
    ///     Validates that each page gets its own styler, and that stylers compose with <see cref="WireStylers.Combine"/>.
    /// </summary>
    [Fact]
    public void WireStylers_PerPage_KeepsPagesApartAndCombines()
    {
        // Arrange: one wire on each of two pages; the factory styles by the page's name length
        var diagram = TestModel.CreateDiagram();
        var shortPage = diagram.Pages.Add("Ab");
        var longPage = diagram.Pages.Add("Abcdef");
        var shortWire = Wire(shortPage);
        var longWire = Wire(longPage);
        diagram.WireStyler = WireStylers.Combine(
            WireStylers.ByPortType(),
            WireStylers.PerPage(page => wire => new WireStyle { Thickness = page.Name.Length }));

        // Act
        var shortStroke = Stroke(Build(shortPage), shortWire);
        var longStroke = Stroke(Build(longPage), longWire);

        // Assert: per-page thickness, and the port-type color from the other layer is kept
        Assert.Multiple(
            () => Assert.Equal(2, shortStroke.StrokeThickness),
            () => Assert.Equal(6, longStroke.StrokeThickness),
            () => Assert.Equal(Color.Parse("#FF8C00"), shortStroke.Stroke));
    }

    /// <summary>
    ///     Validates that a failing factory falls back to the theme and is tried again, and that a null styler styles nothing.
    /// </summary>
    [Fact]
    public void WireStylers_PerPage_FailingFactoryFallsBackAndRetries_NullStylerStylesNothing()
    {
        // Arrange
        var (diagram, page, wires) = CreatePageWithWires(1);
        var attempts = 0;
        diagram.WireStyler = WireStylers.PerPage(p =>
        {
            attempts++;
            return attempts == 1 ? throw new InvalidOperationException("host failure") : null!;
        });

        // Act: the first build fails (theme look); the second retries and gets a null styler, which also means the theme look
        var failed = Stroke(Build(page), wires[0]).Stroke;
        var retried = Stroke(Build(page), wires[0]).Stroke;
        var callsAfterRetry = attempts;
        _ = Stroke(Build(page), wires[0]);

        // Assert: the null result is cached for the version, so no third attempt is made
        Assert.Multiple(
            () => Assert.Equal(diagram.Theme.WireColor, failed),
            () => Assert.Equal(diagram.Theme.WireColor, retried),
            () => Assert.Equal(2, callsAfterRetry),
            () => Assert.Equal(2, attempts));
    }

    /// <summary>
    ///     Validates the argument checks and that a wire not on a page is not styled.
    /// </summary>
    [Fact]
    public void WireStylers_PerPage_ArgumentChecksAndDetachedWire()
    {
        // Arrange: a wire that was removed from its page
        var (_, page, wires) = CreatePageWithWires(1);
        var calls = 0;
        var styler = WireStylers.PerPage(p =>
        {
            calls++;
            return wire => new WireStyle { Thickness = 9 };
        });
        var attached = styler(wires[0]);
        page.Remove(wires[0]);
        var detached = styler(wires[0]);

        // Assert
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => WireStylers.PerPage(null!)),
            () => Assert.Equal(9, attached!.Thickness),
            () => Assert.Null(detached),
            () => Assert.Equal(1, calls));
    }

    /// <summary>
    ///     Creates a diagram with a page that holds producers wired to consumers.
    /// </summary>
    /// <param name="count">The number of wires.</param>
    /// <returns>The diagram, the page and the wires.</returns>
    private static (Diagram Diagram, DiagramPage Page, List<Wire> Wires) CreatePageWithWires(int count)
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        var wires = new List<Wire>();
        for (var i = 0; i < count; i++)
        {
            var producer = page.AddNode("acme.Producer", 30, 30 + (i * 200), $"P{i}");
            var consumer = page.AddNode("acme.Consumer", 400, 30 + (i * 200), $"C{i}");
            wires.Add(page.Connect(producer.Port("value"), consumer.Port("in")));
        }

        return (diagram, page, wires);
    }

    /// <summary>
    ///     Adds a producer wired to a consumer on a page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The wire.</returns>
    private static Wire Wire(DiagramPage page) =>
        page.Connect(page.AddNode("acme.Producer", 30, 30).Port("value"), page.AddNode("acme.Consumer", 400, 30).Port("in"));

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
}

