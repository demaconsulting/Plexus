using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DemaConsulting.Plexus.Puppet.Automation;

/// <summary>
///     Safe, pattern-aware actions on UI Automation elements.
/// </summary>
internal static class UiActions
{
    /// <summary>
    ///     Activates an element the way a user would: invoke, select, toggle or, failing those, click.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <exception cref="InvalidOperationException">Thrown when the element is disabled.</exception>
    internal static void Activate(AutomationElement element)
    {
        ScrollIntoView(element);
        if (!Safe(() => element.IsEnabled, true))
        {
            throw new InvalidOperationException($"Cannot activate '{Describe(element)}' because it is disabled.");
        }

        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
        }
        else if (element.Patterns.Toggle.IsSupported)
        {
            element.Patterns.Toggle.Pattern.Toggle();
        }
        else
        {
            element.Click();
        }
    }

    /// <summary>
    ///     Gives an element keyboard focus and waits until it has it.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="timeout">How long to wait; two seconds by default.</param>
    /// <returns><see langword="true"/> when the element holds focus.</returns>
    internal static bool TryFocus(AutomationElement element, TimeSpan? timeout = null)
    {
        ScrollIntoView(element);
        _ = Safe(() => { element.FocusNative(); return true; }, false);
        _ = Safe(() => { element.Focus(); return true; }, false);

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow <= deadline)
        {
            if (Safe(() => element.Properties.HasKeyboardFocus.ValueOrDefault, false))
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return false;
    }

    /// <summary>
    ///     Scrolls an element into view when it supports it.
    /// </summary>
    /// <param name="element">The element.</param>
    internal static void ScrollIntoView(AutomationElement element)
    {
        _ = Safe(
            () =>
            {
                if (element.Patterns.ScrollItem.IsSupported)
                {
                    element.Patterns.ScrollItem.Pattern.ScrollIntoView();
                }

                return true;
            },
            false);
    }

    /// <summary>
    ///     Names an element for a message: its AutomationId, else its name.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The description.</returns>
    internal static string Describe(AutomationElement element)
    {
        var id = Safe(() => element.AutomationId, string.Empty);
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        var name = Safe(() => element.Name, string.Empty);
        return string.IsNullOrWhiteSpace(name) ? "<unnamed>" : name;
    }

    /// <summary>
    ///     Reads the text an element shows: its value, else its name, else the text of its children.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The text.</returns>
    internal static string ReadText(AutomationElement element)
    {
        if (element.Patterns.Value.IsSupported)
        {
            var value = Safe(() => element.Patterns.Value.Pattern.Value.ValueOrDefault, null);
            if (value is not null)
            {
                return value;
            }
        }

        var name = Safe(() => element.Name, string.Empty);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var parts = Safe(
            () => element.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(e => Safe(() => e.Name, string.Empty))
                .Where(text => !string.IsNullOrWhiteSpace(text)),
            []);
        return string.Join(" ", parts);
    }

    /// <summary>
    ///     Runs a UI Automation read that may fail because the element went away.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="read">The read.</param>
    /// <param name="fallback">The value to use when the read throws.</param>
    /// <returns>The value read, or the fallback.</returns>
    internal static T Safe<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return fallback;
        }
    }
}
