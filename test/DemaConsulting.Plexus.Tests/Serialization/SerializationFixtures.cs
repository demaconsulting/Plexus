using System.Text.Json;
using DemaConsulting.Plexus.Geometry;

namespace DemaConsulting.Plexus.Tests.Serialization;

/// <summary>
///     A payload type serialized by System.Text.Json through <see cref="NodeType.DataType"/>.
/// </summary>
/// <param name="Label">A text member.</param>
/// <param name="Count">A number member.</param>
internal sealed record SensorData(string Label, int Count);

/// <summary>
///     A payload type serialized by a custom <see cref="INodeDataSerializer"/>.
/// </summary>
/// <param name="Text">A text member.</param>
internal sealed record CustomData(string Text);

/// <summary>
///     Serializer for <see cref="CustomData"/> that uses a deliberately non-default JSON shape.
/// </summary>
internal sealed class CustomDataSerializer : INodeDataSerializer
{
    /// <inheritdoc />
    public string Serialize(object data) => JsonSerializer.Serialize(new { custom = ((CustomData)data).Text });

    /// <inheritdoc />
    public object? Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("custom", out var text) && text.GetString() is { } value
            ? new CustomData(value)
            : null;
    }
}

/// <summary>
///     Shared fixtures for the snapshot, clipboard and duplicate tests.
/// </summary>
/// <remarks>
///     Builds on <see cref="TestModel"/> and adds node types with host payloads, plus a rich diagram that
///     exercises every feature a snapshot must preserve.
/// </remarks>
internal static class SerializationFixtures
{
    /// <summary>
    ///     Creates the standard test types plus the payload-carrying types.
    /// </summary>
    /// <returns>A new registry; each call returns an independent but equivalent registry.</returns>
    public static NodeTypeRegistry CreateTypes()
    {
        var types = TestModel.CreateTypes();
        types.Add(new NodeType("acme.Sensor")
        {
            DataType = typeof(SensorData),
            Outputs = { new PortDefinition("value", "f32") },
        });
        types.Add(new NodeType("acme.Custom")
        {
            DataSerializer = new CustomDataSerializer(),
            Inputs = { new PortDefinition("in", "f32") },
        });
        types.Add(new NodeType("acme.Opaque"));
        return types;
    }

    /// <summary>
    ///     Creates a diagram using <see cref="CreateTypes"/>.
    /// </summary>
    /// <returns>The diagram.</returns>
    public static Diagram CreateDiagram() => new(CreateTypes());

    /// <summary>
    ///     Creates the standard types plus an <c>acme.Merger</c> type (replacing the one of the standard types) whose input does or does not allow fan-in.
    /// </summary>
    /// <param name="allowsFanIn">Whether the merger's input allows several wires.</param>
    /// <returns>A new registry.</returns>
    public static NodeTypeRegistry CreateTypesWithMerger(bool allowsFanIn)
    {
        var types = CreateTypes();
        types.Remove("acme.Merger");
        types.Add(new NodeType("acme.Merger") { Inputs = { new PortDefinition("in", "f32") { AllowsMultipleWires = allowsFanIn } } });
        return types;
    }

    /// <summary>
    ///     Builds a two-page diagram covering properties of every kind, payloads, annotations, manual routes,
    ///     nested groups (one collapsed) and a non-default z-order.
    /// </summary>
    /// <returns>The diagram.</returns>
    public static Diagram CreateRichDiagram()
    {
        var diagram = CreateDiagram();
        var main = diagram.Pages.Add("Main");

        var producer = main.AddNode("acme.Producer", 30, 45);
        producer.Properties["Gain"] = 2.5;
        producer.Properties["Enabled"] = false;
        producer.Properties["Count"] = 7;
        producer.Properties["Big"] = 1L << 40;
        producer.Properties["Tint"] = Color.FromArgb(0x80, 0x11, 0x22, 0x33);
        producer.Properties["Mode"] = "B";
        producer.Properties["Note"] = "ad hoc text";
        producer.Properties["Ratio"] = 0.1;
        producer.Properties["Flag"] = true;
        producer.Properties["Steps"] = 12;
        producer.Properties["Huge"] = long.MaxValue;
        producer.Properties["Accent"] = Color.Red;

        var gain = main.AddNode("acme.Gain", 240, 45, "Amplifier");
        var sensor = main.AddNode("acme.Sensor", 30, 210);
        sensor.Data = new SensorData("front \"left\"", 3);
        var custom = main.AddNode("acme.Custom", 240, 210);
        custom.Data = new CustomData("hello ünïcode");

        // Routes the router would never produce prove that loading keeps stored routes untouched
        main.Connect(producer.Port("value"), gain.Port("in"), [new Point(111.5, 7), new Point(113, 9.25)]);
        main.Connect(sensor.Port("value"), custom.Port("in"), []);

        var note = main.AddAnnotation("remember", new Rect(300, 20, 140, 60));
        note.Fill = Color.FromArgb(0x7F, 0xFF, 0xFF, 0x00);
        main.AddAnnotation("plain", new Rect(10, 400, 100, 30));

        var inner = main.CreateGroup([producer, gain], "Inner");
        main.CreateGroup([inner, note], "Outer");
        inner.IsCollapsed = true;
        inner.IsCollapsed = false;
        inner.IsCollapsed = true;

        var second = diagram.Pages.Add("Second");
        var mixer = second.AddNode("acme.Mixer", 15, 15);
        var source = second.AddNode("acme.Producer", 200, 15);
        second.Connect(source.Port("value"), mixer.Port("a"));
        second.AddNode("acme.Opaque", 400, 400);
        return diagram;
    }
}
