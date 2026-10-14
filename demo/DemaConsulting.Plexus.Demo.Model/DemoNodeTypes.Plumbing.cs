namespace DemaConsulting.Plexus.Demo.Model;

/// <content>
///     The plumbing node types: pumps, valves and tanks joined by pipes that carry a fluid.
/// </content>
public static partial class DemoNodeTypes
{
    /// <summary>
    ///     The key of the pump node type.
    /// </summary>
    public const string Pump = "demo.plumbing.Pump";

    /// <summary>
    ///     The key of the valve node type.
    /// </summary>
    public const string Valve = "demo.plumbing.Valve";

    /// <summary>
    ///     The key of the tank node type.
    /// </summary>
    public const string Tank = "demo.plumbing.Tank";

    /// <summary>
    ///     The key of the junction (flow splitter) node type.
    /// </summary>
    public const string Junction = "demo.plumbing.Junction";

    /// <summary>
    ///     The key of the flow meter node type.
    /// </summary>
    public const string FlowMeter = "demo.plumbing.FlowMeter";

    /// <summary>
    ///     The key of the drain node type.
    /// </summary>
    public const string Drain = "demo.plumbing.Drain";

    /// <summary>
    ///     Adds the plumbing node types to a registry.
    /// </summary>
    /// <param name="types">The registry to fill.</param>
    private static void AddPlumbingTypes(NodeTypeRegistry types)
    {
        types.Add(Define(Pump, "PUMP", "Pump", PlumbingCategory, "Pump",
            [new("in", Fluid)],
            [new("out", Fluid)],
            [
                Choice("Fluid", "Water", FluidChoices),
                Summarized("Rate", 12.0, "L/min", "Performance", "How much fluid the pump moves."),
                Summarized("Head", 20.0, "m", "Performance", "How high the pump can lift the fluid."),
            ]));
        types.Add(Define(Valve, "VALVE", "Valve", PlumbingCategory, "Valve",
            [new("in", Fluid)],
            [new("out", Fluid)],
            [Summarized("Open", 100, "%", description: "How far the valve is open.", editorHint: "slider")]));
        types.Add(Define(Tank, "TANK", "Tank", PlumbingCategory, "Tank",
            [new("in", Fluid)],
            [new("out", Fluid), new("overflow", Fluid)],
            [
                Choice("Fluid", "Water", FluidChoices),
                Summarized("Capacity", 500, "L"),
                Summarized("Level", 50, "%"),
            ]));
        types.Add(Define(Junction, "JUNCTION", "Junction", PlumbingCategory, "Junction",
            [new("in", Fluid)],
            [new("a", Fluid), new("b", Fluid)],
            [Summarized("Split", 50, "%")]));

        // The flow meter passes the fluid through (the same port name on both sides) and reports a number
        types.Add(Define(FlowMeter, "FLOW METER", "Flow meter", PlumbingCategory, "FlowMeter",
            [new("flow", Fluid)],
            [new("flow", Fluid), new("reading", Number)],
            [Choice("Unit", "L/min", ["L/min", "m3/h"])]));
        types.Add(Define(Drain, "DRAIN", "Drain", PlumbingCategory, "Drain",
            [new("in", Fluid)],
            [],
            [new PropertyDefinition("CheckValve", true) { DisplayName = "Check valve", ShowInSummary = true }]));
    }
}
