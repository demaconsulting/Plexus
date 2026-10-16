namespace DemaConsulting.Plexus;

/// <summary>
///     Groups the commands executed while it is open into a single undo entry.
/// </summary>
/// <remarks>
///     Most code should use <see cref="UndoHistory.Transact(string, Action)"/>, which wraps this pattern and rolls back on failure;
///     obtain a transaction object from <see cref="UndoHistory.BeginTransaction"/> only when the edits cannot be one delegate,
///     and wrap it in a
///     <see langword="using"/> statement: disposing commits the group. Transactions nest by
///     flattening - only the outermost commit creates a history entry - while
///     <see cref="Rollback"/> on any level reverts just the commands recorded since that
///     transaction began.
/// </remarks>
/// <example>
///     Commit a group of edits as one step, or roll them back.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                     // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var node = page.AddNode("demo.Source", 0, 0, name: "A");
///
///     using (var transaction = diagram.History.BeginTransaction("Edit A"))
///     {
///         node.Properties["Value"] = 4.0;
///         node.MoveTo(100, 100);
///         if (node.Properties.Get&lt;double&gt;("Value") &gt; 3)
///         {
///             transaction.Rollback();                       // changed our mind: reverts both edits, records nothing
///         }
///     }                                                     // Dispose commits unless the transaction was rolled back
///
///     Console.WriteLine(diagram.History.UndoCount);         // 1: only "Add node" is in the history
///     </code>
/// </example>
public sealed class HistoryTransaction : IDisposable
{
    /// <summary>
    ///     The history that owns this transaction.
    /// </summary>
    private readonly UndoHistory _history;

    /// <summary>
    ///     Nesting depth of this transaction (1 for the outermost).
    /// </summary>
    private readonly int _depth;

    /// <summary>
    ///     Index in the open command list at which this transaction's commands begin.
    /// </summary>
    private readonly int _startIndex;

    /// <summary>
    ///     Whether the transaction has already been committed or rolled back.
    /// </summary>
    private bool _completed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoryTransaction"/> class.
    /// </summary>
    /// <param name="history">The owning history.</param>
    /// <param name="depth">Nesting depth (1 for the outermost).</param>
    /// <param name="startIndex">Index in the open command list where this transaction begins.</param>
    internal HistoryTransaction(UndoHistory history, int depth, int startIndex)
    {
        _history = history;
        _depth = depth;
        _startIndex = startIndex;
    }

    /// <summary>
    ///     Reverts and discards every command recorded since this transaction began.
    /// </summary>
    /// <remarks>
    ///     After a rollback the transaction is complete; disposing it afterwards does nothing. A change handler that throws
    ///     while the commands are reverted does not stop the rollback; the first such exception is rethrown once the
    ///     rollback has completed. A command whose inverse throws does not stop the rollback either: every inverse is
    ///     attempted, the transaction is completed (disposing it afterwards commits nothing), and the failure is rethrown
    ///     afterwards (several failures as an <see cref="AggregateException"/>).
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the transaction is already complete or is not the innermost open one.</exception>
    public void Rollback()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The transaction has already been completed.");
        }

        var handlerFailure = _history.EndTransaction(_depth, _startIndex, commit: false);
        _completed = true;
        handlerFailure?.Throw();
    }

    /// <summary>
    ///     Reverts and discards every command recorded since this transaction began because the transaction failed.
    /// </summary>
    /// <remarks>
    ///     Like <see cref="Rollback"/>, but change-handler exceptions raised while reverting are dropped, so the caller can
    ///     rethrow the failure that caused the rollback. Does nothing when the transaction is already complete (for example
    ///     when the failing code rolled it back itself). Every inverse is attempted; when some fail, an
    ///     <see cref="AggregateException"/> carrying <paramref name="cause"/> first and then the inverse failures is thrown instead.
    /// </remarks>
    /// <param name="cause">The failure that caused the rollback.</param>
    /// <exception cref="AggregateException">Thrown when at least one inverse failed.</exception>
    internal void RollbackAfterFailure(Exception cause)
    {
        if (_completed)
        {
            return;
        }

        var failure = _history.EndTransaction(_depth, _startIndex, commit: false, discardHandlerFailures: true, cause: cause);
        _completed = true;
        failure?.Throw();
    }

    /// <summary>
    ///     Commits the transaction (a no-op when it was already rolled back).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when transactions are disposed out of order.</exception>
    public void Dispose()
    {
        // Disposal after Rollback (or a second Dispose) is deliberately harmless
        if (_completed)
        {
            return;
        }

        // Mark completion only after the history accepted it, so a rejected out-of-order disposal can be retried
        _history.EndTransaction(_depth, _startIndex, commit: true);
        _completed = true;
    }
}
