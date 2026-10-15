namespace DemaConsulting.Plexus;

/// <summary>
///     The input and output port declarations of one node, as returned by a <see cref="NodeType.PortProvider"/>.
/// </summary>
/// <remarks>
///     <para>
///         A port provider computes the ports of each node from its <see cref="Node.Properties"/> or <see cref="Node.Data"/>
///         (for example one input per channel). The set is validated when it is constructed, so a provider can never hand the
///         model two inputs (or two outputs) with the same name: the port name within a direction is the identity Plexus uses
///         to keep a port object and its wires when the ports are recomputed. An input and an output may share a name.
///     </para>
///     <para>
///         Either list, or both, may be empty: a node whose provider returns an empty list has no ports in that direction
///         (its former ports there are removed with their wires). A provider must return a set, though - a null result is a
///         provider failure.
///     </para>
///     <para>
///         The constructor copies both sequences, so later changes to the caller's collections do not affect the set.
///         Instances are immutable (the lists are read-only wrappers over private copies) and may be cached and shared between nodes and threads; the <see cref="PortDefinition"/>
///         objects are immutable too, so a provider can keep a static array of them and return slices of it.
///     </para>
/// </remarks>
/// <example>
///     Build the ports of a node with a configurable number of inputs.
///     <code>
///     using DemaConsulting.Plexus;
///
///     static PortSet BusPorts(int channels) => new(
///         Enumerable.Range(0, channels).Select(i =&gt; new PortDefinition($"in{i}", "number")),   // in0, in1, ... (stable names)
///         [new PortDefinition("out", "number")]);
///
///     var set = BusPorts(3);
///     Console.WriteLine(set.Inputs.Count);                  // 3
///     Console.WriteLine(set.Outputs[0].Name);               // "out"
///     </code>
/// </example>
public sealed class PortSet
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PortSet"/> class.
    /// </summary>
    /// <param name="inputs">The input declarations in top-to-bottom order; may be empty.</param>
    /// <param name="outputs">The output declarations in top-to-bottom order; may be empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inputs"/> or <paramref name="outputs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a sequence contains a null entry, or two entries of the same direction share a name.</exception>
    public PortSet(IEnumerable<PortDefinition> inputs, IEnumerable<PortDefinition> outputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(outputs);
        Inputs = Array.AsReadOnly(Validate([.. inputs], "input", nameof(inputs)));
        Outputs = Array.AsReadOnly(Validate([.. outputs], "output", nameof(outputs)));
    }

    /// <summary>
    ///     Gets the input declarations in top-to-bottom order.
    /// </summary>
    public IReadOnlyList<PortDefinition> Inputs { get; }

    /// <summary>
    ///     Gets the output declarations in top-to-bottom order.
    /// </summary>
    public IReadOnlyList<PortDefinition> Outputs { get; }

    /// <summary>
    ///     Checks one direction's declarations for null entries and duplicate names.
    /// </summary>
    /// <param name="ports">The copied declarations.</param>
    /// <param name="what">"input" or "output", for the message.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>The same array, which the set wraps read-only.</returns>
    /// <exception cref="ArgumentException">Thrown when an entry is null or a name repeats.</exception>
    private static PortDefinition[] Validate(PortDefinition[] ports, string what, string paramName)
    {
        // Names are the identity of ports within a direction, so they must be unique there
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in ports)
        {
            if (port is null)
            {
                throw new ArgumentException($"The {what} ports contain a null entry.", paramName);
            }

            if (!names.Add(port.Name))
            {
                throw new ArgumentException($"The {what} port name '{port.Name}' is used more than once.", paramName);
            }
        }

        return ports;
    }
}
