using System.ComponentModel;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Host data of the test node type: a small annotated settings object.
/// </summary>
public sealed class ToneSettings
{
    /// <summary>Gets or sets the level.</summary>
    [Category("Tone"), Description("How loud the tone is."), DefaultValue(3)]
    public int Level { get; set; } = 3;

    /// <summary>Gets or sets the label.</summary>
    [Category("Tone")]
    public string Label { get; set; } = "";

    /// <summary>Gets or sets the tags.</summary>
    [Category("Tone")]
    public List<string> Tags { get; set; } = [];

    /// <summary>Gets or sets a value that is not shown.</summary>
    [Browsable(false)]
    public string Hidden { get; set; } = "";
}

/// <summary>
///     Builds diagrams with a node type that exercises every kind of row of the Plexus adapter.
/// </summary>
internal static class NodeFixture
{
    /// <summary>
    ///     The key of the test node type.
    /// </summary>
    internal const string OscillatorKey = "test.Oscillator";

    /// <summary>
    ///     The key of a node type without host data.
    /// </summary>
    internal const string PlainKey = "test.Plain";

    /// <summary>
    ///     Creates a diagram with an oscillator node named "Osc", a second node named "Other" and a plain node.
    /// </summary>
    /// <returns>The diagram and the oscillator node.</returns>
    internal static (Diagram Diagram, Node Node) Create()
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType(OscillatorKey)
        {
            Properties =
            {
                new PropertyDefinition("Frequency", 440.0) { Unit = "Hz", Category = "Signal", Description = "The pitch." },
                new PropertyDefinition("Waveform", "Sine") { Choices = ["Sine", "Square", "Saw"], Category = "Signal" },
                new PropertyDefinition("Enabled", true),
                new PropertyDefinition("Tint", Color.Orange) { Category = "Look" },
                new PropertyDefinition("Label", string.Empty),
                new PropertyDefinition("Serial", "A-1") { IsReadOnly = true },
                new PropertyDefinition("Gain", 1) { EditorHint = "slider" },
            },
            DataType = typeof(ToneSettings),
        });
        types.Add(new NodeType(PlainKey) { Properties = { new PropertyDefinition("Mode", "Fast") { Choices = ["Fast", "Slow"] } } });

        var diagram = new Diagram(types);
        var page = diagram.Pages.Add("Main");
        var node = page.AddNode(OscillatorKey, 0, 0, name: "Osc");
        page.AddNode(OscillatorKey, 300, 0, name: "Other");
        node.Data = new ToneSettings { Level = 5, Label = "lead", Tags = ["a", "b"] };
        return (diagram, node);
    }

    /// <summary>
    ///     The key of the node type whose properties declare a display order.
    /// </summary>
    internal const string EnvelopeKey = "test.Envelope";

    /// <summary>
    ///     Creates a diagram with an envelope node named "Env" whose properties are declared out of alphabetical order and
    ///     ranked with <see cref="PropertyDefinition.Order"/>: Attack 1, Decay 2, Sustain 3, Release 4 under "Envelope",
    ///     plus two uncategorized properties (Mode with order 2, Gate with order 1) that land under General.
    /// </summary>
    /// <returns>The diagram and the envelope node.</returns>
    internal static (Diagram Diagram, Node Node) CreateOrdered()
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType(EnvelopeKey)
        {
            Properties =
            {
                new PropertyDefinition("Release", 300.0) { Unit = "ms", Category = "Envelope", Order = 4 },
                new PropertyDefinition("Sustain", 70.0) { Unit = "%", Category = "Envelope", Order = 3 },
                new PropertyDefinition("Attack", 10.0) { Unit = "ms", Category = "Envelope", Order = 1 },
                new PropertyDefinition("Decay", 120.0) { Unit = "ms", Category = "Envelope", Order = 2 },
                new PropertyDefinition("Mode", "Linear") { Order = 2 },
                new PropertyDefinition("Gate", true) { Order = 1 },
            },
        });

        var diagram = new Diagram(types);
        var node = diagram.Pages.Add("Main").AddNode(EnvelopeKey, 0, 0, name: "Env");
        return (diagram, node);
    }

    /// <summary>
    ///     The key of the node type whose properties share one display order.
    /// </summary>
    internal const string TiedKey = "test.Tied";

    /// <summary>
    ///     Creates a diagram with a node named "Tie" whose properties are declared out of alphabetical order and mostly share
    ///     the same <see cref="PropertyDefinition.Order"/>: Zeta, Idle, alpha, ice and Hue all rank 7 under "Tied" (mixed case,
    ///     so the alphabetical tie-break must ignore case), and Early ranks -5 (a negative order lists first).
    /// </summary>
    /// <returns>The diagram and the tied node.</returns>
    internal static (Diagram Diagram, Node Node) CreateTied()
    {
        var types = new NodeTypeRegistry();
        types.Add(new NodeType(TiedKey)
        {
            Properties =
            {
                new PropertyDefinition("Zeta", 1) { Category = "Tied", Order = 7 },
                new PropertyDefinition("Idle", 1) { Category = "Tied", Order = 7 },
                new PropertyDefinition("alpha", 1) { Category = "Tied", Order = 7 },
                new PropertyDefinition("Early", 1) { Category = "Tied", Order = -5 },
                new PropertyDefinition("ice", 1) { Category = "Tied", Order = 7 },
                new PropertyDefinition("Hue", 1) { Category = "Tied", Order = 7 },
            },
        });

        var diagram = new Diagram(types);
        var node = diagram.Pages.Add("Main").AddNode(TiedKey, 0, 0, name: "Tie");
        return (diagram, node);
    }
}
