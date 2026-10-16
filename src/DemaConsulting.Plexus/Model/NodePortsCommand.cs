namespace DemaConsulting.Plexus;

/// <summary>
///     Swaps a node's ports for the ports its type's <see cref="NodeType.PortProvider"/> now returns, remembering both states exactly.
/// </summary>
/// <remarks>
///     <para>
///         Port identity is the port name within a direction: a port whose name survives keeps its object (hosts and wires
///         hold it by reference) and only its definition and row change; new names get new port objects; ports whose name
///         disappeared are taken off the node. The command stores every affected port with its definition and row in
///         both states, so undo puts the very same objects back in their old rows and redo replays the new state without
///         calling the provider again.
///     </para>
///     <para>
///         The command only swaps ports and drops the page's routing caches; it raises no notification. Removing the wires
///         of removed ports, re-checking retyped ports, resizing the node, re-routing wires and reporting the change (once,
///         when the model is settled) are planned around it by <see cref="DiagramPage.PlanPortUpdate"/>.
///     </para>
/// </remarks>
internal sealed class NodePortsCommand : IUndoableCommand
{
    /// <summary>
    ///     The node whose ports change.
    /// </summary>
    private readonly Node _node;

    /// <summary>
    ///     The input and output ports before the change, with their definitions and rows.
    /// </summary>
    private readonly PortState _old;

    /// <summary>
    ///     The input and output ports after the change, with their definitions and rows.
    /// </summary>
    private readonly PortState _new;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePortsCommand"/> class.
    /// </summary>
    /// <param name="node">The node whose ports change.</param>
    /// <param name="oldState">The ports before the change.</param>
    /// <param name="newState">The ports after the change.</param>
    /// <param name="removedPorts">The ports of the old state that are not in the new one.</param>
    /// <param name="retypedPorts">Kept ports whose type key or fan-in flag changes.</param>
    private NodePortsCommand(Node node, PortState oldState, PortState newState, IReadOnlyList<Port> removedPorts, IReadOnlyList<Port> retypedPorts)
    {
        _node = node;
        _old = oldState;
        _new = newState;
        RemovedPorts = removedPorts;
        RetypedPorts = retypedPorts;
    }

    /// <inheritdoc />
    public string Description => "Update ports";

    /// <summary>
    ///     Gets the ports that the change takes off the node; their wires must be removed first.
    /// </summary>
    public IReadOnlyList<Port> RemovedPorts { get; }

    /// <summary>
    ///     Gets the kept ports whose type key or fan-in flag changes; their wires must be checked against the connection rules again.
    /// </summary>
    public IReadOnlyList<Port> RetypedPorts { get; }

    /// <summary>
    ///     Computes the port change implied by the node's current properties and data.
    /// </summary>
    /// <param name="node">A node whose type declares a <see cref="NodeType.PortProvider"/>.</param>
    /// <returns>The change, or null when every port keeps an equivalent definition in the same row.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provider returns null.</exception>
    public static NodePortsCommand? TryCreate(Node node)
    {
        // Ask the provider; an equivalent result means nothing changes (no entry, no notification)
        var set = node.Type.ResolvePorts(node);
        if (IsUnchanged(node.Inputs, set.Inputs) && IsUnchanged(node.Outputs, set.Outputs))
        {
            return null;
        }

        // Build both columns, reusing surviving ports by name and noting the retyped ones
        var retyped = new List<Port>();
        var inputs = BuildColumn(node, node.Inputs, set.Inputs, PortDirection.Input, retyped);
        var outputs = BuildColumn(node, node.Outputs, set.Outputs, PortDirection.Output, retyped);
        var kept = new HashSet<Port>(inputs.Concat(outputs).Select(s => s.Port));
        var removed = node.Ports.Where(p => !kept.Contains(p)).ToList();

        var oldState = new PortState(Capture(node.Inputs), Capture(node.Outputs));
        var newState = new PortState([.. inputs], [.. outputs]);
        return new NodePortsCommand(node, oldState, newState, removed, retyped);
    }

    /// <summary>
    ///     Builds one column of ports from new definitions, reusing the current port of the same name.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="current">The current ports of the column.</param>
    /// <param name="definitions">The new definitions in row order.</param>
    /// <param name="direction">The column's direction.</param>
    /// <param name="retyped">Receives kept ports whose type key or fan-in flag changes, or null when not needed.</param>
    /// <returns>The port, definition and row of every entry of the new column.</returns>
    public static List<PortSlot> BuildColumn(
        Node node,
        IReadOnlyList<Port> current,
        IReadOnlyList<PortDefinition> definitions,
        PortDirection direction,
        List<Port>? retyped)
    {
        var byName = current.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var column = new List<PortSlot>(definitions.Count);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (byName.TryGetValue(definition.Name, out var port))
            {
                // A surviving name keeps its port object; a changed type or fan-in invalidates its wires' approval
                if (retyped is not null
                    && (!string.Equals(port.TypeKey, definition.TypeKey, StringComparison.Ordinal)
                        || port.Definition.AllowsMultipleWires != definition.AllowsMultipleWires))
                {
                    retyped.Add(port);
                }
            }
            else
            {
                port = new Port(node, definition, direction, i);
            }

            column.Add(new PortSlot(port, definition, i));
        }

