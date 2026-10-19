namespace DemaConsulting.Plexus;

/// <summary>
///     An ordered group of commands that is applied and reverted as one history entry.
/// </summary>
/// <remarks>
///     <para>
///         Do replays the children in order and Undo reverts them in reverse order, which is what makes
///         transactions and multi-step model operations round-trip exactly.
///     </para>
///     <para>
///         Replay is failure-atomic: when a child throws, the children already replayed by this call are compensated
///         (a failing <see cref="Do"/> reverts them newest first, a failing <see cref="Undo"/> re-applies them in their
///         original order) and the child's exception is rethrown, so the model is back in the state before the call. This
///         relies on the <see cref="IUndoableCommand"/> contract that a command which throws has not changed anything. When a
///         compensation also throws, every remaining compensation is still attempted and an <see cref="AggregateException"/>
///         carrying the original failure followed by every compensation failure is thrown.
///     </para>
/// </remarks>
internal sealed class CompositeCommand : IUndoableCommand
{
    /// <summary>
    ///     The message of the exception thrown when a failed replay could not be fully compensated.
    /// </summary>
    private const string RestoreFailedMessage = "The entry failed and could not be fully restored.";

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
    /// <exception cref="AggregateException">Thrown when a child fails and re-reverting the children replayed before it also fails.</exception>
    public void Do()
    {
        for (var i = 0; i < _commands.Count; i++)
        {
            try
            {
                _commands[i].Do();
            }
            catch (Exception cause)
            {
                // Revert the children this call already applied, newest first, then report the original failure
                var reverts = new List<Action>(i);
                for (var k = i - 1; k >= 0; k--)
                {
                    reverts.Add(_commands[k].Undo);
                }

                Compensate(cause, reverts, RestoreFailedMessage);
                throw;
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="AggregateException">Thrown when a child fails and re-applying the children reverted before it also fails.</exception>
    public void Undo()
    {
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            try
            {
                _commands[i].Undo();
            }
            catch (Exception cause)
            {
                // Re-apply the children this call already reverted, in their original order, then report the original failure
                var reapplies = new List<Action>(_commands.Count - i - 1);
                for (var k = i + 1; k < _commands.Count; k++)
                {
                    reapplies.Add(_commands[k].Do);
                }

                Compensate(cause, reapplies, RestoreFailedMessage);
                throw;
            }
        }
    }

    /// <summary>
    ///     Runs every compensating step after a failure, collecting the failures of the steps themselves.
    /// </summary>
    /// <remarks>
    ///     Every step is attempted even when an earlier one throws. Returns normally when all of them succeeded, so the
    ///     caller can rethrow <paramref name="cause"/> unchanged; shared by grouped entries and <see cref="CommandPlan.Abort"/>.
    /// </remarks>
    /// <param name="cause">The failure that made the compensation necessary.</param>
    /// <param name="steps">The compensating steps, in the order they must run.</param>
    /// <param name="message">The message of the aggregate exception.</param>
    /// <exception cref="AggregateException">
    ///     Thrown when a step failed; the inner exceptions are <paramref name="cause"/> followed by the step failures in order.
    /// </exception>
    internal static void Compensate(Exception cause, IEnumerable<Action> steps, string message)
    {
        List<Exception>? failures = null;
        foreach (var step in steps)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                // Intentional catch-all: every failing inverse must be collected and reported together with the original failure
                (failures ??= [cause]).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException(message, failures);
        }
    }
}
