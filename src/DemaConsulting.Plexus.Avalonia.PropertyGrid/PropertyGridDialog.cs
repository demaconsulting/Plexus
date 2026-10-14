using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
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
///         The dialog derives from Avalonia's <c>Window</c>: <c>Show()</c>, <c>ShowDialog()</c>, <c>Close()</c>, <c>Title</c>, <c>Width</c> and the rest of the window API are available but are not
///         repeated in the member tables of this page.
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

        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(Grid);
        Content = root;
    }

    /// <summary>
    ///     Gets the grid that shows and edits the object.
    /// </summary>
    public PropertyGrid Grid { get; }

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
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
    public static Task<bool> ShowAsync(Window owner, object target, string? title = null, Action<Action>? commitScope = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
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
                if (result.Failed.FirstOrDefault() is { } failed)
                {
                    Grid.SelectItem(failed.Item);
                }

                _status.Text = result.Error ?? result.Failed.Select(f => f.Error).FirstOrDefault(e => e is not null) ?? session.GetErrors().Values.FirstOrDefault() ?? "The changes could not be applied.";
                return false;
            }
        }

        Close(true);
        return true;
    }
}