        return column;
    }

    /// <inheritdoc />
    public void Do() => Apply(_new);

    /// <inheritdoc />
    public void Undo() => Apply(_old);

    /// <summary>
    ///     Determines whether a column already matches new definitions, row by row.
    /// </summary>
    /// <param name="current">The current ports.</param>
    /// <param name="definitions">The new definitions.</param>
    /// <returns><see langword="true"/> when the counts match and every definition is equivalent to the current one in its row.</returns>
    private static bool IsUnchanged(IReadOnlyList<Port> current, IReadOnlyList<PortDefinition> definitions)
    {
        if (current.Count != definitions.Count)
        {
            return false;
        }

        for (var i = 0; i < current.Count; i++)
        {
            if (!current[i].Definition.IsEquivalentTo(definitions[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Captures the current ports of a column with their definitions and rows.
    /// </summary>
    /// <param name="ports">The column.</param>
    /// <returns>The captured slots.</returns>
    private static PortSlot[] Capture(IReadOnlyList<Port> ports) =>
        [.. ports.Select(p => new PortSlot(p, p.Definition, p.Index))];

    /// <summary>
    ///     Puts one state's ports on the node, without notifications.
    /// </summary>
    /// <remarks>
    ///     Only the page's routing caches are dropped here; the node's <c>Ports</c> and its groups' <c>Bounds</c> are reported
    ///     by the notification steps of <see cref="CreateNotificationSteps"/> once the whole recomputation is settled.
    /// </remarks>
    /// <param name="state">The state to apply.</param>
    private void Apply(PortState state)
    {
        // Definitions and rows first, so anchors are right when the lists change
        foreach (var slot in state.Inputs.Concat(state.Outputs))
        {
            slot.Port.Update(slot.Definition, slot.Index);
        }

        _node.ReplacePortsCore(state.Inputs.Select(s => s.Port), state.Outputs.Select(s => s.Port));
        _node.Page?.OnElementChanging(_node);
    }

    /// <summary>
    ///     Creates the two marker steps that report a port recomputation once the model is settled.
    /// </summary>
    /// <remarks>
    ///     The page records the opening step at the start of the plan (before the triggering change) and runs the closing step
    ///     after the last step of the recomputation. The closing step reports the change when the recomputation is applied
    ///     (first time and redo), after every wire removal, bounds and route change; the opening step reports it when the
    ///     entry is reverted (undo or rollback), after the wires, bounds, routes and the triggering change are restored. A
    ///     recomputation that failed before its closing step ever ran is rolled back silently, because nothing was reported
    ///     for it. The report is the node's <c>Ports</c> change followed by a <c>Bounds</c> change of every enclosing group,
    ///     so frames re-fit.
    /// </remarks>
    /// <param name="node">The node whose ports are recomputed.</param>
    /// <returns>The opening and closing steps.</returns>
    public static (IUndoableCommand Opening, IUndoableCommand Closing) CreateNotificationSteps(Node node)
    {
        var settled = false;
        var opening = new DelegateCommand(
            "Update ports",
            () => { },
            () =>
            {
                if (settled)
                {
                    Notify(node);
                }
            });
        var closing = new DelegateCommand(
            "Update ports",
            () =>
            {
                settled = true;
                Notify(node);
            },
            () => { });
        return (opening, closing);
    }

    /// <summary>
    ///     Reports a changed set of ports: the node's <c>Ports</c>, then the <c>Bounds</c> of every enclosing group.
    /// </summary>
    /// <param name="node">The node.</param>
    private static void Notify(Node node)
    {
        node.NotifyChanged(nameof(Node.Ports));
        for (var group = node.Parent; group is not null; group = group.Parent)
        {
            group.NotifyChanged(nameof(Group.Bounds));
        }
    }

    /// <summary>
    ///     One port of a column with the definition and row it has in a state.
    /// </summary>
    /// <param name="Port">The port object.</param>
    /// <param name="Definition">The port's definition in that state.</param>
    /// <param name="Index">The port's row in that state.</param>
    internal readonly record struct PortSlot(Port Port, PortDefinition Definition, int Index);

    /// <summary>
    ///     The complete port lists of a node in one state.
    /// </summary>
    /// <param name="Inputs">The input slots in row order.</param>
    /// <param name="Outputs">The output slots in row order.</param>
    private sealed record PortState(PortSlot[] Inputs, PortSlot[] Outputs);
}
