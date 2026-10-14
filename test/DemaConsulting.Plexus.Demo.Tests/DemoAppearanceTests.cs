using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Plexus.Demo.Cli;
using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Rendering;
using DemaConsulting.Plexus.Scene;
using PlexusColor = DemaConsulting.Plexus.Geometry.Color;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for <see cref="DemoAppearance"/>: wires colored by port type, by the fluid they carry and by live logic state.
/// </summary>
public class DemoAppearanceTests
{
    /// <summary>
    ///     The color of a logic wire that carries true.
    /// </summary>
    private static readonly PlexusColor LogicOn = PlexusColor.Parse("#00E83C");

    /// <summary>
    ///     The color of a logic wire that carries false.
    /// </summary>
    private static readonly PlexusColor LogicOff = PlexusColor.Parse("#3A3A3A");

    /// <summary>
    ///     Finds the wire that leaves a named port, which must be unique unless the target port is also named.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="from">The source port as <c>Node.port</c>.</param>
    /// <param name="to">The target port as <c>Node.port</c>, or null when the source port has one wire.</param>
    /// <returns>The wire.</returns>
    private static Wire WireFrom(Diagram diagram, string from, string? to = null) =>
        diagram.Pages.SelectMany(p => p.Wires).Single(w => w.Source.ToString() == from && (to is null || w.Target.ToString() == to));

    /// <summary>
    ///     Gets the style the diagram's styler gives a wire.
    /// </summary>
    /// <param name="wire">The wire.</param>
    /// <returns>The style, or null.</returns>
    private static WireStyle? StyleOf(Wire wire) => wire.Page!.Diagram.WireStyler!(wire);

