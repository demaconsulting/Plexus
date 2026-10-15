using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Tests of the live preview of <see cref="NodePropertiesDialog"/> (<see cref="NodePropertiesDialogOptions"/>) and of
///     <see cref="NodePreviewContext"/>, driven through headless windows.
/// </summary>
public class NodePropertiesDialogPreviewTests
{
    /// <summary>
    ///     The automation id of the preview panel.
    /// </summary>
    private const string PreviewId = "PropertyGridDialog_Preview";

    /// <summary>
    ///     The row path of the frequency property of the test node.
    /// </summary>
    private static readonly string FrequencyPath = NodePropertySource.PropertyPath("Frequency");

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
    ///     Opens the dialog of a node with options over a host window.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="options">The dialog options.</param>
    /// <param name="owner">The host window.</param>
    /// <param name="dialog">The opened dialog.</param>
    /// <returns>The task that completes when the dialog closes.</returns>
    private static Task<bool> Open(Node node, NodePropertiesDialogOptions? options, out Window owner, out PropertyGridDialog dialog)
    {
        owner = GridHarness.Host(new TextBlock { Text = "owner" });
        var task = NodePropertiesDialog.ShowAsync(owner, node, options);
        GridHarness.Pump(owner);
        dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        return task;
    }

    /// <summary>
    ///     Builds options whose preview is a text block and that capture the context the dialog creates.
    /// </summary>
    /// <param name="captured">Receives the context when the dialog calls the factory.</param>
    /// <param name="placement">Where the preview goes.</param>
    /// <returns>The options.</returns>
    private static NodePropertiesDialogOptions Capturing(Action<NodePreviewContext> captured, NodePreviewPlacement placement = NodePreviewPlacement.Side) =>
        new()
        {
            PreviewPlacement = placement,
            Preview = context =>
            {
                captured(context);
                return new TextBlock { Text = "preview" };
            },
        };

    /// <summary>
    ///     A side preview is shown to the right of the grid under the preview automation id and widens the dialog.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Preview_IsShownBesideTheGridAndWidensTheDialog() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var control = new TextBlock { Text = "preview" };
        var options = new NodePropertiesDialogOptions { Preview = _ => control };

        // Act
        _ = Open(node, options, out var owner, out var dialog);

        // Assert: the panel holds the factory's control, sits right of the grid, and the dialog is wider
        var panel = GridHarness.Get<Border>(dialog, PreviewId);
        Assert.Same(control, panel.Child);
        Assert.Equal("Preview", global::Avalonia.Automation.AutomationProperties.GetName(panel));
        var previewX = panel.TranslatePoint(default, dialog)!.Value.X;
        var gridX = dialog.Grid.TranslatePoint(default, dialog)!.Value.X;
        Assert.True(previewX > gridX);
        Assert.Equal(880, dialog.Width);
        Assert.Equal(640, dialog.Height);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     A top preview is shown above the grid and makes the dialog taller.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Preview_TopPlacementIsAboveTheGrid() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var options = Capturing(_ => { }, NodePreviewPlacement.Top);

        // Act
        _ = Open(node, options, out var owner, out var dialog);

        // Assert
        var panel = GridHarness.Get<Border>(dialog, PreviewId);
        var previewY = panel.TranslatePoint(default, dialog)!.Value.Y;
        var gridY = dialog.Grid.TranslatePoint(default, dialog)!.Value.Y;
        Assert.True(previewY < gridY);
        Assert.Equal(480, dialog.Width);
        Assert.Equal(860, dialog.Height);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     The preview reads the staged value of an edited row and the node's value of an unedited row, while the node is
    ///     untouched; an unknown path reads as null.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Preview_SeesStagedValues() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        NodePreviewContext? context = null;
        _ = Open(node, Capturing(c => context = c), out var owner, out var dialog);

        // Act
        Type(dialog, dialog.Grid, FrequencyPath, "880 Hz");
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");

        // Assert
        Assert.NotNull(context);
        Assert.Same(node, context.Node);
        Assert.Equal(880.0, context.GetPropertyValue("Frequency"));
        Assert.Equal(880.0, context.GetValue(FrequencyPath));
        Assert.Equal("Lead", context.GetValue(NodePropertySource.NamePath));
        Assert.Equal(5, context.GetValue(NodePropertySource.DataPath("Level")));
        Assert.Equal("Sine", context.GetPropertyValue("Waveform"));
        Assert.Null(context.GetValue("Property.Missing"));
        Assert.Null(context.GetPropertyValue("Missing"));
        Assert.Equal(440.0, node.Properties["Frequency"]);
        Assert.Equal("Osc", node.Name);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     The preview is notified when a value is staged, reset or cancelled, and not for an invalid text that only sets
    ///     an error.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Preview_IsNotifiedOnEditResetAndCancel() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        node.Properties["Frequency"] = 220.0;
        NodePreviewContext? context = null;
        _ = Open(node, Capturing(c => context = c), out var owner, out var dialog);
        var raised = 0;
        context!.Changed += (_, _) => raised++;
        var session = dialog.Grid.Session!;
        var frequency = session.FindItem(FrequencyPath)!;

