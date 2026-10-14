using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     Small helpers that keep the code-built user interface readable.
/// </summary>
internal static class Ui
{
    /// <summary>
    ///     Sets the AutomationId of a control.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="control">The control.</param>
    /// <param name="id">The AutomationId (see <see cref="AutomationIds"/>).</param>
    /// <returns>The same control, for chaining.</returns>
    public static T WithId<T>(this T control, string id)
        where T : Control
    {
        AutomationProperties.SetAutomationId(control, id);
        return control;
    }

    /// <summary>
    ///     Creates a menu item.
    /// </summary>
    /// <param name="header">The caption; an underscore marks the access key.</param>
    /// <param name="id">The AutomationId.</param>
    /// <param name="onClick">What to do when it is chosen; null for a parent item.</param>
    /// <param name="gesture">The shortcut shown beside the caption (display only unless <paramref name="hotKey"/> is true).</param>
    /// <param name="hotKey">True to make the gesture an active window-wide shortcut.</param>
    /// <returns>The menu item.</returns>
    public static MenuItem Item(string header, string id, Action? onClick = null, KeyGesture? gesture = null, bool hotKey = false)
    {
        var item = new MenuItem { Header = header };
        AutomationProperties.SetAutomationId(item, id);
        if (gesture is not null)
        {
            item.InputGesture = gesture;
            if (hotKey)
            {
                item.HotKey = gesture;
            }
        }

        if (onClick is not null)
        {
            item.Click += (_, _) => onClick();
        }

        return item;
    }

    /// <summary>
    ///     Creates a keyboard shortcut.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers (Control by default).</param>
    /// <returns>The gesture.</returns>
    public static KeyGesture Keys(Key key, KeyModifiers modifiers = KeyModifiers.Control) => new(key, modifiers);

    /// <summary>
    ///     Creates a horizontal row of buttons aligned to the right.
    /// </summary>
    /// <param name="buttons">The buttons.</param>
    /// <returns>The panel.</returns>
    public static StackPanel ButtonRow(params Button[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        foreach (var button in buttons)
        {
            button.MinWidth = 80;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            row.Children.Add(button);
        }

        return row;
    }
}
