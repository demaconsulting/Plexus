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

        // Inside a deferral scope the change is just executed; the one port update for the final state runs when the scope ends
        if (_portDeferralDepth > 0)
        {
            history.Execute(command);
            _portsMayBeStale = true;
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

    /// <summary>
    ///     Runs an action that changes several properties and/or the data of this node, and recomputes the ports once, for
    ///     the final state, instead of after every change.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use it when a <see cref="NodeType.PortProvider"/> depends on more than one value: recomputing after each
    ///         change could pass through an intermediate state that drops a port, and with it that port's wires, even
    ///         though the final state has the port again. <see cref="PropertyBag.TrySetAll"/> already does this.
    ///     </para>
    ///     <para>
    ///         While the action runs, property and data changes of this node are applied without consulting the provider.
    ///         When the outermost scope ends normally and something changed, one port update is planned and recorded;
    ///         nothing is recorded when the ports do not change. Scopes nest: only the outermost one recomputes. Without a
    ///         <see cref="NodeType.PortProvider"/> this just runs the action.
    ///     </para>
    ///     <para>
    ///         Everything is one undo entry: the scope runs in its own (possibly nested) transaction (<c>"Edit {Name}"</c>). A
    ///         provider failure propagates and the transaction rolls every change back. When the action throws, its changes
    ///         are rolled back too and no recomputation happens, so a caller that catches the exception inside an outer
    ///         transaction never keeps a change whose ports were not recomputed.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     node.DeferPortUpdates(() =>
    ///     {
    ///         node.Properties["Channels"] = 8;     // alone this could drop the port "aux"
    ///         node.Properties["Aux"] = true;       // ... which the final state has again, so its wire is kept
    ///     });
    ///     </code>
    /// </example>
    /// <param name="action">The changes to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the node is removed from its page, an undo or redo is running, or the provider returns null.</exception>
    public void DeferPortUpdates(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var history = RequirePage().Diagram.History;

        // Always a transaction of its own, nested when one is open: if the action throws and a caller catches it inside an
        // outer transaction, this scope's changes are rolled back instead of being kept with ports that were not recomputed
        history.Transact($"Edit {Name}", () => DeferPortUpdatesCore(action));
    }

    /// <summary>
    ///     Runs the deferral scope of <see cref="DeferPortUpdates"/>; the caller has an undo transaction open.
    /// </summary>
    /// <param name="action">The changes to apply.</param>
    private void DeferPortUpdatesCore(Action action)
    {
        _portDeferralDepth++;
        var completed = false;
        try
        {
            action();
            completed = true;
        }
        finally
        {
            _portDeferralDepth--;
            if (!completed && _portDeferralDepth == 0)
            {
                _portsMayBeStale = false;
            }
        }

        if (_portDeferralDepth == 0 && _portsMayBeStale)
        {
            _portsMayBeStale = false;
            RecomputePorts();
        }
    }

    /// <summary>
    ///     Plans and records one port update for the node's current properties and data (nothing when the ports do not change).
    /// </summary>
    /// <remarks>
    ///     Runs as one history operation like <see cref="ExecuteAffectingPorts"/>: a provider or rule failure reverts the steps
    ///     planned so far and propagates, and a throwing change handler is rethrown after the entry was recorded.
    /// </remarks>
    private void RecomputePorts()
    {
        var page = RequirePage();
        var history = page.Diagram.History;
        history.EnsureNotApplying();
        var plan = new CommandPlan();
        var before = history.BeginOperation();
        try
        {
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

        var handlerFailure = history.EndOperation();
        if (!plan.IsEmpty)
        {
            history.Record(plan.Build("Update ports"));
        }

        handlerFailure?.Throw();
    }
}
