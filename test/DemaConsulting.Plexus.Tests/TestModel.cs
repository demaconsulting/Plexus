using System.Globalization;
using System.Text;
using DemaConsulting.Plexus;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Shared test fixtures: a small node type registry and a deterministic full-state dump of a diagram.
/// </summary>
/// <remarks>
///     The dump captures everything an undo/redo round trip must restore - ids, list order, z-order,
///     bounds, routes, properties, data, group structure - so tests can compare whole-model state
///     with a single string assertion.
/// </remarks>
internal static class TestModel
{
    /// <summary>
    ///     Creates a registry with port types and the node types used across the tests.
    /// </summary>
    /// <returns>The registry.</returns>
    public static NodeTypeRegistry CreateTypes()
    {
        var types = new NodeTypeRegistry();
        types.PortTypes.Add(new PortType("f32", Color.Parse("#FF8C00")));
        types.PortTypes.Add(new PortType("i32", Color.Parse("#1E90FF")));

        types.Add(new NodeType("acme.Producer")
        {
            Title = "PRODUCER",
            Outputs = { new PortDefinition("value", "f32") },
            Properties =
            {
                new PropertyDefinition("Format", "F32") { ShowInSummary = true },
                new PropertyDefinition("Gain", 1.0) { Unit = "x", ShowInSummary = true, DisplayName = "Gain" },
                new PropertyDefinition("Enabled", true),
                new PropertyDefinition("Count", 1),
                new PropertyDefinition("Big", 5L),
                new PropertyDefinition("Tint", Color.Parse("#112233")),
                new PropertyDefinition("Mode", "A") { Choices = ["A", "B"] },
            },
        });
        types.Add(new NodeType("acme.Consumer") { Inputs = { new PortDefinition("in", "f32") } });

        // An input that opts into fan-in, for tests of merging wires
        types.Add(new NodeType("acme.Merger") { Inputs = { new PortDefinition("in", "f32") { AllowsMultipleWires = true } } });
        types.Add(new NodeType("acme.IntConsumer") { Inputs = { new PortDefinition("in", "i32") } });
        types.Add(new NodeType("acme.Gain")
        {
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
        });
        types.Add(new NodeType("acme.Mixer")
        {
            Inputs =
            {
                new PortDefinition("a", "f32"),
                new PortDefinition("b", "f32"),
                new PortDefinition("c", "f32"),
                new PortDefinition("d", "f32"),
            },
            Outputs = { new PortDefinition("out", "f32") },
        });
        return types;
    }

