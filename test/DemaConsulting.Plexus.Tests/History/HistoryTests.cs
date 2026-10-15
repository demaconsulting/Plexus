namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Subsystem tests for the History subsystem, exercising the history, transactions and commands together
///     through the public API only.
/// </summary>
public class HistoryTests
{
    /// <summary>
    ///     A mutable document used as the model state in these tests.
    /// </summary>
    private sealed class Document
    {
        /// <summary>
        ///     Gets or sets the current value.
        /// </summary>
        public int Value { get; set; }
    }

    /// <summary>
    ///     A mergeable command setting the document value; consecutive sets merge into one entry.
    /// </summary>
    private sealed class SetCommand : IMergeableCommand
    {
        /// <summary>
        ///     The document being changed.
        /// </summary>
        private readonly Document _document;

        /// <summary>
        ///     The value before the change.
        /// </summary>
        private readonly int _oldValue;

        /// <summary>
        ///     The value after the change; replaced when a later command merges in.
        /// </summary>
        private int _newValue;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SetCommand"/> class.
        /// </summary>
        /// <param name="document">The document to change.</param>
        /// <param name="newValue">The value to apply.</param>
        public SetCommand(Document document, int newValue)
        {
            _document = document;
            _oldValue = document.Value;
            _newValue = newValue;
        }

        /// <inheritdoc />
        public string Description => "Set value";

        /// <inheritdoc />
        public void Do() => _document.Value = _newValue;

        /// <inheritdoc />
        public void Undo() => _document.Value = _oldValue;

