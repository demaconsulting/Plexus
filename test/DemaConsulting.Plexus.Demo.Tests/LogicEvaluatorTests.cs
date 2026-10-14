using DemaConsulting.Plexus.Demo.Model;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Demo.Tests;

/// <summary>
///     Tests for <see cref="LogicEvaluator"/> and the custom gate visual of the logic node types.
/// </summary>
public class LogicEvaluatorTests
{
    /// <summary>
    ///     Creates a page on a fresh demo diagram.
    /// </summary>
    /// <returns>The page.</returns>
    private static DiagramPage CreatePage() => new Diagram(DemoNodeTypes.CreateRegistry()).Pages.Add("Main");

    /// <summary>
    ///     Adds a switch to a page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="name">The node name.</param>
    /// <param name="on">Whether the switch is on.</param>
    /// <returns>The switch node.</returns>
    private static Node AddSwitch(DiagramPage page, string name, bool on)
    {
        var node = page.AddNode(DemoNodeTypes.Switch, 0, 0, name);
        node.Properties["On"] = on;
        return node;
    }

    /// <summary>
    ///     Validates the truth tables of the two-input gates.
    /// </summary>
    /// <param name="type">The gate node type key.</param>
    /// <param name="a">The first switch state.</param>
    /// <param name="b">The second switch state.</param>
    /// <param name="expected">The expected gate output.</param>
    [Theory]
    [InlineData(DemoNodeTypes.And, false, false, false)]
    [InlineData(DemoNodeTypes.And, true, false, false)]
    [InlineData(DemoNodeTypes.And, false, true, false)]
    [InlineData(DemoNodeTypes.And, true, true, true)]
    [InlineData(DemoNodeTypes.Or, false, false, false)]
    [InlineData(DemoNodeTypes.Or, true, false, true)]
    [InlineData(DemoNodeTypes.Or, false, true, true)]
    [InlineData(DemoNodeTypes.Or, true, true, true)]
    [InlineData(DemoNodeTypes.Xor, false, false, false)]
    [InlineData(DemoNodeTypes.Xor, true, false, true)]
    [InlineData(DemoNodeTypes.Xor, false, true, true)]
    [InlineData(DemoNodeTypes.Xor, true, true, false)]
    public void LogicEvaluator_Evaluate_TwoInputGate_FollowsItsTruthTable(string type, bool a, bool b, bool expected)
    {
        // Arrange
        var page = CreatePage();
        AddSwitch(page, "A", a);
        AddSwitch(page, "B", b);
        var gate = page.AddNode(type, 0, 0, "G");
        page.Connect("A.out", "G.a");
        page.Connect("B.out", "G.b");

        // Act
        var values = LogicEvaluator.Evaluate(page);

        // Assert: the inputs read what the switches drive and the output is the gate function
        Assert.Equal(a, values[gate.Input("a")]);
        Assert.Equal(b, values[gate.Input("b")]);
        Assert.Equal(expected, values[gate.Output("out")]);
    }

    /// <summary>
    ///     Validates that a switch drives its own state and an inverter negates its input.
    /// </summary>
    /// <param name="on">The switch state.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LogicEvaluator_Evaluate_SwitchAndNot_PassAndInvert(bool on)
    {
        // Arrange
        var page = CreatePage();
        var source = AddSwitch(page, "S", on);
        var inverter = page.AddNode(DemoNodeTypes.Not, 0, 0, "N");
        var lamp = page.AddNode(DemoNodeTypes.Lamp, 0, 0, "L");
        page.Connect("S.out", "N.in");
        page.Connect("N.out", "L.in");

        // Act
        var values = LogicEvaluator.Evaluate(page);

        // Assert
        Assert.Equal(on, values[source.Output("out")]);
        Assert.Equal(!on, values[inverter.Output("out")]);
        Assert.Equal(!on, values[lamp.Input("in")]);
    }

    /// <summary>
    ///     Validates that an input with no wire reads false, so an unconnected inverter outputs true.
    /// </summary>
    [Fact]
    public void LogicEvaluator_Evaluate_UnconnectedInputs_ReadFalse()
    {
        // Arrange
        var page = CreatePage();
        AddSwitch(page, "A", true);
        var and = page.AddNode(DemoNodeTypes.And, 0, 0, "G");
        var inverter = page.AddNode(DemoNodeTypes.Not, 0, 0, "N");
        var lamp = page.AddNode(DemoNodeTypes.Lamp, 0, 0, "L");
        page.Connect("A.out", "G.a");

        // Act
        var values = LogicEvaluator.Evaluate(page);

        // Assert
        Assert.False(values[and.Input("b")]);
        Assert.False(values[and.Output("out")]);
        Assert.True(values[inverter.Output("out")]);
        Assert.False(values[lamp.Input("in")]);
    }

