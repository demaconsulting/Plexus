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
        const string pump = "Moves fluid from its inlet to its outlet.";
        types.Add(Define(Pump, "PUMP", "Pump", PlumbingCategory, "Pump",
            [new("in", Fluid) { Description = "Fluid drawn in." }],
            [new("out", Fluid) { Description = "Fluid pumped out." }],
            [
                Choice("Fluid", "Water", FluidChoices, description: "What the pump carries."),
                Summarized("Rate", 12.0, "L/min", "Performance", "How much fluid the pump moves."),
                Summarized("Head", 20.0, "m", "Performance", "How high the pump can lift the fluid."),
            ],
            description: pump,
            toolTip: Live(pump, node => $"{Value(node, "Fluid")} · {Value(node, "Rate")} · {Value(node, "Head")}")));
        const string valve = "Lets fluid through, as much as its opening allows.";
        types.Add(Define(Valve, "VALVE", "Valve", PlumbingCategory, "Valve",
            [new("in", Fluid) { Description = "Fluid arriving at the valve." }],
            [new("out", Fluid) { Description = "Fluid let through." }],
            [Summarized("Open", 100, "%", description: "How far the valve is open.", editorHint: "slider")],
            description: valve,
            toolTip: Live(valve, node => $"Open {Value(node, "Open")}")));
        const string tank = "Stores fluid; what no longer fits leaves through the overflow.";
        types.Add(Define(Tank, "TANK", "Tank", PlumbingCategory, "Tank",
            [new("in", Fluid) { Description = "Fluid filling the tank." }],
            [new("out", Fluid) { Description = "Fluid drawn from the tank." }, new("overflow", Fluid) { Description = "Fluid that no longer fits." }],
            [
                Choice("Fluid", "Water", FluidChoices, description: "What the tank holds."),
                Summarized("Capacity", 500, "L", description: "How much the tank holds when full."),
                Summarized("Level", 50, "%", description: "How full the tank is."),
            ],
            description: tank,
            toolTip: Live(tank, node => $"{Value(node, "Fluid")} · {Value(node, "Level")} of {Value(node, "Capacity")}")));
        const string junction = "Splits one flow into two.";
        types.Add(Define(Junction, "JUNCTION", "Junction", PlumbingCategory, "Junction",
            [new("in", Fluid) { Description = "The flow to split." }],
            [new("a", Fluid) { Description = "The Split share of the flow." }, new("b", Fluid) { Description = "The rest of the flow." }],
            [Summarized("Split", 50, "%", description: "The share of the flow that leaves through a; b gets the rest.")],
            description: junction,
            toolTip: Live(junction, node => SplitSummary(node.Properties.Get<int>("Split")))));

        // The flow meter passes the fluid through (the same port name on both sides) and reports a number
        const string flowMeter = "Passes fluid through and reports how much flows.";
        types.Add(Define(FlowMeter, "FLOW METER", "Flow meter", PlumbingCategory, "FlowMeter",
            [new("flow", Fluid) { Description = "Fluid to measure." }],
            [new("flow", Fluid) { Description = "The same fluid, passed through." }, new("reading", Number) { Description = "The measured flow rate." }],
            [Choice("Unit", "L/min", ["L/min", "m3/h"], description: "The unit of the reading.")],
            description: flowMeter,
            toolTip: Live(flowMeter, node => $"Reports {Value(node, "Unit")}")));
        const string drain = "Takes fluid out of the system.";
        types.Add(Define(Drain, "DRAIN", "Drain", PlumbingCategory, "Drain",
            [new("in", Fluid) { Description = "Fluid to drain away." }],
            [],
            [new PropertyDefinition("CheckValve", true) { DisplayName = "Check valve", ShowInSummary = true, Description = "Stops fluid from flowing back." }],
            description: drain,
            toolTip: Live(drain, node => $"Check valve {(node.Properties.Get<bool>("CheckValve") ? "on" : "off")}")));
    }

    /// <summary>
    ///     Describes how a junction shares its flow, for example <c>a 30 % · b 70 %</c>.
    /// </summary>
    /// <param name="split">The share of output a in percent; values outside 0..100 are clamped.</param>
    /// <returns>The shares of both outputs.</returns>
    private static string SplitSummary(int split)
    {
        var a = Math.Clamp(split, 0, 100);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a {a} % · b {100 - a} %");
    }
}
