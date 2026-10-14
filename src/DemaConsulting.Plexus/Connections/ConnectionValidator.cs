namespace DemaConsulting.Plexus;

/// <summary>
///     A host-supplied rule that decides whether an output port may be wired to an input port.
/// </summary>
/// <remarks>
///     Rules run after the structural checks Plexus always enforces (one output and one input, same
///     page, no connection of a node to itself, no duplicate wire), so implementations only express domain policy and never
///     need to test for a self-loop. The validator is always called as <c>(output, input)</c>, even when the caller
///     passed the two ports in the opposite order, and this order is guaranteed: <paramref name="from"/> is never an input.
///     <see cref="DiagramPage.CanReconnect"/> calls it with the wire's new (or kept) ports in the same order.
///     The
///     diagram's initial rule, <see cref="ConnectionRules.Default"/>, refuses fan-in (a second wire into
///     an input) unless the input opts in with <see cref="PortDefinition.AllowsMultipleWires"/>; a rule that
///     replaces it takes over that decision too, so combine custom rules with the default using <see cref="ConnectionRules.All"/>.
///     Express degree limits with <see cref="ConnectionRules.MaxOutgoingWires"/> and <see cref="ConnectionRules.MaxIncomingWires"/>, or
///     count wires yourself with <see cref="Port.CountWiresIgnoringReconnect"/> (not <c>port.Wires.Count</c>): while a wire is being moved it is
///     still attached to its old ports and must not count against its new ones; <see cref="DiagramPage.WireBeingReconnected"/> names it.
///     A rule must be fast and free of side effects: editors call it repeatedly while the user drags a wire over candidate ports.
/// </remarks>
/// <example>
///     A rule that only lets ports of the same "family" connect, with a message for the user.
///     <code>
///     using DemaConsulting.Plexus;
///
///     // Port type keys here are "power.ac", "power.dc", "signal.analog": the part before the dot is the family.
///     ConnectionValidator sameFamily = (from, to) =&gt;
///         from.TypeKey.Split('.')[0] == to.TypeKey.Split('.')[0]
///             ? ConnectionCheck.Allowed
///             : ConnectionCheck.Denied($"{from.TypeKey} cannot drive {to.TypeKey}.");
///
///     var diagram = new Diagram(new NodeTypeRegistry());
///     diagram.ConnectionRules = ConnectionRules.All(ConnectionRules.Default, sameFamily);   // keep single-input behavior
///     </code>
/// </example>
/// <param name="from">The output (source) port.</param>
/// <param name="to">The input (target) port.</param>
/// <returns>The decision, with a reason when denied.</returns>
public delegate ConnectionCheck ConnectionValidator(Port from, Port to);
