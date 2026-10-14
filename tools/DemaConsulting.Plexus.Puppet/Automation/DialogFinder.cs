using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DemaConsulting.Plexus.Puppet.Automation;

/// <summary>
///     One control of a dialog, as <c>dialogs</c> lists it.
/// </summary>
/// <param name="AutomationId">The AutomationId, or <see langword="null"/> when there is none.</param>
/// <param name="Name">The control's name (its caption or text).</param>
/// <param name="Kind">The kind: <c>button</c>, <c>edit</c>, <c>checkbox</c> or <c>combo</c>.</param>
/// <param name="IsEnabled">Whether the control accepts input.</param>
internal sealed record DialogControl(string? AutomationId, string Name, string Kind, bool IsEnabled);

/// <summary>
///     A dialog and its controls.
/// </summary>
/// <param name="Title">The window title.</param>
/// <param name="Controls">The visible controls.</param>
internal sealed record DialogDescription(string Title, IReadOnlyList<DialogControl> Controls);

/// <summary>
///     Finds and describes the dialogs (extra top-level windows) of an instance.
/// </summary>
internal static class DialogFinder
{
    /// <summary>
    ///     Lists the open dialogs: the titled, visible top-level windows of the process other than the main window.
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <returns>Each dialog's window and description.</returns>
    internal static IReadOnlyList<(AutomationElement Window, DialogDescription Description)> Snapshot(AttachContext context)
    {
        var result = new List<(AutomationElement, DialogDescription)>();
        foreach (var dialog in context.Roots().Skip(1))
        {
            var title = UiActions.Safe(() => dialog.Properties.Name.ValueOrDefault, string.Empty);
            if (string.IsNullOrWhiteSpace(title) || UiActions.Safe(() => dialog.Properties.IsOffscreen.ValueOrDefault, false))
            {
                continue;
            }

            result.Add((dialog, Describe(dialog, title)));
        }

        return result;
    }

    /// <summary>
    ///     Lists every visible window of the instance other than the main window: dialogs and open pop-ups (a context menu
    ///     has no title, so it is not a <see cref="Snapshot"/> dialog but a screenshot should still show it).
    /// </summary>
    /// <param name="context">The attached instance.</param>
    /// <returns>The windows.</returns>
    internal static IReadOnlyList<AutomationElement> VisibleWindows(AttachContext context) =>
        [.. context.Roots().Skip(1).Where(w => !UiActions.Safe(() => w.Properties.IsOffscreen.ValueOrDefault, false))];

    /// <summary>
    ///     Formats the dialogs as the text <c>dialogs</c> prints.
    /// </summary>
    /// <param name="pid">The instance's process id.</param>
    /// <param name="dialogs">The dialogs.</param>
    /// <returns>The text.</returns>
    internal static string Format(int pid, IReadOnlyList<DialogDescription> dialogs)
    {
        if (dialogs.Count == 0)
        {
            return $"NO_DIALOGS pid={pid}";
        }

        return string.Join(
            Environment.NewLine,
            dialogs.Select((dialog, index) =>
                $"DIALOG pid={pid} index={index} title=\"{dialog.Title}\"{Environment.NewLine}" +
                string.Join(Environment.NewLine, dialog.Controls.Select(c =>
                    $"  {c.Kind} id=\"{c.AutomationId ?? string.Empty}\" name=\"{c.Name}\" enabled={c.IsEnabled}"))));
    }

    /// <summary>
    ///     Lists the buttons, edits, check boxes and combo boxes of a dialog.
    /// </summary>
    /// <param name="dialog">The dialog window.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>The description.</returns>
    private static DialogDescription Describe(AutomationElement dialog, string title)
    {
        var elements = UiActions.Safe(
            () => dialog.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.Button)
                    .Or(cf.ByControlType(ControlType.Edit))
                    .Or(cf.ByControlType(ControlType.CheckBox))
                    .Or(cf.ByControlType(ControlType.ComboBox))),
            []);

        var controls = new List<DialogControl>();
        foreach (var element in elements)
        {
            // Title-bar buttons (minimize, close) are chrome, not part of the dialog's content
            if (UiActions.Safe(() => element.IsOffscreen, false) || UiActions.Safe(() => element.Parent?.ControlType == ControlType.TitleBar, false))
            {
                continue;
            }

            var kind = element.ControlType switch
            {
                ControlType.Edit => "edit",
                ControlType.CheckBox => "checkbox",
                ControlType.ComboBox => "combo",
                _ => "button",
            };
            var id = UiActions.Safe(() => element.AutomationId, string.Empty);
            var name = UiActions.Safe(() => element.Name, string.Empty);
            controls.Add(new DialogControl(string.IsNullOrWhiteSpace(id) ? null : id, name, kind, UiActions.Safe(() => element.IsEnabled, true)));
        }

        return new DialogDescription(title, controls);
    }
}
