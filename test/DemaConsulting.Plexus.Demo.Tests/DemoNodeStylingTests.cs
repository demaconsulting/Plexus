using Avalonia;
using DemaConsulting.Plexus.Demo.Gui;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Scene;
using AvaloniaPoint = Avalonia.Point;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for the node styling of <see cref="DemoAppearance"/>: lamps that light up with their input and tank icons tinted by fluid.
/// </summary>
public class DemoNodeStylingTests
{
    /// <summary>
    ///     The warm fill of a lit green lamp.
    /// </summary>
    private static readonly PlexusColor LitGreen = PlexusColor.Parse("#1E8A3C");

    /// <summary>
    ///     The warm fill of a lit red lamp.
    /// </summary>
    private static readonly PlexusColor LitRed = PlexusColor.Parse("#B02A2A");

    /// <summary>
    ///     The warm fill of a lit amber lamp.
    /// </summary>
    private static readonly PlexusColor LitAmber = PlexusColor.Parse("#B87A0A");

    /// <summary>
    ///     Gets the style the diagram's node styler gives a node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The style, or null.</returns>
    private static NodeStyle? StyleOf(Node node) => node.Page!.Diagram.NodeStyler!(node);

    /// <summary>
    ///     Gets the fill the scene draws for a node's box.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="node">The node.</param>
    /// <returns>The box fill.</returns>
    private static PlexusColor? BoxFill(DiagramScene scene, Node node) =>
        scene.Primitives.OfType<RectPrimitive>().Single(r => ReferenceEquals(r.Element, node) && r.Fill is not null).Fill;

    /// <summary>
    ///     Validates that every sample and every loaded file has the node styler installed, and that a bare diagram does not.
    /// </summary>
    [Fact]
    public void DemoAppearance_Apply_InstallsTheNodeStylerOnEverySampleAndLoad()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = Path.Join(temp.Create(), "logic.json");
        DemoDocument.Save(DemoSamples.Logic(), path);
        var bare = new Diagram(DemoNodeTypes.CreateRegistry());

        // Act
        bare.Apply(DemoAppearance.Options);

