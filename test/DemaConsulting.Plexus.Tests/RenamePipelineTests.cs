namespace DemaConsulting.Plexus.Tests;

/// <summary>
///     Unit tests for the node rename pipeline: validation, correction, veto, events and undo.
/// </summary>
public class RenamePipelineTests
{
    /// <summary>
    ///     Creates a diagram with two nodes named Alpha and Beta on one page.
    /// </summary>
    /// <returns>The diagram and its two nodes.</returns>
    private static (Diagram Diagram, Node Alpha, Node Beta) Create()
    {
        var diagram = TestModel.CreateDiagram();
        var page = diagram.Pages.Add("Main");
        return (diagram, page.AddNode("acme.Producer", 0, 0, "Alpha"), page.AddNode("acme.Producer", 100, 0, "Beta"));
    }

    /// <summary>
    ///     Validates that setting Name renames the node and records an undoable entry.
    /// </summary>
    [Fact]
    public void Node_Name_Set_RenamesAndRoundTripsThroughUndo()
    {
        // Arrange
        var (diagram, alpha, _) = Create();

        // Act / Assert: one entry that round-trips
        TestModel.AssertRoundTrip(diagram, () => alpha.Name = "Gamma");
        Assert.Equal("Gamma", alpha.Name);
        Assert.Equal("Rename node", diagram.History.UndoDescription);
    }

    /// <summary>
    ///     Validates that surrounding whitespace is trimmed from the proposed name.
    /// </summary>
    [Fact]
    public void Diagram_Rename_WhitespaceAroundName_IsTrimmed()
    {
        // Arrange
        var (diagram, alpha, _) = Create();

        // Act
        diagram.Rename(alpha, "  Gamma  ");

        // Assert
        Assert.Equal("Gamma", alpha.Name);
    }

    /// <summary>
    ///     Validates that an empty name is rejected by the setter with an argument exception and by TryRename with a reason.
    /// </summary>
    [Fact]
    public void Diagram_Rename_EmptyName_IsRejected()
    {
        // Arrange
        var (diagram, alpha, _) = Create();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => alpha.Name = "   ");
        Assert.False(diagram.TryRename(alpha, "", out var reason));
        Assert.Contains("empty", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Alpha", alpha.Name);
    }

    /// <summary>
    ///     Validates that a duplicate name is rejected when uniqueness is required.
    /// </summary>
    [Fact]
    public void Diagram_Rename_DuplicateName_IsRejectedWithReason()
    {
        // Arrange
        var (diagram, alpha, _) = Create();

        // Act
        var ok = diagram.TryRename(alpha, "Beta", out var reason);

        // Assert
        Assert.False(ok);
        Assert.Contains("Beta", reason, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => alpha.Name = "Beta");
        Assert.False(diagram.History.UndoDescription == "Rename node");
    }

    /// <summary>
    ///     Validates that duplicate names are allowed when RequireUniqueNodeNames is false.
    /// </summary>
    [Fact]
    public void Diagram_Rename_DuplicateWithUniquenessOff_Succeeds()
    {
        // Arrange
        var (diagram, alpha, _) = Create();
        diagram.RequireUniqueNodeNames = false;

        // Act
        alpha.Name = "Beta";

        // Assert
        Assert.Equal("Beta", alpha.Name);
    }

    /// <summary>
    ///     Validates that renaming to the current name succeeds without raising events or recording history.
    /// </summary>
    [Fact]
    public void Diagram_Rename_SameName_IsNoOp()
    {
        // Arrange
        var (diagram, alpha, _) = Create();
        var entries = diagram.History.UndoCount;
        var events = 0;
        diagram.NodeRenaming += (_, _) => events++;
        diagram.NodeRenamed += (_, _) => events++;

        // Act
        var ok = diagram.TryRename(alpha, "Alpha", out var reason);

        // Assert
        Assert.True(ok);
        Assert.Null(reason);
        Assert.Equal(0, events);
        Assert.Equal(entries, diagram.History.UndoCount);
    }

    /// <summary>
    ///     Validates that a NodeRenaming handler can correct the proposed name.
    /// </summary>
    [Fact]
    public void Diagram_Rename_HandlerCorrectsName_AppliesCorrection()
    {
        // Arrange: a handler that upper-cases names
        var (diagram, alpha, _) = Create();
        diagram.NodeRenaming += (_, e) => e.NewName = e.ProposedName.ToUpperInvariant();

        // Act
        alpha.Name = "gamma";

        // Assert
        Assert.Equal("GAMMA", alpha.Name);
    }