    /// <summary>
    ///     Validates that one shared options object serves new, loaded and sample diagrams alike.
    /// </summary>
    [Fact]
    public void DemoAppearance_Options_IsTheOneConfigurationOfEveryDiagram()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Create(), "logic.json");
        DemoDocument.Save(DemoSamples.Logic(), path);

        // Act
        var created = new Diagram(DemoNodeTypes.CreateRegistry(), DemoAppearance.Options);
        var loaded = DemoDocument.Load(path);
        var sample = DemoSamples.Create(DemoSamples.Names[0]);

        // Assert
        Assert.All(new[] { created, loaded, sample }, diagram =>
        {
            Assert.Same(DemoAppearance.Options.WireStyler, diagram.WireStyler);
            Assert.Same(DemoAppearance.Options.NodeStyler, diagram.NodeStyler);
            Assert.Same(DemoAppearance.Options.ConnectionRules, diagram.ConnectionRules);
        });
    }

    /// <summary>
    ///     Validates that the demo file helpers read and write through streams without leaving the file locked.
    /// </summary>
    [Fact]
    public void DemoDocument_SaveLoad_UsesStreamsAndReleasesTheFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Create(), "logic.json");
        var sample = DemoSamples.Logic();

        // Act
        DemoDocument.Save(sample, path);
        var loaded = DemoDocument.Load(path);
        File.Delete(path);

        // Assert
        Assert.Equal(sample.ToJson(), loaded.ToJson());
        Assert.False(File.Exists(path));
    }

    /// <summary>
    ///     Validates that every sample and every loaded file has the styler installed, and that a bare diagram does not.
    /// </summary>
    [Fact]
    public void DemoAppearance_Apply_IsInstalledByEverySampleAndByLoad()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Create(), "logic.json");
        DemoDocument.Save(DemoSamples.Logic(), path);

        // Act
        var bare = new Diagram(DemoNodeTypes.CreateRegistry());

        // Assert: the styler is configuration, so it is not in the file and Load must install it again
        Assert.All(DemoSamples.Names, name => Assert.NotNull(DemoSamples.Create(name).WireStyler));
        Assert.NotNull(DemoDocument.Load(path).WireStyler);
        Assert.Null(bare.WireStyler);
        bare.Apply(DemoAppearance.Options);
        Assert.NotNull(bare.WireStyler);
        Assert.Same(DemoAppearance.Options.WireStyler, bare.WireStyler);
        Assert.Throws<ArgumentNullException>(() => bare.Apply(null!));
    }

    /// <summary>
    ///     Validates that the demo refuses a wire between ports of different types (a number cannot drive an audio input) and still allows equal types.
    /// </summary>
    [Fact]
    public void DemoAppearance_Apply_RefusesWiresBetweenDifferentPortTypes()
    {
        // Arrange
        var diagram = DemoSamples.Math();
        var page = diagram.Pages[0];
        var constant = page.Nodes.Single(n => n.Name == "Bias");
        var speaker = page.AddNode("demo.audio.Speaker", 900, 400);
        var scope = page.AddNode("demo.math.Scope", 900, 600);

        // Act
        var mismatch = page.CanConnect(constant.Output("value"), speaker.Input("in"));
        var match = page.CanConnect(constant.Output("value"), scope.Input("signal"));

        // Assert
        Assert.Multiple(
            () => Assert.False(mismatch.IsAllowed),
            () => Assert.Contains("number", mismatch.Reason!, StringComparison.Ordinal),
            () => Assert.True(match.IsAllowed));
    }

    /// <summary>
    ///     Validates that the styler needs no subscription to the diagram's changes: the per-page state is keyed by the diagram version.
    /// </summary>
    [Fact]
    public void DemoAppearance_Apply_SubscribesToNothingAndFollowsEditsWithoutRefresh()
    {
        // Arrange
        var diagram = DemoSamples.Logic();
        var changedField = typeof(Diagram).GetField(nameof(Diagram.Changed), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var switchB = diagram.Pages[0].Nodes.Single(n => n.Name == "SwitchB");
        var wire = switchB.Output("out").Wires[0];

        // Act: read the wire before and after a property edit, with no RefreshAppearance in between
        var off = StyleOf(wire)!.Color;
        switchB.Properties["On"] = true;
        var on = StyleOf(wire)!.Color;

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(changedField),
            () => Assert.Null(changedField!.GetValue(diagram)),
            () => Assert.Equal(LogicOff, off),
            () => Assert.Equal(LogicOn, on));
    }

    /// <summary>
    ///     Validates that wires that are neither fluid nor logic keep the color of their port type.
    /// </summary>
    [Fact]
    public void DemoAppearance_Styler_OtherWires_UsePortTypeColor()
    {
        // Arrange
        var plumbing = DemoSamples.Plumbing();
        var math = DemoSamples.Math();

        // Act
        var reading = StyleOf(WireFrom(plumbing, "Meter.reading"));
        var number = StyleOf(WireFrom(math, "Wave.value"));

        // Assert
        Assert.Equal(PlexusColor.Parse("#FF8C00"), reading!.Color);
        Assert.Equal(PlexusColor.Parse("#FF8C00"), number!.Color);
        Assert.Null(number.Dash);
    }

    /// <summary>
    ///     Validates that the fluid is resolved upstream: through a valve, a flow meter and a junction to the pump or tank that supplies it.
    /// </summary>
    [Fact]
    public void DemoAppearance_FluidOf_WalksUpstreamThroughValvesMetersAndJunctions()
    {
        // Arrange: the sample is all water; make the pump pump oil
        var diagram = DemoSamples.Plumbing();
        diagram.FindNode("Feed")!.Properties["Fluid"] = "Oil";

        // Act
        var fluids = new[] { "Supply.out", "Feed.out", "Throttle.out", "Meter.flow", "Split.a", "Split.b" }
            .ToDictionary(port => port, port => DemoAppearance.FluidOf(WireFrom(diagram, port)));

        // Assert: the tank upstream of the pump still supplies water; everything after the pump carries oil
        Assert.Equal("Water", fluids["Supply.out"]);
        Assert.All(fluids.Where(f => f.Key != "Supply.out"), f => Assert.Equal("Oil", f.Value));
        Assert.Null(DemoAppearance.FluidOf(WireFrom(diagram, "Meter.reading")));
    }

    /// <summary>
    ///     Validates the colors and dash of each fluid, and that changing the fluid upstream restyles the wires downstream.
    /// </summary>
    [Fact]
    public void DemoAppearance_Styler_FluidWires_TakeTheFluidColorAndSteamIsDashed()
    {
        // Arrange
        var diagram = DemoSamples.Plumbing();
        var wire = WireFrom(diagram, "Throttle.out");
        var water = StyleOf(wire)!;

        // Act
        diagram.FindNode("Feed")!.Properties["Fluid"] = "Oil";
        var oil = StyleOf(wire)!;
        diagram.FindNode("Feed")!.Properties["Fluid"] = "Steam";
        var steam = StyleOf(wire)!;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(PlexusColor.Parse("#1E78E6"), water.Color),
            () => Assert.Null(water.Dash),
            () => Assert.Equal(PlexusColor.Parse("#A8600F"), oil.Color),
            () => Assert.Null(oil.Dash),
            () => Assert.Equal(PlexusColor.Parse("#E4E8EC"), steam.Color),
            () => Assert.Equal([8, 5], steam.Dash));
    }

    /// <summary>
    ///     Validates that a pipe nothing feeds, and a pipe network that loops back on itself, keep the plain fluid port color instead of failing.
    /// </summary>
    [Fact]
    public void DemoAppearance_Styler_UnfedAndLoopedPipes_FallBackToPortColor()
    {
        // Arrange: two valves feeding each other, and a lone valve into a drain
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry(), DemoAppearance.Options);
        var page = diagram.Pages.Add("Loop");
        page.AddNode(DemoNodeTypes.Valve, 0, 0, "V1");
        page.AddNode(DemoNodeTypes.Valve, 200, 0, "V2");
        page.AddNode(DemoNodeTypes.Drain, 400, 0, "D");
        page.Connect("V1.out", "V2.in");
        page.Connect("V2.out", "V1.in");
        page.Connect("V2.out", "D.in");

        // Act
        var styles = page.Wires.Select(StyleOf).ToList();

        // Assert
        Assert.Null(DemoAppearance.FluidOf(WireFrom(diagram, "V1.out")));
        Assert.All(styles, style => Assert.Equal(PlexusColor.Parse("#1E90FF"), style!.Color));
    }

    /// <summary>
    ///     Validates that logic wires are bright and thick while true and dark and thin while false, following the switches.
    /// </summary>
    [Fact]
    public void DemoAppearance_Styler_LogicWires_FollowTheSwitches()
    {
        // Arrange: SwitchA is on and SwitchB is off in the sample, so Sum is true and Carry is false
        var diagram = DemoSamples.Logic();
        var switchA = WireFrom(diagram, "SwitchA.out", "Sum.a");
        var sum = WireFrom(diagram, "Sum.out");
        var carry = WireFrom(diagram, "Carry.out");

        // Act
        var before = (switchA: StyleOf(switchA)!, sum: StyleOf(sum)!, carry: StyleOf(carry)!);
        var switchB = diagram.FindNode("SwitchB")!;
        switchB.Properties["On"] = true;
        var after = (sum: StyleOf(sum)!, carry: StyleOf(carry)!, switchB: StyleOf(WireFrom(diagram, "SwitchB.out", "Sum.b"))!);

        // Assert: with both switches on the XOR drops to false and the AND rises to true
        Assert.Multiple(
            () => Assert.Equal((LogicOn, 4), (before.switchA.Color, before.switchA.Thickness)),
            () => Assert.Equal((LogicOn, 4), (before.sum.Color, before.sum.Thickness)),
            () => Assert.Equal((LogicOff, 2), (before.carry.Color, before.carry.Thickness)),
            () => Assert.Equal(LogicOff, after.sum.Color),
            () => Assert.Equal(LogicOn, after.carry.Color),
            () => Assert.Equal(LogicOn, after.switchB.Color));
    }

    /// <summary>
    ///     Validates that the styled colors always agree with a fresh evaluation, across toggles, undo and redo.
    /// </summary>
    [Fact]
    public void DemoAppearance_Styler_LogicWires_StayInStepWithTheEvaluatorAcrossUndoAndRedo()
    {
        // Arrange
        var diagram = DemoSamples.Logic();
        var page = diagram.Pages.Single();
        var switchB = diagram.FindNode("SwitchB")!;

        // Act / Assert
        void AssertInStep()
        {
            var truth = LogicEvaluator.Evaluate(page);
            Assert.All(page.Wires, wire => Assert.Equal(truth[wire.Source] ? LogicOn : LogicOff, StyleOf(wire)!.Color));
        }

        AssertInStep();
        switchB.Properties["On"] = true;
        AssertInStep();
        Assert.True(diagram.History.Undo());
        AssertInStep();
        Assert.True(diagram.History.Redo());
        AssertInStep();
        Assert.Equal(LogicOn, StyleOf(WireFrom(diagram, "SwitchB.out", "Sum.b"))!.Color);
    }

    /// <summary>
    ///     Validates that the command-line renderer draws the styled wires: the logic page contains the "true" color and an unstyled copy does not.
    /// </summary>
    [Fact]
    public void CliRunner_Run_LogicSample_DrawsTheStyledWires()
    {
        // Arrange
        using var temp = new TempDirectory();
        var styledDir = Path.Combine(temp.Path, "styled");
        var plainFile = Path.Combine(temp.Path, "plain.png");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var plain = DemoSamples.Logic();
        plain.WireStyler = null;

        // Act
        var code = CliRunner.Run(["--sample", "logic", "--out", styledDir], output, error);
        PlexusRenderer.SavePng(plain.Pages.Single(), plainFile);

        // Assert
        Assert.Equal(CliRunner.Success, code);
        using var styled = PngCodec.Load(Path.Combine(styledDir, "Logic.png"));
        using var unstyled = PngCodec.Load(plainFile);
        var on = new Rgba32(LogicOn.R, LogicOn.G, LogicOn.B, LogicOn.A);
        Assert.True(Count(styled, on) > 200, "the styled render has bright green logic wires");
        Assert.Equal(0, Count(unstyled, on));
    }

    /// <summary>
    ///     Validates that the command-line renderer draws steam pipes in the dashed light gray of the styler.
    /// </summary>
    [Fact]
    public void CliRunner_Run_PlumbingSample_DrawsFluidColoredPipes()
    {
        // Arrange
        using var temp = new TempDirectory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var code = CliRunner.Run(["--sample", "plumbing", "--out", temp.Path], output, error);

        // Assert: oil brown and steam gray both occur on the page, which the theme alone would draw black
        Assert.Equal(CliRunner.Success, code);
        using var image = PngCodec.Load(Path.Combine(temp.Path, "Plumbing.png"));
        Assert.True(Count(image, new Rgba32(0xA8, 0x60, 0x0F, 0xFF)) > 100, "oil pipes are brown");
        Assert.True(Count(image, new Rgba32(0xE4, 0xE8, 0xEC, 0xFF)) > 50, "steam pipes are light gray");
        Assert.True(Count(image, new Rgba32(0x1E, 0x78, 0xE6, 0xFF)) > 200, "water pipes are blue");
    }

    /// <summary>
    ///     Counts the pixels of an image that have exactly one color.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of matching pixels.</returns>
    private static int Count(Surface image, Rgba32 color)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].Equals(color))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
