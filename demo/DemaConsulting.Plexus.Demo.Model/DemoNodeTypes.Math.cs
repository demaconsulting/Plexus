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
        types.Add(Define(Constant, "CONSTANT", "Constant", MathCategory, "Constant",
            [],
            [new("value", Number)],
            [Summarized("Value", 1.0)]));
        types.Add(Define(Add, "ADD", "Add", MathCategory, "Add",
            [new("a", Number), new("b", Number)],
            [new("sum", Number)],
            []));
        types.Add(Define(Multiply, "MULTIPLY", "Multiply", MathCategory, "Multiply",
            [new("a", Number), new("b", Number)],
            [new("product", Number)],
            []));
        types.Add(Define(Sine, "SINE", "Sine wave", MathCategory, "Sine",
            [],
            [new("value", Number)],
            [Summarized("Amplitude", 1.0), Summarized("Frequency", 1.0, "Hz")]));
        types.Add(Define(Clamp, "CLAMP", "Clamp", MathCategory, "Clamp",
            [new("value", Number)],
            [new("result", Number)],
            [Summarized("Min", 0.0), Summarized("Max", 1.0)]));
        types.Add(Define(Scope, "SCOPE", "Scope", MathCategory, "Scope",

            // A scope overlays every trace wired to it, so it is one of the inputs that accept fan-in
            [new("signal", Number) { AllowsMultipleWires = true }],
            [],
            [Summarized("Samples", 256), new PropertyDefinition("Autoscale", true) { ShowInSummary = true }]));
    }
}