    /// <summary>
    ///     Validates that the event arguments carry node, old name and proposed name.
    /// </summary>
    [Fact]
    public void Diagram_Rename_RenamingArgs_CarryOldAndProposedName()
    {
        // Arrange
        var (diagram, alpha, _) = Create();
        NodeRenamingEventArgs? seen = null;
        diagram.NodeRenaming += (_, e) => seen = e;

        // Act
        alpha.Name = "Gamma";

        // Assert
        Assert.NotNull(seen);
        Assert.Same(alpha, seen.Node);
        Assert.Equal("Alpha", seen.OldName);
        Assert.Equal("Gamma", seen.ProposedName);
        Assert.Equal("Gamma", seen.NewName);
        Assert.False(seen.Cancel);
    }

    /// <summary>
    ///     Validates that a handler correction that collides with another name is rejected.
    /// </summary>
    [Fact]
    public void Diagram_Rename_HandlerCorrectionDuplicates_IsRejected()
    {
        // Arrange: a handler that redirects every rename to an existing name
        var (diagram, alpha, _) = Create();
        diagram.NodeRenaming += (_, e) => e.NewName = "Beta";

        // Act
        var ok = diagram.TryRename(alpha, "Gamma", out var reason);

        // Assert
        Assert.False(ok);
        Assert.NotNull(reason);
        Assert.Equal("Alpha", alpha.Name);
    }

    /// <summary>
    ///     Validates that a handler can cancel a rename and supply the reason.
    /// </summary>
    [Fact]
    public void Diagram_Rename_HandlerCancels_RejectsWithReason()
    {
        // Arrange: a handler that vetoes names starting with "X"
        var (diagram, alpha, _) = Create();
        diagram.NodeRenaming += (_, e) =>
        {
            if (e.ProposedName.StartsWith('X'))
            {
                e.Cancel = true;
                e.Reason = "No X names.";
            }
        };

        // Act
        var ok = diagram.TryRename(alpha, "Xavier", out var reason);

        // Assert
        Assert.False(ok);
        Assert.Equal("No X names.", reason);
        Assert.Equal("Alpha", alpha.Name);
        var ex = Assert.Throws<ArgumentException>(() => alpha.Name = "Xena");
        Assert.Contains("No X names.", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a cancelled rename without a reason still reports a default reason.
    /// </summary>
    [Fact]
    public void Diagram_Rename_CancelWithoutReason_ReportsDefaultReason()
    {
        // Arrange
        var (diagram, alpha, _) = Create();
        diagram.NodeRenaming += (_, e) => e.Cancel = true;

        // Act
        var ok = diagram.TryRename(alpha, "Gamma", out var reason);

        // Assert
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    /// <summary>
    ///     Validates that NodeRenamed fires on rename, undo and redo, but NodeRenaming only on the rename itself.
    /// </summary>
    [Fact]
    public void Diagram_Rename_Events_RenamedFiresOnUndoRedoButRenamingDoesNot()
    {
        // Arrange
        var (diagram, alpha, _) = Create();
        var renaming = 0;
        var renamed = new List<string>();
        diagram.NodeRenaming += (_, _) => renaming++;
        diagram.NodeRenamed += (_, e) => renamed.Add($"{e.OldName}->{e.NewName}");

        // Act
        alpha.Name = "Gamma";
        diagram.History.Undo();
        diagram.History.Redo();

        // Assert
        Assert.Equal(1, renaming);
        Assert.Equal(new[] { "Alpha->Gamma", "Gamma->Alpha", "Alpha->Gamma" }, renamed);
    }

    /// <summary>
    ///     Validates that a rename frees the old name and takes the new one in uniqueness checks.
    /// </summary>
    [Fact]
    public void Diagram_Rename_FreesOldName_ForReuse()
    {
        // Arrange
        var (diagram, alpha, beta) = Create();

        // Act: rename Alpha away, then Beta can take Alpha
        alpha.Name = "Gamma";
        beta.Name = "Alpha";

        // Assert
        Assert.Equal("Alpha", beta.Name);
        Assert.Same(alpha, diagram.FindNode("Gamma"));
    }

    /// <summary>
    ///     Validates argument checks of Rename and TryRename.
    /// </summary>
    [Fact]
    public void Diagram_Rename_InvalidArguments_Throw()
    {
        // Arrange: a node attached to another diagram
        var (diagram, alpha, _) = Create();
        var foreign = TestModel.CreateDiagram().Pages.Add("P").AddNode("acme.Producer", 0, 0);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => diagram.Rename(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => diagram.Rename(alpha, null!));
        Assert.Throws<ArgumentException>(() => diagram.Rename(foreign, "x"));
    }

    /// <summary>
    ///     Validates that a removed node cannot be renamed through the setter.
    /// </summary>
    [Fact]
    public void Node_Name_Set_OnRemovedNode_ThrowsInvalidOperationException()
    {
        // Arrange
        var (_, alpha, _) = Create();
        alpha.Remove();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => alpha.Name = "Zed");
    }
}
