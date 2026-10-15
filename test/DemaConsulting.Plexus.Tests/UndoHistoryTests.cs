namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for <see cref="UndoHistory"/>, <see cref="HistoryTransaction"/> and the command interfaces.
/// </summary>
public class UndoHistoryTests
{
    /// <summary>
    ///     A mutable integer used as the model state in these tests.
    /// </summary>
    private sealed class Counter
    {
        /// <summary>
        ///     Gets or sets the current value.
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    ///     A mergeable command adding an amount to a counter; consecutive additions merge into one entry.
    /// </summary>
    private sealed class AddCommand : IMergeableCommand
    {
        /// <summary>
        ///     The counter being changed.
        /// </summary>
        private readonly Counter _counter;

        /// <summary>
        ///     The amount added by this (possibly merged) command.
        /// </summary>
        private int _amount;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AddCommand"/> class.
        /// </summary>
        /// <param name="counter">The counter to change.</param>
        /// <param name="amount">The amount to add.</param>
        public AddCommand(Counter counter, int amount)
        {
            _counter = counter;
            _amount = amount;
        }

        /// <inheritdoc />
        public string Description => "Add";

        /// <inheritdoc />
        public void Do() => _counter.Value += _amount;

        /// <inheritdoc />
        public void Undo() => _counter.Value -= _amount;

        /// <inheritdoc />
        public bool TryMerge(IUndoableCommand next)
        {
            if (next is AddCommand other && ReferenceEquals(other._counter, _counter))
            {
                _amount += other._amount;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Executes a lambda command that increments the counter by one.
    /// </summary>
    /// <param name="history">The history.</param>
    /// <param name="counter">The counter.</param>
    /// <param name="description">The description.</param>
    private static void Increment(UndoHistory history, Counter counter, string description = "Increment") =>
        history.Execute(description, () => counter.Value++, () => counter.Value--);

    /// <summary>
    ///     Validates that Execute applies the change and records an undoable entry.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_LambdaCommand_AppliesAndRecords()
    {
        // Arrange: an empty history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: execute an increment
        Increment(history, counter);

        // Assert: change applied, entry recorded with its description
        Assert.Equal(1, counter.Value);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Increment", history.UndoDescription);
        Assert.Null(history.RedoDescription);
    }

    /// <summary>
    ///     Validates that undo and redo move entries between the stacks and replay the change.
    /// </summary>
    [Fact]
    public void UndoHistory_UndoRedo_RoundTripsState()
    {
        // Arrange: two executed commands
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter, "First");
        Increment(history, counter, "Second");

        // Act / Assert: undo twice, then redo once, checking state and descriptions
        Assert.True(history.Undo());
        Assert.Equal(1, counter.Value);
        Assert.Equal("Second", history.RedoDescription);
        Assert.True(history.Undo());
        Assert.Equal(0, counter.Value);
        Assert.False(history.CanUndo);
        Assert.True(history.Redo());
        Assert.Equal(1, counter.Value);
        Assert.Equal("First", history.UndoDescription);
    }

    /// <summary>
    ///     Validates that undo and redo on empty stacks report false instead of throwing.
    /// </summary>
    [Fact]
    public void UndoHistory_UndoRedo_EmptyStacks_ReturnFalse()
    {
        // Arrange: a fresh history
        var history = new UndoHistory();

        // Act / Assert: nothing to undo or redo
        Assert.False(history.Undo());
        Assert.False(history.Redo());
    }

    /// <summary>
    ///     Validates that executing a new command discards the redo stack.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_AfterUndo_ClearsRedo()
    {
        // Arrange: one command, undone
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        history.Undo();
        Assert.True(history.CanRedo);

        // Act: execute something new
        Increment(history, counter);

        // Assert: redo is gone
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.RedoCount);
    }

