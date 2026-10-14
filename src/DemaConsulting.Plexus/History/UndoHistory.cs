namespace DemaConsulting.Plexus;

/// <summary>
///     The undo/redo stack shared by everything in one <see cref="Diagram"/>.
/// </summary>
/// <remarks>
///     Every public model mutation executes a command through this class, so hosts get undo for
///     free and normally only call <see cref="Undo"/>, <see cref="Redo"/>, transactions and the
///     dirty-tracking members. Hosts can also record their own changes with
///     <see cref="Execute(string, Action, Action)"/>. The history is single-threaded and not safe for
///     concurrent use; all calls must come from the thread that owns the diagram.
/// </remarks>
/// <example>
///     Undo and redo, group edits into one step, and record your own changes.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                     // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var history = diagram.History;
///
///     // Several edits become one undo step; disposing the transaction commits it
///     using (history.BeginTransaction("Add pair"))
///     {
///         var x = page.AddNode("demo.Source", 0, 240);
///         var y = page.AddNode("demo.Sink", 180, 240);
///         page.Connect(x.Output("out"), y.Input("in"));
///     }
///
///     history.Undo();                                       // removes both nodes and the wire together
///     history.Redo();
///
///     // Property edits work the same way: two assignments, one undo entry
///     var source = diagram.FindNode("Source1")!;            // generated name of the first node above
///     using (history.BeginTransaction("Edit Source1"))
///     {
///         source.Properties["Value"] = 3.0;
///         source.Name = "Feed";
///     }
///     history.Undo();                                       // both edits revert together
///
///     // Record a change of your own with a do and an undo delegate
///     var title = "Draft";
///     history.Execute("Rename document", () =&gt; title = "Final", () =&gt; title = "Draft");
///     history.Undo();                                       // title is "Draft" again
///     Console.WriteLine($"{history.UndoDescription} / dirty: {history.IsDirty}");
///     history.MarkClean();                                  // call after saving
///     </code>
/// </example>
public sealed class UndoHistory
{
    /// <summary>
    ///     Default maximum number of undo entries kept.
    /// </summary>
    public const int DefaultCapacity = 1000;

    /// <summary>
    ///     Entries that can be undone; the last element is the most recent.
    /// </summary>
    private readonly List<IUndoableCommand> _undo = [];

    /// <summary>
    ///     Entries that can be redone; the last element is the next one to redo.
    /// </summary>
    private readonly List<IUndoableCommand> _redo = [];

    /// <summary>
    ///     Start indices (into <see cref="_open"/>) of the currently open transactions, innermost last.
    /// </summary>
    private readonly List<int> _transactionStarts = [];

    /// <summary>
    ///     Commands recorded by the open outermost transaction, or null when none is open.
    /// </summary>
    private List<IUndoableCommand>? _open;

    /// <summary>
    ///     Description of the outermost open transaction.
    /// </summary>
    private string _openDescription = string.Empty;

    /// <summary>
    ///     Number of active <see cref="Suspend"/> scopes.
    /// </summary>
    private int _suspendCount;

    /// <summary>
    ///     Whether the history was dirty when the outermost <see cref="Suspend"/> scope began.
    /// </summary>
    private bool _dirtyBeforeSuspend;

    /// <summary>
    ///     True while an Undo or Redo is running, so re-entrant recording can be rejected.
    /// </summary>
    private bool _applying;

    /// <summary>
    ///     Maximum number of undo entries.
    /// </summary>
    private int _capacity = DefaultCapacity;

    /// <summary>
    ///     Undo-stack depth at which the document was last marked clean; null when that state is no longer reachable.
    /// </summary>
    private int? _savePoint = 0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UndoHistory"/> class as an empty history with the default capacity.
    /// </summary>
    public UndoHistory()
    {
        // Nothing to initialize: every member has a usable default
    }

    /// <summary>
    ///     Occurs whenever the undo/redo state (stacks or dirty flag) changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    ///     Gets a value indicating whether there is an entry to undo.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether there is an entry to redo.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    ///     Gets the description of the entry <see cref="Undo"/> would revert, or null when there is none.
    /// </summary>
    public string? UndoDescription => _undo.Count > 0 ? _undo[^1].Description : null;

