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
}
