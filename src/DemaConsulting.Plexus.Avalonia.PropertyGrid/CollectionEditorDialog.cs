using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid;

/// <summary>
///     A window that hosts a <see cref="CollectionEditorView"/> with OK and Cancel buttons. OK applies the arranged
///     list through the <see cref="CollectionEditor"/> working copy into the owning session; Cancel discards it.
/// </summary>
/// <remarks>
///     Shown by a <see cref="PropertyGrid"/> when the user presses the "…" button of a collection row; hosts can also
///     open it directly. When the owning session is staged the applied list is a pending edit that the host commits
///     with the rest; when it is live the collection is written at once.
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var dialog = new CollectionEditorDialog(grid.Session!, grid.Session!.FindItem("Tags")!);
///     bool applied = await dialog.ShowDialog<bool>(owner);
///     ]]></code>
/// </example>
public sealed class CollectionEditorDialog : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CollectionEditorDialog"/> class.
    /// </summary>
    /// <param name="session">The session that owns the collection property.</param>
    /// <param name="item">The collection property.</param>
    /// <param name="itemFormatter">Produces the text of an element; <see langword="null"/> uses the defaults.</param>
    /// <param name="editors">The editor registry of the nested grid; <see langword="null"/> uses the shared one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="item"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The item is not a collection.</exception>
    public CollectionEditorDialog(
        PropertyEditSession session,
        PropertyItem item,
        Func<object?, string>? itemFormatter = null,
        PropertyEditorRegistry<PropertyEditorFactory>? editors = null)
    {
        View = new CollectionEditorView(session, item, itemFormatter, editors);
        Title = item.Label + " - Collection Editor";
        Width = 720;
        Height = 480;
        MinWidth = 520;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        AutomationProperties.SetAutomationId(this, "CollectionEditorDialog");

        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetAutomationId(ok, "CollectionEditorDialog_OK");
        AutomationProperties.SetAutomationId(cancel, "CollectionEditorDialog_Cancel");
        ok.Click += (_, _) => Accept();
        cancel.Click += (_, _) => Close(false);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10, 0, 10, 10),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(View);
        Content = root;
    }

    /// <summary>
    ///     Gets the embedded editor view.
    /// </summary>
    public CollectionEditorView View { get; }

    /// <summary>
    ///     Applies the arranged list (the OK button) and closes the dialog with <see langword="true"/>; when the owning
    ///     session rejects the list the dialog stays open and shows the reason.
    /// </summary>
    /// <returns><see langword="true"/> when the list was applied and the dialog closed.</returns>
    public bool Accept()
    {
        if (!View.Apply().Succeeded)
        {
            return false;
        }

        Close(true);
        return true;
    }
}