    /// <summary>
    ///     Gets the description of the entry <see cref="Redo"/> would re-apply, or null when there is none.
    /// </summary>
    public string? RedoDescription => _redo.Count > 0 ? _redo[^1].Description : null;

    /// <summary>
    ///     Gets the number of undo entries currently stored.
    /// </summary>
    public int UndoCount => _undo.Count;

    /// <summary>
    ///     Gets the number of redo entries currently stored.
    /// </summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    ///     Gets a value indicating whether a transaction is currently open.
    /// </summary>
    public bool IsInTransaction => _transactionStarts.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether recording is currently suspended.
    /// </summary>
    public bool IsSuspended => _suspendCount > 0;

    /// <summary>
    ///     Gets or sets the maximum number of undo entries kept; the oldest entries are dropped first.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than one.</exception>
    public int Capacity
    {
        get => _capacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _capacity = value;

            // Shrinking takes effect immediately so memory is released
            if (TrimToCapacity())
            {
                OnChanged();
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the document changed since <see cref="MarkClean"/> (or since creation).
    /// </summary>
    /// <remarks>
    ///     Undoing back to the clean point makes the history clean again. Ending a <see cref="Suspend"/>
    ///     scope restores the dirty state the history had when the scope began, so loading a document
    ///     into a clean diagram leaves it clean.
    /// </remarks>
    public bool IsDirty => _savePoint != _undo.Count;

    /// <summary>
    ///     Records the current state as the saved (clean) state.
    /// </summary>
    public void MarkClean()
    {
        _savePoint = _undo.Count;
        OnChanged();
    }

    /// <summary>
    ///     Discards all undo and redo entries and marks the history clean.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a transaction is open.</exception>
    public void Clear()
    {
        if (IsInTransaction)
        {
            throw new InvalidOperationException("The history cannot be cleared while a transaction is open.");
        }

        _undo.Clear();
        _redo.Clear();
        _savePoint = 0;
        OnChanged();
    }

    /// <summary>
    ///     Applies a command and records it so it can be undone.
    /// </summary>
    /// <remarks>
    ///     Executing clears the redo stack. Inside a transaction the command joins the transaction
    ///     (merging into the previous command when it supports <see cref="IMergeableCommand"/>);
    ///     while suspended it is applied but not recorded.
    /// </remarks>
    /// <param name="command">The command to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when called while an undo or redo is running.</exception>
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureNotApplying();

        // Apply first: a command that throws must never be recorded
        command.Do();
        Record(command);
    }

    /// <summary>
    ///     Applies a change expressed as two delegates and records it so it can be undone.
    /// </summary>
    /// <param name="description">Human-readable description of the change.</param>
    /// <param name="doAction">Applies the change; also used for redo.</param>
    /// <param name="undoAction">Reverts the change exactly.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public void Execute(string description, Action doAction, Action undoAction)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(doAction);
        ArgumentNullException.ThrowIfNull(undoAction);
        Execute(new DelegateCommand(description, doAction, undoAction));
    }

    /// <summary>
    ///     Records a command that has already been applied.
    /// </summary>
    /// <remarks>
    ///     Used by the model for multi-step operations that must apply each step while planning the
    ///     next. Follows the same transaction, merge and suspension rules as <see cref="Execute(IUndoableCommand)"/>.
    /// </remarks>
    /// <param name="command">The command whose effect is already applied.</param>
    internal void Record(IUndoableCommand command)
    {
        EnsureNotApplying();

        // Suspended changes are not recorded and leave the document permanently unsaved until MarkClean/Clear
        if (_suspendCount > 0)
        {
            _savePoint = null;
            return;
        }

        // Inside a transaction, try to merge into the previous command of the innermost transaction
        if (_open is not null)
        {
            var floor = _transactionStarts[^1];
            if (_open.Count > floor && _open[^1] is IMergeableCommand mergeable && mergeable.TryMerge(command))
            {
                return;
            }

            _open.Add(command);
            return;
        }

        Push(command);
    }

