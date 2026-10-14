namespace DemaConsulting.Plexus;

/// <summary>
///     An ordered group of commands that is applied and reverted as one history entry.
/// </summary>
/// <remarks>
///     Do replays the children in order and Undo reverts them in reverse order, which is what makes
///     transactions and multi-step model operations round-trip exactly.
/// </remarks>
internal sealed class CompositeCommand : IUndoableCommand
{
    /// <summary>
    ///     The child commands in the order they were originally applied.
    /// </summary>
    private readonly List<IUndoableCommand> _commands;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompositeCommand"/> class.
    /// </summary>
    /// <param name="description">Human-readable description of the whole group.</param>
    /// <param name="commands">The child commands, already in application order. The list is copied.</param>
    public CompositeCommand(string description, IEnumerable<IUndoableCommand> commands)
    {
        Description = description;
        _commands = [.. commands];
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>
    ///     Gets the number of child commands.
    /// </summary>
    public int Count => _commands.Count;

    /// <inheritdoc />
    public void Do()
    {
        foreach (var command in _commands)
        {
            command.Do();
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
