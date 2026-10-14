using Avalonia.Controls;
using Avalonia.Layout;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The custom property editors of the demo: a slider for whole-number and fractional properties that are annotated
///     with <c>[UIHint("slider")]</c> (host data) or carry the editor hint <c>slider</c> (node properties).
/// </summary>
/// <remarks>
///     Editors are registered once for the whole application in <see cref="PropertyGrid.DefaultEditors"/>, which the
///     properties dialog and the docked panel both use. The editor never writes to the edited object itself; it goes
///     through <see cref="PropertyEditorContext.SetValue"/>, so staged dialogs, validation, errors and undo all apply.
/// </remarks>
internal static class DemoEditors
{
    /// <summary>
    ///     The editor key the annotations and hints use.
    /// </summary>
    internal const string SliderKey = "slider";

    /// <summary>
    ///     The largest value of the slider (the smallest is 0): the demo's sliders are percentages.
    /// </summary>
    private const double SliderMaximum = 100;

    /// <summary>
    ///     Registers the demo's editors in the shared registry of the property grid; repeated calls are harmless.
    /// </summary>
    internal static void Register() => PropertyGrid.DefaultEditors.Register(SliderKey, CreateSlider);

    /// <summary>
    ///     Creates the slider editor of a row.
    /// </summary>
    /// <param name="context">The row's editor context.</param>
    /// <returns>The editor control.</returns>
    internal static Control CreateSlider(PropertyEditorContext context)
    {
        var integer = (Nullable.GetUnderlyingType(context.Item.PropertyType) ?? context.Item.PropertyType) != typeof(double);
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = SliderMaximum,
            SmallChange = 1,
            LargeChange = 10,
            IsEnabled = !context.IsReadOnly,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60,
        };
        var label = new TextBlock { Width = 34, TextAlignment = global::Avalonia.Media.TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new global::Avalonia.Thickness(6, 0, 4, 0) };
        var updating = false;

        void Show()
        {
            updating = true;
            var value = Convert.ToDouble(context.Value ?? 0.0, System.Globalization.CultureInfo.InvariantCulture);
            slider.Value = Math.Clamp(value, 0, SliderMaximum);
            label.Text = integer ? ((int)Math.Round(value)).ToString(System.Globalization.CultureInfo.InvariantCulture) : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            updating = false;
        }

        slider.ValueChanged += (_, e) =>
        {
            if (updating)
            {
                return;
            }

            var next = integer ? (object)(int)Math.Round(e.NewValue) : Math.Round(e.NewValue, 1);
            context.SetValue(next);
            Show();
        };
        context.Refreshed += (_, _) => Show();
        Show();

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(label, 1);
        root.Children.Add(slider);
        root.Children.Add(label);
        return root;
    }
}
