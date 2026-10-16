using Avalonia.Controls;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of <see cref="NodePropertiesDialog"/> and of a grid that is given a node, driven through a headless window.
/// </summary>
public class NodePropertiesDialogTests
{
    /// <summary>
    ///     Types text into the editor of a row and presses Enter.
    /// </summary>
    /// <param name="window">The window that holds the grid.</param>
    /// <param name="grid">The grid.</param>
    /// <param name="path">The item path.</param>
    /// <param name="text">The text.</param>
    private static void Type(TopLevel window, PropertyGrid grid, string path, string text)
    {
        var box = GridHarness.Inner<TextBox>(GridHarness.Editor(grid, path));
        box.Focus();
        box.Text = text;
        GridHarness.Press(window, PhysicalKey.Enter);
    }

    /// <summary>
    ///     Opens the dialog of a node over a host window.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="owner">The host window.</param>
    /// <param name="dialog">The opened dialog.</param>
    /// <returns>The task that completes when the dialog closes.</returns>
    private static Task<bool> Open(Node node, out Window owner, out PropertyGridDialog dialog)
    {
        owner = GridHarness.Host(new TextBlock { Text = "owner" });
        var task = NodePropertiesDialog.ShowAsync(owner, node);
        GridHarness.Pump(owner);
        dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        return task;
    }

    /// <summary>
    ///     The dialog is titled with the node name, shows the name, properties and data rows and uses a staged grid.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_ShowAsync_ShowsTheNodeInAStagedGrid() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();

        // Act
        var task = Open(node, out var owner, out var dialog);

        // Assert
        Assert.Equal("Osc - Properties", dialog.Title);
        Assert.Equal(PropertyEditMode.Staged, dialog.Grid.EditMode);
        var paths = GridHarness.PropertyPaths(dialog.Grid);
        Assert.Equal(NodePropertySource.NamePath, paths[0]);
        Assert.Contains(NodePropertySource.PropertyPath("Frequency"), paths);
        Assert.Contains(NodePropertySource.DataPath("Level"), paths);
        Assert.NotNull(GridHarness.Find<Control>(dialog, "PropertyGrid_Row_Node.Name"));
        Assert.False(task.IsCompleted);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     OK commits the rename, a property and a data edit as one undo entry and completes with true.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Ok_CommitsAsOneUndoEntry() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var before = diagram.History.UndoCount;
        var task = Open(node, out var owner, out var dialog);