        // Assert
        Assert.All(DemoSamples.Names, name => Assert.NotNull(DemoSamples.Create(name).NodeStyler));
        Assert.NotNull(DemoDocument.Load(path).NodeStyler);
        Assert.NotNull(bare.NodeStyler);
    }

    /// <summary>
    ///     Validates that with the default switches only the sum lamp is lit, as the evaluator says, and that the scene draws it with the lit fill and a halo.
    /// </summary>
    [Fact]
    public void DemoAppearance_NodeStyler_LogicSample_LightsOnlyTheLampsWithATrueInput()
    {
        // Arrange: SwitchA on, SwitchB off => XOR true (sum lamp), AND false (carry lamp), NOR true => neither lamp false
        var diagram = DemoSamples.Logic();
        var page = diagram.Pages.Single();
        var sum = diagram.FindNode("SumLamp")!;
        var carry = diagram.FindNode("CarryLamp")!;
        var neither = diagram.FindNode("NeitherLamp")!;
        var truth = LogicEvaluator.Evaluate(page);

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert
        Assert.Multiple(
            () => Assert.True(truth[sum.Input("in")]),
            () => Assert.False(truth[carry.Input("in")]),
            () => Assert.False(truth[neither.Input("in")]),
            () => Assert.Equal(LitGreen, StyleOf(sum)!.Fill),
            () => Assert.Null(StyleOf(carry)),
            () => Assert.Null(StyleOf(neither)),
            () => Assert.Equal(LitGreen, BoxFill(scene, sum)),
            () => Assert.Equal(diagram.Theme.NodeFill, BoxFill(scene, carry)),
            () => Assert.Equal(diagram.Theme.NodeFill, BoxFill(scene, neither)),
            () => Assert.NotNull(StyleOf(sum)!.Glow),
            () => Assert.NotNull(StyleOf(sum)!.IconTint),
            () => Assert.NotNull(scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, sum)).Tint),
            () => Assert.Null(scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, carry)).Tint));
    }

    /// <summary>
    ///     Validates that the lamps always agree with a fresh evaluation, across toggles, undo and redo, without any refresh.
    /// </summary>
    [Fact]
    public void DemoAppearance_NodeStyler_Lamps_StayInStepWithTheEvaluatorAcrossToggleUndoAndRedo()
    {
        // Arrange
        var diagram = DemoSamples.Logic();
        var page = diagram.Pages.Single();
        var lamps = page.Nodes.Where(n => n.Type.Key == DemoNodeTypes.Lamp).ToList();
        var switchB = diagram.FindNode("SwitchB")!;

        // Act / Assert
        void AssertInStep()
        {
            var truth = LogicEvaluator.Evaluate(page);
            var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });
            Assert.All(lamps, lamp => Assert.Equal(truth[lamp.Input("in")], BoxFill(scene, lamp) != diagram.Theme.NodeFill));
        }

        AssertInStep();
        DemoSession.ToggleSwitch(switchB);
        AssertInStep();

        // Both switches on: the AND (carry, red) is true and the XOR (sum) is false
        Assert.Equal(LitRed, BoxFill(SceneBuilder.Build(page, new SceneOptions { ShowGrid = false }), diagram.FindNode("CarryLamp")!));
        Assert.Equal(diagram.Theme.NodeFill, BoxFill(SceneBuilder.Build(page, new SceneOptions { ShowGrid = false }), diagram.FindNode("SumLamp")!));
        Assert.True(diagram.History.Undo());
        AssertInStep();
        Assert.True(diagram.History.Redo());
        AssertInStep();
    }

    /// <summary>
    ///     Validates that each lamp color has its own warm fill and that changing the Color property restyles a lit lamp.
    /// </summary>
    [Fact]
    public void DemoAppearance_NodeStyler_LampColorProperty_ChoosesTheGlow()
    {
        // Arrange: a lit lamp (switch on feeding it directly)
        var diagram = DemoSamples.Logic();
        var lamp = diagram.FindNode("SumLamp")!;

        // Act
        var green = StyleOf(lamp)!;
        lamp.Properties["Color"] = "Red";
        var red = StyleOf(lamp)!;
        lamp.Properties["Color"] = "Amber";
        var amber = StyleOf(lamp)!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LitGreen, green.Fill),
            () => Assert.Equal(LitRed, red.Fill),
            () => Assert.Equal(LitAmber, amber.Fill),
            () => Assert.Equal(3, new[] { green.Glow, red.Glow, amber.Glow }.Distinct().Count()),
            () => Assert.Equal(3, new[] { green.IconTint, red.IconTint, amber.IconTint }.Distinct().Count()));
    }

    /// <summary>
    ///     Validates that a tank's icon is tinted by its fluid, follows the Fluid property, and that other nodes are untouched.
    /// </summary>
    [Fact]
    public void DemoAppearance_NodeStyler_Tanks_TintTheIconByFluid()
    {
        // Arrange: the plumbing sample has a water tank, an oil tank and a steam tank
        var diagram = DemoSamples.Plumbing();
        var water = diagram.FindNode("Supply")!;
        var oil = diagram.FindNode("OilTank")!;
        var steam = diagram.FindNode("Boiler")!;
        var pump = diagram.FindNode("Feed")!;

        // Act
        var scene = SceneBuilder.Build(diagram.Pages.Single(), new SceneOptions { ShowGrid = false });
        var tints = new[] { water, oil, steam }.Select(t => StyleOf(t)!.IconTint).ToList();
        water.Properties["Fluid"] = "Oil";
        var changed = StyleOf(water)!.IconTint;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(PlexusColor.Parse("#4DA8FF"), tints[0]),
            () => Assert.Equal(PlexusColor.Parse("#C2791E"), tints[1]),
            () => Assert.Equal(PlexusColor.Parse("#E4E8EC"), tints[2]),
            () => Assert.Equal(tints[1], changed),
            () => Assert.Equal(tints[0], scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, water)).Tint),
            () => Assert.Null(StyleOf(pump)),
            () => Assert.Null(scene.Primitives.OfType<IconPrimitive>().Single(i => ReferenceEquals(i.Element, pump)).Tint));
    }

    /// <summary>
    ///     Validates that gates honor a style (fill, border and opacity) because the gate visual reads it from its context.
    /// </summary>
    [Fact]
    public void GateVisual_Build_HonorsTheNodeStyle()
    {
        // Arrange
        var diagram = DemoSamples.Logic();
        var page = diagram.Pages.Single();
        var gate = diagram.FindNode("Carry")!;
        diagram.NodeStyler = node => ReferenceEquals(node, gate)
            ? new NodeStyle { Fill = PlexusColor.Parse("#336699"), Border = PlexusColor.Parse("#FF0000"), Opacity = 0.5, Glow = PlexusColor.Parse("#00FF00") }
            : null;

        // Act
        var scene = SceneBuilder.Build(page, new SceneOptions { ShowGrid = false });

        // Assert: the filled gate outline has the styled fill and border at half opacity, and no rectangular halo is drawn around a gate symbol
        var outline = scene.Primitives.OfType<PathPrimitive>().Single(p => ReferenceEquals(p.Element, gate) && p.Fill is not null && p.Stroke is not null);
        Assert.Multiple(
            () => Assert.Equal(PlexusColor.Parse("#336699") with { A = 128 }, outline.Fill),
            () => Assert.Equal(PlexusColor.Parse("#FF0000") with { A = 128 }, outline.Stroke),
            () => Assert.DoesNotContain(scene.Primitives.OfType<RectPrimitive>(), r => ReferenceEquals(r.Element, gate)));
    }

    /// <summary>
    ///     Validates in the real window that double-clicking a switch lights the lamps immediately and undo darkens them again.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task MainWindow_SwitchDoubleClick_LightsAndDarkensTheLampsImmediately() => GuiTest.Run(() =>
    {
        // Arrange: SwitchA on, SwitchB off, so only the sum lamp is lit; turning SwitchB on lights the carry lamp and darkens the sum lamp
        using var host = new MainWindowHost(MainWindowHost.Options("--sample", "logic"));
        var nodes = host.Session.RequirePage().Nodes;
        var switchB = nodes.Single(n => n.Name == "SwitchB");
        var sum = nodes.Single(n => n.Name == "SumLamp");
        var carry = nodes.Single(n => n.Name == "CarryLamp");
        var theme = host.Session.Diagram.Theme;
        var point = host.Editor.TranslatePoint(host.Editor.DiagramToScreen(new AvaloniaPoint(switchB.Bounds.Center.X, switchB.Bounds.Center.Y)), host.Window) ?? default;
        var before = (sum: BoxFill(host.Editor.Scene!, sum), carry: BoxFill(host.Editor.Scene!, carry));

        // Act
        host.DoubleClick(point);
        var toggled = (sum: BoxFill(host.Editor.Scene!, sum), carry: BoxFill(host.Editor.Scene!, carry));
        host.Session.Diagram.History.Undo();
        var undone = (sum: BoxFill(host.Editor.Scene!, sum), carry: BoxFill(host.Editor.Scene!, carry));

        // Assert
        Assert.Multiple(
            () => Assert.Equal((LitGreen, theme.NodeFill), before),
            () => Assert.Equal((theme.NodeFill, LitRed), toggled),
            () => Assert.Equal(before, undone));
    });

    /// <summary>
    ///     Validates that the toggle-switch automation command lights the lamps in the editor's scene as well.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public Task AutomationDispatcher_ToggleSwitch_LightsTheLampsInTheEditorScene() => GuiTest.RunAsync(async host =>
    {
        // Arrange
        host.Session.OpenSample("logic");
        var carry = host.Session.RequirePage().Nodes.Single(n => n.Name == "CarryLamp");
        var theme = host.Session.Diagram.Theme;
        var off = BoxFill(host.Editor.Scene!, carry);

        // Act
        await host.OkAsync("""{"cmd":"toggle-switch","node":"SwitchB"}""");
        var on = BoxFill(host.Editor.Scene!, carry);

        // Assert
        Assert.Equal(theme.NodeFill, off);
        Assert.Equal(LitRed, on);
    });
}
