namespace DemaConsulting.Plexus;

/// <summary>
///     A command assembled from two delegates; the building block for lambda-based history entries.
/// </summary>
/// <remarks>
///     Used by <see cref="UndoHistory.Execute(string, Action, Action)"/> and by the model's own
///     primitive operations, so most mutations do not need a dedicated command class.
/// </remarks>
internal sealed class DelegateCommand : IUndoableCommand
{
    /// <summary>
    ///     The action that applies the change.
    /// </summary>
    private readonly Action _do;

    /// <summary>
    ///     The action that reverts the change.
    /// </summary>
    private readonly Action _undo;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegateCommand"/> class.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="doAction">Applies the change.</param>
    /// <param name="undoAction">Reverts the change.</param>
    public DelegateCommand(string description, Action doAction, Action undoAction)
    {
        Description = description;
        _do = doAction;
        _undo = undoAction;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Do() => _do();

    /// <inheritdoc />
    public void Undo() => _undo();
}
