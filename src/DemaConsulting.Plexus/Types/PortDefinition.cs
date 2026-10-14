namespace DemaConsulting.Plexus;

/// <summary>
///     Declares one port of a <see cref="NodeType"/>: its name, its data type and its label.
/// </summary>
/// <remarks>
///     Whether the port is an input or an output is decided by which list of the node type it is
///     added to, keeping the definition itself direction-neutral. The type key is a host-defined string; see <see cref="PortType"/>.
/// </remarks>
/// <example>
///     Declare ports, including an input that accepts many wires and a port with a label that differs from its name.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var mixer = new NodeType("audio.Mixer")
///     {
///         Inputs =
///         {
///             new PortDefinition("in", "audio") { AllowsMultipleWires = true },   // fan-in: many sources may feed this input
///             new PortDefinition("gain", "number") { Label = "Gain (dB)" },       // name for code, label for display
///         },
///         Outputs = { new PortDefinition("out", "audio") },                       // outputs always allow any number of wires
///     };
///
///     var types = new NodeTypeRegistry();
///     types.Add(mixer);
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     var node = page.AddNode("audio.Mixer", 0, 0, name: "Mix");
///     Console.WriteLine(node.Input("in").AllowsMultipleWires);                   // True
///     Console.WriteLine(node.Input("gain").Label);                               // "Gain (dB)"
///     </code>
/// </example>
public sealed class PortDefinition
{
    /// <summary>
    ///     Optional explicit label; when null the name is used.
    /// </summary>
    private string? _label;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortDefinition"/> class.
    /// </summary>
    /// <param name="name">The port name, unique among the node type's ports of the same direction (an input and an output may share a name).</param>
    /// <param name="typeKey">The key of the port's <see cref="PortType"/>.</param>
    /// <exception cref="ArgumentException">Thrown when either argument is null, empty or whitespace.</exception>
    public PortDefinition(string name, string typeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);
        Name = name;
        TypeKey = typeKey;
    }

    /// <summary>
    ///     Gets the port name, used to look the port up on a node.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the key of the port's data type.
    /// </summary>
    public string TypeKey { get; }

    /// <summary>
    ///     Gets or initializes the text drawn next to the port; defaults to <see cref="Name"/>. Set with an object initializer.
    /// </summary>
    public string Label
    {
        get => _label ?? Name;
        init => _label = value;
    }

    /// <summary>
    ///     Gets or initializes a value indicating whether an input port made from this definition accepts more than one wire (fan-in); the default is false. Set with an object initializer.
    /// </summary>
    /// <remarks>
    ///     False by default: <see cref="ConnectionRules.Default"/> refuses a second wire into an input
    ///     that already has one. Set it to true on inputs that legitimately merge several wires, such as
    ///     a join or a shared decision point; the wires are then drawn with a junction dot where they
    ///     converge. The value is ignored for outputs, which always allow any number of wires (fan-out).
    ///     A host that replaces the diagram's connection rules without <see cref="ConnectionRules.Default"/>
    ///     (or <see cref="ConnectionRules.SingleInputWire"/>) is not bound by it.
    /// </remarks>
    public bool AllowsMultipleWires { get; init; }
}
