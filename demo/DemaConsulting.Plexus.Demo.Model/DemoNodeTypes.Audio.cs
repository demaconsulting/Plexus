namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The audio synth node types: a small signal chain from oscillator to speaker.
/// </content>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The key of the oscillator node type.
    /// </summary>
    public const string Oscillator = "demo.audio.Oscillator";

    /// <summary>
    ///     The key of the filter node type.
    /// </summary>
    public const string Filter = "demo.audio.Filter";

    /// <summary>
    ///     The key of the envelope (ADSR) node type.
    /// </summary>
    public const string Envelope = "demo.audio.Envelope";

    /// <summary>
    ///     The key of the amplifier node type.
    /// </summary>
    public const string Amplifier = "demo.audio.Amplifier";

    /// <summary>
    ///     The key of the mixer node type.
    /// </summary>
    public const string Mixer = "demo.audio.Mixer";

    /// <summary>
    ///     The key of the speaker node type.
    /// </summary>
    public const string Speaker = "demo.audio.Speaker";

    /// <summary>
    ///     The name of the oscillator's waveform property (Sine, Square, Saw or Triangle).
    /// </summary>
    public const string WaveformProperty = "Waveform";

    /// <summary>
    ///     The name of the oscillator's frequency property, in Hz.
    /// </summary>
    public const string FrequencyProperty = "Frequency";

    /// <summary>
    ///     The name of the oscillator's amplitude property: the peak level from 0 (silent) to 1 (full scale).
    /// </summary>
    public const string AmplitudeProperty = "Amplitude";

    /// <summary>
    ///     The name of the oscillator's phase property, in degrees.
    /// </summary>
    public const string PhaseProperty = "Phase";

    /// <summary>
    ///     The name of the oscillator's DC offset property (shown as "DC Offset").
    /// </summary>
    public const string DcOffsetProperty = "DcOffset";

    /// <summary>
    ///     The name of the filter's type property (Low-pass, High-pass or Band-pass).
    /// </summary>
    public const string FilterTypeProperty = "Type";

    /// <summary>
    ///     The name of the filter's cutoff property, in Hz.
    /// </summary>
    public const string CutoffProperty = "Cutoff";

    /// <summary>
    ///     The name of the envelope's attack time property, in ms.
    /// </summary>
    public const string AttackProperty = "Attack";

    /// <summary>
    ///     The name of the envelope's decay time property, in ms.
    /// </summary>
    public const string DecayProperty = "Decay";

    /// <summary>
    ///     The name of the envelope's sustain level property, in percent.
    /// </summary>
    public const string SustainProperty = "Sustain";

    /// <summary>
    ///     The name of the envelope's release time property, in ms.
    /// </summary>
    public const string ReleaseProperty = "Release";

    /// <summary>
    ///     The waveforms an oscillator can generate, the choices of its <see cref="WaveformProperty"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> WaveformChoices = ["Sine", "Square", "Saw", "Triangle"];

    /// <summary>
    ///     The filter types, the choices of the filter's <see cref="FilterTypeProperty"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> FilterTypeChoices = ["Low-pass", "High-pass", "Band-pass"];

    /// <summary>
    ///     Adds the audio node types to a registry.
    /// </summary>
    /// <param name="types">The registry to fill.</param>
    private static void AddAudioTypes(NodeTypeRegistry types)
    {
        types.Add(Define(Oscillator, "OSCILLATOR", "Oscillator", AudioCategory, "Oscillator",
            [],
            [new("out", Audio)],
            [
                Choice(WaveformProperty, "Sine", WaveformChoices, "Signal", "The shape of the generated wave."),
                Summarized(FrequencyProperty, 440.0, "Hz", "Signal", "The pitch of the generated tone."),

                // Not in the summary, so the node keeps its look; files saved before they existed load with these defaults
                new(PhaseProperty, 0.0) { Unit = "°", Category = "Signal", Description = "Where in its cycle the wave starts." },
                new(AmplitudeProperty, 1.0) { Category = "Level", Description = "The peak level of the wave, from 0 (silent) to 1 (full scale)." },
                new(DcOffsetProperty, 0.0) { DisplayName = "DC Offset", Category = "Level", Description = "A constant added to the wave, which shifts it up or down." },
            ]));
        types.Add(Define(Filter, "FILTER", "Filter", AudioCategory, "Filter",
            [new("in", Audio)],
            [new("out", Audio)],
            [
                Choice(FilterTypeProperty, "Low-pass", FilterTypeChoices, "Signal", "Which frequencies the filter lets through."),
                Summarized(CutoffProperty, 1200.0, "Hz", "Signal", "Where the filter starts to cut."),
            ]));
        types.Add(Define(Envelope, "ENVELOPE", "Envelope (ADSR)", AudioCategory, "Envelope",
            [],
            [new("level", Number)],
            [
                Summarized(AttackProperty, 10, "ms"),
                Summarized(DecayProperty, 120, "ms"),
                Summarized(SustainProperty, 70, "%"),
                Summarized(ReleaseProperty, 300, "ms"),
            ]));
        types.Add(Define(Amplifier, "AMPLIFIER", "Amplifier", AudioCategory, "Amplifier",
            [new("in", Audio), new("gain", Number)],
            [new("out", Audio)],
            [Summarized("Gain", 0.8)]));
        types.Add(Define(Mixer, "MIXER", "Mixer", AudioCategory, "Mixer",
            [new("a", Audio), new("b", Audio), new("c", Audio)],
            [new("mix", Audio)],
            [Summarized("Master", 80, "%", description: "The overall level of the mix.", editorHint: "slider")]));
        types.Add(Define(Speaker, "SPEAKER", "Speaker", AudioCategory, "Speaker",
            [new("in", Audio)],
            [],
            [Summarized("Volume", 50, "%", description: "How loud the speaker plays.", editorHint: "slider")]));
    }
}
