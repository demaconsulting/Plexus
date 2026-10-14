namespace DemaConsulting.Plexus.Tests.History;

/// <summary>
///     Unit tests for <see cref="UndoHistory.Transact(string, Action)"/> and <see cref="UndoHistory.Transact{T}(string, Func{T})"/>.
/// </summary>
public class UndoHistoryTransactTests
{
    /// <summary>
    ///     Validates that a successful action becomes one undo entry with the given description.
    /// </summary>
    [Fact]
    public void UndoHistory_Transact_Success_RecordsOneEntry()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        var before = diagram.History.UndoCount;

        // Act
        diagram.History.Transact("Edit node", () =>
        {
            node.Properties["Gain"] = 3.0;
            node.Properties["Count"] = 9;
            node.MoveTo(50, 60);
        });

        // Assert
        Assert.Equal(before + 1, diagram.History.UndoCount);
        Assert.Equal("Edit node", diagram.History.UndoDescription);
        Assert.False(diagram.History.IsInTransaction);
        diagram.History.Undo();
        Assert.Equal(1.0, node.Properties.Get<double>("Gain"));
        Assert.Equal(1, node.Properties.Get<int>("Count"));
        Assert.Equal(0, node.Position.X);
    }

    /// <summary>
    ///     Validates that an exception rolls back everything the action did, is rethrown unchanged and leaves the history as it was.
    /// </summary>
    [Fact]
    public void UndoHistory_Transact_Throws_RollsBackAndRethrows()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        diagram.History.MarkClean();
        var before = TestModel.Dump(diagram);
        var undoCount = diagram.History.UndoCount;

        // Act
        var thrown = Assert.Throws<ArgumentException>(() => diagram.History.Transact("Edit node", () =>
        {
            node.Properties["Gain"] = 3.0;
            node.Properties["Mode"] = "not a choice";
        }));

        // Assert
        Assert.Contains("allowed choices", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(before, TestModel.Dump(diagram));
        Assert.Equal(undoCount, diagram.History.UndoCount);
        Assert.False(diagram.History.IsInTransaction);
        Assert.False(diagram.History.IsDirty);
        Assert.False(diagram.History.CanRedo);
    }

    /// <summary>
    ///     Validates that the function overload returns the result and rolls back on failure too.
    /// </summary>
    [Fact]
    public void UndoHistory_TransactOfT_ReturnsResultAndRollsBackOnFailure()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");

        // Act
        var count = diagram.History.Transact("Add two", () =>
        {
            page.AddNode("acme.Producer", 0, 0);
            page.AddNode("acme.Consumer", 200, 0);
            return page.Nodes.Count;
        });
        var undoCount = diagram.History.UndoCount;

        // Assert
        Assert.Equal(2, count);
        Assert.Throws<InvalidOperationException>(() => diagram.History.Transact<int>("Fail", () =>
        {
            page.AddNode("acme.Gain", 400, 0);
            throw new InvalidOperationException("boom");
        }));
        Assert.Equal(2, page.Nodes.Count);
        Assert.Equal(undoCount, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that nested calls flatten into the outer entry and that only the failing inner part is rolled back when the outer catches it.
    /// </summary>
    [Fact]
    public void UndoHistory_Transact_Nested_FlattensIntoOuterEntry()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();
        var node = diagram.Pages.Add("Main").AddNode("acme.Producer", 0, 0);
        var before = diagram.History.UndoCount;

        // Act
        diagram.History.Transact("Outer", () =>
        {
            node.Properties["Count"] = 5;
            try
            {
                diagram.History.Transact("Inner", () =>
                {
                    node.Properties["Gain"] = 8.0;
                    throw new InvalidOperationException("inner failed");
                });
            }
            catch (InvalidOperationException)
            {
                // The outer action decides to carry on without the inner part
            }
        });

        // Assert
        Assert.Equal(before + 1, diagram.History.UndoCount);
        Assert.Equal("Outer", diagram.History.UndoDescription);
        Assert.Equal(5, node.Properties.Get<int>("Count"));
        Assert.Equal(1.0, node.Properties.Get<double>("Gain"));
    }

    /// <summary>
    ///     Validates that an action that changes nothing records nothing, and that null arguments are refused.
    /// </summary>
    [Fact]
    public void UndoHistory_Transact_EmptyAndNullArguments()
    {
        // Arrange
        var diagram = TestModel.CreateDiagram();

        // Act
        diagram.History.Transact("Nothing", () => { });

        // Assert
        Assert.False(diagram.History.CanUndo);
        Assert.Throws<ArgumentNullException>(() => diagram.History.Transact("x", (Action)null!));
        Assert.Throws<ArgumentNullException>(() => diagram.History.Transact("x", (Func<int>)null!));
        Assert.Throws<ArgumentNullException>(() => diagram.History.Transact(null!, () => { }));
    }
}
