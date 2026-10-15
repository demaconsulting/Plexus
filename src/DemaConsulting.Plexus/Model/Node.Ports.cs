namespace DemaConsulting.Plexus;

/// <content>
///     Per-node ports: initializing them from the type's port provider and recomputing them, inside the same
///     undo entry, whenever a property or the data of the node changes.
/// </content>
public sealed partial class Node
{
    /// <summary>
    ///     Replaces the default ports built by the constructor with the ports of the type's <see cref="NodeType.PortProvider"/>, without history.
    /// </summary>
    /// <remarks>
    ///     Called once for a detached node, after its properties and data are final and before any wire refers to it:
    ///     by <see cref="DiagramPage.AddNode(NodeType, Point, string?, string?)"/> and by the importer behind loading,
    ///     pasting and duplicating. Does nothing for a type without a provider (the constructor already built the type's ports).
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the provider returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when the provider builds an invalid <see cref="PortSet"/>.</exception>
    internal void InitializePorts()
    {
        if (Type.PortProvider is null)
        {
            return;
        }

        // A detached node has no wires, so the ports can simply be swapped (names that survive keep their objects)
        var set = Type.ResolvePorts(this);
        var inputs = NodePortsCommand.BuildColumn(this, _inputs, set.Inputs, PortDirection.Input, null);
        var outputs = NodePortsCommand.BuildColumn(this, _outputs, set.Outputs, PortDirection.Output, null);
        foreach (var slot in inputs.Concat(outputs))
        {
            slot.Port.Update(slot.Definition, slot.Index);
        }

        ReplacePortsCore(inputs.Select(s => s.Port), outputs.Select(s => s.Port));
    }

    /// <summary>
    ///     Sets the stored bounds of a node that is not on a page yet, without notification or history.
    /// </summary>
    /// <param name="bounds">The bounds to store.</param>
    internal void InitializeBounds(Rect bounds) => _bounds = bounds;

    /// <summary>
    ///     Replaces the contents of the port lists; the public read-only views follow because they wrap the same lists.
    /// </summary>
    /// <param name="inputs">The input ports in row order.</param>
    /// <param name="outputs">The output ports in row order.</param>
    internal void ReplacePortsCore(IEnumerable<Port> inputs, IEnumerable<Port> outputs)
    {
        _inputs.Clear();
        _inputs.AddRange(inputs);
        _outputs.Clear();
        _outputs.AddRange(outputs);
        _ports.Clear();
        _ports.AddRange(_inputs);
        _ports.AddRange(_outputs);
    }

    /// <summary>
    ///     Applies and records a change of the node's properties or data, recomputing the ports in the same undo entry.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Without a <see cref="NodeType.PortProvider"/> this is exactly <see cref="UndoHistory.Execute(IUndoableCommand)"/>.
    ///         With one, the change is applied, the page plans the port update (wire removals, the port swap, rule
    ///         re-checks, resize and re-routing) and everything is recorded as one entry. When the ports do not change,
    ///         the command itself is recorded, so consecutive edits of the same value still merge.
    ///     </para>
    ///     <para>
    ///         If the provider or a connection rule throws, every applied step (including the change itself) is reverted
    ///         and the exception propagates; the revert attempts every step even when one of them fails, and then throws an
    ///         <see cref="AggregateException"/> carrying the original exception first. The whole update is one change for the
    ///         history: a change handler that throws does not interrupt it, and its exception is rethrown after the entry
    ///         was recorded.
    ///     </para>
    /// </remarks>
    /// <param name="command">The property or data change, not yet applied.</param>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page, an undo or redo is running, or the provider returns null.</exception>
    internal void ExecuteAffectingPorts(IUndoableCommand command)
    {
        var page = RequirePage();
        var history = page.Diagram.History;
        if (Type.PortProvider is null)
        {
            history.Execute(command);
            return;
        }

        // Apply the change, then the port update it implies, rolling everything back if any step fails
        history.EnsureNotApplying();
        var plan = new CommandPlan();
        var before = history.BeginOperation();
        try
        {
            plan.Run(command);
            page.PlanPortUpdate(plan, this);
        }
        catch (Exception ex)
        {
            try
            {
                plan.Abort(ex, history);
            }
            finally
            {
                history.AbortOperation(before);
            }

            throw;
        }

        // A lone change is recorded as itself so mergeable edits (sliders, drags) keep merging; a handler failure is
        // reported only once the entry is recorded
        var handlerFailure = history.EndOperation();
        history.Record(plan.Count == 1 ? command : plan.Build(command.Description));
        handlerFailure?.Throw();
    }
}
