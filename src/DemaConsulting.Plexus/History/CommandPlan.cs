namespace DemaConsulting.Plexus;

/// <summary>
///     Collects the primitive commands of a multi-step model operation while applying them, so the
///     whole operation becomes one undo entry.
/// </summary>
/// <remarks>
///     Many operations (remove with wires, group creation, move to page) must apply each step
///     before planning the next. Each step runs immediately and is remembered; if a later step
///     throws, <see cref="Abort"/> reverts the applied steps newest first so the model is never left
///     half-modified. The revert attempts every step even when one of them fails, and then reports the
///     original error together with every revert failure.
/// </remarks>
internal sealed class CommandPlan
{
    /// <summary>
    ///     The steps applied so far, in application order.
    /// </summary>
    private readonly List<IUndoableCommand> _applied = [];

    /// <summary>
    ///     Gets a value indicating whether no step has been applied.
    /// </summary>
    public bool IsEmpty => _applied.Count == 0;

    /// <summary>
    ///     Gets the number of steps applied so far.
    /// </summary>
    /// <remarks>
    ///     Lets a caller record a single step as itself instead of wrapping it in a composite, which keeps mergeable
    ///     commands mergeable.
    /// </remarks>
    public int Count => _applied.Count;

    /// <summary>
    ///     Applies a step and remembers it.
    /// </summary>
    /// <param name="command">The step to apply.</param>
    public void Run(IUndoableCommand command)
    {
        command.Do();
        _applied.Add(command);
    }

    /// <summary>
    ///     Applies a step expressed as two delegates and remembers it.
    /// </summary>
    /// <param name="description">Description of the step.</param>
    /// <param name="doAction">Applies the step.</param>
    /// <param name="undoAction">Reverts the step.</param>
    public void Run(string description, Action doAction, Action undoAction) =>
        Run(new DelegateCommand(description, doAction, undoAction));

    /// <summary>
    ///     Remembers, at the start of the plan, a marker step whose application has no effect, without running it.
    /// </summary>
    /// <remarks>
    ///     Used for notification markers that must be reverted last (after every step already applied), such as the one
    ///     reporting a port recomputation on undo once the triggering change is reverted too. Because the marker's
    ///     <see cref="IUndoableCommand.Do"/> does nothing, recording it as if it had run first leaves the plan exact.
    /// </remarks>
    /// <param name="marker">The marker step; its <see cref="IUndoableCommand.Do"/> must have no effect.</param>
    public void AddFirst(IUndoableCommand marker) => _applied.Insert(0, marker);

    /// <summary>
    ///     Reverts every applied step, newest first, and forgets them.
    /// </summary>
    /// <remarks>
    ///     Every step is reverted even when the revert of a newer step throws, and the plan is always empty afterwards.
    ///     When every revert succeeded the method returns normally, so the caller rethrows <paramref name="cause"/>
    ///     with <c>throw;</c> and keeps its stack trace.
    /// </remarks>
    /// <param name="cause">The exception that made the operation fail.</param>
    /// <param name="history">
    ///     The history whose replay guard the reverts run under, so neither they nor the change handlers they trigger can
    ///     change the history.
    /// </param>
    /// <exception cref="AggregateException">
    ///     Thrown when at least one revert failed; its inner exceptions are <paramref name="cause"/> followed by every revert failure, newest step first.
    /// </exception>
    public void Abort(Exception cause, UndoHistory history)
    {
        // Newest first; the plan forgets the steps before reverting, so it is empty whatever happens
        var reverts = new List<Action>(_applied.Count);
        for (var i = _applied.Count - 1; i >= 0; i--)
        {
            reverts.Add(_applied[i].Undo);
        }

        _applied.Clear();
        try
        {
            history.RunGuarded(() => CompositeCommand.Compensate(cause, reverts, "The operation failed and could not be fully reverted."));
        }
        finally
        {
            // A suspension ended by a revert or its handlers takes effect now that the guard is released
            history.CompletePendingSuspendEnd();
        }
    }

    /// <summary>
    ///     Builds the single history entry representing all applied steps.
    /// </summary>
    /// <param name="description">Description of the whole operation.</param>
    /// <returns>The composite command.</returns>
    public IUndoableCommand Build(string description) => new CompositeCommand(description, _applied);
}