        /// <inheritdoc />
        public bool TryMerge(IUndoableCommand next)
        {
            if (next is SetCommand other && ReferenceEquals(other._document, _document))
            {
                _newValue = other._newValue;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Executes a delegate command that adds an amount to the document.
    /// </summary>
    /// <param name="history">The history.</param>
    /// <param name="document">The document.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="description">The description of the entry.</param>
    private static void Add(UndoHistory history, Document document, int amount, string description) =>
        history.Execute(description, () => document.Value += amount, () => document.Value -= amount);

    /// <summary>
    ///     Validates the undo and redo workflow: ordered replay, redo invalidation and operation guards.
    /// </summary>
    [Fact]
    public void History_UndoRedo_ExecuteUndoRedoWorkflow_ReplaysInOrderAndGuardsOperations()
    {
        // Arrange: a history with three recorded changes
        var history = new UndoHistory();
        var document = new Document();
        Add(history, document, 1, "One");
        Add(history, document, 10, "Ten");
        Add(history, document, 100, "Hundred");

        // Act: undo two entries, then redo one
        history.Undo();
        history.Undo();
        var afterUndo = (document.Value, history.UndoDescription, history.RedoDescription);
        history.Redo();
        var afterRedo = (document.Value, history.UndoDescription, history.RedoDescription);

        // Assert: state and descriptions follow the stacks
        Assert.Equal((1, "One", "Ten"), afterUndo);
        Assert.Equal((11, "Ten", "Hundred"), afterRedo);

        // Act: a new change discards the redo entry
        Add(history, document, 1000, "Thousand");

        // Assert: redo is gone and the newest entry is on top
        Assert.False(history.CanRedo);
        Assert.Equal("Thousand", history.UndoDescription);

        // Act / Assert: a failing undo is reported and its entry stays on the undo stack
        history.Execute("Broken", () => { }, () => throw new InvalidOperationException("cannot undo"));
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal("Broken", history.UndoDescription);
        Assert.False(history.CanRedo);

        // Act / Assert: stack operations are refused while a transaction is open
        using (history.BeginTransaction("Open"))
        {
            Assert.Throws<InvalidOperationException>(() => history.Undo());
            Assert.Throws<InvalidOperationException>(() => history.Clear());
        }
    }

    /// <summary>
    ///     Validates a drag-like transaction that merges commands, nests and rolls back an inner step.
    /// </summary>
    [Fact]
    public void History_Transactions_DragWithNestedRollback_ProducesOneUndoEntry()
    {
        // Arrange: a document and a history
        var history = new UndoHistory();
        var document = new Document();

        // Act: an outer transaction with merging sets, an inner transaction that is rolled back, and another set
        using (history.BeginTransaction("Drag"))
        {
            for (var i = 1; i <= 5; i++)
            {
                history.Execute(new SetCommand(document, i));
            }

            using (var inner = history.BeginTransaction("Probe"))
            {
                history.Execute(new SetCommand(document, 99));
                inner.Rollback();
            }

            history.Execute(new SetCommand(document, 6));
        }

        // Assert: one entry under the outer description; the inner step left no trace
        Assert.Equal(6, document.Value);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal("Drag", history.UndoDescription);

        // Act / Assert: undo restores the original value and redo applies the final one
        history.Undo();
        Assert.Equal(0, document.Value);
        history.Redo();
        Assert.Equal(6, document.Value);
    }

    /// <summary>
    ///     Validates a full-transaction rollback that leaves neither state nor history behind.
    /// </summary>
    [Fact]
    public void History_Transactions_RolledBackTransaction_LeavesStateAndHistoryUntouched()
    {
        // Arrange: a history with one entry
        var history = new UndoHistory();
        var document = new Document();
        Add(history, document, 1, "Base");

        // Act: change inside a transaction, then roll back
        using (var transaction = history.BeginTransaction("Oops"))
        {
            Add(history, document, 5, "Five");
            Add(history, document, 7, "Seven");
            transaction.Rollback();
        }

        // Assert: model reverted, history unchanged and no transaction left open
        Assert.Equal(1, document.Value);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal("Base", history.UndoDescription);
        Assert.False(history.IsInTransaction);
    }

    /// <summary>
    ///     Validates the document loading workflow: suspended changes are unrecorded and the dirty state is restored.
    /// </summary>
    [Fact]
    public void History_Suspend_LoadWorkflow_DropsStaleEntriesAndRestoresDirtyState()
    {
        // Arrange: a dirty history with undo and redo entries
        var history = new UndoHistory();
        var document = new Document();
        Add(history, document, 1, "One");
        Add(history, document, 2, "Two");
        history.Undo();

        // Act: load content with recording suspended, using nested scopes
        using (history.Suspend())
        {
            using (history.Suspend())
            {
                Add(history, document, 50, "Loaded");
            }

            Assert.True(history.IsSuspended);
        }

        // Assert: change applied, stacks emptied, still dirty because the saved state is unknown
        Assert.Equal(51, document.Value);
        Assert.False(history.IsSuspended);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.True(history.IsDirty);

        // Act: a clean history loaded the same way stays clean
        var clean = new UndoHistory();
        using (clean.Suspend())
        {
            Add(clean, document, 1, "Loaded");
        }

        // Assert
        Assert.False(clean.IsDirty);
        Assert.False(clean.CanUndo);
    }

    /// <summary>
    ///     Validates dirty tracking across save, undo, divergence and clear.
    /// </summary>
    [Fact]
    public void History_DirtyTracking_SaveUndoDiverge_TracksSavedState()
    {
        // Arrange: a new history is clean
        var history = new UndoHistory();
        var document = new Document();
        Assert.False(history.IsDirty);

        // Act / Assert: a change makes it dirty, saving makes it clean
        Add(history, document, 1, "One");
        Add(history, document, 2, "Two");
        Assert.True(history.IsDirty);
        history.MarkClean();
        Assert.False(history.IsDirty);

        // Act / Assert: undoing leaves the saved state, redoing returns to it
        history.Undo();
        Assert.True(history.IsDirty);
        history.Redo();
        Assert.False(history.IsDirty);

        // Act / Assert: diverging after an undo makes the saved state unreachable
        history.Undo();
        Add(history, document, 20, "Different");
        Assert.Equal(2, history.UndoCount);
        Assert.True(history.IsDirty);

        // Act / Assert: clearing empties both stacks and marks the history clean
        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.False(history.IsDirty);
    }

    /// <summary>
    ///     Validates that the capacity limit drops the oldest entries and can lose the saved state.
    /// </summary>
    [Fact]
    public void History_Capacity_LimitedHistory_DropsOldestAndRejectsInvalidValues()
    {
        // Arrange: a history limited to three entries
        var history = new UndoHistory { Capacity = 3 };
        var document = new Document();

        // Act: five changes, then step back as far as possible
        for (var i = 1; i <= 5; i++)
        {
            Add(history, document, i, $"Step {i}");
        }

        var count = history.UndoCount;
        while (history.Undo())
        {
            // Keep stepping until the stack is exhausted
        }

        // Assert: only the newest three entries were reversible and the lost clean state keeps it dirty
        Assert.Equal(3, count);
        Assert.Equal(3, document.Value);
        Assert.True(history.IsDirty);

        // Act: redo everything, then shrink the capacity
        while (history.Redo())
        {
            // Keep stepping until the stack is exhausted
        }

        history.Capacity = 1;

        // Assert: shrinking applies immediately and invalid capacities are rejected
        Assert.Equal(1, history.UndoCount);
        Assert.Equal("Step 5", history.UndoDescription);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Capacity = 0);
    }

    /// <summary>
    ///     Validates that listeners are notified once per completed state change across the whole workflow.
    /// </summary>
    [Fact]
    public void History_StateNotification_Workflow_RaisesChangedOncePerStateChange()
    {
        // Arrange: a history counting Changed notifications
        var history = new UndoHistory();
        var document = new Document();
        var raised = 0;
        history.Changed += (_, _) => raised++;

        // Act / Assert: execute, undo, redo and save each notify once
        Add(history, document, 1, "One");
        Assert.Equal(1, raised);
        history.Undo();
        Assert.Equal(2, raised);
        history.Redo();
        Assert.Equal(3, raised);
        history.MarkClean();
        Assert.Equal(4, raised);

        // Act / Assert: a transaction notifies once on commit, not per command
        using (history.BeginTransaction("Group"))
        {
            Add(history, document, 1, "A");
            Add(history, document, 1, "B");
            Assert.Equal(4, raised);
        }

        Assert.Equal(5, raised);

        // Act / Assert: ending a suspension notifies
        using (history.Suspend())
        {
            Add(history, document, 1, "Loaded");
        }

        Assert.Equal(6, raised);

        // Act / Assert: two more entries notify, and shrinking the capacity below their count notifies again
        Add(history, document, 1, "Another");
        Add(history, document, 1, "Last");
        Assert.Equal(8, raised);
        history.Capacity = 1;
        Assert.Equal(9, raised);
    }

    /// <summary>
    ///     Validates that host command code which tries to change the history while its entry is undone or redone cannot
    ///     corrupt the stacks.
    /// </summary>
    [Fact]
    public void History_ReplayIntegrity_CommandChangesHistoryDuringUndo_IsRejected()
    {
        // Arrange: a base entry, then a host command whose undo and redo try to clear the history and change its state
        var history = new UndoHistory();
        var document = new Document();
        Add(history, document, 1, "Base");
        var armed = false;
        var refused = 0;
        void Meddle()
        {
            if (!armed)
            {
                return;
            }

            foreach (var attempt in new Action[] { history.Clear, history.MarkClean, () => history.Capacity = 1, () => history.Suspend() })
            {
                try
                {
                    attempt();
                }
                catch (InvalidOperationException)
                {
                    refused++;
                }
            }
        }

        history.Execute("Meddle", () => { document.Value += 10; Meddle(); }, () => { document.Value -= 10; Meddle(); });
        armed = true;

        // Act: undo and redo the meddling entry
        history.Undo();
        var afterUndo = (document.Value, history.UndoCount, history.RedoCount);
        history.Redo();

        // Assert: every attempt was refused both times; the stacks moved exactly as for a plain undo and redo
        Assert.Equal(8, refused);
        Assert.Equal((1, 1, 1), afterUndo);
        Assert.Equal(11, document.Value);
        Assert.Equal(2, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal(1000, history.Capacity);
        Assert.False(history.IsSuspended);
    }

    /// <summary>
    ///     Validates that a grouped entry whose replay fails part-way leaves the state and the stacks as before the attempt.
    /// </summary>
    [Fact]
    public void History_ReplayAtomic_FailingCommandInGroupedEntry_LeavesStateAndStacksUnchanged()
    {
        // Arrange: one transaction of three host commands; the second can be told to fail
        var history = new UndoHistory();
        var document = new Document();
        var fail = false;
        history.Transact("Edit", () =>
        {
            Add(history, document, 1, "One");
            history.Execute(
                "Ten",
                () => document.Value = fail ? throw new InvalidOperationException("redo failed") : document.Value + 10,
                () => document.Value = fail ? throw new InvalidOperationException("undo failed") : document.Value - 10);
            Add(history, document, 100, "Hundred");
        });
        fail = true;

        // Act / Assert: the failing undo leaves the state and the entry where they were
        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Equal(111, document.Value);
        Assert.Equal((1, 0), (history.UndoCount, history.RedoCount));

        // Act / Assert: once it works the entry undoes; a failing redo then also leaves everything where it was
        fail = false;
        Assert.True(history.Undo());
        fail = true;
        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.Equal(0, document.Value);
        Assert.Equal((0, 1), (history.UndoCount, history.RedoCount));

        // Act / Assert: and the retry replays the whole entry
        fail = false;
        Assert.True(history.Redo());
        Assert.Equal(111, document.Value);
    }
}
