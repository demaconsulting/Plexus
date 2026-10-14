namespace DemaConsulting.Plexus;

/// <summary>
///     Collects the primitive commands of a multi-step model operation while applying them, so the
///     whole operation becomes one undo entry.
/// </summary>
/// <remarks>
///     Many operations (remove with wires, group creation, move to page) must apply each step
///     before planning the next. Each step runs immediately and is remembered; if a later step
///     throws, <see cref="Abort"/> reverts the applied steps newest first so the model is never left
///     half-modified.
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
    ///     Reverts every applied step, newest first, and forgets them.
    /// </summary>
    public void Abort()
    {
        for (var i = _applied.Count - 1; i >= 0; i--)
        {
            _applied[i].Undo();
        }

        _applied.Clear();
    }

    /// <summary>
    ///     Builds the single history entry representing all applied steps.
    /// </summary>
    /// <param name="description">Description of the whole operation.</param>
    /// <returns>The composite command.</returns>
    public IUndoableCommand Build(string description) => new CompositeCommand(description, _applied);
}
