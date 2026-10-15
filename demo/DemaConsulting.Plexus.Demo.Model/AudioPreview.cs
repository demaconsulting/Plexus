using System.Globalization;

namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     The math behind the demo's audio inspectors: what an oscillator, an envelope and a filter do with their settings,
///     as plots the GUI draws.
/// </summary>
/// <remarks>
///     <para>
///         Everything here is pure and UI-free so it can be unit-tested; the GUI's plot control only draws the
///         <see cref="PlotData"/> it is given. The models are deliberately simple and illustrative, not a synthesizer:
///         ideal waveforms, a straight-line ADSR envelope and a second-order filter response.
///     </para>
///     <para>
///         Settings are clamped to sane ranges before plotting so a half-typed value never produces an empty or absurd
///         plot: the oscillator frequency to <see cref="MinimumOscillatorFrequency"/>..<see cref="MaximumFrequency"/> Hz, the
///         filter cutoff to <see cref="MinimumFrequency"/>..<see cref="MaximumFrequency"/> Hz (the plotted band), amplitude to 0..1,
///         DC offset to ±<see cref="MaximumDcOffset"/>, envelope times to 0..<see cref="MaximumEnvelopeTime"/> ms and the
///         sustain level to 0..100 %. Stateless and thread-safe.
///     </para>
/// </remarks>
public static class AudioPreview
{
    /// <summary>
    ///     The lowest frequency plotted, in Hz (the low end of human hearing).
    /// </summary>
    public const double MinimumFrequency = 20.0;

    /// <summary>
    ///     The lowest oscillator frequency plotted, in Hz; slower oscillators (LFOs) are drawn at this rate so two cycles
    ///     still fit a two-second window.
    /// </summary>
    public const double MinimumOscillatorFrequency = 1.0;

    /// <summary>
    ///     The highest frequency plotted, in Hz (the high end of human hearing).
    /// </summary>
    public const double MaximumFrequency = 20000.0;

    /// <summary>
    ///     The largest DC offset plotted, either way.
    /// </summary>
    public const double MaximumDcOffset = 10.0;

    /// <summary>
    ///     The longest envelope time plotted, in ms.
    /// </summary>
    public const double MaximumEnvelopeTime = 10000.0;

    /// <summary>
    ///     How long the envelope preview holds the sustain level, in ms: an illustrative note length, since the real hold
    ///     lasts as long as the key is down.
    /// </summary>
    public const double SustainHoldMs = 200.0;

    /// <summary>
    ///     How many cycles of the oscillator the preview shows.
    /// </summary>
    public const double PreviewCycles = 2.0;

    /// <summary>
    ///     How many segments the oscillator trace has (the trace has one more point).
    /// </summary>
    public const int OscillatorSegments = 256;

    /// <summary>
    ///     How many segments the filter response trace has.
    /// </summary>
    public const int FilterSegments = 200;

    /// <summary>
    ///     The lowest gain the filter response reports, in dB, so a deep notch stays finite.
    /// </summary>
    public const double FilterFloorDb = -60.0;

    /// <summary>
    ///     The quality factor of the filter model: 1/√2 gives the maximally flat (Butterworth) response, -3 dB at the
    ///     cutoff for low-pass and high-pass.
    /// </summary>
    private static readonly double FilterQ = 1.0 / Math.Sqrt(2.0);

    /// <summary>
    ///     The labelled ticks of the logarithmic frequency axis.
    /// </summary>
    private static readonly IReadOnlyList<PlotTick> FrequencyTicks =
    [
        new(20, "20"), new(50, "50"), new(100, "100"), new(200, "200"), new(500, "500"),
        new(1000, "1k"), new(2000, "2k"), new(5000, "5k"), new(10000, "10k"), new(20000, "20k"),
    ];

    /// <summary>
    ///     Computes one sample of an oscillator's output.
    /// </summary>
    /// <param name="settings">The oscillator settings (clamped as described on the class).</param>
    /// <param name="seconds">The time since the start of the wave, in seconds.</param>
    /// <returns>
    ///     The DC offset plus the amplitude times the wave, where the wave runs from -1 to 1 and, at phase 0, starts at 0
    ///     rising (sine, saw, triangle) or at +1 (square).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static double OscillatorSample(OscillatorSettings settings, double seconds)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The angle of the wave: frequency times time plus the start phase, in radians
        var angle = (2.0 * Math.PI * ClampOscillatorFrequency(settings.Frequency) * seconds) + (settings.Phase * Math.PI / 180.0);

        // The unit wave, -1..1, aligned so every shape starts its cycle at angle 0
        var wave = settings.Waveform switch
        {
            "Square" => Math.Sin(angle) >= 0 ? 1.0 : -1.0,
            "Saw" => (2.0 * Fraction((angle / (2.0 * Math.PI)) + 0.5)) - 1.0,
            "Triangle" => 2.0 / Math.PI * Math.Asin(Math.Sin(angle)),
            _ => Math.Sin(angle),
        };

