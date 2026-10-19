using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A modal dialog that shows the properties of an object in a staged <see cref="PropertyGrid"/>: OK commits the edits,
///     Cancel discards them.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="ShowAsync"/> for the common case. The dialog's grid edits through a staged
///         <see cref="PropertyEditSession"/>, so the target object is untouched until OK. OK is refused while a value is
///         invalid (the grid selects the first row with an error and the dialog shows the reason) or when the commit
///         reports a failure. The optional commit scope lets the host run the writes inside its own undo transaction, so
///         the whole dialog becomes one undo step.
///     </para>
///     <para>
///         The title defaults to <c>"{DisplayName or type name} - Properties"</c>, where the display name is the
///         <c>DisplayName</c> annotation of the object's type.
///     </para>
///     <para>
///         The dialog's grid uses the shared <see cref="PropertyGrid.DefaultEditors"/>, so custom editors registered there at start-up work in
///         every dialog. To give one dialog its own editors, construct it, assign <c>dialog.Grid.EditorRegistry</c> and show it yourself.
///         The dialog derives from Avalonia's <c>Window</c>, so the window API (<c>Show()</c>, <c>ShowDialog()</c>, <c>Close()</c>, <c>Title</c>, <c>Width</c>, ...) applies; see Inherited Members.
///     </para>
///     <para>
///         The dialog's controls carry automation ids: <c>PropertyGridDialog</c> (the window), <c>PropertyGridDialog_OK</c>,
///         <c>PropertyGridDialog_Cancel</c>, <c>PropertyGridDialog_Status</c> and, when a preview panel is shown next to the
///         grid (the live preview of <c>Diagrams.NodePropertiesDialog</c>), <c>PropertyGridDialog_Preview</c>.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     using System.ComponentModel;
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid;
///
///     public static class SettingsDialogSample
///     {
///         // owner: the window that opens the dialog; history: the diagram's undo history (diagram.History)
///         public static async Task<bool> EditAsync(Window owner, UndoHistory history, Settings settings)
///         {
///             return await PropertyGridDialog.ShowAsync(
///                 owner,
///                 settings,
///                 commitScope: apply => history.Transact("Edit settings", apply));   // OK becomes one undo step; apply is run exactly once
///         }
///     }
///
///     public class Settings
///     {
///         [Category("General"), DefaultValue(10)]
///         public int Retries { get; set; } = 10;
///     }
///     ]]></code>
/// </example>
public sealed class PropertyGridDialog : Window
{
    /// <summary>
    ///     The text that shows why OK was refused.
    /// </summary>
    private readonly TextBlock _status = new();

    /// <summary>
    ///     The root layout: the footer docked at the bottom, an optional preview (see <see cref="SetPreview"/>) and the grid
    ///     filling the rest.
    /// </summary>
    private readonly DockPanel _root;

    /// <summary>
    ///     The border that hosts the preview, or <see langword="null"/> when the dialog has none.
    /// </summary>
    private Border? _preview;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyGridDialog"/> class.
    /// </summary>
    /// <param name="target">The object whose properties are edited.</param>
    /// <param name="title">The window title; <see langword="null"/> uses <see cref="GetDefaultTitle"/>.</param>
    /// <param name="commitScope">
    ///     An optional hook that runs the commit's writes (for example inside an undo transaction); it must invoke the
    ///     action it receives exactly once.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public PropertyGridDialog(object target, string? title = null, Action<Action>? commitScope = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        Title = title ?? GetDefaultTitle(target);
        Width = 440;
        Height = 560;
        MinWidth = 320;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        AutomationProperties.SetAutomationId(this, "PropertyGridDialog");

        Grid = new PropertyGrid { EditMode = PropertyEditMode.Staged, CommitScope = commitScope, SelectedObject = target };
        Grid.Margin = new Thickness(8, 8, 8, 0);

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetAutomationId(ok, "PropertyGridDialog_OK");
        AutomationProperties.SetAutomationId(cancel, "PropertyGridDialog_Cancel");
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => Close(false);

        _status.Foreground = GridTheme.ErrorBrush;
        _status.FontSize = GridTheme.FontSize;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
        AutomationProperties.SetAutomationId(_status, "PropertyGridDialog_Status");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var footer = new DockPanel { Margin = new Thickness(8, 8, 8, 8), LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        footer.Children.Add(_status);

        _root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        _root.Children.Add(footer);
        _root.Children.Add(Grid);
        Content = _root;
    }

    /// <summary>
    ///     Occurs when the close of the dialog is confirmed (OK, Cancel or the window's close button), before the grid leaves
    ///     the window. It is not raised when a handler of <c>Closing</c> cancels the close.
    /// </summary>
    /// <remarks>
    ///     Internal: the Plexus adapter layer uses it to dispose its preview context before the grid's session is replaced by
    ///     the window's teardown, so the preview is never notified by a dialog that is closing.
    /// </remarks>
    internal event EventHandler? CloseConfirmed;

    /// <summary>
    ///     Gets the grid that shows and edits the object.
    /// </summary>
    public PropertyGrid Grid { get; }