    /// <summary>
    ///     Validates that a command whose Do throws is not recorded.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_DoThrows_NothingRecorded()
    {
        // Arrange: a history and a failing command
        var history = new UndoHistory();

        // Act / Assert: the exception propagates and history stays empty
        Assert.Throws<InvalidOperationException>(() =>
            history.Execute("Fail", () => throw new InvalidOperationException("boom"), () => { }));
        Assert.False(history.CanUndo);
    }

    /// <summary>
    ///     Validates argument validation of the Execute overloads.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: a history
        var history = new UndoHistory();

        // Act / Assert: every null argument is rejected
        Assert.Throws<ArgumentNullException>(() => history.Execute(null!));
        Assert.Throws<ArgumentNullException>(() => history.Execute(null!, () => { }, () => { }));
        Assert.Throws<ArgumentNullException>(() => history.Execute("x", null!, () => { }));
        Assert.Throws<ArgumentNullException>(() => history.Execute("x", () => { }, null!));
    }

    /// <summary>
    ///     Validates that a transaction groups several commands into one entry that undoes together.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_Commit_CreatesSingleEntry()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: three increments inside one transaction
        using (history.BeginTransaction("Triple"))
        {
            Increment(history, counter);
            Increment(history, counter);
            Increment(history, counter);
        }

        // Assert: one entry with the transaction description; undo reverts all three
        Assert.Equal(1, history.UndoCount);
        Assert.Equal("Triple", history.UndoDescription);
        history.Undo();
        Assert.Equal(0, counter.Value);
        history.Redo();
        Assert.Equal(3, counter.Value);
    }

    /// <summary>
    ///     Validates that an empty transaction records nothing and does not make the history dirty.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_Empty_RecordsNothing()
    {
        // Arrange: a clean history
        var history = new UndoHistory();

        // Act: open and close a transaction without commands
        using (history.BeginTransaction("Nothing"))
        {
            // Intentionally no commands
        }

        // Assert: no entry, still clean
        Assert.False(history.CanUndo);
        Assert.False(history.IsDirty);
    }

    /// <summary>
    ///     Validates that nested transactions flatten into the outermost entry.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_Nested_FlattensIntoOuterEntry()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: an inner transaction inside an outer one
        using (history.BeginTransaction("Outer"))
        {
            Increment(history, counter);
            using (history.BeginTransaction("Inner"))
            {
                Increment(history, counter);
            }

            Increment(history, counter);
        }

