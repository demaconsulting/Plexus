using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace DemaConsulting.Plexus;

/// <summary>
///     A block in the diagram: an instance of a <see cref="NodeType"/> with a name, a position, ports and properties.
/// </summary>
/// <remarks>
///     Create nodes with <see cref="DiagramPage.AddNode(string, double, double, string?, string?)"/>. Every
///     setter (name, bounds, data) is an undoable mutation and throws
///     <see cref="InvalidOperationException"/> while the node is removed from its page. A node owns
///     its ports (<see cref="Inputs"/>, <see cref="Outputs"/>; declared by the node type, or computed per node by its
///     <see cref="NodeType.PortProvider"/> and then recomputed whenever the properties or data change), a
///     <see cref="PropertyBag"/> of typed values (<see cref="Properties"/>) and an optional host payload
///     (<see cref="Data"/>). Not thread-safe.
///     <para>
///         Inherited from <see cref="DiagramElement"/> (the member tables of this page list only what <see cref="Node"/> declares): <see cref="DiagramElement.Id"/>,
///         <see cref="DiagramElement.Page"/> (the page the node is on; null while it is removed, so <c>node.Page!.Diagram</c> reaches the diagram and its history),
///         <see cref="DiagramElement.Parent"/> (the group it belongs to, or null), <see cref="DiagramElement.IsVisible"/>, <see cref="DiagramElement.GetBounds"/>,
///         <see cref="DiagramElement.Remove"/> and the <see cref="DiagramElement.PropertyChanged"/> event.
///     </para>
/// </remarks>
/// <example>
///     Rename, move, edit and delete a node, and find its ports.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     Node source = page.AddNode("demo.Source", 0, 0);     // the name is generated: "Source1"
///
///     source.Name = "Sensor";                              // goes through the rename rules; throws ArgumentException when refused
///     source.MoveTo(60, 30);                               // or: source.Bounds = new Rect(60, 30, 140, 90)
///     source.Properties["Value"] = 2.5;                    // typed and validated against the node type
///     Console.WriteLine(source.Type.Key);                  // "demo.Source"
///     Console.WriteLine(source.Output("out"));             // "Sensor.out"
///     Console.WriteLine(source.Wires.Count);               // 0
///
///     diagram.History.Undo();                              // undoes the property edit, then the move, the rename ...
///     source.Remove();                                     // also removes its wires; undoable
///     </code>
/// </example>
public sealed partial class Node : DiagramElement
{
    /// <summary>
    ///     The node's current name.
    /// </summary>
    private string _name;

    /// <summary>
    ///     The node's current bounding box.
    /// </summary>
    private Rect _bounds;

    /// <summary>
    ///     The host payload.
    /// </summary>
    private object? _data;

    /// <summary>
    ///     The input ports in column order.
    /// </summary>
    private readonly List<Port> _inputs = [];

    /// <summary>
    ///     The output ports in column order.
    /// </summary>
    private readonly List<Port> _outputs = [];

    /// <summary>
    ///     Inputs followed by outputs.
    /// </summary>
    private readonly List<Port> _ports = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="Node"/> class.
    /// </summary>
    /// <param name="id">The element id.</param>
    /// <param name="type">The node type.</param>
    /// <param name="name">The initial (already validated) name.</param>
    /// <param name="bounds">The initial bounds.</param>
    internal Node(string id, NodeType type, string name, Rect bounds)
        : base(id)
    {
        Type = type;
        _name = name;
        _bounds = bounds;

        // Ports start as the (frozen) type's declared ports; a port provider replaces them via InitializePorts
        // once the properties and data are known. Port objects are kept by name so they can be compared by reference
        for (var i = 0; i < type.Inputs.Count; i++)
        {
            _inputs.Add(new Port(this, type.Inputs[i], PortDirection.Input, i));
        }

        for (var i = 0; i < type.Outputs.Count; i++)
        {
            _outputs.Add(new Port(this, type.Outputs[i], PortDirection.Output, i));
        }

        _ports.AddRange(_inputs);
        _ports.AddRange(_outputs);
        Inputs = new ReadOnlyCollection<Port>(_inputs);
        Outputs = new ReadOnlyCollection<Port>(_outputs);
        Ports = new ReadOnlyCollection<Port>(_ports);
        Properties = new PropertyBag(this);
    }

    /// <summary>
    ///     Gets the node's type.
    /// </summary>
    public NodeType Type { get; }

    /// <summary>
    ///     Gets or sets the node's name; assignment goes through the rename pipeline and is undoable.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the name is rejected (empty, duplicate, vetoed by a handler).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    [SuppressMessage("Major Code Smell", "S4275:Getters and setters should access the expected fields", Justification = "The setter routes through the undoable rename pipeline, which writes the field via Node.SetNameCore.")]
    public string Name
    {
        get => _name;
        set => RequirePage().Diagram.Rename(this, value);
    }

