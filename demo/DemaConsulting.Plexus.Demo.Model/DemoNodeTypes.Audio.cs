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
    ///     The category of the oscillator's properties in a properties editor.
    /// </summary>
    public const string OscillatorCategory = "Oscillator";

    /// <summary>
    ///     The category of the filter's properties in a properties editor.
    /// </summary>
    public const string FilterCategory = "Filter";

    /// <summary>
    ///     The category of the envelope's properties in a properties editor.
    /// </summary>
    public const string EnvelopeCategory = "Envelope";

    /// <summary>
    ///     Adds the audio node types to a registry.
    /// </summary>
    /// <remarks>
    ///     The oscillator, filter and envelope each list their properties under one category named after the node, in the
    ///     order a musician reads them (<see cref="PropertyDefinition.Order"/>): waveform, frequency, amplitude, phase and DC
    ///     offset; type then cutoff; attack, decay, sustain and release.
    /// </remarks>
    /// <param name="types">The registry to fill.</param>
    private static void AddAudioTypes(NodeTypeRegistry types)
    {
        const string oscillator = "Generates a periodic wave.";
        types.Add(Define(Oscillator, "OSCILLATOR", "Oscillator", AudioCategory, "Oscillator",
            [],
            [new("out", Audio) { Description = "The generated wave." }],
            [
                Choice(WaveformProperty, "Sine", WaveformChoices, OscillatorCategory, "The shape of the generated wave.", order: 1),
                Summarized(FrequencyProperty, 440.0, "Hz", OscillatorCategory, "The pitch of the generated tone.", order: 2),

                // Not in the summary, so the node keeps its look; files saved before they existed load with these defaults
                new(AmplitudeProperty, 1.0) { Category = OscillatorCategory, Order = 3, Description = "The peak level of the wave, from 0 (silent) to 1 (full scale)." },
                new(PhaseProperty, 0.0) { Unit = "°", Category = OscillatorCategory, Order = 4, Description = "Where in its cycle the wave starts." },
                new(DcOffsetProperty, 0.0) { DisplayName = "DC Offset", Category = OscillatorCategory, Order = 5, Description = "A constant added to the wave, which shifts it up or down." },
            ],
            description: oscillator,
            toolTip: Live(oscillator, node => AudioPreview.Describe(OscillatorSettings.FromNode(node)))));
        const string filter = "Shapes the sound by letting some frequencies through and cutting others.";
        types.Add(Define(Filter, "FILTER", "Filter", AudioCategory, "Filter",
            [new("in", Audio) { Description = "The sound to filter." }],
            [new("out", Audio) { Description = "The filtered sound." }],
            [
                Choice(FilterTypeProperty, "Low-pass", FilterTypeChoices, FilterCategory, "Which frequencies the filter lets through.", order: 1),
                Summarized(CutoffProperty, 1200.0, "Hz", FilterCategory, "Where the filter starts to cut.", order: 2),
            ],
            description: filter,
            toolTip: Live(filter, node => AudioPreview.Describe(FilterSettings.FromNode(node)))));
        const string envelope = "Shapes a note's level over time: attack, decay, sustain and release.";
        types.Add(Define(Envelope, "ENVELOPE", "Envelope (ADSR)", AudioCategory, "Envelope",
            [],
            [new("level", Number) { Description = "The envelope level, from 0 to 1." }],
            [
                Summarized(AttackProperty, 10, "ms", EnvelopeCategory, "How long the level takes to rise from silence to full when a note starts.", order: 1),
                Summarized(DecayProperty, 120, "ms", EnvelopeCategory, "How long the level then takes to fall to the sustain level.", order: 2),
                Summarized(SustainProperty, 70, "%", EnvelopeCategory, "The level held while the note is held, as a percentage of full.", order: 3),
                Summarized(ReleaseProperty, 300, "ms", EnvelopeCategory, "How long the level takes to fade to silence after the note ends.", order: 4),
            ],
            description: envelope,
            toolTip: Live(envelope, node => AudioPreview.Describe(EnvelopeSettings.FromNode(node)))));
        const string amplifier = "Changes the level of a sound.";
        types.Add(Define(Amplifier, "AMPLIFIER", "Amplifier", AudioCategory, "Amplifier",
            [new("in", Audio) { Description = "The sound to amplify." }, new("gain", Number) { Description = "Varies the gain, for example from an envelope." }],
            [new("out", Audio) { Description = "The amplified sound." }],
            [Summarized("Gain", 0.8, description: "How much the sound is amplified; 1 leaves it unchanged.")],
            description: amplifier,
            toolTip: Live(amplifier, node => $"Gain {Value(node, "Gain")}")));
        const string mixer = "Combines up to three sounds into one.";
        types.Add(Define(Mixer, "MIXER", "Mixer", AudioCategory, "Mixer",
            [new("a", Audio) { Description = "A sound to mix." }, new("b", Audio) { Description = "A sound to mix." }, new("c", Audio) { Description = "A sound to mix." }],
            [new("mix", Audio) { Description = "The mixed sound." }],
            [Summarized("Master", 80, "%", description: "The overall level of the mix.", editorHint: "slider")],
            description: mixer,
            toolTip: Live(mixer, node => $"Master {Value(node, "Master")}")));
        const string speaker = "Plays the sound.";
        types.Add(Define(Speaker, "SPEAKER", "Speaker", AudioCategory, "Speaker",
            [new("in", Audio) { Description = "The sound to play." }],
            [],
            [Summarized("Volume", 50, "%", description: "How loud the speaker plays.", editorHint: "slider")],
            description: speaker,
            toolTip: Live(speaker, node => $"Volume {Value(node, "Volume")}")));
    }
}
