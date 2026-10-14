using Avalonia.Controls;
using Avalonia.Layout;
using DemaConsulting.Plexus.Avalonia.PropertyGrid;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The custom property editors of the demo: a slider for whole-number and fractional properties that are annotated
///     with <c>[UIHint("slider")]</c> (host data) or carry the editor hint <c>slider</c> (node properties), and a 2-to-8
///     number box for the <c>Inputs</c> property of the AND, OR and XOR gates (editor hint <c>gate-inputs</c>).
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
    internal static void Register()
    {
        PropertyGrid.DefaultEditors.Register(SliderKey, CreateSlider);
        PropertyGrid.DefaultEditors.Register(Model.DemoNodeTypes.GateInputsEditorHint, CreateGateInputs);
    }

    /// <summary>
    ///     Creates the number box that edits how many inputs a gate has.
    /// </summary>
    /// <remarks>
    ///     The box only offers 2 to 8 in steps of one; the gate's port provider also clamps any other value. Each change is
    ///     written through the context, so in the docked panel one step is one undo entry, which also adds or removes the
    ///     gate's inputs (and the wires on removed inputs).
    /// </remarks>
    /// <param name="context">The row's editor context.</param>
    /// <returns>The editor control.</returns>
    internal static Control CreateGateInputs(PropertyEditorContext context)
    {
        var box = new NumericUpDown
        {
            Minimum = Model.DemoNodeTypes.MinGateInputs,
            Maximum = Model.DemoNodeTypes.MaxGateInputs,
            Increment = 1,
            FormatString = "0",
            IsEnabled = !context.IsReadOnly,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60,
        };
        var updating = false;

        void Show()
        {
            updating = true;
            var value = Convert.ToInt32(context.Value ?? Model.DemoNodeTypes.MinGateInputs, System.Globalization.CultureInfo.InvariantCulture);
            box.Value = Math.Clamp(value, Model.DemoNodeTypes.MinGateInputs, Model.DemoNodeTypes.MaxGateInputs);
            updating = false;
        }

        box.ValueChanged += (_, e) =>
        {
            if (!updating && e.NewValue is { } next)
            {
                context.SetValue((int)next);
                Show();
            }
        };
        context.Refreshed += (_, _) => Show();
        Show();
        return box;
    }

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
