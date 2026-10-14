using Avalonia.Controls;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

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
        Assert.Equal("Name", paths[0]);
        Assert.Contains("Frequency", paths);
        Assert.Contains("Level", paths);
        Assert.NotNull(GridHarness.Find<Control>(dialog, "PropertyGrid_Row_Name"));
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
        Type(dialog, dialog.Grid, "Name", "Lead");
        Type(dialog, dialog.Grid, "Frequency", "880 Hz");
        Type(dialog, dialog.Grid, "Level", "9");
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
        Type(dialog, dialog.Grid, "Name", "Lead");
        Type(dialog, dialog.Grid, "Level", "9");
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
        Type(dialog, dialog.Grid, "Name", "Other");
        var accepted = dialog.Accept();

        // Assert
        Assert.False(accepted);
        Assert.False(task.IsCompleted);
        Assert.Equal("The name 'Other' is already used by another node.", dialog.Grid.Session!.GetErrors()["Name"]);
        Assert.Equal("The name 'Other' is already used by another node.", GridHarness.Get<TextBlock>(dialog, "PropertyGridDialog_Status").Text);

        Type(dialog, dialog.Grid, "Name", "Fixed");
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
        Type(dialog, dialog.Grid, "Name", "Lead");
        Type(dialog, dialog.Grid, "Frequency", "880");

        // Act
        var accepted = dialog.Accept();

        // Assert
        Assert.False(accepted);
        Assert.False(task.IsCompleted);
        Assert.Equal("Renaming is locked.", dialog.Grid.Session!.GetErrors()["Name"]);
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
        Type(window, grid, "Frequency", "880");
        Type(window, grid, "Level", "9");

        // Assert
        Assert.Equal(before + 2, diagram.History.UndoCount);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal(9, ((ToneSettings)node.Data!).Level);

        diagram.History.Undo();
        GridHarness.Pump(window);
        Assert.Equal("5", GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Level")).Text);
        diagram.History.Undo();
        GridHarness.Pump(window);
        Assert.Equal("440", GridHarness.Inner<TextBox>(GridHarness.Editor(grid, "Frequency")).Text);

        // Showing another node (or nothing) detaches from the first one
        grid.SelectedObject = null;
        window.Close();
    });
}
