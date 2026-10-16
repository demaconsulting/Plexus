namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The math / dataflow node types: numbers flow from sources through operators into a display.
/// </content>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The key of the constant node type.
    /// </summary>
    public const string Constant = "demo.math.Constant";

    /// <summary>
    ///     The key of the add node type.
    /// </summary>
    public const string Add = "demo.math.Add";

    /// <summary>
    ///     The key of the multiply node type.
    /// </summary>
    public const string Multiply = "demo.math.Multiply";

    /// <summary>
    ///     The key of the sine node type.
    /// </summary>
    public const string Sine = "demo.math.Sine";

    /// <summary>
    ///     The key of the clamp node type.
    /// </summary>
    public const string Clamp = "demo.math.Clamp";

    /// <summary>
    ///     The key of the scope (display) node type.
    /// </summary>
    public const string Scope = "demo.math.Scope";

    /// <summary>
    ///     Adds the math node types to a registry.
    /// </summary>
    /// <param name="types">The registry to fill.</param>
    private static void AddMathTypes(NodeTypeRegistry types)
    {
        const string constant = "Outputs a fixed number.";
        types.Add(Define(Constant, "CONSTANT", "Constant", MathCategory, "Constant",
            [],
            [new("value", Number) { Description = "The constant's value." }],
            [Summarized("Value", 1.0, description: "The number the node outputs.")],
            description: constant,
            toolTip: Live(constant, node => $"Value: {Value(node, "Value")}")));
        types.Add(Define(Add, "ADD", "Add", MathCategory, "Add",
            [new("a", Number) { Description = "The first addend." }, new("b", Number) { Description = "The second addend." }],
            [new("sum", Number) { Description = "a + b" }],
            [],
            description: "Adds two numbers."));
        types.Add(Define(Multiply, "MULTIPLY", "Multiply", MathCategory, "Multiply",
            [new("a", Number) { Description = "The first factor." }, new("b", Number) { Description = "The second factor." }],
            [new("product", Number) { Description = "a × b" }],
            [],
            description: "Multiplies two numbers."));
        const string sine = "Outputs a sine wave that changes over time.";
        types.Add(Define(Sine, "SINE", "Sine wave", MathCategory, "Sine",
            [],
            [new("value", Number) { Description = "The wave's current value." }],
            [Summarized("Amplitude", 1.0, description: "The peak value of the wave."), Summarized("Frequency", 1.0, "Hz", description: "How many cycles the wave makes per second.")],
            description: sine,
            toolTip: Live(sine, node => $"Amplitude {Value(node, "Amplitude")} · {Value(node, "Frequency")}")));
        const string clamp = "Limits a value to the range Min..Max.";
        types.Add(Define(Clamp, "CLAMP", "Clamp", MathCategory, "Clamp",
            [new("value", Number) { Description = "The value to limit." }],
            [new("result", Number) { Description = "The value, limited to Min..Max." }],
            [Summarized("Min", 0.0), Summarized("Max", 1.0)],
            description: clamp,
            toolTip: Live(clamp, node => $"Range {Value(node, "Min")} .. {Value(node, "Max")}")));
        const string scope = "Plots every signal wired to it.";
        types.Add(Define(Scope, "SCOPE", "Scope", MathCategory, "Scope",

            // A scope overlays every trace wired to it, so it is one of the inputs that accept fan-in
            [new("signal", Number) { AllowsMultipleWires = true, Description = "The traces to plot; accepts several wires." }],
            [],
            [Summarized("Samples", 256), new PropertyDefinition("Autoscale", true) { ShowInSummary = true }],
            description: scope,
            toolTip: Live(scope, node => $"Traces: {TraceCount(node)} · {Value(node, "Samples")} samples")));
    }

    /// <summary>
    ///     Counts the wires that feed a scope's <c>signal</c> input.
    /// </summary>
    /// <param name="node">The scope node.</param>
    /// <returns>The number of traces; 0 when the node is not on a page.</returns>
    private static int TraceCount(Node node)
    {
        var signal = node.Input("signal");
        return node.Page?.Wires.Count(w => ReferenceEquals(w.Target, signal)) ?? 0;
    }
}
