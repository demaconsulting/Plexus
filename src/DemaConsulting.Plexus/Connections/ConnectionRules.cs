namespace DemaConsulting.Plexus;

/// <summary>
///     Ready-made <see cref="ConnectionValidator"/> rules that hosts can use or combine.
/// </summary>
/// <remarks>
///     <para>
///         Plexus allows fan-out (one output to many inputs) but, by default, not fan-in: an input accepts a
///         single wire unless its <see cref="PortDefinition.AllowsMultipleWires"/> is set. That default lives
///         in <see cref="Default"/>, which a diagram uses until the host assigns its own rule. A host that
///         assigns a rule built without <see cref="Default"/> (or <see cref="SingleInputWire"/>) therefore
///         takes over the whole policy and may allow fan-in on every input. To add domain policy and keep the
///         default, compose: <c>diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);</c>.
///     </para>
///     <para>
///         Naming: <see cref="Diagram.ConnectionRules"/> (a property of type <see cref="ConnectionValidator"/>) and this static class
///         share a name on purpose. The class holds the building blocks, the property holds the rule the diagram applies.
///     </para>
///     <para>
///         Plexus always runs its structural checks first (output to input, same page, different nodes, no duplicate wire), so
///         a rule never needs to test for a self-loop. A rule always receives the ports as (output, input), even when the caller
///         passed them the other way round.
///     </para>
/// </remarks>
/// <example>
///     Keep the default fan-in rule and add domain policy, or opt single inputs into fan-in.
///     <code>
///     using DemaConsulting.Plexus;
///
///     // types: the registry of the DemaConsulting.Plexus example; give the sink input fan-in via its definition
///     var mixer = new NodeType("audio.Mixer")
///     {
///         Inputs = { new PortDefinition("in", "number") { AllowsMultipleWires = true } },   // many wires may end here
///     };
///     types.Add(mixer);
///
///     var diagram = new Diagram(types);
///     var page = diagram.Pages.Add("Main");
///     var a = page.AddNode("demo.Source", 0, 0, name: "A");
///     var m = page.AddNode("audio.Mixer", 200, 0, name: "M");
///
///     // Default: fan-out is free, an input accepts one wire. Add "types must match" and keep the default:
///     diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType);
///
///     // A custom rule: your own policy, with a reason that is reported to the user (here: an output feeds at most 3 inputs)
///     ConnectionValidator maxThreeOutputs = (from, to) =&gt;
///         from.Wires.Count &gt;= 3
///             ? ConnectionCheck.Denied($"{from} already feeds three inputs.")
///             : ConnectionCheck.Allowed;
///     diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, ConnectionRules.SameType, maxThreeOutputs);
///
///     var verdict = page.CanConnect(a.Output("out"), m.Input("in"));   // ask without connecting
///     Console.WriteLine(verdict.IsAllowed ? "allowed" : verdict.Reason);
///     </code>
/// </example>
public static class ConnectionRules
{
    /// <summary>
    ///     Gets the default rule: an input accepts at most one wire unless it opts into fan-in.
    /// </summary>
    /// <remarks>
    ///     Equivalent to <see cref="SingleInputWire"/>. Output ports are unrestricted (fan-out). The
    ///     structural checks (one output and one input, same page, no self-loop, no duplicate wire) run
    ///     before every rule and cannot be replaced.
    /// </remarks>
    public static ConnectionValidator Default { get; } = static (_, to) => CheckSingleInput(to);

    /// <summary>
    ///     Gets a rule that only allows ports with the same type key to be connected (ordinal, case-sensitive comparison of <see cref="Port.TypeKey"/>).
    /// </summary>
    /// <remarks>
    ///     Not part of <see cref="Default"/>: without this rule an output of one port type may feed an input of another.
    ///     Combine it with <see cref="Default"/> through <see cref="All"/>.
    /// </remarks>
    public static ConnectionValidator SameType { get; } = static (from, to) =>
        string.Equals(from.TypeKey, to.TypeKey, StringComparison.Ordinal)
            ? ConnectionCheck.Allowed
            : ConnectionCheck.Denied($"Port types differ: '{from.TypeKey}' cannot connect to '{to.TypeKey}'.");

    /// <summary>
    ///     Gets a rule that allows at most one wire per input port, unless the input's definition allows fan-in.
    /// </summary>
    /// <remarks>
    ///     This is the same rule as <see cref="Default"/>; it is kept as a named building block for hosts
    ///     that compose rules explicitly. A wire being reconnected does not count against its own target.
    /// </remarks>
    public static ConnectionValidator SingleInputWire { get; } = static (_, to) => CheckSingleInput(to);

    /// <summary>
    ///     Refuses a wire into an input that already has one, unless the input allows fan-in.
    /// </summary>
    /// <param name="to">The input port the wire would end at.</param>
    /// <returns>The decision.</returns>
    private static ConnectionCheck CheckSingleInput(Port to) =>
        to.AllowsMultipleWires || to.CountWiresExceptChecked() == 0
            ? ConnectionCheck.Allowed
            : ConnectionCheck.Denied($"Input '{to}' already has a wire; it does not accept more than one.");

    /// <summary>
    ///     Combines rules so a connection must satisfy all of them; the first denial wins.
    /// </summary>
    /// <param name="rules">The rules to combine.</param>
    /// <returns>A validator that evaluates the rules in order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> or an element is null.</exception>
    public static ConnectionValidator All(params ConnectionValidator[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var captured = (ConnectionValidator[])rules.Clone();
        foreach (var rule in captured)
        {
            ArgumentNullException.ThrowIfNull(rule);
        }

        return (from, to) =>
        {
            foreach (var rule in captured)
            {
                var result = rule(from, to);
                if (!result.IsAllowed)
                {
                    return result;
                }
            }

            return ConnectionCheck.Allowed;
        };
    }
}
