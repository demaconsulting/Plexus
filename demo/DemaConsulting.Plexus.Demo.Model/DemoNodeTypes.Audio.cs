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
    ///     Adds the audio node types to a registry.
    /// </summary>
    /// <param name="types">The registry to fill.</param>
    private static void AddAudioTypes(NodeTypeRegistry types)
    {
        types.Add(Define(Oscillator, "OSCILLATOR", "Oscillator", AudioCategory, "Oscillator",
            [],
            [new("out", Audio)],
            [
                Choice("Waveform", "Sine", ["Sine", "Square", "Saw", "Triangle"], "Signal", "The shape of the generated wave."),
                Summarized("Frequency", 440.0, "Hz", "Signal", "The pitch of the generated tone."),
            ]));
        types.Add(Define(Filter, "FILTER", "Filter", AudioCategory, "Filter",
            [new("in", Audio)],
            [new("out", Audio)],
            [
                Choice("Type", "Low-pass", ["Low-pass", "High-pass", "Band-pass"], "Signal", "Which frequencies the filter lets through."),
                Summarized("Cutoff", 1200.0, "Hz", "Signal", "Where the filter starts to cut."),
            ]));
        types.Add(Define(Envelope, "ENVELOPE", "Envelope (ADSR)", AudioCategory, "Envelope",
            [],
            [new("level", Number)],
            [
                Summarized("Attack", 10, "ms"),
                Summarized("Decay", 120, "ms"),
                Summarized("Sustain", 70, "%"),
                Summarized("Release", 300, "ms"),
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