        // Act
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");
        Type(dialog, dialog.Grid, NodePropertySource.PropertyPath("Frequency"), "880 Hz");
        Type(dialog, dialog.Grid, NodePropertySource.DataPath("Level"), "9");
        Assert.Equal("Osc", node.Name);
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal(9, ((ToneSettings)node.Data!).Level);
        Assert.Equal(before + 1, diagram.History.UndoCount);
        diagram.History.Undo();
        Assert.Equal("Osc", node.Name);
        Assert.Equal(5, ((ToneSettings)node.Data).Level);
        owner.Close();
    });

    /// <summary>
    ///     Cancel changes nothing and completes with false.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Cancel_ChangesNothing() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var data = node.Data;
        var before = diagram.History.UndoCount;
        var task = Open(node, out var owner, out var dialog);

        // Act
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");
        Type(dialog, dialog.Grid, NodePropertySource.DataPath("Level"), "9");
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.Equal("Osc", node.Name);
        Assert.Same(data, node.Data);
        Assert.Equal(before, diagram.History.UndoCount);
        owner.Close();
    });

    /// <summary>
    ///     A duplicate name is refused inline while typing and OK stays refused with the reason until it is fixed.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Ok_InvalidNameShowsTheReasonAndStaysOpen() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var task = Open(node, out var owner, out var dialog);

        // Act
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Other");
        var accepted = dialog.Accept();

        // Assert
        Assert.False(accepted);
        Assert.False(task.IsCompleted);
        Assert.Equal("The name 'Other' is already used by another node.", dialog.Grid.Session!.GetErrors()[NodePropertySource.NamePath]);
        Assert.Equal("The name 'Other' is already used by another node.", GridHarness.Get<TextBlock>(dialog, "PropertyGridDialog_Status").Text);

        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Fixed");
        Assert.True(dialog.Accept());
        GridHarness.Pump(owner);
        Assert.True(task.Result);
        Assert.Equal("Fixed", node.Name);
        owner.Close();
    });

    /// <summary>
    ///     A rename that a handler vetoes keeps the dialog open with the reason on the name row and changes nothing.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Ok_VetoedRenameShowsTheReasonOnTheNameRow() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        diagram.NodeRenaming += (_, e) =>
        {
            e.Cancel = true;
            e.Reason = "Renaming is locked.";
        };
        var before = diagram.History.UndoCount;
        var task = Open(node, out var owner, out var dialog);
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");
        Type(dialog, dialog.Grid, NodePropertySource.PropertyPath("Frequency"), "880");

        // Act
        var accepted = dialog.Accept();

        // Assert
        Assert.False(accepted);
        Assert.False(task.IsCompleted);
        Assert.Equal("Renaming is locked.", dialog.Grid.Session!.GetErrors()[NodePropertySource.NamePath]);
        Assert.Equal("Osc", node.Name);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        Assert.Equal(before, diagram.History.UndoCount);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     The dialog helpers validate their arguments.
    /// </summary>
    [Fact]
    public void NodePropertiesDialog_Arguments_AreValidated()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();

        // Act / Assert
        Assert.Equal("Osc - Properties", NodePropertiesDialog.GetTitle(node));
        Assert.Throws<ArgumentNullException>(() => NodePropertiesDialog.GetTitle(null!));
        Assert.Throws<ArgumentNullException>(() => NodePropertiesDialog.Create(null!));
        Assert.Throws<ArgumentNullException>(() => { _ = NodePropertiesDialog.ShowAsync(null!, node); });
    }

    /// <summary>
    ///     A node that is not attached to a diagram is rejected with an argument exception by every entry point.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_UnattachedNode_IsRejected() => UiTest.Run(() =>
    {
        // Arrange: a node that was removed from its page
        var (_, node) = NodeFixture.Create();
        node.Remove();
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });
        var options = new NodePropertiesDialogOptions { Title = "Inspector" };

        // Act / Assert: the same error for the plain and the optioned overloads of ShowAsync and Create
        Assert.Throws<ArgumentException>(() => NodePropertiesDialog.Create(node));
        Assert.Throws<ArgumentException>(() => NodePropertiesDialog.Create(node, options));
        Assert.Throws<ArgumentException>(() => { _ = NodePropertiesDialog.ShowAsync(owner, node); });
        Assert.Throws<ArgumentException>(() => { _ = NodePropertiesDialog.ShowAsync(owner, node, options); });
        Assert.Empty(owner.OwnedWindows);
        owner.Close();
    });

    /// <summary>
    ///     A live grid given a node makes each edit one undo entry and follows undo.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_SelectedObjectNode_LiveEditsAreUndoEntriesAndFollowUndo() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var before = diagram.History.UndoCount;
        var grid = new PropertyGrid { EditMode = PropertyEditMode.Live, SelectedObject = node };
        var window = GridHarness.Host(grid);

        // Act
        Type(window, grid, NodePropertySource.PropertyPath("Frequency"), "880");
        Type(window, grid, NodePropertySource.DataPath("Level"), "9");

        // Assert
        Assert.Equal(before + 2, diagram.History.UndoCount);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal(9, ((ToneSettings)node.Data!).Level);

        diagram.History.Undo();
        GridHarness.Pump(window);
        Assert.Equal("5", GridHarness.Inner<TextBox>(GridHarness.Editor(grid, NodePropertySource.DataPath("Level"))).Text);
        diagram.History.Undo();
        GridHarness.Pump(window);
        Assert.Equal("440", GridHarness.Inner<TextBox>(GridHarness.Editor(grid, NodePropertySource.PropertyPath("Frequency"))).Text);

        // Showing another node (or nothing) detaches from the first one
        grid.SelectedObject = null;
        window.Close();
    });

    /// <summary>
    ///     A source the grid created for SelectedObject is disposed when the content changes; a source assigned to Source is never disposed by the grid.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_SelectedObjectSource_IsDisposedByTheGridButHostSourceIsNot() => UiTest.Run(() =>
    {
        // Arrange: the grid creates a source for the node
        var (_, node) = NodeFixture.Create();
        var grid = new PropertyGrid { SelectedObject = node };
        var window = GridHarness.Host(grid);
        var created = Assert.IsType<NodePropertySource>(grid.Session!.Source);
        var createdRaised = 0;
        created.Changed += (_, _) => createdRaised++;
        node.Properties["Frequency"] = 100.0;
        Assert.True(createdRaised > 0);

        // Act: replace the content with a host-owned source
        using var hosted = new NodePropertySource(node);
        var hostedRaised = 0;
        hosted.Changed += (_, _) => hostedRaised++;
        grid.Source = hosted;
        var afterReplace = createdRaised;
        node.Properties["Frequency"] = 200.0;
        var hostedWhileShown = hostedRaised;
        grid.Source = null;
        node.Properties["Frequency"] = 300.0;

        // Assert: the created source stopped listening, the host source still works after the grid let go of it
        Assert.Equal(afterReplace, createdRaised);
        Assert.True(hostedWhileShown > 0);
        Assert.True(hostedRaised > hostedWhileShown);
        window.Close();
    });

    /// <summary>
    ///     A source the grid created is released while the grid is out of the visual tree and created again, showing the same node, when it returns.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGrid_SelectedObjectSource_IsReleasedWhileDetachedAndRecreatedOnAttach() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var grid = new PropertyGrid { SelectedObject = node };
        var window = GridHarness.Host(grid);
        var first = Assert.IsType<NodePropertySource>(grid.Session!.Source);
        var firstRaised = 0;
        first.Changed += (_, _) => firstRaised++;
        grid.SetExpanded(NodePropertySource.DataPath("Tags"), true);

        // Act: take the grid out of the window, change the node, put the grid back
        window.Content = null;
        GridHarness.Pump(window);
        node.Properties["Frequency"] = 100.0;
        var whileDetached = firstRaised;
        window.Content = grid;
        GridHarness.Pump(window);

        // Assert
        Assert.Equal(0, whileDetached);
        var second = Assert.IsType<NodePropertySource>(grid.Session.Source);
        Assert.NotSame(first, second);
        Assert.Same(node, second.Node);
        Assert.Same(node, grid.SelectedObject);
        Assert.Contains(NodePropertySource.PropertyPath("Frequency"), GridHarness.PropertyPaths(grid));
        Assert.True(grid.IsExpanded(NodePropertySource.DataPath("Tags")));
        window.Close();
    });
}