        // Assert: a single entry named after the outer transaction
        Assert.Equal(1, history.UndoCount);
        Assert.Equal("Outer", history.UndoDescription);
        history.Undo();
        Assert.Equal(0, counter.Value);
    }

    /// <summary>
    ///     Validates that rolling back reverts the transaction's commands and records nothing.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_Rollback_RevertsAndRecordsNothing()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: roll back a transaction after two increments
        using (var transaction = history.BeginTransaction("Oops"))
        {
            Increment(history, counter);
            Increment(history, counter);
            transaction.Rollback();
        }

        // Assert: state reverted and nothing recorded
        Assert.Equal(0, counter.Value);
        Assert.False(history.CanUndo);
    }

    /// <summary>
    ///     Validates that rolling back an inner transaction reverts only the inner commands.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_InnerRollback_KeepsOuterCommands()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: outer increment, inner increment rolled back, outer increment
        using (history.BeginTransaction("Outer"))
        {
            Increment(history, counter);
            using (var inner = history.BeginTransaction("Inner"))
            {
                Increment(history, counter);
                inner.Rollback();
            }

            Increment(history, counter);
        }

        // Assert: two increments survive and form one entry
        Assert.Equal(2, counter.Value);
        Assert.Equal(1, history.UndoCount);
        history.Undo();
        Assert.Equal(0, counter.Value);
    }

    /// <summary>
    ///     Validates that completing transactions out of order is rejected.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_DisposedOutOfOrder_ThrowsInvalidOperationException()
    {
        // Arrange: two open transactions
        var history = new UndoHistory();
        var outer = history.BeginTransaction("Outer");
        var inner = history.BeginTransaction("Inner");

        // Act / Assert: the outer cannot complete before the inner
        Assert.Throws<InvalidOperationException>(() => outer.Dispose());
        inner.Dispose();
        outer.Dispose();
        Assert.False(history.IsInTransaction);
    }

    /// <summary>
    ///     Validates that rolling back twice is rejected while disposing twice is harmless.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_CompletedTwice_RollbackThrowsDisposeIgnored()
    {
        // Arrange: a transaction that was committed
        var history = new UndoHistory();
        var transaction = history.BeginTransaction("T");
        transaction.Dispose();

        // Act / Assert: second dispose is a no-op, rollback after completion is an error
        transaction.Dispose();
        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
    }

    /// <summary>
    ///     Validates that undo and redo are unavailable while a transaction is open.
    /// </summary>
    [Fact]
    public void UndoHistory_UndoRedoClear_InsideTransaction_ThrowInvalidOperationException()
    {
        // Arrange: a history with an entry and an open transaction
        var history = new UndoHistory();
        Increment(history, new Counter());
        using var transaction = history.BeginTransaction("Open");

        // Act / Assert: all stack-changing operations are rejected
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.Throws<InvalidOperationException>(() => history.Clear());
    }

    /// <summary>
    ///     Validates that consecutive mergeable commands inside a transaction compact into one command.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_MergeableCommands_MergeIntoOne()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: ten additions inside one transaction
        using (history.BeginTransaction("Drag"))
        {
            for (var i = 0; i < 10; i++)
            {
                history.Execute(new AddCommand(counter, 2));
            }
        }

        // Assert: one entry; undo and redo apply the combined amount exactly
        Assert.Equal(20, counter.Value);
        Assert.Equal(1, history.UndoCount);
        history.Undo();
        Assert.Equal(0, counter.Value);
        history.Redo();
        Assert.Equal(20, counter.Value);
    }

    /// <summary>
    ///     Validates that commands outside a transaction are never merged.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_MergeableOutsideTransaction_DoesNotMerge()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: two additions without a transaction
        history.Execute(new AddCommand(counter, 1));
        history.Execute(new AddCommand(counter, 1));

        // Assert: two separate entries
        Assert.Equal(2, history.UndoCount);
    }

    /// <summary>
    ///     Validates that a rolled-back inner transaction never reaches into commands merged before it began.
    /// </summary>
    [Fact]
    public void UndoHistory_Transaction_InnerRollback_DoesNotMergeIntoOuterCommand()
    {
        // Arrange: an outer transaction with one mergeable command
        var history = new UndoHistory();
        var counter = new Counter();
        using (history.BeginTransaction("Outer"))
        {
            history.Execute(new AddCommand(counter, 5));

            // Act: an inner transaction adds and rolls back a mergeable command
            using (var inner = history.BeginTransaction("Inner"))
            {
                history.Execute(new AddCommand(counter, 7));
                inner.Rollback();
            }
        }

        // Assert: only the outer command's effect remains
        Assert.Equal(5, counter.Value);
    }

    /// <summary>
    ///     Validates that suspended changes are applied but not recorded.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_ChangesAppliedButNotRecorded()
    {
        // Arrange: a history and a counter
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: execute inside and outside a suspension
        using (history.Suspend())
        {
            Assert.True(history.IsSuspended);
            Increment(history, counter);
        }

        Increment(history, counter);

        // Assert: both applied, only the second recorded
        Assert.False(history.IsSuspended);
        Assert.Equal(2, counter.Value);
        Assert.Equal(1, history.UndoCount);
    }

    /// <summary>
    ///     Validates that nested suspensions resume only when all scopes are disposed and double disposal is harmless.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_Nested_ResumesAfterLastScope()
    {
        // Arrange: two nested suspensions
        var history = new UndoHistory();
        var outer = history.Suspend();
        var inner = history.Suspend();

        // Act / Assert: still suspended until both are disposed; double disposal does not underflow
        inner.Dispose();
        inner.Dispose();
        Assert.True(history.IsSuspended);
        outer.Dispose();
        Assert.False(history.IsSuspended);
    }

    /// <summary>
    ///     Validates that a clean history stays clean after a suspension that changed the model.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_CleanHistory_StaysClean()
    {
        // Arrange: a clean history
        var history = new UndoHistory();
        var counter = new Counter();

        // Act: an unrecorded change (loading a document)
        using (history.Suspend())
        {
            Increment(history, counter);
        }

        // Assert: applied, nothing recorded, and still clean
        Assert.Multiple(
            () => Assert.Equal(1, counter.Value),
            () => Assert.False(history.IsDirty),
            () => Assert.False(history.CanUndo),
            () => Assert.False(history.CanRedo));
    }

    /// <summary>
    ///     Validates that a dirty history stays dirty after a suspension, while the stale undo and redo entries are dropped.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_DirtyHistory_StaysDirtyAndClearsStacks()
    {
        // Arrange: a history with one undo entry and one redo entry after a clean point
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        history.MarkClean();
        Increment(history, counter);
        Increment(history, counter);
        history.Undo();
        Assert.True(history.IsDirty);
        Assert.True(history.CanRedo);

        // Act
        using (history.Suspend())
        {
            Increment(history, counter);
        }

        // Assert: nothing to undo or redo any more, but the document is still unsaved
        Assert.Multiple(
            () => Assert.False(history.CanUndo),
            () => Assert.False(history.CanRedo),
            () => Assert.True(history.IsDirty));

        // And MarkClean makes it clean as usual
        history.MarkClean();
        Assert.False(history.IsDirty);
    }

    /// <summary>
    ///     Validates that only the outermost of nested suspensions clears the history and restores the dirty state.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_Nested_OnlyOutermostScopeClearsHistory()
    {
        // Arrange: a clean history with one recorded entry that is then marked clean
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        history.MarkClean();
        var outer = history.Suspend();
        var inner = history.Suspend();

        // Act: end the inner scope first
        Increment(history, counter);
        inner.Dispose();
        var undoAfterInner = history.UndoCount;
        outer.Dispose();

        // Assert: the stack survives the inner scope, goes with the outer one, and the clean state is restored
        Assert.Multiple(
            () => Assert.Equal(1, undoAfterInner),
            () => Assert.Equal(0, history.UndoCount),
            () => Assert.False(history.IsDirty));
    }

    /// <summary>
    ///     Validates that ending a suspension notifies listeners so UI such as an undo button refreshes.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_Disposed_RaisesChanged()
    {
        // Arrange
        var history = new UndoHistory();
        var raised = 0;
        history.Changed += (_, _) => raised++;
        var scope = history.Suspend();

        // Act
        scope.Dispose();
        scope.Dispose();

        // Assert: once, even though the scope was disposed twice
        Assert.Equal(1, raised);
    }

    /// <summary>
    ///     Validates that building a diagram under Suspend leaves it clean without any MarkClean or Clear call.
    /// </summary>
    [Fact]
    public void UndoHistory_Suspend_BuildingDiagram_LeavesDiagramCleanAndUndoEmpty()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();

        // Act: load-style construction
        using (diagram.History.Suspend())
        {
            var page = diagram.Pages.Add("Main");
            page.AddNode("acme.Producer", 0, 0, "P");
        }

        // Assert
        Assert.Multiple(
            () => Assert.False(diagram.History.IsDirty),
            () => Assert.False(diagram.History.CanUndo),
            () => Assert.Single(diagram.Pages));
    }

    /// <summary>
    ///     Validates that exceeding the capacity drops the oldest entries.
    /// </summary>
    [Fact]
    public void UndoHistory_Capacity_Exceeded_DropsOldestEntries()
    {
        // Arrange: a history limited to three entries
        var history = new UndoHistory { Capacity = 3 };
        var counter = new Counter();

        // Act: execute five commands
        for (var i = 1; i <= 5; i++)
        {
            Increment(history, counter, $"Step {i}");
        }

        // Assert: only the newest three can be undone
        Assert.Equal(3, history.UndoCount);
        while (history.Undo())
        {
            // Keep stepping until the stack is exhausted
        }

        Assert.Equal(2, counter.Value);
    }

    /// <summary>
    ///     Validates that lowering the capacity trims immediately and that invalid capacities are rejected.
    /// </summary>
    [Fact]
    public void UndoHistory_Capacity_Lowered_TrimsAndValidates()
    {
        // Arrange: a history with four entries
        var history = new UndoHistory();
        var counter = new Counter();
        for (var i = 0; i < 4; i++)
        {
            Increment(history, counter);
        }

        // Act: lower the capacity
        history.Capacity = 2;

        // Assert: trimmed, default is 1000, zero is rejected
        Assert.Equal(2, history.UndoCount);
        Assert.Equal(1000, new UndoHistory().Capacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Capacity = 0);
    }

    /// <summary>
    ///     Validates dirty tracking across execute, undo, redo and MarkClean.
    /// </summary>
    [Fact]
    public void UndoHistory_IsDirty_TracksCleanPointThroughUndoRedo()
    {
        // Arrange: a history with one entry marked clean
        var history = new UndoHistory();
        var counter = new Counter();
        Assert.False(history.IsDirty);
        Increment(history, counter);
        Assert.True(history.IsDirty);
        history.MarkClean();
        Assert.False(history.IsDirty);

        // Act / Assert: undoing leaves the clean point; redoing returns to it
        history.Undo();
        Assert.True(history.IsDirty);
        history.Redo();
        Assert.False(history.IsDirty);
    }

    /// <summary>
    ///     Validates that a new command after undoing past the clean point makes the saved state unreachable.
    /// </summary>
    [Fact]
    public void UndoHistory_IsDirty_NewCommandAfterUndoPastCleanPoint_StaysDirty()
    {
        // Arrange: save at depth 2, undo to depth 1, then execute a different command (depth 2 again)
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        Increment(history, counter);
        history.MarkClean();
        history.Undo();

        // Act: diverge from the saved state
        Increment(history, counter, "Different");

        // Assert: same depth as the save point but different content, so still dirty
        Assert.Equal(2, history.UndoCount);
        Assert.True(history.IsDirty);
    }

    /// <summary>
    ///     Validates that trimming the entry the clean point refers to leaves the history permanently dirty.
    /// </summary>
    [Fact]
    public void UndoHistory_IsDirty_CleanPointTrimmedByCapacity_StaysDirty()
    {
        // Arrange: a history of capacity 2, clean at the start
        var history = new UndoHistory { Capacity = 2 };
        var counter = new Counter();

        // Act: three commands push the original clean state out of the stack
        Increment(history, counter);
        Increment(history, counter);
        Increment(history, counter);
        while (history.Undo())
        {
            // Keep stepping until the stack is exhausted
        }

        // Assert: fully undone yet dirty, because the saved state can no longer be reached
        Assert.True(history.IsDirty);
    }

    /// <summary>
    ///     Validates that Clear empties both stacks and marks the history clean.
    /// </summary>
    [Fact]
    public void UndoHistory_Clear_EmptiesStacksAndMarksClean()
    {
        // Arrange: a history with undo and redo entries
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        Increment(history, counter);
        history.Undo();

        // Act
        history.Clear();

        // Assert
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.IsDirty);
    }

    /// <summary>
    ///     Validates that the Changed event fires for execute, undo, redo, clear and MarkClean.
    /// </summary>
    [Fact]
    public void UndoHistory_Changed_RaisedForStateChanges()
    {
        // Arrange: a history counting Changed notifications
        var history = new UndoHistory();
        var counter = new Counter();
        var raised = 0;
        history.Changed += (_, _) => raised++;

        // Act: five state-changing operations
        Increment(history, counter);
        history.Undo();
        history.Redo();
        history.MarkClean();
        history.Clear();

        // Assert
        Assert.Equal(5, raised);
    }

    /// <summary>
    ///     Validates that recording from inside an undo is rejected to protect history integrity.
    /// </summary>
    [Fact]
    public void UndoHistory_Execute_DuringUndo_ThrowsInvalidOperationException()
    {
        // Arrange: a command whose undo tries to record another command
        var history = new UndoHistory();
        history.Execute("Reentrant", () => { }, () => history.Execute("Inner", () => { }, () => { }));

        // Act / Assert: the nested execute is rejected
        Assert.Throws<InvalidOperationException>(() => history.Undo());
    }

    /// <summary>
    ///     Validates that lowering the capacity below the entry count notifies listeners, and that no trim means no event.
    /// </summary>
    [Fact]
    public void UndoHistory_Capacity_Lowered_RaisesChanged()
    {
        // Arrange: two entries and a change counter
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        Increment(history, counter);
        var raised = 0;
        history.Changed += (_, _) => raised++;

        // Act: a capacity that trims nothing, then one that does
        history.Capacity = 5;
        var afterNoTrim = raised;
        history.Capacity = 1;

        // Assert
        Assert.Equal(0, afterNoTrim);
        Assert.Equal(1, raised);
    }

    /// <summary>
    ///     Validates that an entry whose undo throws stays on the undo stack and the history remains usable.
    /// </summary>
    [Fact]
    public void UndoHistory_Undo_UndoThrows_EntryStaysOnUndoStack()
    {
        // Arrange: an entry whose undo fails
        var history = new UndoHistory();
        history.Execute("Broken", () => { }, () => throw new InvalidOperationException("boom"));

        // Act / Assert: the failure propagates, the entry is not moved, and recording still works afterwards
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        history.Execute("Next", () => { }, () => { });
        Assert.Equal(2, history.UndoCount);
    }

    /// <summary>
    ///     Validates that aborting a command plan reverts every applied step newest first and empties the plan.
    /// </summary>
    [Fact]
    public void CommandPlan_Abort_RevertsEveryStepNewestFirstAndClears()
    {
        // Arrange: three applied steps that log their reverts
        var reverted = new List<string>();
        var plan = new CommandPlan();
        foreach (var name in new[] { "a", "b", "c" })
        {
            plan.Run(name, () => { }, () => reverted.Add(name));
        }

        // Act: abort because of a (simulated) later failure
        plan.Abort(new InvalidOperationException("later step failed"));

        // Assert: every step reverted, newest first, and nothing left to record
        Assert.Equal(["c", "b", "a"], reverted);
        Assert.True(plan.IsEmpty);
        Assert.Equal(0, plan.Count);
    }

    /// <summary>
    ///     Validates that a failing revert does not stop the abort and that every failure is reported with the original error.
    /// </summary>
    [Fact]
    public void CommandPlan_Abort_InverseThrows_RevertsRemainingStepsAndReportsAllFailures()
    {
        // Arrange: three steps; the middle one cannot be reverted
        var reverted = new List<string>();
        var revertFailure = new InvalidOperationException("cannot revert b");
        var plan = new CommandPlan();
        plan.Run("a", () => { }, () => reverted.Add("a"));
        plan.Run("b", () => { }, () => throw revertFailure);
        plan.Run("c", () => { }, () => reverted.Add("c"));
        var cause = new ArgumentException("the operation failed");

        // Act
        var ex = Assert.Throws<AggregateException>(() => plan.Abort(cause));

        // Assert: the other steps were still reverted; the original error comes first, then the revert failure; plan empty
        Assert.Equal(["c", "a"], reverted);
        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Same(cause, ex.InnerExceptions[0]);
        Assert.Same(revertFailure, ex.InnerExceptions[1]);
        Assert.True(plan.IsEmpty);
    }

    /// <summary>
    ///     Validates that clearing the history from a command's undo code is rejected and leaves the stacks consistent.
    /// </summary>
    [Fact]
    public void UndoHistory_Clear_FromUndoCallback_ThrowsAndKeepsStacks()
    {
        // Arrange: an increment, then an entry whose undo tries to clear the history
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        InvalidOperationException? rejected = null;
        history.Execute(
            "Clear on undo",
            () => { },
            () =>
            {
                try
                {
                    history.Clear();
                }
                catch (InvalidOperationException ex)
                {
                    rejected = ex;
                }
            });

        // Act
        Assert.True(history.Undo());

        // Assert: the clear was refused; the undo moved its entry normally and the earlier entry is still undoable
        Assert.NotNull(rejected);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(1, history.RedoCount);
        Assert.True(history.Undo());
        Assert.Equal(0, counter.Value);
    }

    /// <summary>
    ///     Validates that clearing the history from a command's redo code is rejected and leaves the stacks consistent.
    /// </summary>
    [Fact]
    public void UndoHistory_Clear_FromRedoCallback_ThrowsAndKeepsStacks()
    {
        // Arrange: an increment, then an entry whose redo (armed after it was executed) tries to clear the history
        var history = new UndoHistory();
        var counter = new Counter();
        Increment(history, counter);
        var armed = false;
        InvalidOperationException? rejected = null;
        history.Execute(
            "Clear on redo",
            () =>
            {
                if (!armed)
                {
                    return;
                }

                try
                {
                    history.Clear();
                }
                catch (InvalidOperationException ex)
                {
                    rejected = ex;
                }
            },
            () => { });
        Assert.True(history.Undo());
        armed = true;

        // Act
        Assert.True(history.Redo());

        // Assert: the clear was refused; the redo moved its entry normally and both entries can still be undone
        Assert.NotNull(rejected);
        Assert.Equal(2, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.True(history.Undo());
        Assert.True(history.Undo());
        Assert.Equal(0, counter.Value);
    }

    /// <summary>
    ///     Validates that marking clean, changing the capacity and suspending from a command's undo code are rejected.
    /// </summary>
    [Fact]
    public void UndoHistory_MarkCleanCapacityAndSuspend_FromUndoCallback_AreRejected()
    {
        // Arrange: an entry whose undo tries the three state changes and remembers what each one did
        var history = new UndoHistory();
        var outcomes = new List<Type?>();
        void Try(Action action)
        {
            try
            {
                action();
                outcomes.Add(null);
            }
            catch (Exception ex)
            {
                outcomes.Add(ex.GetType());
            }
        }

        history.Execute(
            "Change history on undo",
            () => { },
            () =>
            {
                Try(history.MarkClean);
                Try(() => history.Capacity = 5);
                Try(() => history.Suspend());
            });
        var capacity = history.Capacity;

        // Act
        Assert.True(history.Undo());

        // Assert: all three refused; capacity, suspension and dirty state are those of a plain undo to the clean point
        Assert.Equal([typeof(InvalidOperationException), typeof(InvalidOperationException), typeof(InvalidOperationException)], outcomes);
        Assert.Equal(capacity, history.Capacity);
        Assert.False(history.IsSuspended);
        Assert.False(history.IsDirty);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(1, history.RedoCount);
    }

    /// <summary>
    ///     Validates that ending the outermost suspension from a command's undo code takes effect only after the undo.
    /// </summary>
    [Fact]
    public void UndoHistory_SuspendScopeDisposedFromUndoCallback_TakesEffectAfterUndo()
    {
        // Arrange: an entry whose undo ends a suspension that is opened afterwards
        var history = new UndoHistory();
        IDisposable? scope = null;
        var undoCountInCallback = -1;
        history.Execute(
            "End suspension on undo",
            () => { },
            () =>
            {
                scope!.Dispose();
                undoCountInCallback = history.UndoCount;
            });
        scope = history.Suspend();

        // Act
        var undone = history.Undo();

        // Assert: no failure; the stacks were untouched during the undo and cleared right after it
        Assert.True(undone);
        Assert.Equal(1, undoCountInCallback);
        Assert.False(history.IsSuspended);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    /// <summary>
    ///     Creates a history holding one grouped entry of three commands adding 1, 10 and 100 to a counter, whose middle
    ///     command fails while <paramref name="failMiddle"/> says so.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="failMiddle">Returns whether the middle command's do or undo should throw.</param>
    /// <param name="failLastDo">Returns whether the last command's do should throw.</param>
    /// <returns>The history.</returns>
    private static UndoHistory CreateGroupedEntry(Counter counter, Func<bool> failMiddle, Func<bool> failLastDo)
    {
        var history = new UndoHistory();
        history.Transact("Group", () =>
        {
            history.Execute("Add 1", () => counter.Value += 1, () => counter.Value -= 1);
            history.Execute(
                "Add 10",
                () => counter.Value = failMiddle() ? throw new InvalidOperationException("middle do failed") : counter.Value + 10,
                () => counter.Value = failMiddle() ? throw new InvalidOperationException("middle undo failed") : counter.Value - 10);
            history.Execute(
                "Add 100",
                () => counter.Value = failLastDo() ? throw new ArgumentException("last do failed") : counter.Value + 100,
                () => counter.Value -= 100);
        });
        return history;
    }

    /// <summary>
    ///     Validates that a failing command in a grouped entry's redo restores the other commands and keeps the entry.
    /// </summary>
    [Fact]
    public void UndoHistory_Redo_MiddleCommandFails_RestoresStateAndKeepsEntry()
    {
        // Arrange: the grouped entry, undone; its middle command will fail on redo
        var counter = new Counter();
        var fail = false;
        var history = CreateGroupedEntry(counter, () => fail, () => false);
        Assert.True(history.Undo());
        fail = true;

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => history.Redo());

        // Assert: the first command was re-reverted, the entry is still on the redo stack, and a retry works
        Assert.Equal("middle do failed", ex.Message);
        Assert.Equal(0, counter.Value);
        Assert.Equal(1, history.RedoCount);
        Assert.Equal(0, history.UndoCount);
        fail = false;
        Assert.True(history.Redo());
        Assert.Equal(111, counter.Value);
    }

    /// <summary>
    ///     Validates that a failing command in a grouped entry's undo restores the other commands and keeps the entry.
    /// </summary>
    [Fact]
    public void UndoHistory_Undo_MiddleCommandFails_RestoresStateAndKeepsEntry()
    {
        // Arrange: the grouped entry; its middle command will fail on undo
        var counter = new Counter();
        var fail = false;
        var history = CreateGroupedEntry(counter, () => fail, () => false);
        fail = true;

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => history.Undo());

        // Assert: the last command was re-applied, the entry is still on the undo stack, and a retry works
        Assert.Equal("middle undo failed", ex.Message);
        Assert.Equal(111, counter.Value);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        fail = false;
        Assert.True(history.Undo());
        Assert.Equal(0, counter.Value);
    }

    /// <summary>
    ///     Validates that when restoring a failed grouped undo also fails, both failures are reported together.
    /// </summary>
    [Fact]
    public void UndoHistory_Undo_MiddleCommandAndCompensationFail_ReportsAllFailures()
    {
        // Arrange: the middle command fails on undo and the last command cannot be re-applied
        var counter = new Counter();
        var fail = false;
        var history = CreateGroupedEntry(counter, () => fail, () => fail);
        fail = true;

        // Act
        var ex = Assert.Throws<AggregateException>(() => history.Undo());

        // Assert: the original failure first, then the compensation failure; the entry stays on the undo stack
        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.Equal("middle undo failed", ex.InnerExceptions[0].Message);
        Assert.IsType<ArgumentException>(ex.InnerExceptions[1]);
        Assert.Equal(1, history.UndoCount);
    }
}
