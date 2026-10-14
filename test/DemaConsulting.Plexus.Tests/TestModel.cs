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
            .Append('\n');
    }
}
