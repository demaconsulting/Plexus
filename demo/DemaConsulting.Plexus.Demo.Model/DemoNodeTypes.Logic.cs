using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The logic gate node types: switches feed gates that drive lamps. The gates use a custom node visual.
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
    ///     The size of the two-input gates: the input rows sit symmetrically about the middle.
    /// </summary>
    private static readonly Size GateSize = new(90, 75);

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
            [new("out", Bool)],
            [new PropertyDefinition("On", false) { ShowInSummary = true }]));
        types.Add(Gate(And, "AND", GateShape.And, 2));
        types.Add(Gate(Or, "OR", GateShape.Or, 2));
        types.Add(Gate(Not, "NOT", GateShape.Not, 1));
        types.Add(Gate(Xor, "XOR", GateShape.Xor, 2));
        types.Add(Define(Lamp, "LAMP", "Lamp", LogicCategory, "Lamp",
            [new("in", Bool)],
            [],
            [Choice("Color", "Amber", ["Amber", "Red", "Green"])]));
    }

    /// <summary>
    ///     Creates a gate node type drawn with a <see cref="GateVisual"/>.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <param name="title">The gate name, also its display name.</param>
    /// <param name="shape">The symbol to draw.</param>
    /// <param name="inputCount">The number of inputs: one or two.</param>
    /// <returns>The node type.</returns>
    private static NodeType Gate(string key, string title, GateShape shape, int inputCount) => Define(
        key,
        title,
        $"{title} gate",
        LogicCategory,
        ShortName(key),
        inputCount == 1 ? [new("in", Bool)] : [new("a", Bool), new("b", Bool)],
        [new("out", Bool)],
        [],
        new GateVisual(shape),
        size: inputCount == 1 ? InverterSize : GateSize);

    /// <summary>
    ///     Gets the part of a node type key after its last dot.
    /// </summary>
    /// <param name="key">The node type key.</param>
    /// <returns>The short name, which is also the icon name.</returns>
    private static string ShortName(string key) => key[(key.LastIndexOf('.') + 1)..];
}
