namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     Evaluates the logic gate nodes of a page to the live true/false value of every port.
/// </summary>
/// <remarks>
///     The evaluator is a pure function of the page: it never changes the model. A host can use the
///     result to color logic wires by their current state, because a wire carries the value of its
///     source port. Only the demo's logic node types are understood (switch, AND, OR, NOT, XOR and
///     lamp); nodes of other types are ignored, and a port that is fed by one of them, or by
///     nothing, reads as false. A feedback loop is cut where it closes, which also reads as false.
/// </remarks>
public static class LogicEvaluator
{
    /// <summary>
    ///     Evaluates every port of every logic node on a page.
    /// </summary>
    /// <param name="page">The page to evaluate. Must not be null.</param>
    /// <returns>The value of each input and output port of the page's logic nodes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is null.</exception>
    public static IReadOnlyDictionary<Port, bool> Evaluate(DiagramPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        // Outputs are computed on demand and memoized; the visiting set breaks feedback loops
        var outputs = new Dictionary<Port, bool>();
        var visiting = new HashSet<Port>();
        var result = new Dictionary<Port, bool>();
        foreach (var node in page.Nodes.Where(IsLogic))
        {
            foreach (var output in node.Outputs)
            {
                result[output] = OutputValue(output, outputs, visiting);
            }

            foreach (var input in node.Inputs)
            {
                result[input] = InputValue(input, outputs, visiting);
            }
        }

        return result;
    }

    /// <summary>
    ///     Determines whether a node is one of the demo's logic types.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>True for a switch, gate or lamp.</returns>
    private static bool IsLogic(Node node) => node.Type.Key is
        DemoNodeTypes.Switch or DemoNodeTypes.And or DemoNodeTypes.Or or DemoNodeTypes.Not or DemoNodeTypes.Xor or DemoNodeTypes.Lamp;

    /// <summary>
    ///     Gets the value arriving at an input port: true when any wire into it carries true.
    /// </summary>
    /// <param name="input">The input port.</param>
    /// <param name="outputs">The memoized output values.</param>
    /// <param name="visiting">The outputs currently being evaluated.</param>
    /// <returns>The input value; false when nothing is connected.</returns>
    private static bool InputValue(Port input, Dictionary<Port, bool> outputs, HashSet<Port> visiting) =>
        input.Wires.Any(w => w.Target == input && OutputValue(w.Source, outputs, visiting));

    /// <summary>
    ///     Gets the value an output port drives.
    /// </summary>
    /// <param name="output">The output port.</param>
    /// <param name="outputs">The memoized output values.</param>
    /// <param name="visiting">The outputs currently being evaluated.</param>
    /// <returns>The output value; false for ports of nodes that are not logic nodes.</returns>
    private static bool OutputValue(Port output, Dictionary<Port, bool> outputs, HashSet<Port> visiting)
    {
        if (outputs.TryGetValue(output, out var known))
        {
            return known;
        }

        // Re-entering an output that is still being computed means a loop; cut it
        if (!IsLogic(output.Node) || !visiting.Add(output))
        {
            return false;
        }

        var node = output.Node;
        var value = node.Type.Key switch
        {
            DemoNodeTypes.Switch => node.Properties.Get<bool>("On"),
            DemoNodeTypes.Not => !InputValue(node.Input("in"), outputs, visiting),
            DemoNodeTypes.And or DemoNodeTypes.Or or DemoNodeTypes.Xor => Combine(node, outputs, visiting),
            _ => false,
        };
        visiting.Remove(output);
        outputs[output] = value;
        return value;
    }

    /// <summary>
    ///     Evaluates a two-input gate.
    /// </summary>
    /// <param name="node">The AND, OR or XOR node.</param>
    /// <param name="outputs">The memoized output values.</param>
    /// <param name="visiting">The outputs currently being evaluated.</param>
    /// <returns>The gate's output value.</returns>
    private static bool Combine(Node node, Dictionary<Port, bool> outputs, HashSet<Port> visiting)
    {
        // Both inputs are always read, so every input of the gate takes part in the loop detection
        var a = InputValue(node.Input("a"), outputs, visiting);
        var b = InputValue(node.Input("b"), outputs, visiting);
        return node.Type.Key switch
        {
            DemoNodeTypes.And => a && b,
            DemoNodeTypes.Or => a || b,
            _ => a != b,
        };
    }
}
