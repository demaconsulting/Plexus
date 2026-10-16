using System.Runtime.ExceptionServices;

namespace DemaConsulting.Plexus;

/// <summary>
///     The undo/redo stack shared by everything in one <see cref="Diagram"/>.
/// </summary>
/// <remarks>
///     <para>
///         Every public model mutation executes a command through this class, so hosts get undo for
///         free and normally only call <see cref="Undo"/>, <see cref="Redo"/>, <see cref="Transact(string, Action)"/> and the
///         dirty-tracking members. <see cref="Transact(string, Action)"/> is the way to make several edits one undo step (it also
///         rolls the edits back when one of them throws); <see cref="BeginTransaction"/> is the lower-level form for code that cannot
///         be written as one delegate. Hosts can also record their own changes with
///         <see cref="Execute(string, Action, Action)"/>. The history is single-threaded and not safe for
///         concurrent use; all calls must come from the thread that owns the diagram.
///     </para>
///     <para>
///         <b>Change handlers that throw.</b> While the history applies a change (an execute, a multi-step model operation,
///         an undo, a redo or a rollback), an exception thrown by a handler of the diagram's change notifications
///         (<see cref="Diagram.Changed"/>, <see cref="Diagram.NodeRenamed"/>, <see cref="DiagramElement.PropertyChanged"/>,
///         <see cref="DiagramPage.PropertyChanged"/>, the page content collections, <see cref="PageCollection.CollectionChanged"/>
///         and <see cref="PropertyBag.PropertyChanged"/>) does not interrupt the change: the remaining notifications are
///         raised, the change is completed and recorded (or moved to the other stack), and only then is the first handler
///         exception rethrown to the caller with its original stack trace; later handler exceptions of the same change are
///         dropped. Inside <see cref="Transact(string, Action)"/> the rethrown exception rolls the transaction back like any
///         other failure. When the change itself fails, its own exception propagates and handler exceptions are dropped.
///         Handlers of this class's <see cref="Changed"/> event are not covered: the stacks are already consistent when it is raised.
///     </para>
///     <para>
///         <b>Changes from replay code.</b> Code that runs while an entry is undone, redone or rolled back (a command's
///         do or undo code and the change handlers it triggers) cannot change the history:
///         <see cref="Execute(IUndoableCommand)"/>,
///         <see cref="BeginTransaction"/>, <see cref="Clear"/>, <see cref="MarkClean"/>, setting <see cref="Capacity"/> and
///         <see cref="Suspend"/> throw <see cref="InvalidOperationException"/> and change nothing. Ending the outermost
///         <see cref="Suspend"/> scope from such code is deferred until the undo, redo or rollback has finished.
///         The same guard covers the reverts of a failed multi-step model operation. <see cref="Suspend"/> also throws while a
///         transaction is open.
///     </para>
/// </remarks>
/// <example>
///     Undo and redo, group edits into one step with <c>Transact</c>, and record your own changes.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var diagram = new Diagram(types);                     // types: the registry of the DemaConsulting.Plexus example
///     var page = diagram.Pages.Add("Main");
///     var history = diagram.History;
///
///     // Several edits become one undo step; if the delegate throws, everything it did is rolled back
///     history.Transact("Add pair", () =>
///     {
///         var x = page.AddNode("demo.Source", 0, 240);
///         var y = page.AddNode("demo.Sink", 180, 240);
///         page.Connect(x.Output("out"), y.Input("in"));
///     });
///
///     history.Undo();                                       // removes both nodes and the wire together
///     history.Redo();
///
///     // Property edits work the same way: two assignments, one undo entry
///     var source = diagram.FindNode("Source1")!;            // generated name of the first node above
///     history.Transact("Edit Source1", () =>
///     {
///         source.Properties["Value"] = 3.0;
///         source.Name = "Feed";
///     });
///     history.Undo();                                       // both edits revert together
///
///     // The advanced form: dispose the transaction to commit, or call Rollback() to cancel
///     using (history.BeginTransaction("Rename"))
///     {
///         source.Name = "Feed2";
///     }
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
    ///     Whether the outermost open transaction began while recording was suspended: its commands are kept only so a
    ///     rollback can revert them, and are discarded instead of recorded when it commits.
    /// </summary>
    private bool _openSuspended;

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
    ///     Number of change operations (execute, model operation, undo, redo, rollback) currently running, nested.
    /// </summary>
    /// <remarks>
    ///     While it is above zero, exceptions from change handlers are captured by <see cref="Notify{TState}"/> instead of
    ///     interrupting the change.
    /// </remarks>
    private int _operationDepth;

    /// <summary>
    ///     The first change-handler exception captured during the running operation, or null when none was thrown.
    /// </summary>
    private ExceptionDispatchInfo? _handlerFailure;

    /// <summary>
    ///     True when the outermost <see cref="Suspend"/> scope ended while an undo, redo or rollback was running, so its
    ///     effect (clearing the stacks, restoring the dirty state) runs once that replay has finished.
    /// </summary>
    private bool _suspendEndPending;

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
    /// <exception cref="InvalidOperationException">Thrown when set while an undo, redo or rollback is running.</exception>
    public int Capacity
    {
        get => _capacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            EnsureNotApplying();
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
    /// <exception cref="InvalidOperationException">Thrown when called while an undo, redo or rollback is running.</exception>
    public void MarkClean()
    {
        EnsureNotApplying();
        _savePoint = _undo.Count;
        OnChanged();
    }

    /// <summary>
    ///     Discards all undo and redo entries and marks the history clean.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a transaction is open or an undo, redo or rollback is running.</exception>
    public void Clear()
    {
        EnsureNotApplying();
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
    ///     while suspended it is applied but not recorded. A change handler that throws while the command applies does not
    ///     stop it: the command is completed and recorded, and the first handler exception is rethrown afterwards.
    /// </remarks>
    /// <param name="command">The command to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when called while an undo or redo is running.</exception>
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureNotApplying();

        // Apply first: a command that throws must never be recorded (and handler failures it caused are dropped)
        var before = BeginOperation();
        try
        {
            command.Do();
        }
        catch
        {
            AbortOperation(before);
            throw;
        }

        // Record the completed change before reporting a handler failure, so model and history stay consistent
        var handlerFailure = EndOperation();
        Record(command);
        handlerFailure?.Throw();
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

        // Suspended changes are not recorded and leave the document permanently unsaved until MarkClean/Clear. Inside a
        // transaction (which then began suspended, as Suspend is refused while one is open) they are still kept, so a rollback
        // can revert them; the commit discards them
        if (_suspendCount > 0)
        {
            _savePoint = null;
            if (_open is null)
            {
                return;
            }
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
    /// <remarks>
    ///     When a command of the entry fails, the entry's other commands are restored and the entry stays on the undo
    ///     stack. A change handler that throws does not stop the undo: the entry moves to the redo stack and the first
    ///     handler exception is rethrown afterwards.
    /// </remarks>
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
        Replay(command.Undo);

        // Only move the entry after the revert succeeded, so a failing command stays on the undo stack
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);
        FinishReplay();
        return true;
    }

    /// <summary>
    ///     Re-applies the most recently undone entry.
    /// </summary>
    /// <remarks>
    ///     When a command of the entry fails, the entry's other commands are restored and the entry stays on the redo
    ///     stack. A change handler that throws does not stop the redo: the entry moves to the undo stack and the first
    ///     handler exception is rethrown afterwards.
    /// </remarks>
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
        Replay(command.Do);
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        FinishReplay();
        return true;
    }

    /// <summary>
    ///     Opens a transaction that groups subsequent commands into one undo entry.
    /// </summary>
    /// <remarks>
    ///     This is the advanced form: prefer <see cref="Transact(string, Action)"/>, which commits, rolls back on failure and
    ///     cannot be forgotten. Use a transaction object when the edits span code that cannot be one delegate (for example
    ///     an event handler that starts it and another that completes it).
    ///     Nested transactions flatten: only the outermost one creates an entry, and it uses the
    ///     outermost description. Empty transactions record nothing.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);              // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     using (var transaction = diagram.History.BeginTransaction("Add source"))
    ///     {
    ///         page.AddNode("demo.Source", 0, 0);
    ///         if (page.Nodes.Count > 5)
    ///         {
    ///             transaction.Rollback();                // undo what the transaction did so far
    ///         }
    ///     }                                              // disposing commits one undo entry
    ///     </code>
    /// </example>
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
            _openSuspended = _suspendCount > 0;
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
    ///     An action that changes nothing records nothing. A change handler that throws during one of the edits makes that
    ///     edit throw after it completed; unless the action catches it, the whole transaction is rolled back and the
    ///     handler's exception propagates.
    /// </remarks>
    /// <example>
    ///     Two edits, one undo step; a failure undoes the part that already ran.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);              // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     var node = page.AddNode("demo.Source", 0, 0, name: "A");
    ///
    ///     diagram.History.Transact("Edit node", () =>
    ///     {
    ///         node.Properties["Value"] = 3.0;
    ///         node.Name = "Feed";
    ///     });
    ///     diagram.History.Undo();                        // Value and Name revert together
    ///
    ///     try
    ///     {
    ///         diagram.History.Transact("Edit node", () =>
    ///         {
    ///             node.Properties["Value"] = 4.0;
    ///             throw new InvalidOperationException("The second edit failed.");
    ///         });
    ///     }
    ///     catch (InvalidOperationException)
    ///     {
    ///         Console.WriteLine(node.Properties["Value"]);   // 1: the first assignment was rolled back
    ///     }
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
    /// <example>
    ///     Add two nodes as one undo step and return how many were added.
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///
    ///     var diagram = new Diagram(types);              // types: the registry of the DemaConsulting.Plexus example
    ///     var page = diagram.Pages.Add("Main");
    ///     int count = diagram.History.Transact("Add two", () =>
    ///     {
    ///         page.AddNode("demo.Source", 0, 0);
    ///         page.AddNode("demo.Sink", 200, 0);
    ///         return 2;
    ///     });
    ///     </code>
    /// </example>
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
        catch (Exception ex)
        {
            // Revert whatever the function did before it failed, then let the caller see the original exception
            // (change-handler exceptions raised while reverting are dropped in favor of it; failing inverses are
            // reported together with it)
            transaction.RollbackAfterFailure(ex);
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
    ///     Disposing the outermost scope from code that runs during an undo, redo or rollback takes effect right after
    ///     that undo, redo or rollback has finished. Suspending is not allowed while a transaction is open, because the
    ///     transaction's commit could otherwise record entries after the suspension discarded the stacks. A transaction
    ///     begun while recording is suspended still rolls back everything it did when it fails or is rolled back; when it
    ///     commits, nothing is recorded.
    /// </remarks>
    /// <returns>A scope that resumes recording when disposed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when called while an undo, redo or rollback is running, or while a transaction is open.</exception>
    public IDisposable Suspend()
    {
        EnsureNotApplying();
        if (IsInTransaction)
        {
            throw new InvalidOperationException("Recording cannot be suspended while a transaction is open.");
        }

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
    /// <remarks>
    ///     A rollback runs as one change operation: change-handler exceptions raised while reverting are captured and,
    ///     unless <paramref name="discardHandlerFailures"/> is set, the first one is returned for the caller to rethrow once
    ///     the transaction is complete. A rollback attempts every inverse even when one throws: the transaction is always
    ///     completed (it can never be committed afterwards) and the inverse failures are returned instead, together with
    ///     <paramref name="cause"/> when given.
    /// </remarks>
    /// <param name="depth">Nesting depth of the transaction being completed.</param>
    /// <param name="startIndex">Index where the transaction's commands begin in the open list.</param>
    /// <param name="commit">True to keep the commands; false to revert and discard them.</param>
    /// <param name="discardHandlerFailures">True to drop change-handler exceptions of a rollback (used when the rollback is caused by a failure).</param>
    /// <param name="cause">The failure that caused the rollback, reported together with any inverse failures; null for an explicit rollback.</param>
    /// <returns>The exception of the rollback to rethrow once the transaction is complete (inverse failures, else the first change-handler exception), or null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when transactions are completed out of order.</exception>
    internal ExceptionDispatchInfo? EndTransaction(
        int depth,
        int startIndex,
        bool commit,
        bool discardHandlerFailures = false,
        Exception? cause = null)
    {
        if (depth != _transactionStarts.Count || _open is null)
        {
            throw new InvalidOperationException("Transactions must be completed in the reverse order they were started.");
        }

        // Rollback reverts this transaction's commands newest first, then forgets them. The whole revert runs under the
        // replay guard, so neither the inverses nor the change handlers they trigger can change the history.
        ExceptionDispatchInfo? handlerFailure = null;
        if (!commit)
        {
            var before = BeginOperation();
            var revertFailures = RevertOpenCommands(startIndex);
            if (revertFailures is not null)
            {
                // The transaction is complete either way; the revert failures (and the cause) are reported, handler failures dropped
                AbortOperation(before);
                handlerFailure = DescribeRevertFailure(cause, revertFailures);
            }
            else if (discardHandlerFailures)
            {
                AbortOperation(before);
            }
            else
            {
                handlerFailure = EndOperation();
            }

            CompletePendingSuspendEnd();
        }

        _transactionStarts.RemoveAt(_transactionStarts.Count - 1);

        // Only the outermost completion produces a history entry
        if (_transactionStarts.Count > 0)
        {
            return handlerFailure;
        }

        var commands = _open;
        _open = null;

        // A transaction that began while recording was suspended kept its commands only for a rollback: nothing is recorded
        if (_openSuspended)
        {
            _openSuspended = false;
            return handlerFailure;
        }

        if (commands.Count > 0)
        {
            Push(new CompositeCommand(_openDescription, commands));
        }

        return handlerFailure;
    }

    /// <summary>
    ///     Reverts and forgets the open commands from <paramref name="startIndex"/> on, newest first, attempting every one.
    /// </summary>
    /// <param name="startIndex">Index where the completing transaction's commands begin in the open list.</param>
    /// <returns>The failures of the inverses, newest command first, or null when every inverse succeeded.</returns>
    private List<Exception>? RevertOpenCommands(int startIndex)
    {
        List<Exception>? failures = null;
        RunGuarded(() =>
        {
            for (var i = _open!.Count - 1; i >= startIndex; i--)
            {
                try
                {
                    _open[i].Undo();
                }
                catch (Exception ex)
                {
                    (failures ??= []).Add(ex);
                }
            }
        });

        // Every command is forgotten even when its inverse failed, so the transaction can never commit a half-reverted state
        _open!.RemoveRange(startIndex, _open.Count - startIndex);
        return failures;
    }

    /// <summary>
    ///     Builds the failure to report when inverses failed during a rollback.
    /// </summary>
    /// <param name="cause">The failure that made the rollback necessary, or null for an explicit rollback.</param>
    /// <param name="revertFailures">The inverse failures, newest command first.</param>
    /// <returns>The exception to throw once the transaction is complete.</returns>
    private static ExceptionDispatchInfo DescribeRevertFailure(Exception? cause, List<Exception> revertFailures)
    {
        // A lone inverse failure of an explicit rollback propagates as itself
        if (cause is null && revertFailures.Count == 1)
        {
            return ExceptionDispatchInfo.Capture(revertFailures[0]);
        }

        var all = cause is null ? revertFailures : [cause, .. revertFailures];
        return ExceptionDispatchInfo.Capture(
            new AggregateException("The transaction was rolled back but not every change could be reverted.", all));
    }

    /// <summary>
    ///     Runs compensation code under the replay guard, so it cannot change the history.
    /// </summary>
    /// <remarks>
    ///     Like an undo or redo, code run this way (and the change handlers it triggers) cannot call
    ///     <see cref="Execute(IUndoableCommand)"/>, <see cref="BeginTransaction"/>, <see cref="Clear"/> and the like; an
    ///     outermost <see cref="Suspend"/> scope ended meanwhile takes effect when <see cref="CompletePendingSuspendEnd"/> runs.
    ///     The previous guard state is restored afterwards.
    /// </remarks>
    /// <param name="action">The compensation to run.</param>
    internal void RunGuarded(Action action)
    {
        var wasApplying = _applying;
        _applying = true;
        try
        {
            action();
        }
        finally
        {
            _applying = wasApplying;
        }
    }

    /// <summary>
    ///     Raises a change notification, capturing a handler exception while a change operation is running.
    /// </summary>
    /// <remarks>
    ///     Outside an operation (for example a settings change, which is not undoable) the notification is raised directly
    ///     and a handler exception propagates immediately. Inside one, an exception (other than running out of memory) is
    ///     remembered - only the first of the operation - and the change continues; the operation rethrows it once the
    ///     change is complete and recorded.
    /// </remarks>
    /// <typeparam name="TState">The type of the state passed to <paramref name="raise"/>.</typeparam>
    /// <param name="state">The state the notification needs (sender, arguments, list and item, ...).</param>
    /// <param name="raise">Raises the notification; a static lambda avoids allocations.</param>
    internal void Notify<TState>(TState state, Action<TState> raise)
    {
        if (_operationDepth == 0)
        {
            raise(state);
            return;
        }

        try
        {
            raise(state);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _handlerFailure ??= ExceptionDispatchInfo.Capture(ex);
        }
    }

    /// <summary>
    ///     Starts a change operation, during which change-handler exceptions are captured by <see cref="Notify{TState}"/>.
    /// </summary>
    /// <returns>The handler failure captured before this operation, to pass to <see cref="AbortOperation"/> if it fails.</returns>
    internal ExceptionDispatchInfo? BeginOperation()
    {
        _operationDepth++;
        return _handlerFailure;
    }

    /// <summary>
    ///     Ends a change operation that completed.
    /// </summary>
    /// <returns>
    ///     The first handler exception captured, for the caller to rethrow after recording the change, when this was the
    ///     outermost operation; null otherwise (an enclosing operation reports it).
    /// </returns>
    internal ExceptionDispatchInfo? EndOperation()
    {
        _operationDepth--;
        if (_operationDepth > 0)
        {
            return null;
        }

        var failure = _handlerFailure;
        _handlerFailure = null;
        return failure;
    }

    /// <summary>
    ///     Ends a change operation that failed (or whose handler failures must not be reported), dropping the handler
    ///     exceptions captured during it.
    /// </summary>
    /// <param name="before">The value <see cref="BeginOperation"/> returned.</param>
    internal void AbortOperation(ExceptionDispatchInfo? before)
    {
        _operationDepth--;
        _handlerFailure = _operationDepth > 0 ? before : null;
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
    private void Apply(Action action) => RunGuarded(action);

    /// <summary>
    ///     Runs the undo or redo of an entry as one change operation.
    /// </summary>
    /// <remarks>
    ///     When the entry fails, the handler exceptions it caused are dropped, a suspension end deferred during it takes
    ///     effect, and the entry's exception propagates (the caller has not moved the entry yet, so it stays on its stack).
    ///     On success the caller moves the entry and calls <see cref="FinishReplay"/>.
    /// </remarks>
    /// <param name="action">The entry's undo or do action.</param>
    private void Replay(Action action)
    {
        var before = BeginOperation();
        try
        {
            Apply(action);
        }
        catch
        {
            AbortOperation(before);
            CompletePendingSuspendEnd();
            throw;
        }
    }

    /// <summary>
    ///     Completes a successful undo or redo after its entry moved: applies a deferred suspension end, reports the new
    ///     state and rethrows the first change-handler exception of the replay.
    /// </summary>
    private void FinishReplay()
    {
        var handlerFailure = EndOperation();
        CompletePendingSuspendEnd();
        OnChanged();
        handlerFailure?.Throw();
    }

    /// <summary>
    ///     Throws when an undo or redo is currently running.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown while an undo or redo is running.</exception>
    internal void EnsureNotApplying()
    {
        if (_applying)
        {
            throw new InvalidOperationException("The history cannot be changed while an undo, redo or rollback is running.");
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
    /// <remarks>
    ///     When the outermost scope ends while an undo, redo or rollback is running, the effect is deferred until that
    ///     replay has moved its entry (see <see cref="CompletePendingSuspendEnd"/>), so the stacks never change under it.
    /// </remarks>
    private void EndSuspend()
    {
        _suspendCount--;
        if (_suspendCount > 0)
        {
            return;
        }

        if (_applying)
        {
            _suspendEndPending = true;
            return;
        }

        ResumeRecording();
    }

    /// <summary>
    ///     Applies a suspension end that was deferred because it happened during an undo, redo or rollback.
    /// </summary>
    internal void CompletePendingSuspendEnd()
    {
        if (!_suspendEndPending)
        {
            return;
        }

        _suspendEndPending = false;
        ResumeRecording();
    }

    /// <summary>
    ///     Clears both stacks and restores the dirty state of the time the outermost suspension began.
    /// </summary>
    private void ResumeRecording()
    {
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