        // Act / Assert: a staged edit
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        var afterEdit = raised;
        Assert.True(afterEdit > 0);
        Assert.Equal(880.0, context.GetValue(FrequencyPath));

        // Act / Assert: an invalid text only sets an error, so no notification
        session.SetText(frequency, "not a number");
        Assert.Equal(afterEdit, raised);
        Assert.Equal(880.0, context.GetValue(FrequencyPath));

        // Act / Assert: a reset stages the default
        session.Reset(frequency);
        var afterReset = raised;
        Assert.True(afterReset > afterEdit);
        Assert.Equal(440.0, context.GetValue(FrequencyPath));

        // Act / Assert: cancel returns to the node's value
        session.Cancel();
        Assert.True(raised > afterReset);
        Assert.Equal(220.0, context.GetValue(FrequencyPath));
        Assert.Equal(220.0, node.Properties["Frequency"]);
        dialog.Close(false);
        GridHarness.Pump(owner);
        owner.Close();
    });

    /// <summary>
    ///     OK in a dialog with a preview commits the edits as one undo entry and completes with true.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_PreviewOk_CommitsOneUndoEntry() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var before = diagram.History.UndoCount;
        var task = Open(node, Capturing(_ => { }), out var owner, out var dialog);
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        Type(dialog, dialog.Grid, NodePropertySource.DataPath("Level"), "9");

        // Act
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
        Assert.Equal("Lead", node.Name);
        Assert.Equal(880.0, node.Properties["Frequency"]);
        Assert.Equal(9, ((ToneSettings)node.Data!).Level);
        Assert.Equal(before + 1, diagram.History.UndoCount);
        owner.Close();
    });

    /// <summary>
    ///     OK in a dialog with a preview notifies the preview after the commit, when the node already holds the new values.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_PreviewOk_IsNotifiedAfterCommit() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        NodePreviewContext? context = null;
        var task = Open(node, Capturing(c => context = c), out var owner, out var dialog);
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        var raised = 0;
        object? nodeValueAtLastNotification = null;
        object? contextValueAtLastNotification = null;
        context!.Changed += (_, _) =>
        {
            raised++;
            nodeValueAtLastNotification = node.Properties["Frequency"];
            contextValueAtLastNotification = context.GetPropertyValue("Frequency");
        };

        // Act
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        // Assert: notified once for the commit, and that notification saw the committed value on the node and in the context
        Assert.True(task.Result);
        Assert.Equal(1, raised);
        Assert.Equal(880.0, nodeValueAtLastNotification);
        Assert.Equal(880.0, contextValueAtLastNotification);
        Assert.Equal(880.0, context.GetPropertyValue("Frequency"));
        owner.Close();
    });

    /// <summary>
    ///     The dialog's Cancel button does not cancel the session and raises no notification: the context is disposed as the
    ///     close is confirmed, before the grid is torn down.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_PreviewCancelButton_RaisesNoNotification() => UiTest.Run(() =>
    {
        // Arrange: a staged edit, then count the notifications
        var (_, node) = NodeFixture.Create();
        NodePreviewContext? context = null;
        var task = Open(node, Capturing(c => context = c), out var owner, out var dialog);
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        var raised = 0;
        context!.Changed += (_, _) => raised++;
        var beforeClick = raised;

        // The context must already be disposed when the close is confirmed: an edit staged by a later handler of that moment
        // (which stands for the grid's teardown) is not notified
        var session = dialog.Grid.Session!;
        dialog.CloseConfirmed += (_, _) => session.SetText(session.FindItem(FrequencyPath)!, "990");

        // Act
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);

        // Assert: the dialog closed with false and the preview heard nothing during the close
        Assert.False(task.Result);
        Assert.Equal(beforeClick, raised);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        owner.Close();
    });

    /// <summary>
    ///     Closing the window (its close button) raises no notification either.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_PreviewWindowClose_RaisesNoNotification() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        NodePreviewContext? context = null;
        var task = Open(node, Capturing(c => context = c), out var owner, out var dialog);
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        var raised = 0;
        context!.Changed += (_, _) => raised++;

        // Act
        dialog.Close();
        GridHarness.Pump(owner);

        // Assert
        Assert.True(task.IsCompleted);
        Assert.Equal(0, raised);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        owner.Close();
    });

    /// <summary>
    ///     Cancel in a dialog with a preview changes nothing, completes with false and stops the preview's notifications.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_PreviewCancel_ChangesNothing() => UiTest.Run(() =>
    {
        // Arrange
        var (diagram, node) = NodeFixture.Create();
        var data = node.Data;
        var before = diagram.History.UndoCount;
        NodePreviewContext? context = null;
        var task = Open(node, Capturing(c => context = c), out var owner, out var dialog);
        var raised = 0;
        context!.Changed += (_, _) => raised++;
        var session = dialog.Grid.Session!;
        Type(dialog, dialog.Grid, NodePropertySource.NamePath, "Lead");
        Type(dialog, dialog.Grid, FrequencyPath, "880");
        Type(dialog, dialog.Grid, NodePropertySource.DataPath("Level"), "9");

        // Act
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);
        var afterClose = raised;
        session.Cancel();

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.Equal("Osc", node.Name);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        Assert.Same(data, node.Data);
        Assert.Equal(5, ((ToneSettings)node.Data!).Level);
        Assert.Equal(before, diagram.History.UndoCount);
        Assert.Equal(afterClose, raised);
        owner.Close();
    });

    /// <summary>
    ///     The title and size options are applied, and the plain dialog keeps its size without options.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Options_TitleAndSizeAreApplied() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();

        // Act
        var custom = NodePropertiesDialog.Create(node, new NodePropertiesDialogOptions { Title = "Inspector", Width = 700, Height = 500 });
        var withPreview = NodePropertiesDialog.Create(node, Capturing(_ => { }) with { Width = 1000 });
        var plain = NodePropertiesDialog.Create(node, null);
        var empty = NodePropertiesDialog.Create(node, new NodePropertiesDialogOptions());

        // Assert
        Assert.Equal("Inspector", custom.Title);
        Assert.Equal(700, custom.Width);
        Assert.Equal(500, custom.Height);
        Assert.Null(GridHarness.Find<Border>(custom, PreviewId));
        Assert.Equal(1000, withPreview.Width);
        Assert.Equal(640, withPreview.Height);
        Assert.Equal("Osc - Properties", plain.Title);
        Assert.Equal(480, plain.Width);
        Assert.Equal(640, plain.Height);
        Assert.Equal(480, empty.Width);
        Assert.Equal(640, empty.Height);
        withPreview.Close();
    });

    /// <summary>
    ///     The new overloads validate the owner, the node, the size and the factory's result.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePropertiesDialog_Options_ArgumentsAreValidated() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });
        NodePreviewContext? context = null;
        var throwing = new NodePropertiesDialogOptions
        {
            Preview = c =>
            {
                context = c;
                throw new FormatException("boom");
            },
        };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => { _ = NodePropertiesDialog.ShowAsync(null!, node, null); });
        Assert.Throws<ArgumentNullException>(() => { _ = NodePropertiesDialog.ShowAsync(owner, null!, null); });
        Assert.Throws<ArgumentNullException>(() => NodePropertiesDialog.Create(null!, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => NodePropertiesDialog.Create(node, new NodePropertiesDialogOptions { Width = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => NodePropertiesDialog.Create(node, new NodePropertiesDialogOptions { Height = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = NodePropertiesDialog.ShowAsync(owner, node, new NodePropertiesDialogOptions { Width = double.PositiveInfinity }); });
        Assert.Throws<InvalidOperationException>(() => NodePropertiesDialog.Create(node, new NodePropertiesDialogOptions { Preview = _ => null! }));
        Assert.Throws<FormatException>(() => NodePropertiesDialog.Create(node, throwing));
        Assert.NotNull(context);
        owner.Close();
    });

    /// <summary>
    ///     The context constructors and readers validate their arguments and refuse content that is not a node.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePreviewContext_Constructors_ValidateArguments() => UiTest.Run(() =>
    {
        // Arrange
        using var reflected = new ReflectionPropertySource(new Person());
        var objectGrid = new PropertyGrid { SelectedObject = new Person() };
        var emptyGrid = new PropertyGrid();
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        using var context = new NodePreviewContext(new PropertyEditSession(source, PropertyEditMode.Staged));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new NodePreviewContext((PropertyGrid)null!));
        Assert.Throws<ArgumentNullException>(() => new NodePreviewContext((PropertyEditSession)null!));
        Assert.Throws<ArgumentException>(() => new NodePreviewContext(objectGrid));
        Assert.Throws<ArgumentException>(() => new NodePreviewContext(emptyGrid));
        Assert.Throws<ArgumentException>(() => new NodePreviewContext(new PropertyEditSession(reflected)));
        Assert.Throws<ArgumentNullException>(() => context.GetValue(null!));
        Assert.Throws<ArgumentNullException>(() => context.GetPropertyValue(null!));
    });

    /// <summary>
    ///     A context over a grid that comes to show something other than a node is notified once, then reads nothing while
    ///     <see cref="NodePreviewContext.Node"/> keeps the last node shown; showing a node again restores the values.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePreviewContext_GridShowsNonNodeObject_ReadsNothing() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var grid = new PropertyGrid { EditMode = PropertyEditMode.Staged, SelectedObject = node };
        var window = GridHarness.Host(grid);
        using var context = new NodePreviewContext(grid);
        var nodeSession = grid.Session!;
        var raised = 0;
        context.Changed += (_, _) => raised++;

        // Act: show an ordinary object, then stage in the detached node session
        grid.SelectedObject = new Person();
        var afterReplace = raised;
        var nameWhileDetached = context.GetValue(NodePropertySource.NamePath);
        var frequencyWhileDetached = context.GetPropertyValue("Frequency");
        var nodeWhileDetached = context.Node;
        nodeSession.SetValue(nodeSession.FindItem(FrequencyPath)!, 123.0);
        var afterStagingInOldSession = raised;

        // Act: show the node again
        grid.SelectedObject = node;
        var afterReturn = raised;
        var frequencyAfterReturn = context.GetPropertyValue("Frequency");

        // Assert
        Assert.Equal(1, afterReplace);
        Assert.Null(nameWhileDetached);
        Assert.Null(frequencyWhileDetached);
        Assert.Same(node, nodeWhileDetached);
        Assert.Equal(afterReplace, afterStagingInOldSession);
        Assert.True(afterReturn > afterReplace);
        Assert.Equal(440.0, frequencyAfterReturn);
        Assert.Same(node, context.Node);
        window.Close();
    });

    /// <summary>
    ///     A context over a grid follows the grid's new session when the grid leaves the window and returns: it is
    ///     notified and reads through the new session.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task NodePreviewContext_FromGrid_FollowsANewSession() => UiTest.Run(() =>
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        var grid = new PropertyGrid { EditMode = PropertyEditMode.Staged, SelectedObject = node };
        var window = GridHarness.Host(grid);
        using var context = new NodePreviewContext(grid);
        var raised = 0;
        context.Changed += (_, _) => raised++;
        var first = grid.Session;

        // Act: take the grid out of the window and put it back, then stage an edit in the new session
        window.Content = null;
        GridHarness.Pump(window);
        window.Content = grid;
        GridHarness.Pump(window);
        var afterReplace = raised;
        var second = grid.Session!;
        second.SetValue(second.FindItem(FrequencyPath)!, 660.0);

        // Assert
        Assert.NotSame(first, second);
        Assert.True(afterReplace > 0);
        Assert.True(raised > afterReplace);
        Assert.Equal(660.0, context.GetPropertyValue("Frequency"));
        Assert.Same(node, context.Node);
        Assert.Equal(440.0, node.Properties["Frequency"]);
        window.Close();
    });

    /// <summary>
    ///     A context over a host-owned staged session (no grid, no dialog) reads the staged values, notifies on edit and
    ///     cancel, and stops notifying once disposed.
    /// </summary>
    [Fact]
    public void NodePreviewContext_FromSession_ReadsAndNotifies()
    {
        // Arrange
        var (_, node) = NodeFixture.Create();
        using var source = new NodePropertySource(node);
        var session = new PropertyEditSession(source, PropertyEditMode.Staged);
        var context = new NodePreviewContext(session);
        var raised = 0;
        context.Changed += (_, _) => raised++;

        // Act
        session.SetText(session.FindItem(FrequencyPath)!, "1000", System.Globalization.CultureInfo.InvariantCulture);
        var staged = context.GetPropertyValue("Frequency");
        var afterEdit = raised;
        session.Cancel();
        var cancelled = context.GetPropertyValue("Frequency");
        var afterCancel = raised;
        context.Dispose();
        context.Dispose();
        session.SetText(session.FindItem(FrequencyPath)!, "2000", System.Globalization.CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(1000.0, staged);
        Assert.True(afterEdit > 0);
        Assert.Equal(440.0, cancelled);
        Assert.True(afterCancel > afterEdit);
        Assert.Equal(afterCancel, raised);
        Assert.Equal(2000.0, context.GetPropertyValue("Frequency"));
        Assert.Same(node, context.Node);
        Assert.Equal(440.0, node.Properties["Frequency"]);
    }
}
