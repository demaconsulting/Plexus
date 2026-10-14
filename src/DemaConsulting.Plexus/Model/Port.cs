using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <summary>
///     A connection point on a <see cref="Node"/>: an input on the left edge or an output on the right edge.
/// </summary>
/// <remarks>
///     Ports are lightweight and created once per node from its <see cref="NodeType"/>, so a port
///     object can be compared by reference and kept by hosts. A port has no state of its own;
///     <see cref="Wires"/> and <see cref="Anchor"/> are derived from the page and the node's bounds.
///     Find a port with <see cref="Node.Input"/> / <see cref="Node.Output"/>, with a <c>"Node.port"</c> path
///     (<see cref="DiagramPage.FindPort"/>, <see cref="Diagram.FindPort"/>), or by enumerating
///     <see cref="Node.Inputs"/> and <see cref="Node.Outputs"/>.
/// </remarks>
/// <example>
///     Look up ports, inspect them and connect two of them.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var b = page.AddNode("demo.Sink", 180, 0, name: "B");
///
///     Port output = a.Output("out");                       // by name; equivalent: page.FindPort("A.out")
///     Port input = b.Input("in");
///     Console.WriteLine($"{output} ({output.TypeKey}) -&gt; {input}");     // "A.out (number) -> B.in"
///
///     Wire wire = output.ConnectTo(input);                 // same as page.Connect(output, input)
///     Console.WriteLine(output.Wires.Count);               // 1: the out-degree of the port
///     Console.WriteLine(input.AllowsMultipleWires);        // False: a second wire into "in" would be refused
///     Console.WriteLine(output.Anchor);                    // where wires attach, in diagram coordinates
///     </code>
/// </example>
public sealed class Port
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Port"/> class.
    /// </summary>
    /// <param name="node">The owning node.</param>
    /// <param name="definition">The declaration the port was created from.</param>
    /// <param name="direction">Whether the port is an input or an output.</param>
    /// <param name="index">The zero-based row of the port within its column.</param>
    internal Port(Node node, PortDefinition definition, PortDirection direction, int index)
    {
        Node = node;
        Definition = definition;
        Direction = direction;
        Index = index;
    }

    /// <summary>
    ///     Gets the node the port belongs to.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    ///     Gets the port declaration.
    /// </summary>
    public PortDefinition Definition { get; }

    /// <summary>
    ///     Gets the port name, unique among the node's ports of the same direction.
    /// </summary>
    public string Name => Definition.Name;

    /// <summary>
    ///     Gets the text drawn next to the port.
    /// </summary>
    public string Label => Definition.Label;

    /// <summary>
    ///     Gets whether the port is an input or an output.
    /// </summary>
    public PortDirection Direction { get; }

    /// <summary>
    ///     Gets the key of the port's data type.
    /// </summary>
    public string TypeKey => Definition.TypeKey;

    /// <summary>
    ///     Gets a value indicating whether the port accepts more than one wire (fan-in); see <see cref="PortDefinition.AllowsMultipleWires"/>.
    /// </summary>
    /// <remarks>
    ///     Meaningful for inputs only: an output always allows any number of wires, whatever its definition says.
    /// </remarks>
    public bool AllowsMultipleWires => Direction == PortDirection.Output || Definition.AllowsMultipleWires;

    /// <summary>
    ///     Gets the zero-based row of the port within its column (inputs and outputs are counted separately).
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///     Gets the wires currently connected to this port, in page order; <c>Wires.Count</c> is the port's fan-out or fan-in.
    /// </summary>
    /// <remarks>
    ///     The list is a snapshot computed on each call (empty while the node is not on a page), so it does
    ///     not change as wires are added later. Connection rules use <c>port.Wires.Count</c> to express
    ///     degree limits, for example "an output feeds at most three inputs".
    /// </remarks>
    public IReadOnlyList<Wire> Wires =>
        Node.Page is { } page
            ? [.. page.Wires.Where(w => ReferenceEquals(w.Source, this) || ReferenceEquals(w.Target, this))]
            : [];

    /// <summary>
    ///     Counts the wires connected to this port, leaving out the wire that a reconnect check is moving.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this instead of <see cref="Wires"/><c>.Count</c> in a <see cref="ConnectionValidator"/> that limits the number of wires
    ///         per port. <see cref="DiagramPage.CanReconnect"/> (and so every wire-end drag in the editor) judges a wire that is still attached to its
    ///         old ports; counted naively, that wire would count against the port it is being moved to or kept on, so keeping a wire on a port
    ///         that is "full" would be refused. Outside a reconnect check the result equals <c>Wires.Count</c>. The built-in
    ///         <see cref="ConnectionRules.MaxOutgoingWires"/>, <see cref="ConnectionRules.MaxIncomingWires"/> and <see cref="ConnectionRules.SingleInputWire"/> use it.
    ///     </para>
    ///     <para>
    ///         For other rules read <see cref="DiagramPage.WireBeingReconnected"/> directly.
    ///     </para>
    /// </remarks>
    /// <returns>The number of wires, not counting the wire under a reconnect check.</returns>
    public int CountWiresIgnoringReconnect() =>
        Node.Page is { } page
            ? page.Wires.Count(w => (ReferenceEquals(w.Source, this) || ReferenceEquals(w.Target, this)) && !ReferenceEquals(w, page.WireBeingReconnected))
            : 0;

    /// <summary>
    ///     Gets the point where wires attach to this port, in diagram coordinates.
    /// </summary>
    /// <remarks>
    ///     This is the outer end of the port's triangle and label (left of the box for inputs, right
    ///     for outputs), not a point on the box edge; see <see cref="NodeGeometry.PortAnchor(Rect, PortDirection, int, int, string, DiagramTheme)"/>.
    /// </remarks>
    public Point Anchor => NodeGeometry.PortAnchor(Node.Bounds, Direction, Index, ColumnCount, Label, Node.Theme);

    /// <summary>
    ///     Gets the number of ports in this port's column (the node's inputs or outputs), which positions it when ports are centered.
    /// </summary>
    internal int ColumnCount => Direction == PortDirection.Input ? Node.Inputs.Count : Node.Outputs.Count;

    /// <summary>
    ///     Connects this port to another one (undoable); shorthand for <see cref="DiagramPage.Connect(Port, Port, IReadOnlyList{Point}?, string?)"/>.
    /// </summary>
    /// <param name="other">The port to connect to; the direction is fixed up automatically.</param>
    /// <returns>The new wire.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection is not allowed or the node is detached.</exception>
    public Wire ConnectTo(Port other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Node.RequirePage().Connect(this, other);
    }

    /// <summary>
    ///     Returns the port's path in the form <c>NodeName.portName</c>, the same form <see cref="DiagramPage.Connect(string, string, IReadOnlyList{Point}?, string?)"/> accepts.
    /// </summary>
    /// <returns>The path text, for example <c>"A.out"</c>.</returns>
    public override string ToString() => $"{Node.Name}.{Name}";
}