    /// <summary>
    ///     Validates that a feedback loop is cut instead of recursing forever.
    /// </summary>
    /// <returns>A task for xUnit to await.</returns>
    [Fact]
    public async Task LogicEvaluator_Evaluate_FeedbackLoop_Terminates()
    {
        // Arrange: two inverters feeding each other, and a ring of three
        var page = CreatePage();
        var first = page.AddNode(DemoNodeTypes.Not, 0, 0, "N1");
        var second = page.AddNode(DemoNodeTypes.Not, 0, 0, "N2");
        var ring = Enumerable.Range(3, 3).Select(i => page.AddNode(DemoNodeTypes.Not, 0, 0, $"N{i}")).ToList();
        page.Connect("N1.out", "N2.in");
        page.Connect("N2.out", "N1.in");
        page.Connect("N3.out", "N4.in");
        page.Connect("N4.out", "N5.in");
        page.Connect("N5.out", "N3.in");

        // Act
        var values = await Task.Run(() => LogicEvaluator.Evaluate(page), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: every port still gets a value
        Assert.All([first, second, .. ring], node => Assert.All(node.Inputs.Concat(node.Outputs), port => Assert.Contains(port, values.Keys)));
    }

    /// <summary>
    ///     Validates that nodes of other domains are ignored and do not disturb the logic nodes next to them.
    /// </summary>
    [Fact]
    public void LogicEvaluator_Evaluate_NonLogicNodes_AreIgnored()
    {
        // Arrange
        var page = CreatePage();
        var add = page.AddNode(DemoNodeTypes.Add, 0, 0, "Sum");
        var pump = page.AddNode(DemoNodeTypes.Pump, 0, 0, "Feed");
        var source = AddSwitch(page, "S", true);

        // Act
        var values = LogicEvaluator.Evaluate(page);

        // Assert
        Assert.Equal([.. source.Inputs.Concat(source.Outputs)], values.Keys);
        Assert.DoesNotContain(add.Output("sum"), values.Keys);
        Assert.DoesNotContain(pump.Input("in"), values.Keys);
    }

    /// <summary>
    ///     Validates that an empty page evaluates to nothing and a null page is rejected.
    /// </summary>
    [Fact]
    public void LogicEvaluator_Evaluate_EmptyAndNullPage_AreHandled()
    {
        // Arrange / Act / Assert
        Assert.Empty(LogicEvaluator.Evaluate(CreatePage()));
        Assert.Throws<ArgumentNullException>(() => LogicEvaluator.Evaluate(null!));
    }

    /// <summary>
    ///     Validates the logic sample: with switch A on and switch B off the sum lamp lights, the carry and neither lamps do not.
    /// </summary>
    [Fact]
    public void LogicEvaluator_Evaluate_LogicSample_LightsTheExpectedLamps()
    {
        // Arrange
        var diagram = DemoSamples.Logic();

        // Act
        var values = LogicEvaluator.Evaluate(diagram.Pages.Single());

        // Assert
        Assert.True(diagram.FindNode("SwitchA")!.Properties.Get<bool>("On"));
        Assert.False(diagram.FindNode("SwitchB")!.Properties.Get<bool>("On"));
        Assert.True(values[diagram.FindNode("SumLamp")!.Input("in")]);
        Assert.False(values[diagram.FindNode("CarryLamp")!.Input("in")]);
        Assert.False(values[diagram.FindNode("NeitherLamp")!.Input("in")]);
    }

    /// <summary>
    ///     Validates that the gate node types use a custom visual and the other logic types the standard one.
    /// </summary>
    [Fact]
    public void DemoNodeTypes_Gates_HaveACustomVisual()
    {
        // Arrange
        var registry = DemoNodeTypes.CreateRegistry();

        // Act / Assert
        foreach (var key in new[] { DemoNodeTypes.And, DemoNodeTypes.Or, DemoNodeTypes.Not, DemoNodeTypes.Xor })
        {
            Assert.IsType<GateVisual>(registry.Get(key).Visual);
        }

        Assert.Null(registry.Get(DemoNodeTypes.Switch).Visual);
        Assert.Null(registry.Get(DemoNodeTypes.Lamp).Visual);
    }

    /// <summary>
    ///     Validates that a scene built from the logic sample draws the gates as paths and that a gate's centre hits the node.
    /// </summary>
    [Fact]
    public void GateVisual_LogicSampleScene_DrawsPathsAndHitTestsTheNode()
    {
        // Arrange
        var page = DemoSamples.Logic().Pages.Single();
        var gate = page.Nodes.Single(n => n.Name == "Sum");

        // Act
        var scene = SceneBuilder.Build(page);
        Assert.True(scene.TryGetNodeLayout(gate, out var layout));
        var hit = scene.HitTest(layout.Box.Center);

        // Assert: three leads, the outline and the extra XOR curve are paths inside the gate's box
        Assert.True(scene.Primitives.OfType<PathPrimitive>().Count(p => layout.Box.Contains(p.Bounds.Center)) >= 5);
        Assert.Equal(HitKind.Node, hit.Kind);
        Assert.Same(gate, hit.Node);
    }
}
