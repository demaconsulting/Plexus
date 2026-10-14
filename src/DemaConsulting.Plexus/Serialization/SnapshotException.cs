namespace DemaConsulting.Plexus;

/// <summary>
///     Thrown when a <see cref="DiagramSnapshot"/> cannot be turned into a diagram.
/// </summary>
/// <remarks>
///     Typical causes are an unsupported format version, node types missing from the registry
///     (all of them are listed in <see cref="MissingNodeTypes"/>, not just the first) and structurally
///     invalid content such as wires that reference unknown nodes.
/// </remarks>
public sealed class SnapshotException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnapshotException"/> class.
    /// </summary>
    /// <param name="message">What is wrong with the snapshot.</param>
    public SnapshotException(string message)
        : this(message, [])
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SnapshotException"/> class for missing node types.
    /// </summary>
    /// <param name="message">What is wrong with the snapshot.</param>
    /// <param name="missingNodeTypes">Every node type key the registry does not contain.</param>
    public SnapshotException(string message, IReadOnlyList<string> missingNodeTypes)
        : base(message) => MissingNodeTypes = missingNodeTypes;

    /// <summary>
    ///     Gets every node type key used by the snapshot that the registry does not contain; empty when types are not the problem.
    /// </summary>
    public IReadOnlyList<string> MissingNodeTypes { get; }
}