    /// <summary>
    ///     Gets or sets the bounding box (undoable).
    /// </summary>
    /// <remarks>
    ///     Changing only the position behaves like <see cref="DiagramPage.Move"/> (wires translate or
    ///     re-route per its rule); changing the size re-routes attached wires.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    [SuppressMessage("Major Code Smell", "S4275:Getters and setters should access the expected fields", Justification = "The setter routes through the undoable layout pipeline, which writes the field via Node.SetBoundsCore.")]
    public Rect Bounds
    {
        get => _bounds;
        set => RequirePage().ApplyBounds(this, value);
    }

    /// <summary>
    ///     Gets the rectangle the node occupies on the page (the same value as <see cref="Bounds"/>).
    /// </summary>
    /// <returns>The node's bounding box in diagram coordinates; it excludes the port triangles, port labels, name and summary text drawn outside the box.</returns>
    public override Rect GetBounds() => _bounds;

    /// <summary>
    ///     Gets the top-left corner of the node (<c>Bounds.Position</c>).
    /// </summary>
    public Point Position => _bounds.Position;

    /// <summary>
    ///     Gets the node's typed property values; see <see cref="PropertyBag"/> for validation and undo behavior.
    /// </summary>
    public PropertyBag Properties { get; }

    /// <summary>
    ///     Gets or sets the host payload: any application object attached to the node (undoable; replace semantics: assign a new or copied object rather than mutating the current one).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The setter accepts any object unless the node type declares <see cref="NodeType.DataType"/>: then an instance of an
    ///         incompatible type throws <see cref="ArgumentException"/> immediately (a derived type and null are accepted), instead of failing
    ///         later in <see cref="Diagram.ToJson(bool)"/>. A type that declares only a <see cref="NodeType.DataSerializer"/> accepts any object.
    ///         Assigning an equal value
    ///         (by <see cref="object.Equals(object?)"/>) records nothing. Immutable records are the simplest payload: assign a modified copy,
    ///         <c>node.Data = (MyData)node.Data! with { Level = 3 };</c> (one undo entry). A mutable class (the kind a property grid edits) is edited on a
    ///         copy from <see cref="CloneData"/> that is assigned back afterwards; see <see cref="NodeType.DataType"/> for what such a class needs.
    ///         New nodes start with the payload produced by <see cref="NodeType.CreateData"/>, if the type declares one.
    ///         When the type declares a <see cref="NodeType.PortProvider"/>, the ports are recomputed in the same undo entry.
    ///     </para>
    ///     <para>
    ///         The payload takes part in JSON persistence, copy and paste only when the node type declares
    ///         <see cref="NodeType.DataType"/> or <see cref="NodeType.DataSerializer"/>; otherwise it is silently left out
    ///         of JSON and of the clipboard (an in-process duplicate shares the reference). See <see cref="NodeType.DataType"/> for a worked example.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when the node type declares a <see cref="NodeType.DataType"/> and the value is an instance of an incompatible type.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    public object? Data
    {
        get => _data;
        set
        {
            if (Type.DescribeDataMismatch(value) is { } mismatch)
            {
                throw new ArgumentException(mismatch, nameof(value));
            }

            RequirePage();
            if (Equals(_data, value))
            {
                return;
            }

            ExecuteAffectingPorts(new SetValueCommand<object?>(
                "Set node data",
                this,
                nameof(Data),
                _data,
                value,
                v =>
                {
                    _data = v;
                    NotifyChanged(nameof(Data));
                }));
        }
    }

    /// <summary>
    ///     Gets all ports: inputs first, then outputs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A live read-only view. For a type without a <see cref="NodeType.PortProvider"/> it never changes. With a
    ///         provider it is recomputed (in the same undo entry) whenever <see cref="Properties"/> or <see cref="Data"/> change:
    ///         ports whose name survives within their direction keep their objects and wires, other ports and their wires
    ///         are removed, new names get new ports, and the node is resized to fit when the number of ports changes.
    ///     </para>
    ///     <para>
    ///         Hosts observe a recomputation through <see cref="DiagramElement.PropertyChanged"/> and <see cref="Diagram.Changed"/>
    ///         with the property name <c>Ports</c>, raised exactly once per change, undo and redo, after the model is settled:
    ///         after every wire removal (or re-add on undo), the node's new bounds and every re-routed wire, followed by a
    ///         <c>Bounds</c> change of each enclosing group. A recomputation that fails is rolled back without a <c>Ports</c>
    ///         notification. Copy the list (for example with <c>ToList()</c>) to keep a snapshot.
    ///     </para>
    /// </remarks>
    public IReadOnlyList<Port> Ports { get; }

    /// <summary>
    ///     Gets the input ports, top to bottom.
    /// </summary>
    /// <remarks>
    ///     A live view that changes when a <see cref="NodeType.PortProvider"/> recomputes the ports; see <see cref="Ports"/>.
    /// </remarks>
    public IReadOnlyList<Port> Inputs { get; }

    /// <summary>
    ///     Gets the output ports, top to bottom.
    /// </summary>
    /// <remarks>
    ///     A live view that changes when a <see cref="NodeType.PortProvider"/> recomputes the ports; see <see cref="Ports"/>.
    /// </remarks>
    public IReadOnlyList<Port> Outputs { get; }

