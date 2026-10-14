using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DemaConsulting.Plexus.Demo.Gui.Dialogs;

/// <summary>
///     The button a user chose in a <see cref="MessageDialog"/>.
/// </summary>
internal enum DialogChoice
{
    /// <summary>The dialog was closed without choosing.</summary>
    None,

    /// <summary>OK.</summary>
    Ok,

    /// <summary>Yes (or "Save" in a save prompt).</summary>
    Yes,

    /// <summary>No (or "Discard" in a save prompt).</summary>
    No,

    /// <summary>Cancel.</summary>
    Cancel,
}

/// <summary>
///     A small modal message box with one to three buttons.
/// </summary>
internal sealed class MessageDialog : Window
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MessageDialog"/> class.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="message">The message text.</param>
    /// <param name="choices">The buttons to offer, left to right.</param>
    public MessageDialog(string title, string message, params (DialogChoice Choice, string Caption)[] choices)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 320;
        MaxWidth = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowPlacement.KeepOnScreen(this);
        this.WithId(AutomationIds.MessageDialog);

        var text = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }.WithId(AutomationIds.MessageText);
        var buttons = choices.Select(c =>
        {
            var button = new Button { Content = c.Caption }.WithId(AutomationIds.MessageButton(c.Choice.ToString()));
            button.Click += (_, _) => Close(c.Choice);
            button.IsDefault = c.Choice is DialogChoice.Ok or DialogChoice.Yes;
            button.IsCancel = c.Choice is DialogChoice.Cancel;
            return button;
        }).ToArray();

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Children = { text, Ui.ButtonRow(buttons) },
        };
    }

    /// <summary>
    ///     Shows an informational or error message with an OK button.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="title">The window title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    public static async Task ShowAsync(Window owner, string title, string message) =>
        await new MessageDialog(title, message, (DialogChoice.Ok, "OK")).ShowDialog<DialogChoice>(owner).ConfigureAwait(true);

    /// <summary>
    ///     Asks whether unsaved changes should be saved, discarded, or the action cancelled.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="documentName">The name of the document with unsaved changes.</param>
    /// <returns><see cref="DialogChoice.Yes"/> to save, <see cref="DialogChoice.No"/> to discard, otherwise <see cref="DialogChoice.Cancel"/>.</returns>
    public static async Task<DialogChoice> ConfirmDiscardAsync(Window owner, string documentName)
    {
        var dialog = new MessageDialog(
            "Unsaved changes",
            $"'{documentName}' has unsaved changes. Save them first?",
            (DialogChoice.Yes, "Save"),
            (DialogChoice.No, "Discard"),
            (DialogChoice.Cancel, "Cancel"));
        var choice = await dialog.ShowDialog<DialogChoice>(owner).ConfigureAwait(true);
        return choice is DialogChoice.Yes or DialogChoice.No ? choice : DialogChoice.Cancel;
    }
}