    /// <summary>
    ///     Reverts the most recent entry.
    /// </summary>
    /// <returns><see langword="true"/> when an entry was undone; <see langword="false"/> when there was nothing to undo.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a transaction is open or an undo/redo is already running.</exception>
    public bool Undo()
    {
        EnsureIdle();
        if (_undo.Count == 0)
        {
            return false;
        }

        var command = _undo[^1];
        Apply(command.Undo);

        // Only move the entry after the revert succeeded, so a failing command stays on the undo stack
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);
        OnChanged();
        return true;
    }

    /// <summary>
    ///     Re-applies the most recently undone entry.
    /// </summary>
    /// <returns><see langword="true"/> when an entry was redone; <see langword="false"/> when there was nothing to redo.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a transaction is open or an undo/redo is already running.</exception>
    public bool Redo()
    {
        EnsureIdle();
        if (_redo.Count == 0)
        {
            return false;
        }

        var command = _redo[^1];
        Apply(command.Do);
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        OnChanged();
        return true;
    }

    /// <summary>
    ///     Opens a transaction that groups subsequent commands into one undo entry.
    /// </summary>
    /// <remarks>
    ///     Nested transactions flatten: only the outermost one creates an entry, and it uses the
    ///     outermost description. Empty transactions record nothing.
    /// </remarks>
    /// <param name="description">Description of the combined entry.</param>
    /// <returns>The transaction; dispose it to commit.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
    public HistoryTransaction BeginTransaction(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        EnsureNotApplying();

        if (_open is null)
        {
            _open = [];
            _openDescription = description;
        }

        var startIndex = _open.Count;
        _transactionStarts.Add(startIndex);
        return new HistoryTransaction(this, _transactionStarts.Count, startIndex);
    }

    /// <summary>
    ///     Runs an action as ONE undo entry: commits when it returns, rolls everything back and rethrows when it throws.
    /// </summary>
    /// <remarks>
    ///     Shorthand for the correct transaction pattern: <see cref="BeginTransaction"/>, the action, and
    ///     <see cref="HistoryTransaction.Rollback"/> followed by a rethrow on failure, so a failed edit never leaves half of its
    ///     changes in the history. Nested calls flatten like nested transactions (only the outermost creates an entry).
    ///     An action that changes nothing records nothing.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     diagram.History.Transact("Edit properties", () =&gt;
    ///     {
    ///         node.Properties["Gain"] = 3.0;
    ///         node.Properties["Mode"] = "Slow";     // throws: not a choice, so the Gain change is undone as well
    ///     });
    ///
    ///     int count = diagram.History.Transact("Add two", () =&gt; { page.AddNode("demo.Source", 0, 0); page.AddNode("demo.Sink", 200, 0); return 2; });
    ///     </code>
    /// </example>
    /// <param name="description">Description of the combined entry.</param>
    /// <param name="action">The edits to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when called while an undo or redo is running.</exception>
    public void Transact(string description, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Transact(description, () =>
        {
            action();
            return true;
        });
    }

    /// <summary>
    ///     Runs a function as ONE undo entry and returns its result: commits when it returns, rolls everything back and rethrows when it throws.
    /// </summary>
    /// <remarks>
    ///     See <see cref="Transact(string, Action)"/>.
    /// </remarks>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="description">Description of the combined entry.</param>
    /// <param name="function">The edits to apply.</param>
    /// <returns>What <paramref name="function"/> returned.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when called while an undo or redo is running.</exception>
    public T Transact<T>(string description, Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        using var transaction = BeginTransaction(description);
        try
        {
            return function();
        }
        catch
        {
            // Revert whatever the function did before it failed, then let the caller see the original exception
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    ///     Suspends recording; changes made until the returned scope is disposed are applied but not recorded.
    /// </summary>
    /// <remarks>
    ///     Intended for loading or bulk-building a document. Entries recorded before the suspension
    ///     may no longer match the model afterwards, so when the outermost scope is disposed the undo
    ///     and redo stacks are cleared, and the dirty state returns to what it was when the scope began:
    ///     a clean diagram stays clean, a dirty one stays dirty. Nested scopes only take effect when
    ///     the outermost one ends. Call <see cref="MarkClean"/> afterwards if the loaded content should
    ///     count as saved content of an already dirty history.
    /// </remarks>
    /// <returns>A scope that resumes recording when disposed.</returns>
    public IDisposable Suspend()
    {
        // Remember the dirty state only for the outermost scope; it is restored when that scope ends
        if (_suspendCount == 0)
        {
            _dirtyBeforeSuspend = IsDirty;
        }

        _suspendCount++;
        return new SuspendScope(this);
    }

    /// <summary>
    ///     Completes the innermost open transaction.
    /// </summary>
    /// <param name="depth">Nesting depth of the transaction being completed.</param>
    /// <param name="startIndex">Index where the transaction's commands begin in the open list.</param>
    /// <param name="commit">True to keep the commands; false to revert and discard them.</param>
    /// <exception cref="InvalidOperationException">Thrown when transactions are completed out of order.</exception>
    internal void EndTransaction(int depth, int startIndex, bool commit)
    {
        if (depth != _transactionStarts.Count || _open is null)
        {
            throw new InvalidOperationException("Transactions must be completed in the reverse order they were started.");
        }

        // Rollback reverts this transaction's commands newest first, then forgets them
        if (!commit)
        {
            for (var i = _open.Count - 1; i >= startIndex; i--)
            {
                var command = _open[i];
                Apply(command.Undo);
                _open.RemoveAt(i);
            }
        }

        _transactionStarts.RemoveAt(_transactionStarts.Count - 1);

        // Only the outermost completion produces a history entry
        if (_transactionStarts.Count > 0)
        {
            return;
        }

        var commands = _open;
        _open = null;
        if (commands.Count > 0)
        {
            Push(new CompositeCommand(_openDescription, commands));
        }
    }

    /// <summary>
    ///     Pushes a finished entry onto the undo stack, clearing redo and enforcing capacity.
    /// </summary>
    /// <param name="command">The entry to push.</param>
    private void Push(IUndoableCommand command)
    {
        // Discarding redo entries makes any save point that lay beyond the undo depth unreachable
        _redo.Clear();
        if (_savePoint > _undo.Count)
        {
            _savePoint = null;
        }

        _undo.Add(command);
        TrimToCapacity();
        OnChanged();
    }

    /// <summary>
    ///     Drops the oldest undo entries until the stack fits <see cref="Capacity"/>.
    /// </summary>
    /// <returns><see langword="true"/> when at least one entry was dropped.</returns>
    private bool TrimToCapacity()
    {
        var trimmed = false;
        while (_undo.Count > _capacity)
        {
            _undo.RemoveAt(0);
            trimmed = true;

            // Every remaining entry moved one position closer to the base; a save point at the base is lost
            _savePoint = _savePoint is > 0 ? _savePoint - 1 : null;
        }

        return trimmed;
    }

    /// <summary>
    ///     Runs an undo or redo action while guarding against re-entrant recording.
    /// </summary>
    /// <param name="action">The action to run.</param>
    private void Apply(Action action)
    {
        _applying = true;
        try
        {
            action();
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>
    ///     Throws when an undo or redo is currently running.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown while an undo or redo is running.</exception>
    internal void EnsureNotApplying()
    {
        if (_applying)
        {
            throw new InvalidOperationException("The history cannot be modified while an undo or redo is running.");
        }
    }

    /// <summary>
    ///     Throws unless no undo, redo or transaction is in progress.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an operation is in progress.</exception>
    private void EnsureIdle()
    {
        EnsureNotApplying();
        if (IsInTransaction)
        {
            throw new InvalidOperationException("Undo and redo are not available while a transaction is open.");
        }
    }

    /// <summary>
    ///     Raises the <see cref="Changed"/> event.
    /// </summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    ///     Ends one suspension scope; the outermost one discards stale entries and restores the dirty state.
    /// </summary>
    private void EndSuspend()
    {
        _suspendCount--;
        if (_suspendCount > 0)
        {
            return;
        }

        // Entries recorded earlier may not apply to the changed model any more, so they are dropped
        _undo.Clear();
        _redo.Clear();

        // With the stacks empty, save point 0 is "clean" and an unreachable save point is "dirty"
        _savePoint = _dirtyBeforeSuspend ? null : 0;
        OnChanged();
    }

    /// <summary>
    ///     Scope returned by <see cref="Suspend"/>; resumes recording once when disposed.
    /// </summary>
    private sealed class SuspendScope : IDisposable
    {
        /// <summary>
        ///     The history to resume.
        /// </summary>
        private readonly UndoHistory _owner;

        /// <summary>
        ///     Whether the scope was already disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SuspendScope"/> class.
        /// </summary>
        /// <param name="owner">The history that was suspended.</param>
        public SuspendScope(UndoHistory owner) => _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            // Double disposal must not underflow the suspension counter
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.EndSuspend();
        }
    }
}