    /// <summary>
    ///     Gets the wires connected to any port of this node, in page order.
    /// </summary>
    public IReadOnlyList<Wire> Wires =>
        Page is { } page
            ? [.. page.Wires.Where(w => ReferenceEquals(w.Source.Node, this) || ReferenceEquals(w.Target.Node, this))]
            : [];

    /// <summary>
    ///     Finds a port by name when the name is unique on the node.
    /// </summary>
    /// <remarks>
    ///     An input and an output of one node type may share a name (for example <c>pos</c> on both
    ///     sides); this method then cannot tell which is meant and throws. Use <see cref="Input"/>
    ///     or <see cref="Output"/> to be explicit.
    /// </remarks>
    /// <param name="name">The port name.</param>
    /// <returns>The port.</returns>
    /// <exception cref="ArgumentException">Thrown when the node has no port with that name.</exception>
    /// <exception cref="InvalidOperationException">Thrown when both an input and an output have that name.</exception>
    public Port Port(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var input = FindPort(_inputs, name);
        var output = FindPort(_outputs, name);
        if (input is not null && output is not null)
        {
            throw new InvalidOperationException(
                $"Port name '{name}' is ambiguous on node type '{Type.Key}': it names both an input and an output; use Input(\"{name}\") or Output(\"{name}\").");
        }

        return input ?? output
            ?? throw new ArgumentException($"Node type '{Type.Key}' has no port named '{name}'.", nameof(name));
    }

    /// <summary>
    ///     Finds an input port by name.
    /// </summary>
    /// <param name="name">The port name.</param>
    /// <returns>The input port.</returns>
    /// <exception cref="ArgumentException">Thrown when the node has no input with that name.</exception>
    public Port Input(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindPort(_inputs, name)
            ?? throw new ArgumentException($"Node type '{Type.Key}' has no input named '{name}'.", nameof(name));
    }

    /// <summary>
    ///     Finds an output port by name.
    /// </summary>
    /// <param name="name">The port name.</param>
    /// <returns>The output port.</returns>
    /// <exception cref="ArgumentException">Thrown when the node has no output with that name.</exception>
    public Port Output(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FindPort(_outputs, name)
            ?? throw new ArgumentException($"Node type '{Type.Key}' has no output named '{name}'.", nameof(name));
    }

    /// <summary>
    ///     Attempts to find a port by name, optionally restricted to one direction.
    /// </summary>
    /// <remarks>
    ///     Without a <paramref name="direction"/> the name must be unique on the node: when an input and
    ///     an output share the name (for example <c>pos</c> on both sides) the lookup is ambiguous and
    ///     returns <see langword="false"/>. Pass the direction to resolve it.
    /// </remarks>
    /// <param name="name">The port name.</param>
    /// <param name="direction">The required direction, or null to accept either when unambiguous.</param>
    /// <param name="port">Receives the port when found.</param>
    /// <returns><see langword="true"/> when exactly one matching port was found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool TryGetPort(string name, PortDirection? direction, [NotNullWhen(true)] out Port? port)
    {
        ArgumentNullException.ThrowIfNull(name);
        port = LookupPort(name, direction, out _);
        return port is not null;
    }

    /// <summary>
    ///     Looks a port up by name and direction, reporting ambiguity.
    /// </summary>
    /// <param name="name">The port name.</param>
    /// <param name="direction">The required direction, or null for either.</param>
    /// <param name="ambiguous">True when no direction was given and both an input and an output match.</param>
    /// <returns>The port, or null when absent or ambiguous.</returns>
    internal Port? LookupPort(string name, PortDirection? direction, out bool ambiguous)
    {
        ambiguous = false;
        var input = direction == PortDirection.Output ? null : FindPort(_inputs, name);
        var output = direction == PortDirection.Input ? null : FindPort(_outputs, name);
        if (input is not null && output is not null)
        {
            ambiguous = true;
            return null;
        }

        return input ?? output;
    }

    /// <summary>
    ///     Finds a port by name in one column.
    /// </summary>
    /// <param name="column">The ports to search.</param>
    /// <param name="name">The port name.</param>
    /// <returns>The port, or null when absent.</returns>
    private static Port? FindPort(List<Port> column, string name) =>
        column.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    /// <summary>
    ///     Moves the node so its top-left corner is at the given position (undoable); size is unchanged.
    /// </summary>
    /// <param name="x">The new left edge.</param>
    /// <param name="y">The new top edge.</param>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page.</exception>
    public void MoveTo(double x, double y) => Bounds = _bounds.WithPosition(new Point(x, y));

    /// <summary>
    ///     Sets the stored name without validation or notification; used by commands.
    /// </summary>
    /// <param name="name">The name to store.</param>
    internal void SetNameCore(string name) => _name = name;

    /// <summary>
    ///     Sets the stored bounds and raises change notifications; used by commands.
    /// </summary>
    /// <param name="bounds">The bounds to store.</param>
    internal void SetBoundsCore(Rect bounds)
    {
        _bounds = bounds;
        NotifyChanged(nameof(Bounds));
    }
}
