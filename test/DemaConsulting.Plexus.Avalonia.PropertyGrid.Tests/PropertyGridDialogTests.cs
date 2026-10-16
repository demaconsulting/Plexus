using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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
    ///     The internal <c>SetPreview</c> hook rejects a null preview and a size that is not a positive finite number, and
    ///     leaves the dialog without a preview.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_SetPreview_InvalidArguments_ThrowsAndShowsNothing() => UiTest.Run(() =>
    {
        // Arrange
        var dialog = new PropertyGridDialog(new Widget());
        dialog.Show();
        GridHarness.Pump(dialog);
        var preview = new TextBlock { Text = "preview" };

        // Act / Assert: a null preview and every invalid size are rejected
        Assert.Throws<ArgumentNullException>(() => dialog.SetPreview(null!, Dock.Right, 100));
        foreach (var size in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => dialog.SetPreview(preview, Dock.Right, size));
        }

        GridHarness.Pump(dialog);
        Assert.Null(GridHarness.Find<Border>(dialog, "PropertyGridDialog_Preview"));
        Assert.Null(preview.Parent);
        dialog.Close();
    });

    /// <summary>
    ///     The preview border carries the automation id, the name <c>Preview</c>, the content accessibility view and the group
    ///     control type, and hosts the given control.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_SetPreview_ExposesAutomationMetadata() => UiTest.Run(() =>
    {
        // Arrange
        var dialog = new PropertyGridDialog(new Widget());
        dialog.Show();
        var preview = new TextBlock { Text = "preview" };

        // Act
        dialog.SetPreview(preview, Dock.Right, 150);
        GridHarness.Pump(dialog);

        // Assert
        var border = GridHarness.Get<Border>(dialog, "PropertyGridDialog_Preview");
        Assert.Same(preview, border.Child);
        Assert.Equal("Preview", AutomationProperties.GetName(border));
        Assert.Equal(AccessibilityView.Content, AutomationProperties.GetAccessibilityView(border));
        Assert.Equal(AutomationControlType.Group, AutomationProperties.GetControlTypeOverride(border));
        dialog.Close();
    });

    /// <summary>
    ///     The preview is docked on the requested side with the size as its width (left, right) or height (top, bottom), a
    ///     separator line facing the grid, and the grid still fills the rest of the dialog.
    /// </summary>
    /// <param name="dock">The side the preview is docked to.</param>
    /// <returns>A task that completes when the body has run.</returns>
    [Theory]
    [InlineData(Dock.Left)]
    [InlineData(Dock.Right)]
    [InlineData(Dock.Top)]
    [InlineData(Dock.Bottom)]
    public Task PropertyGridDialog_SetPreview_DocksOnEveryDirection(Dock dock) => UiTest.Run(() =>
    {
        // Arrange
        var dialog = new PropertyGridDialog(new Widget());
        dialog.Show();

        // Act
        dialog.SetPreview(new TextBlock { Text = "preview" }, dock, 120);
        GridHarness.Pump(dialog);

        // Assert: the dock, the sized dimension and the separator on the side that faces the grid
        var border = GridHarness.Get<Border>(dialog, "PropertyGridDialog_Preview");
        var horizontal = dock is Dock.Left or Dock.Right;
        Assert.Equal(dock, DockPanel.GetDock(border));
        Assert.Equal(horizontal ? 120 : double.NaN, border.Width);
        Assert.Equal(horizontal ? double.NaN : 120, border.Height);
        var expectedLine = dock switch
        {
            Dock.Left => new Thickness(0, 0, 1, 0),
            Dock.Right => new Thickness(1, 0, 0, 0),
            Dock.Top => new Thickness(0, 0, 0, 1),
            _ => new Thickness(0, 1, 0, 0),
        };
        Assert.Equal(expectedLine, border.BorderThickness);

        // Assert: the preview comes before the grid, which is still the last child and so fills the rest
        var root = Assert.IsType<DockPanel>(dialog.Content);
        Assert.Same(dialog.Grid, root.Children[^1]);
        Assert.True(root.Children.IndexOf(border) < root.Children.IndexOf(dialog.Grid));
        var side = dock switch
        {
            Dock.Left => border.Bounds.X < dialog.Grid.Bounds.X,
            Dock.Right => border.Bounds.X > dialog.Grid.Bounds.X,
            Dock.Top => border.Bounds.Y < dialog.Grid.Bounds.Y,
            _ => border.Bounds.Y > dialog.Grid.Bounds.Y,
        };
        Assert.True(side);
        Assert.True(dialog.Grid.Bounds.Width > 0 && dialog.Grid.Bounds.Height > 0);
        dialog.Close();
    });

    /// <summary>
    ///     Calling <c>SetPreview</c> again replaces the previous preview, so the dialog never shows two.
    /// </summary>
    /// <returns>A task that completes when the body has run.</returns>
    [Fact]
    public Task PropertyGridDialog_SetPreview_Replacement_ShowsOnlyTheLatestPreview() => UiTest.Run(() =>
    {
        // Arrange
        var dialog = new PropertyGridDialog(new Widget());
        dialog.Show();
        var first = new TextBlock { Text = "first" };
        var second = new TextBlock { Text = "second" };
        dialog.SetPreview(first, Dock.Right, 100);

        // Act
        dialog.SetPreview(second, Dock.Top, 90);
        GridHarness.Pump(dialog);

        // Assert
        var previews = Assert.IsType<DockPanel>(dialog.Content).Children
            .Where(c => AutomationProperties.GetAutomationId(c) == "PropertyGridDialog_Preview")
            .Cast<Border>()
            .ToList();
        var border = Assert.Single(previews);
        Assert.Same(second, border.Child);
        Assert.Equal(Dock.Top, DockPanel.GetDock(border));
        Assert.Null(first.Parent);
        Assert.Same(border, GridHarness.Get<Border>(dialog, "PropertyGridDialog_Preview"));
        dialog.Close();
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
