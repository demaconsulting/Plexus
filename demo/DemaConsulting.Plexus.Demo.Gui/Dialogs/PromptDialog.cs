using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace DemaConsulting.Plexus.Demo.Gui.Dialogs;

/// <summary>
///     A modal dialog that asks for one line of text (a page name, a group title, annotation text).
/// </summary>
internal sealed class PromptDialog : Window
{
    /// <summary>
    ///     The text box.
    /// </summary>
    private readonly TextBox _input;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PromptDialog"/> class.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="label">The caption above the text box.</param>
    /// <param name="initial">The initial text.</param>
    public PromptDialog(string title, string label, string initial)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 340;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowPlacement.KeepOnScreen(this);
        this.WithId(AutomationIds.PromptDialog);

        _input = new TextBox { Text = initial, MinWidth = 300, AcceptsReturn = false }.WithId(AutomationIds.PromptText);
        var ok = new Button { Content = "OK", IsDefault = true }.WithId(AutomationIds.PromptOk);
        var cancel = new Button { Content = "Cancel", IsCancel = true }.WithId(AutomationIds.PromptCancel);
        ok.Click += (_, _) => Close(_input.Text ?? string.Empty);
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 6,
            Children = { new TextBlock { Text = label }, _input, Ui.ButtonRow(ok, cancel) },
        };
        Opened += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    /// <summary>
    ///     Asks the user for a line of text.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="title">The window title.</param>
    /// <param name="label">The caption above the text box.</param>
    /// <param name="initial">The initial text.</param>
    /// <returns>The entered text, or null when the user cancelled.</returns>
    public static Task<string?> AskAsync(Window owner, string title, string label, string initial) =>
        new PromptDialog(title, label, initial).ShowDialog<string?>(owner);
}
