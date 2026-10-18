using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AutomationProperties = Avalonia.Automation.AutomationProperties;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Helpers that host controls in a headless window, find controls by automation id, and capture screenshots.
/// </summary>
internal static class GridHarness
{
    /// <summary>
    ///     Shows a control in a headless window and lets it lay out.
    /// </summary>
    /// <param name="content">The control.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="variant">The theme variant, or <see langword="null"/> for the default.</param>
    /// <returns>The window.</returns>
    internal static Window Host(Control content, double width = 420, double height = 560, ThemeVariant? variant = null)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        if (variant is not null)
        {
            window.RequestedThemeVariant = variant;
        }

        window.Show();
        Pump(window);
        return window;
    }

    /// <summary>
    ///     Runs pending UI jobs, layout and a render tick.
    /// </summary>
    /// <param name="window">The window.</param>
    internal static void Pump(TopLevel window)
    {
        Dispatcher.UIThread.RunJobs();
        if (window is Window w)
        {
            w.UpdateLayout();
        }

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Finds a control by automation id.
    /// </summary>
    /// <typeparam name="T">The expected control type.</typeparam>
    /// <param name="root">The root.</param>
    /// <param name="id">The automation id.</param>
    /// <returns>The control, or <see langword="null"/>.</returns>
    internal static T? Find<T>(Visual root, string id)
        where T : Control =>
        root.GetSelfAndVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);

    /// <summary>
    ///     Finds a control by automation id and fails when it is missing.
    /// </summary>
    /// <typeparam name="T">The expected control type.</typeparam>
    /// <param name="root">The root.</param>
    /// <param name="id">The automation id.</param>
    /// <returns>The control.</returns>
    internal static T Get<T>(Visual root, string id)
        where T : Control =>
        Find<T>(root, id) ?? throw new InvalidOperationException($"No {typeof(T).Name} with automation id '{id}'.");

    /// <summary>
    ///     Finds the first descendant of a type (or the control itself).
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="root">The root.</param>
    /// <returns>The control.</returns>
    internal static T Inner<T>(Visual root)
        where T : Control =>
        root.GetSelfAndVisualDescendants().OfType<T>().First();

    /// <summary>
    ///     Gets the editor root of a property row.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="path">The item path.</param>
    /// <returns>The editor control.</returns>
    internal static Control Editor(PropertyGrid grid, string path) => Get<Control>(grid, "PropertyGrid_Editor_" + path);

    /// <summary>
    ///     Gets the row view of a property row.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <param name="path">The item path.</param>
    /// <returns>The row view.</returns>
    internal static PropertyRowView Row(PropertyGrid grid, string path) =>
        grid.GetVisualDescendants().OfType<PropertyRowView>().First(r => r.Item!.Path == path);

    /// <summary>
    ///     Presses and releases a key.
    /// </summary>
    /// <param name="window">The window that has the focus.</param>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers.</param>
    internal static void Press(TopLevel window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump(window);
    }

    /// <summary>
    ///     Raises the click of a button.
    /// </summary>
    /// <param name="button">The button.</param>
    internal static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    ///     Gets the element that has the keyboard focus.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The focused element, or <see langword="null"/>.</returns>
    internal static object? Focused(TopLevel window) => window.FocusManager?.GetFocusedElement();

    /// <summary>
    ///     Gets the automation ids of the rows (property and category) in display order.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The ids.</returns>
    internal static List<string> RowIds(PropertyGrid grid) =>
        grid.GetVisualDescendants().OfType<GridRowView>().Select(r => AutomationProperties.GetAutomationId(r) ?? string.Empty).ToList();

    /// <summary>
    ///     Gets the labels of the property rows in display order.
    /// </summary>
    /// <param name="grid">The grid.</param>
    /// <returns>The paths.</returns>
    internal static List<string> PropertyPaths(PropertyGrid grid) =>
        grid.GetVisualDescendants().OfType<PropertyRowView>().Select(r => r.Item!.Path).ToList();

    /// <summary>
    ///     Captures the window as a PNG under the temp folder (<c>plexus-pg</c>).
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The file name without extension.</param>
    /// <returns>The path of the PNG.</returns>
    internal static string Capture(Window window, string name)
    {
        Pump(window);
        var folder = Path.Join(Path.GetTempPath(), "plexus-pg");
        Directory.CreateDirectory(folder);
        var path = Path.Join(folder, name + ".png");
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(path, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        return path;
    }
}