        return ClampOffset(settings.DcOffset) + (Math.Clamp(settings.Amplitude, 0.0, 1.0) * wave);
    }

    /// <summary>
    ///     Plots an oscillator's signal against time over <see cref="PreviewCycles"/> cycles of its frequency.
    /// </summary>
    /// <param name="settings">The oscillator settings.</param>
    /// <returns>
    ///     The plot: X in ms from 0 to two periods, Y the signal on a symmetric axis that fits ±1 and the offset wave.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static PlotData Oscillator(OscillatorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Sample two periods evenly; the trace is in ms
        var durationMs = PreviewCycles * 1000.0 / ClampOscillatorFrequency(settings.Frequency);
        var points = new List<PlotPoint>(OscillatorSegments + 1);
        for (var i = 0; i <= OscillatorSegments; i++)
        {
            var ms = durationMs * i / OscillatorSegments;
            points.Add(new PlotPoint(ms, OscillatorSample(settings, ms / 1000.0)));
        }

        // A symmetric amplitude axis that always shows ±1 and grows in steps of 0.5 to fit the offset wave
        var reach = ClampOffset(Math.Abs(settings.DcOffset)) + Math.Clamp(settings.Amplitude, 0.0, 1.0);
        var limit = Math.Max(1.0, Math.Ceiling(reach * 2.0) / 2.0);
        var yTicks = new[] { -limit, -limit / 2, 0.0, limit / 2, limit }.Select(v => new PlotTick(v, Format(v))).ToList();

        return new PlotData(
            points,
            new PlotAxis(0, durationMs, "Time (ms)", LinearTicks(durationMs)),
            new PlotAxis(-limit, limit, "Amplitude", yTicks),
            [],
            []);
    }

    /// <summary>
    ///     Describes an oscillator in a few words for its tooltip, for example <c>Sine, 440 Hz</c>.
    /// </summary>
    /// <param name="settings">The oscillator settings.</param>
    /// <returns>The waveform and the frequency (as set, not clamped), formatted with the invariant culture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static string Describe(OscillatorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return $"{settings.Waveform}, {Format(settings.Frequency)} Hz";
    }

    /// <summary>
    ///     Describes an envelope in a few words for its tooltip, for example <c>A 10 ms · D 120 ms · S 70 % · R 300 ms</c>.
    /// </summary>
    /// <param name="settings">The envelope settings.</param>
    /// <returns>The four phases in A-D-S-R order (as set, not clamped), formatted with the invariant culture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static string Describe(EnvelopeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return $"A {Format(settings.Attack)} ms · D {Format(settings.Decay)} ms · S {Format(settings.Sustain)} % · R {Format(settings.Release)} ms";
    }

    /// <summary>
    ///     Describes a filter in a few words for its tooltip, for example <c>Low-pass, 900 Hz</c>.
    /// </summary>
    /// <param name="settings">The filter settings.</param>
    /// <returns>The filter type and the cutoff (as set, not clamped), formatted with the invariant culture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static string Describe(FilterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return $"{settings.Type}, {Format(settings.Cutoff)} Hz";
    }

    /// <summary>
    ///     Computes the corner points of an ADSR envelope: silence, the attack peak, the sustain level reached after the decay,
    ///     the end of the illustrative sustain hold, and silence again after the release.
    /// </summary>
    /// <param name="settings">The envelope settings (clamped as described on the class).</param>
    /// <returns>Five points, X in ms and Y the level from 0 to 1.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PlotPoint> EnvelopePoints(EnvelopeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var attack = ClampTime(settings.Attack);
        var decay = ClampTime(settings.Decay);
        var release = ClampTime(settings.Release);
        var sustain = Math.Clamp(settings.Sustain, 0.0, 100.0) / 100.0;
        var held = attack + decay + SustainHoldMs;
        return
        [
            new(0, 0),
            new(attack, 1),
            new(attack + decay, sustain),
            new(held, sustain),
            new(held + release, 0),
        ];
    }

    /// <summary>
    ///     Plots an ADSR envelope with its four phases labelled A, D, S and R.
    /// </summary>
    /// <param name="settings">The envelope settings.</param>
    /// <returns>The plot: X in ms, Y the level from 0 % to 100 %, and one region per phase.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static PlotData Envelope(EnvelopeSettings settings)
    {
        var points = EnvelopePoints(settings);
        var end = points[^1].X;
        var yTicks = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }.Select(v => new PlotTick(v, Format(v * 100) + "%")).ToList();
        return new PlotData(
            points,
            new PlotAxis(0, end, "Time (ms)", LinearTicks(end)),
            new PlotAxis(0, 1, "Level", yTicks),
            [],
            [
                new PlotRegion(points[0].X, points[1].X, "A"),
                new PlotRegion(points[1].X, points[2].X, "D"),
                new PlotRegion(points[2].X, points[3].X, "S"),
                new PlotRegion(points[3].X, points[4].X, "R"),
            ]);
    }

    /// <summary>
    ///     Computes the gain of a filter at a frequency with a second-order model: a Butterworth low-pass or high-pass
    ///     (-3 dB at the cutoff) or a band-pass with the same Q that peaks at 0 dB at the cutoff.
    /// </summary>
    /// <param name="settings">The filter settings; the cutoff is clamped as described on the class.</param>
    /// <param name="hertz">The frequency, in Hz; positive.</param>
    /// <returns>The gain in dB, never below <see cref="FilterFloorDb"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hertz"/> is not a positive finite number.</exception>
    public static double FilterMagnitudeDb(FilterSettings settings, double hertz)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(hertz) || hertz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz), hertz, "The frequency must be a positive finite number.");
        }

        // The standard second-order magnitude with the frequency relative to the cutoff
        var ratio = hertz / ClampFrequency(settings.Cutoff);
        var denominator = Math.Sqrt(Math.Pow(1.0 - (ratio * ratio), 2) + Math.Pow(ratio / FilterQ, 2));
        var magnitude = settings.Type switch
        {
            "High-pass" => ratio * ratio / denominator,
            "Band-pass" => ratio / FilterQ / denominator,
            _ => 1.0 / denominator,
        };

        return Math.Max(FilterFloorDb, 20.0 * Math.Log10(magnitude));
    }

    /// <summary>
    ///     Plots a filter's magnitude response from 20 Hz to 20 kHz on a logarithmic frequency axis, with the cutoff marked.
    /// </summary>
    /// <param name="settings">The filter settings.</param>
    /// <returns>The plot: X in Hz (logarithmic), Y the gain from -48 dB to +6 dB with grid lines every 12 dB, and an <c>fc</c> marker.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static PlotData Filter(FilterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Sample evenly in log frequency so every octave gets the same detail
        var logMin = Math.Log10(MinimumFrequency);
        var logMax = Math.Log10(MaximumFrequency);
        var points = new List<PlotPoint>(FilterSegments + 1);
        for (var i = 0; i <= FilterSegments; i++)
        {
            var hertz = Math.Pow(10, logMin + ((logMax - logMin) * i / FilterSegments));
            points.Add(new PlotPoint(hertz, FilterMagnitudeDb(settings, hertz)));
        }

        var yTicks = new[] { -48.0, -36.0, -24.0, -12.0, 0.0 }.Select(v => new PlotTick(v, Format(v))).ToList();
        return new PlotData(
            points,
            new PlotAxis(MinimumFrequency, MaximumFrequency, "Frequency (Hz)", FrequencyTicks, IsLogarithmic: true),
            new PlotAxis(-48, 6, "Gain (dB)", yTicks),
            [new PlotMarker(ClampFrequency(settings.Cutoff), "fc")],
            []);
    }

    /// <summary>
    ///     Makes about five evenly spaced, round-numbered ticks from 0 to a maximum.
    /// </summary>
    /// <param name="maximum">The end of the axis.</param>
    /// <returns>The ticks, starting at 0; only the 0 tick when the maximum is not a positive finite number.</returns>
    internal static IReadOnlyList<PlotTick> LinearTicks(double maximum)
    {
        if (!double.IsFinite(maximum) || maximum <= 0)
        {
            return [new PlotTick(0, "0")];
        }

        // A 1-2-5 step that gives four to ten intervals
        var raw = maximum / 5.0;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var step = magnitude * normalized switch
        {
            < 1.5 => 1.0,
            < 3.5 => 2.0,
            < 7.5 => 5.0,
            _ => 10.0,
        };

        // Stop at the maximum, allowing for rounding in the last step
        var ticks = new List<PlotTick>();
        for (var i = 0; i * step <= maximum * (1 + 1e-9); i++)
        {
            var value = i * step;
            ticks.Add(new PlotTick(value, Format(value)));
        }

        return ticks;
    }

    /// <summary>
    ///     Formats a number for a label: up to three decimals, invariant culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Format(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Gets the fractional part of a number, in [0, 1).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The fraction.</returns>
    private static double Fraction(double value) => value - Math.Floor(value);

    /// <summary>
    ///     Clamps a frequency to the plotted range.
    /// </summary>
    /// <param name="hertz">The frequency.</param>
    /// <returns>The clamped frequency.</returns>
    private static double ClampFrequency(double hertz) => Math.Clamp(hertz, MinimumFrequency, MaximumFrequency);

    /// <summary>
    ///     Clamps an oscillator frequency to the plotted range.
    /// </summary>
    /// <param name="hertz">The frequency.</param>
    /// <returns>The clamped frequency.</returns>
    private static double ClampOscillatorFrequency(double hertz) => Math.Clamp(hertz, MinimumOscillatorFrequency, MaximumFrequency);

    /// <summary>
    ///     Clamps a DC offset to the plotted range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The clamped offset.</returns>
    private static double ClampOffset(double offset) => Math.Clamp(offset, -MaximumDcOffset, MaximumDcOffset);

    /// <summary>
    ///     Clamps an envelope time to the plotted range.
    /// </summary>
    /// <param name="ms">The time in ms.</param>
    /// <returns>The clamped time.</returns>
    private static double ClampTime(double ms) => Math.Clamp(ms, 0.0, MaximumEnvelopeTime);
}