    /// <summary>
    ///     Creates the standard test types plus three types whose ports come from a port provider.
    /// </summary>
    /// <remarks>
    ///     <c>acme.Bus</c> has one input per channel (<c>in0</c>, <c>in1</c>, ... of the type named by its <c>Kind</c> property,
    ///     with a "Channel n" description) and an output <c>out</c>; its declared defaults are the two inputs of
    ///     <c>Channels = 2</c>. <c>acme.Router</c> has an input <c>in</c> and one output per comma-separated name in its
    ///     <see cref="RouterData"/> payload (default <c>x,y</c>), and a declared size shorter than its two outputs need.
    ///     <c>acme.Collector</c> has one f32 input <c>in</c> and an f32 output <c>out</c>; the input allows fan-in while the
    ///     node holds an ad-hoc property value named <c>FanIn</c>.
    /// </remarks>
    /// <returns>The registry.</returns>
    public static NodeTypeRegistry CreateDynamicTypes()
    {
        var types = CreateTypes();
        types.Add(new NodeType("acme.Bus")
        {
            Inputs = { new PortDefinition("in0", "f32"), new PortDefinition("in1", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            Properties =
            {
                new PropertyDefinition("Channels", 2),
                new PropertyDefinition("Kind", "f32") { Choices = ["f32", "i32"] },
                new PropertyDefinition("Level", 1.0),
            },
            PortProvider = node => new PortSet(
                Enumerable.Range(0, Math.Clamp(node.Properties.Get<int>("Channels"), 0, 16))
                    .Select(i => new PortDefinition($"in{i}", (string)node.Properties["Kind"]) { Description = $"Channel {i}" }),
                [new PortDefinition("out", "f32")]),
        });
        types.Add(new NodeType("acme.Router")
        {
            DataType = typeof(RouterData),
            CreateData = () => new RouterData("x,y"),
            DefaultSize = new Size(120, 60),
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("x", "f32"), new PortDefinition("y", "f32") },
            PortProvider = node => new PortSet(
                [new PortDefinition("in", "f32")],
                ((node.Data as RouterData)?.Outputs ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(name => new PortDefinition(name, "f32"))),
        });
        types.Add(new NodeType("acme.Collector")
        {
            Inputs = { new PortDefinition("in", "f32") },
            Outputs = { new PortDefinition("out", "f32") },
            PortProvider = node => new PortSet(
                [new PortDefinition("in", "f32") { AllowsMultipleWires = node.Properties.ContainsKey("FanIn") }],
                [new PortDefinition("out", "f32")]),
        });
        return types;
    }

    /// <summary>
    ///     Creates a diagram with the standard and dynamic-port test types and no pages.
    /// </summary>
    /// <returns>The diagram.</returns>
    public static Diagram CreateDynamicDiagram() => new(CreateDynamicTypes());

    /// <summary>
    ///     Creates a diagram with the standard test types and no pages.
    /// </summary>
    /// <returns>The diagram.</returns>
    public static Diagram CreateDiagram() => new(CreateTypes());

    /// <summary>
    ///     Produces a deterministic text dump of every observable piece of model state.
    /// </summary>
    /// <param name="diagram">The diagram to dump.</param>
    /// <returns>The dump.</returns>
    public static string Dump(Diagram diagram)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < diagram.Pages.Count; i++)
        {
            var page = diagram.Pages[i];
            sb.Append(CultureInfo.InvariantCulture, $"page[{i}] '{page.Name}' attached={page.IsAttached}\n");
            sb.Append("  z: ").AppendJoin(',', page.Elements.Select(e => e.Id)).Append('\n');
            foreach (var node in page.Nodes)
            {
                DumpNode(sb, node);
            }

            foreach (var wire in page.Wires)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  wire {wire.Id} {wire.Source} -> {wire.Target} route=[")
                    .AppendJoin(';', wire.Route.Select(p => Format(p.X) + "," + Format(p.Y)))
                    .Append("] parent=").Append(wire.Parent?.Id).Append(" page=").Append(wire.Page == page).Append('\n');
            }

            foreach (var annotation in page.Annotations)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  annotation {annotation.Id} '{annotation.Text}' {Format(annotation.Bounds)} fill={annotation.Fill} parent={annotation.Parent?.Id}\n");
            }

            foreach (var group in page.Groups)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  group {group.Id} '{group.Title}' collapsed={group.IsCollapsed} pos={Format(group.CollapsedPosition.X)},{Format(group.CollapsedPosition.Y)} bounds={Format(group.Bounds)} parent={group.Parent?.Id} members=")
                    .AppendJoin(',', group.Members.Select(m => m.Id)).Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Formats a double with invariant culture and round-trip precision.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Formats a rectangle for dumps.
    /// </summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The text.</returns>
    public static string Format(Rect rect) =>
        $"({Format(rect.X)},{Format(rect.Y)},{Format(rect.Width)},{Format(rect.Height)})";

    /// <summary>
    ///     Asserts that a mutation creates exactly one history entry and that undo and redo restore the model exactly, twice over.
    /// </summary>
    /// <param name="diagram">The diagram being mutated.</param>
    /// <param name="mutate">The mutation to perform.</param>
    public static void AssertRoundTrip(Diagram diagram, Action mutate)
    {
        // Capture the full state and history depth before the mutation
        var before = Dump(diagram);
        var entriesBefore = diagram.History.UndoCount;

        // Apply the mutation and verify it is visible and recorded as one entry
        mutate();
        var after = Dump(diagram);
        Assert.NotEqual(before, after);
        Assert.Equal(entriesBefore + 1, diagram.History.UndoCount);

        // Undo then redo twice; each step must reproduce the captured states exactly
        for (var cycle = 0; cycle < 2; cycle++)
        {
            Assert.True(diagram.History.Undo());
            Assert.Equal(before, Dump(diagram));
            Assert.True(diagram.History.Redo());
            Assert.Equal(after, Dump(diagram));
        }
    }

    /// <summary>
    ///     Appends the dump of one node.
    /// </summary>
    /// <param name="sb">The builder.</param>
    /// <param name="node">The node.</param>
    private static void DumpNode(StringBuilder sb, Node node)
    {
        sb.Append(CultureInfo.InvariantCulture, $"  node {node.Id} {node.Type.Key} '{node.Name}' {Format(node.Bounds)} data={node.Data} parent={node.Parent?.Id} props=")
            .AppendJoin(';', node.Properties.Select(p => $"{p.Key}={p.Value}({p.Value.GetType().Name})"))
            .Append(" ports=")
            .AppendJoin(';', node.Ports.Select(p => $"{p.Direction}{p.Index}:{p.Name}:{p.TypeKey}:'{p.Label}':{p.Description}:{(p.AllowsMultipleWires ? "multi" : "single")}"))
            .Append('\n');
    }
}

/// <summary>
///     The payload of the <c>acme.Router</c> test type: the comma-separated names of its outputs.
/// </summary>
/// <param name="Outputs">The output names, for example <c>"x,y"</c>.</param>
internal sealed record RouterData(string Outputs);
