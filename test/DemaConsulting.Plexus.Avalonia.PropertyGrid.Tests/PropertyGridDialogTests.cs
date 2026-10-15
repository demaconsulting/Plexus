using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     A type whose class-level display name is used in the title of the dialog.
/// </summary>
[DisplayName("Gizmo")]
public sealed class Gizmo
{
    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = "x";
}

/// <summary>
///     Tests of <see cref="PropertyGridDialog"/>, driven through a headless test harness.
/// </summary>
public class PropertyGridDialogTests
{
    /// <summary>
    ///     Types text into the dialog's grid and presses Enter.
    /// </summary>
    /// <param name="dialog">The dialog.</param>
    /// <param name="path">The item path.</param>
    /// <param name="text">The text.</param>
    private static void Type(PropertyGridDialog dialog, string path, string text)
    {
        var box = GridHarness.Inner<TextBox>(GridHarness.Editor(dialog.Grid, path));
        box.Focus();
        box.Text = text;
        GridHarness.Press(dialog, PhysicalKey.Enter);
    }

    /// <summary>
    ///     The default title is the display name (or type name) followed by " - Properties".
    /// </summary>
    [Fact]
    public void PropertyGridDialog_GetDefaultTitle_UsesDisplayNameOrTypeName()
    {
        Assert.Equal("Widget - Properties", PropertyGridDialog.GetDefaultTitle(new Widget()));
        Assert.Equal("Gizmo - Properties", PropertyGridDialog.GetDefaultTitle(new Gizmo()));
        Assert.Throws<ArgumentNullException>(() => PropertyGridDialog.GetDefaultTitle(null!));
    }

    /// <summary>
    ///     The dialog shows the object in a staged grid and takes the default or given title.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Constructor_CreatesStagedGridWithTitle() => UiTest.Run(() =>
    {
        var dialog = new PropertyGridDialog(new Widget());
        var named = new PropertyGridDialog(new Widget(), "Custom title");

        Assert.Equal("Widget - Properties", dialog.Title);
        Assert.Equal("Custom title", named.Title);
        Assert.Equal(PropertyEditMode.Staged, dialog.Grid.EditMode);
        Assert.NotNull(dialog.Grid.Session);
        Assert.Throws<ArgumentNullException>(() => new PropertyGridDialog(null!));
    });

    /// <summary>
    ///     OK commits the staged edits to the object and completes the awaited task with true.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Ok_CommitsAndReturnsTrue() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });

        var task = PropertyGridDialog.ShowAsync(owner, widget);
        GridHarness.Pump(owner);
        var dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        Type(dialog, "Caption", "Launch");

        Assert.Equal("Widget", widget.Caption);
        Assert.False(task.IsCompleted);

        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_OK"));
        GridHarness.Pump(owner);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
        Assert.Equal("Launch", widget.Caption);
        owner.Close();
    });

    /// <summary>
    ///     Cancel discards the staged edits and completes the awaited task with false.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Cancel_DiscardsAndReturnsFalse() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });
        var task = PropertyGridDialog.ShowAsync(owner, widget);
        GridHarness.Pump(owner);
        var dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        Type(dialog, "Caption", "Launch");

        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.Equal("Widget", widget.Caption);
        owner.Close();
    });

    /// <summary>
    ///     The commit scope wraps the commit's writes once, so a host can make the dialog one undo step.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Ok_RunsCommitInsideScopeOnce() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var log = new List<string>();
        var dialog = new PropertyGridDialog(widget, null, apply =>
        {
            log.Add("begin");
            apply();
            log.Add("end:" + widget.Caption + ":" + widget.Timeout);
        });
        dialog.Show();
        GridHarness.Pump(dialog);
        Type(dialog, "Caption", "Launch");
        Type(dialog, "Timeout", "90");

        Assert.Empty(log);
        Assert.True(dialog.Accept());

        Assert.Equal(["begin", "end:Launch:90"], log);
    });

    /// <summary>
    ///     OK is refused while a value is invalid: the dialog stays open, selects the row and shows the reason, and
    ///     nothing is written.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Ok_WithInvalidValue_StaysOpen() => UiTest.Run(() =>
    {
        var widget = new Widget();
        var dialog = new PropertyGridDialog(widget);
        dialog.Show();
        GridHarness.Pump(dialog);
        Type(dialog, "Caption", "Launch");
        Type(dialog, "Timeout", "0");

        var accepted = dialog.Accept();

        Assert.False(accepted);
        Assert.True(dialog.IsVisible);
        Assert.Equal("Timeout", dialog.Grid.SelectedItem?.Name);
        Assert.Equal("Timeout must be between 1 and 3600 seconds.", GridHarness.Get<TextBlock>(dialog, "PropertyGridDialog_Status").Text);
        Assert.Equal("Widget", widget.Caption);

        Type(dialog, "Timeout", "10");

        Assert.True(dialog.Accept());
        Assert.Equal("Launch", widget.Caption);
        Assert.Equal(10, widget.Timeout);
    });

    /// <summary>
    ///     <c>ShowAsync</c> rejects a missing owner or target with an argument-null exception before it creates a window.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_ShowAsync_NullOwnerOrTarget_ThrowsArgumentNullException() => UiTest.Run(() =>
    {
        // Arrange
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });

        // Act / Assert: each missing argument is rejected before a dialog is shown
        Assert.Throws<ArgumentNullException>(() => { _ = PropertyGridDialog.ShowAsync(null!, new Widget()); });
        Assert.Throws<ArgumentNullException>(() => { _ = PropertyGridDialog.ShowAsync(owner, null!); });
        Assert.Empty(owner.OwnedWindows);
        owner.Close();
    });

    /// <summary>
    ///     The internal close-confirmed hook is raised once when the close goes ahead (Cancel button or OK) and not when a
    ///     handler of <c>Closing</c> cancels the close.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_CloseConfirmed_IsRaisedOnlyWhenTheCloseGoesAhead() => UiTest.Run(() =>
    {
        // Arrange: a dialog whose first close attempt is vetoed
        var owner = GridHarness.Host(new TextBlock { Text = "owner" });
        var task = PropertyGridDialog.ShowAsync(owner, new Widget());
        GridHarness.Pump(owner);
        var dialog = owner.OwnedWindows.OfType<PropertyGridDialog>().Single();
        var confirmed = 0;
        dialog.CloseConfirmed += (_, _) => confirmed++;
        var veto = true;
        dialog.Closing += (_, e) => e.Cancel = veto;

        // Act: a vetoed close, then the Cancel button
        dialog.Close(false);
        var afterVeto = confirmed;
        veto = false;
        GridHarness.Click(GridHarness.Get<Button>(dialog, "PropertyGridDialog_Cancel"));
        GridHarness.Pump(owner);

        // Assert
        Assert.Equal(0, afterVeto);
        Assert.Equal(1, confirmed);
        Assert.False(task.Result);
        owner.Close();
    });

    /// <summary>
    ///     OK also stays open when the commit itself fails (a setter that throws) and shows the reason.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_Ok_WhenCommitFails_StaysOpenWithReason() => UiTest.Run(() =>
    {
        var dialog = new PropertyGridDialog(new Person());
        dialog.Show();
        GridHarness.Pump(dialog);
        Type(dialog, "Fragile", "5");

        var accepted = dialog.Accept();

        Assert.False(accepted);
        Assert.True(dialog.IsVisible);
        Assert.Contains("Fragile does not accept values.", GridHarness.Get<TextBlock>(dialog, "PropertyGridDialog_Status").Text, StringComparison.Ordinal);
        dialog.Close();
    });
}
