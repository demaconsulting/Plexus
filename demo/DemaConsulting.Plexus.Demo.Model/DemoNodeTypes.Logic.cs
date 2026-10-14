using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The logic gate node types: switches feed gates that drive lamps. The gates use a custom node visual.
///     AND, OR and XOR have an <c>Inputs</c> property (2 to 8): a port provider gives them that many inputs, named
///     <c>a</c>, <c>b</c>, <c>c</c>, ... so wires keep their input when the count changes. Every logic type has port
///     descriptions and a tooltip that shows its live state.
/// </content>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The key of the switch node type.
    /// </summary>
    public const string Switch = "demo.logic.Switch";

    /// <summary>
    ///     The key of the AND gate node type.
    /// </summary>
    public const string And = "demo.logic.And";

    /// <summary>
    ///     The key of the OR gate node type.
    /// </summary>
    public const string Or = "demo.logic.Or";

    /// <summary>
    ///     The key of the NOT gate node type.
    /// </summary>
    public const string Not = "demo.logic.Not";

    /// <summary>
    ///     The key of the XOR gate node type.
    /// </summary>
    public const string Xor = "demo.logic.Xor";

    /// <summary>
    ///     The key of the lamp node type.
    /// </summary>
    public const string Lamp = "demo.logic.Lamp";

    /// <summary>
    ///     The name of the property that sets how many inputs an AND, OR or XOR gate has.
    /// </summary>
    public const string GateInputsProperty = "Inputs";

    /// <summary>
    ///     The editor hint of the <see cref="GateInputsProperty"/> property: a host shows a 2-to-8 number box for it.
    /// </summary>
    public const string GateInputsEditorHint = "gate-inputs";

    /// <summary>
    ///     The fewest inputs an AND, OR or XOR gate has.
    /// </summary>
    public const int MinGateInputs = 2;

    /// <summary>
    ///     The most inputs an AND, OR or XOR gate has.
    /// </summary>
    public const int MaxGateInputs = 8;

    /// <summary>
    ///     The size of the two-input gates: the input rows sit symmetrically about the middle. Gates with more inputs grow taller.
    /// </summary>
    private static readonly Size GateSize = new(90, 75);

    /// <summary>
    ///     The input definitions of the multi-input gates, <c>a</c> to <c>h</c>; shared by every gate node, as definitions are immutable.
    /// </summary>
    private static readonly PortDefinition[] GateInputs =
    [
        .. Enumerable.Range(0, MaxGateInputs).Select(i =>
        {
            var name = ((char)('a' + i)).ToString();
            return new PortDefinition(name, Bool) { Description = $"Gate input {name}." };
        }),
    ];

    /// <summary>
    ///     The output definition of every gate.
    /// </summary>
    private static readonly PortDefinition GateOutput = new("out", Bool) { Description = "The gate's result." };

    /// <summary>
    ///     The size of the NOT gate: its single input and output sit on the middle row.
    /// </summary>
    private static readonly Size InverterSize = new(90, 60);

    /// <summary>
    ///     Adds the logic node types to a registry.
    /// </summary>
    /// <param name="types">The registry to fill.</param>
    private static void AddLogicTypes(NodeTypeRegistry types)
    {
        types.Add(Define(Switch, "SWITCH", "Switch", LogicCategory, "Switch",
            [],
            [new("out", Bool) { Description = "On while the switch is on." }],
            [new PropertyDefinition("On", false) { ShowInSummary = true }],
            description: "A switch the user turns on or off.",
            toolTip: node => $"A switch the user turns on or off.\nNow: {OnOff(node.Properties.Get<bool>("On"))}"));
        types.Add(Gate(And, "AND", GateShape.And, "The output is on when every input is on."));
        types.Add(Gate(Or, "OR", GateShape.Or, "The output is on when any input is on."));
        types.Add(Gate(Not, "NOT", GateShape.Not, "The output is on when the input is off."));
        types.Add(Gate(Xor, "XOR", GateShape.Xor, "The output is on when an odd number of inputs are on."));
        types.Add(Define(Lamp, "LAMP", "Lamp", LogicCategory, "Lamp",
            [new("in", Bool) { Description = "Lights the lamp while on." }],
            [],
            [Choice("Color", "Amber", ["Amber", "Red", "Green"])],
            description: "Lights while its input is on.",
            toolTip: node => $"Lights while its input is on.\nNow: {(LiveValue(node, node.Input("in")) ? "lit" : "dark")}"));
    }

    /// <summary>
    ///     Computes the inputs of an AND, OR or XOR gate node from its <see cref="GateInputsProperty"/> property.
    /// </summary>
    /// <remarks>
    ///     Values outside 2..8 are clamped, so any stored number gives a usable gate. The inputs are named <c>a</c>, <c>b</c>,
    ///     <c>c</c>, ...: a two-input gate has exactly the ports of earlier demo versions, so saved files keep their wires, and
    ///     changing the count keeps every wire on an input that still exists.
    /// </remarks>
    /// <param name="node">The gate node.</param>
    /// <returns>The gate's ports.</returns>
    public static PortSet GatePorts(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var count = Math.Clamp(node.Properties.Get<int>(GateInputsProperty), MinGateInputs, MaxGateInputs);
        return new PortSet(GateInputs.Take(count), [GateOutput]);
    }

    /// <summary>
    ///     Formats a logic value for a tooltip.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>"on" or "off".</returns>
    private static string OnOff(bool value) => value ? "on" : "off";

    /// <summary>
    ///     Evaluates the logic of a node's page and reads one port's current value.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="port">One of the node's ports.</param>
    /// <returns>The port's value; false when the node is not on a page.</returns>
    private static bool LiveValue(Node node, Port port) =>
        node.Page is { } page && LogicEvaluator.Evaluate(page).TryGetValue(port, out var value) && value;

    /// <summary>
    ///     Creates a gate node type drawn with a <see cref="GateVisual"/>.
    /// </summary>
    /// <remarks>
    ///     The NOT gate has one input <c>in</c>. The other gates get the <see cref="GateInputsProperty"/> property and
    ///     <see cref="GatePorts"/> as their port provider; their declared ports (<c>a</c>, <c>b</c>) are what a new gate has.
    /// </remarks>
    /// <param name="key">The node type key.</param>
    /// <param name="title">The gate name, also its display name.</param>
    /// <param name="shape">The symbol to draw.</param>
    /// <param name="rule">What the gate computes, the first line of its description and tooltip.</param>
    /// <returns>The node type.</returns>
    private static NodeType Gate(string key, string title, GateShape shape, string rule)
    {
        var description = $"{title} gate. {rule}";
        string ToolTip(Node node) => $"{description}\nOutput now: {OnOff(LiveValue(node, node.Output("out")))}";
        return shape == GateShape.Not
            ? Define(key, title, $"{title} gate", LogicCategory, ShortName(key),
                [new("in", Bool) { Description = "The value to invert." }],
                [GateOutput],
                [],
                new GateVisual(shape),
                size: InverterSize,
                description: description,
                toolTip: ToolTip)
            : Define(key, title, $"{title} gate", LogicCategory, ShortName(key),
                [GateInputs[0], GateInputs[1]],
                [GateOutput],
                [
                    new PropertyDefinition(GateInputsProperty, MinGateInputs)
                    {
                        Description = $"How many inputs the gate has, {MinGateInputs} to {MaxGateInputs}. They are named a, b, c, ...; other values are clamped.",
                        EditorHint = GateInputsEditorHint,
                    },
                ],
                new GateVisual(shape),
                size: GateSize,
                portProvider: GatePorts,
                description: description,
                toolTip: ToolTip);
    }

    /// <summary>
    ///     Gets the part of a node type key after its last dot.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <returns>The short name, which is also the icon name.</returns>
    private static string ShortName(string key) => key[(key.LastIndexOf('.') + 1)..];
}
