using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
using DemaConsulting.Plexus.Demo.Model;

namespace DemaConsulting.Plexus.Demo.Gui.Inspectors;

/// <summary>
///     The demo's custom node inspectors: live previews for the oscillator, envelope and filter properties dialogs, and the
///     oscillator's waveform tooltip.
/// </summary>
/// <remarks>
///     This is the pattern a host migrating custom inspectors follows: the library's
///     <see cref="NodePropertiesDialog"/> keeps the staged grid, validation and one-undo-entry OK, and the host only
///     supplies a preview factory. Each preview reads the staged values through <see cref="NodePreviewContext"/> (never
///     the node, never writing), turns them into a <see cref="PlotData"/> with the model's math and redraws on
///     <see cref="NodePreviewContext.Changed"/>, so it follows every edit, reset and cancel before OK. Use it from the UI
///     thread.
/// </remarks>
internal static class AudioInspectors
{
    /// <summary>
    ///     The height of the plot in a preview, in device-independent pixels.
    /// </summary>
    private const double PlotHeight = 260;

    /// <summary>
    ///     The size of the waveform thumbnail in the oscillator tooltip, in device-independent pixels.
    /// </summary>
    private static readonly Size ThumbnailSize = new(160, 60);

    /// <summary>
    ///     Gets the preview factory for a node's properties dialog.
    /// </summary>
    /// <param name="node">The node whose dialog is opened.</param>
    /// <returns>The factory for an oscillator, an envelope or a filter; <see langword="null"/> for every other node (the plain dialog).</returns>
    internal static Func<NodePreviewContext, Control>? GetPreview(Node node) => node.Type.Key switch
    {
        DemoNodeTypes.Oscillator => CreateOscillatorPreview,
        DemoNodeTypes.Envelope => CreateEnvelopePreview,
        DemoNodeTypes.Filter => CreateFilterPreview,
        _ => null,
    };

    /// <summary>
    ///     Creates the oscillator preview: two cycles of the signal at the staged waveform, frequency, amplitude, phase and
    ///     DC offset.
    /// </summary>
    /// <param name="context">The dialog's preview context.</param>
    /// <returns>The preview control.</returns>
    internal static Control CreateOscillatorPreview(NodePreviewContext context) => CreatePreview(
        context,
        () => AudioPreview.Describe(OscillatorSettings.Read(context.GetPropertyValue)),
        "Two cycles of the signal against time. The plot follows every setting as you edit; OK applies, Cancel discards.",
        () => AudioPreview.Oscillator(OscillatorSettings.Read(context.GetPropertyValue)));

    /// <summary>
    ///     Creates the envelope preview: the ADSR curve of the staged times and sustain level.
    /// </summary>
    /// <param name="context">The dialog's preview context.</param>
    /// <returns>The preview control.</returns>
    internal static Control CreateEnvelopePreview(NodePreviewContext context) => CreatePreview(
        context,
        () => "ADSR envelope",
        $"Attack, decay to the sustain level, an illustrative {AudioPreview.SustainHoldMs:0} ms sustain hold, then release.",
        () => AudioPreview.Envelope(EnvelopeSettings.Read(context.GetPropertyValue)));

    /// <summary>
    ///     Creates the filter preview: the magnitude response of the staged type and cutoff.
    /// </summary>
    /// <param name="context">The dialog's preview context.</param>
    /// <returns>The preview control.</returns>
    internal static Control CreateFilterPreview(NodePreviewContext context) => CreatePreview(
        context,
        () =>
        {
            var settings = FilterSettings.Read(context.GetPropertyValue);
            return $"{settings.Type} at {settings.Cutoff.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} Hz";
        },
        "Magnitude response of a second-order filter from 20 Hz to 20 kHz; the dashed line marks the cutoff.",
        () => AudioPreview.Filter(FilterSettings.Read(context.GetPropertyValue)));

    /// <summary>
    ///     Creates the oscillator's hover tooltip: a waveform thumbnail of its current values above its name and a short
    ///     description such as <c>Sine, 440 Hz</c>.
    /// </summary>
    /// <param name="node">The oscillator node.</param>
    /// <returns>The tooltip content.</returns>
    internal static Control CreateOscillatorToolTip(Node node)
    {
        var settings = OscillatorSettings.FromNode(node);
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new SignalPlot
        {
            IsCompact = true,
            Width = ThumbnailSize.Width,
            Height = ThumbnailSize.Height,
            Data = AudioPreview.Oscillator(settings),
        });
        panel.Children.Add(new TextBlock { Text = node.Name, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = AudioPreview.Describe(settings) });
        return panel;
    }

    /// <summary>
    ///     Builds a preview panel (a heading, the plot and a note) and keeps it in step with the staged values.
    /// </summary>
    /// <param name="context">The dialog's preview context.</param>
    /// <param name="heading">Computes the heading from the staged values.</param>
    /// <param name="note">The explanation under the plot.</param>
    /// <param name="plot">Computes the plot from the staged values.</param>
    /// <returns>The preview control.</returns>
    private static StackPanel CreatePreview(NodePreviewContext context, Func<string> heading, string note, Func<PlotData> plot)
    {
        var title = new TextBlock { FontWeight = FontWeight.SemiBold, FontSize = 14 };
        var chart = new SignalPlot { Height = PlotHeight }.WithId(AutomationIds.InspectorPlot);
        var text = new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };

        // Redraw from the staged values now and after every edit, reset or cancel; the context stops when the dialog closes
        void Redraw()
        {
            title.Text = heading();
            chart.Data = plot();
        }

        context.Changed += (_, _) => Redraw();
        Redraw();

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(title);
        panel.Children.Add(chart);
        panel.Children.Add(text);
        return panel;
    }
}
