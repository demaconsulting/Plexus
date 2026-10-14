using System.Globalization;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     The settings of an oscillator node that its preview and tooltip draw.
/// </summary>
/// <param name="Waveform">The waveform, one of <see cref="DemoNodeTypes.WaveformChoices"/>.</param>
/// <param name="Frequency">The frequency in Hz.</param>
/// <param name="Amplitude">The peak level, nominally 0 to 1.</param>
/// <param name="Phase">The start phase in degrees.</param>
/// <param name="DcOffset">The constant added to the wave.</param>
public sealed record OscillatorSettings(string Waveform, double Frequency, double Amplitude, double Phase, double DcOffset)
{
    /// <summary>
    ///     Gets the settings of a new oscillator (the property defaults).
    /// </summary>
    public static OscillatorSettings Default { get; } = new("Sine", 440.0, 1.0, 0.0, 0.0);

    /// <summary>
    ///     Reads the settings through a property reader, such as a node's property bag or the staged values of a properties
    ///     dialog.
    /// </summary>
    /// <remarks>
    ///     A missing value, a value of the wrong kind, a number that is not finite or a waveform that is not one of the
    ///     choices falls back to <see cref="Default"/>, so a preview always has something sensible to draw.
    /// </remarks>
    /// <param name="property">Returns the value of a property by name, or <see langword="null"/> when it has none.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    public static OscillatorSettings Read(Func<string, object?> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new OscillatorSettings(
            AudioSettingsReader.Choice(property(DemoNodeTypes.WaveformProperty), DemoNodeTypes.WaveformChoices, Default.Waveform),
            AudioSettingsReader.Number(property(DemoNodeTypes.FrequencyProperty), Default.Frequency),
            AudioSettingsReader.Number(property(DemoNodeTypes.AmplitudeProperty), Default.Amplitude),
            AudioSettingsReader.Number(property(DemoNodeTypes.PhaseProperty), Default.Phase),
            AudioSettingsReader.Number(property(DemoNodeTypes.DcOffsetProperty), Default.DcOffset));
    }

    /// <summary>
    ///     Reads the current settings of an oscillator node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The settings; see <see cref="Read"/> for the fallbacks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static OscillatorSettings FromNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Read(AudioSettingsReader.Of(node));
    }
}

/// <summary>
///     The settings of an envelope (ADSR) node that its preview draws.
/// </summary>
/// <param name="Attack">The attack time in ms.</param>
/// <param name="Decay">The decay time in ms.</param>
/// <param name="Sustain">The sustain level in percent.</param>
/// <param name="Release">The release time in ms.</param>
public sealed record EnvelopeSettings(double Attack, double Decay, double Sustain, double Release)
{
    /// <summary>
    ///     Gets the settings of a new envelope (the property defaults).
    /// </summary>
    public static EnvelopeSettings Default { get; } = new(10, 120, 70, 300);

    /// <summary>
    ///     Reads the settings through a property reader; missing or invalid values fall back to <see cref="Default"/>.
    /// </summary>
    /// <param name="property">Returns the value of a property by name, or <see langword="null"/> when it has none.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    public static EnvelopeSettings Read(Func<string, object?> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new EnvelopeSettings(
            AudioSettingsReader.Number(property(DemoNodeTypes.AttackProperty), Default.Attack),
            AudioSettingsReader.Number(property(DemoNodeTypes.DecayProperty), Default.Decay),
            AudioSettingsReader.Number(property(DemoNodeTypes.SustainProperty), Default.Sustain),
            AudioSettingsReader.Number(property(DemoNodeTypes.ReleaseProperty), Default.Release));
    }

    /// <summary>
    ///     Reads the current settings of an envelope node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The settings; see <see cref="Read"/> for the fallbacks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static EnvelopeSettings FromNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Read(AudioSettingsReader.Of(node));
    }
}

/// <summary>
///     The settings of a filter node that its preview draws.
/// </summary>
/// <param name="Type">The filter type, one of <see cref="DemoNodeTypes.FilterTypeChoices"/>.</param>
/// <param name="Cutoff">The cutoff frequency in Hz.</param>
public sealed record FilterSettings(string Type, double Cutoff)
{
    /// <summary>
    ///     Gets the settings of a new filter (the property defaults).
    /// </summary>
    public static FilterSettings Default { get; } = new("Low-pass", 1200.0);

    /// <summary>
    ///     Reads the settings through a property reader; missing or invalid values fall back to <see cref="Default"/>.
    /// </summary>
    /// <param name="property">Returns the value of a property by name, or <see langword="null"/> when it has none.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    public static FilterSettings Read(Func<string, object?> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return new FilterSettings(
            AudioSettingsReader.Choice(property(DemoNodeTypes.FilterTypeProperty), DemoNodeTypes.FilterTypeChoices, Default.Type),
            AudioSettingsReader.Number(property(DemoNodeTypes.CutoffProperty), Default.Cutoff));
    }

    /// <summary>
    ///     Reads the current settings of a filter node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The settings; see <see cref="Read"/> for the fallbacks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static FilterSettings FromNode(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return Read(AudioSettingsReader.Of(node));
    }
}

/// <summary>
///     The tolerant conversions shared by the audio settings readers.
/// </summary>
internal static class AudioSettingsReader
{
    /// <summary>
    ///     Makes a property reader over a node's property bag.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>A reader that returns the value of a property, or <see langword="null"/> when the bag has none.</returns>
    internal static Func<string, object?> Of(Node node) => name => node.Properties.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    ///     Converts a property value to a finite number.
    /// </summary>
    /// <param name="value">The value: a number of any kind or invariant-culture text.</param>
    /// <param name="fallback">The result when the value is missing, of another kind or not finite.</param>
    /// <returns>The number.</returns>
    internal static double Number(object? value, double fallback)
    {
        double number;
        switch (value)
        {
            case string text:
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    return fallback;
                }

                break;
            case double or float or decimal or int or long or short or byte or uint or ulong or ushort or sbyte:
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            default:
                return fallback;
        }

        return double.IsFinite(number) ? number : fallback;
    }

    /// <summary>
    ///     Converts a property value to one of a set of choices.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="choices">The allowed values.</param>
    /// <param name="fallback">The result when the value is not one of the choices.</param>
    /// <returns>The choice.</returns>
    internal static string Choice(object? value, IReadOnlyList<string> choices, string fallback) =>
        value is string text && choices.Contains(text) ? text : fallback;
}