    /// <summary>
    ///     Handles the dialog closing (OK, Cancel or the window's close button); when no handler cancels the close, the dialog's preview, if any, is released before the grid leaves the window.
    /// </summary>
    /// <param name="e">The closing event data.</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            CloseConfirmed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Shows the dialog modally.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="target">The object whose properties are edited.</param>
    /// <param name="title">The window title; <see langword="null"/> uses <see cref="GetDefaultTitle"/>.</param>
    /// <param name="commitScope">An optional hook that wraps the commit's writes, for example in an undo transaction.</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user pressed OK and the edits were committed, and
    ///     <see langword="false"/> when the user cancelled (the edits are discarded).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="target"/> is <see langword="null"/>
    ///     (checked before a window is created).</exception>
    public static Task<bool> ShowAsync(Window owner, object target, string? title = null, Action<Action>? commitScope = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(target);
        return new PropertyGridDialog(target, title, commitScope).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///     Gets the default dialog title: <c>"{DisplayName or type name} - Properties"</c>.
    /// </summary>
    /// <param name="target">The edited object.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static string GetDefaultTitle(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var name = TypeDescriptor.GetAttributes(target).OfType<DisplayNameAttribute>().FirstOrDefault() is { DisplayName.Length: > 0 } attribute
            ? attribute.DisplayName
            : target.GetType().Name;
        return name + " - Properties";
    }

    /// <summary>
    ///     Shows a preview panel next to the grid (the hook behind the node properties dialog's live preview).
    /// </summary>
    /// <remarks>
    ///     The preview is wrapped in a border with the automation id <c>PropertyGridDialog_Preview</c> (and the automation
    ///     name <c>Preview</c>, exposed to UI Automation as a content group) and a themed separator line, and docked before the grid so the grid still fills the rest of
    ///     the dialog. It is internal because the generic dialog has no notion of what a preview shows; the Plexus adapter
    ///     layer supplies one. It uses only Avalonia types so the generic layer stays free of Plexus types. Calling it again
    ///     replaces the previous preview. Use it from the UI thread.
    /// </remarks>
    /// <param name="preview">The preview control.</param>
    /// <param name="dock">
    ///     Where the preview goes: <see cref="Dock.Right"/> or <see cref="Dock.Left"/> (side by side, <paramref name="size"/>
    ///     is the width) or <see cref="Dock.Top"/> or <see cref="Dock.Bottom"/> (stacked, <paramref name="size"/> is the
    ///     height).
    /// </param>
    /// <param name="size">The width or height of the preview, in device-independent pixels; positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="preview"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is not a positive finite number.</exception>
    internal void SetPreview(Control preview, Dock dock, double size)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!double.IsFinite(size) || size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The preview size must be a positive finite number.");
        }

        // Remove a previous preview so the dialog never shows two
        if (_preview is { } previous)
        {
            previous.Child = null;
            _root.Children.Remove(previous);
        }

        // Wrap the preview in a themed border whose separator line faces the grid
        var horizontal = dock is Dock.Left or Dock.Right;
        var border = new Border
        {
            Child = preview,
            Margin = new Thickness(8, 8, 8, 0),
            Padding = dock switch
            {
                Dock.Right => new Thickness(8, 0, 0, 0),
                Dock.Left => new Thickness(0, 0, 8, 0),
                Dock.Top => new Thickness(0, 0, 0, 8),
                _ => new Thickness(0, 8, 0, 0),
            },
            BorderThickness = dock switch
            {
                Dock.Right => new Thickness(1, 0, 0, 0),
                Dock.Left => new Thickness(0, 0, 1, 0),
                Dock.Top => new Thickness(0, 0, 0, 1),
                _ => new Thickness(0, 1, 0, 0),
            },
        };
        if (horizontal)
        {
            border.Width = size;
        }
        else
        {
            border.Height = size;
        }

        GridTheme.BindBrush(border, Border.BorderBrushProperty, GridTheme.LineKey);
        AutomationProperties.SetAutomationId(border, "PropertyGridDialog_Preview");
        AutomationProperties.SetName(border, "Preview");
        AutomationProperties.SetAccessibilityView(border, AccessibilityView.Content);
        AutomationProperties.SetControlTypeOverride(border, AutomationControlType.Group);
        DockPanel.SetDock(border, dock);

        // Dock it before the grid: the last child of the root fills the remaining space
        _root.Children.Insert(_root.Children.IndexOf(Grid), border);
        _preview = border;
    }

    /// <summary>
    ///     Commits the edits (the OK button) and closes the dialog with <see langword="true"/>. Nothing is committed and
    ///     the dialog stays open while a value is invalid or the commit reports a failure; the reason is shown.
    /// </summary>
    /// <returns><see langword="true"/> when the edits were committed and the dialog closed.</returns>
    public bool Accept()
    {
        if (Grid.Session is { } session)
        {
            if (session.HasErrors)
            {
                var first = session.GetErrors().First();
                if (session.FindItem(first.Key) is { } item)
                {
                    Grid.SelectItem(item);
                }

                _status.Text = first.Value;
                return false;
            }

            var result = session.Commit();
            if (!result.Succeeded)
            {
                // Show the failing row, then the reason
                if (result.Failed.Count > 0)
                {
                    Grid.SelectItem(result.Failed[0].Item);
                }

                _status.Text = result.Error ?? result.Failed.Select(f => f.Error).FirstOrDefault(e => e is not null) ?? session.GetErrors().Values.FirstOrDefault() ?? "The changes could not be applied.";
                return false;
            }
        }

        Close(true);
        return true;
    }
}
