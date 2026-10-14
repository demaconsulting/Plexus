namespace DemaConsulting.Plexus;

/// <summary>
///     What a load left out: the nodes of unknown types and everything that depended on them.
/// </summary>
/// <remarks>
///     Only loads with <see cref="UnknownNodeTypeHandling.Skip"/> can skip anything; read the report from <see cref="Diagram.LoadReport"/>
///     and show <see cref="Message"/> to the user. A strict load (the default) throws instead, so its report is always empty.
/// </remarks>
/// <example>
///     Open a file written by a newer version and tell the user what was dropped.
///     <code>
///     using DemaConsulting.Plexus;
///
///     var options = new DiagramOptions { UnknownNodeTypes = UnknownNodeTypeHandling.Skip };
///     var diagram = Diagram.FromJson(File.ReadAllText("diagram.json"), types, options);   // types: your NodeTypeRegistry
///     if (diagram.LoadReport.HasSkipped)
///     {
///         Console.WriteLine(diagram.LoadReport.Message);   // "Skipped 3 node(s) of unknown type(s) demo.Extra, ..."
///     }
///     </code>
/// </example>
public sealed class LoadReport
{
    /// <summary>
    ///     Gets the report of a load that skipped nothing.
    /// </summary>
    public static LoadReport Empty { get; } = new([], 0, 0, 0);

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadReport"/> class.
    /// </summary>
    /// <param name="skippedNodeTypes">The distinct unknown node type keys in first-seen order.</param>
    /// <param name="skippedNodes">How many nodes were skipped.</param>
    /// <param name="skippedWires">How many wires were skipped because they touched a skipped node.</param>
    /// <param name="skippedGroups">How many groups were skipped because every member was skipped.</param>
    internal LoadReport(IReadOnlyList<string> skippedNodeTypes, int skippedNodes, int skippedWires, int skippedGroups)
    {
        SkippedNodeTypes = skippedNodeTypes;
        SkippedNodeCount = skippedNodes;
        SkippedWireCount = skippedWires;
        SkippedGroupCount = skippedGroups;
    }

    /// <summary>
    ///     Gets the distinct keys of the unknown node types, in first-seen order; empty when nothing was skipped.
    /// </summary>
    public IReadOnlyList<string> SkippedNodeTypes { get; }

    /// <summary>
    ///     Gets how many nodes were skipped.
    /// </summary>
    public int SkippedNodeCount { get; }

    /// <summary>
    ///     Gets how many wires were skipped because one of their nodes was skipped.
    /// </summary>
    public int SkippedWireCount { get; }

    /// <summary>
    ///     Gets how many groups were skipped because all of their members were skipped.
    /// </summary>
    public int SkippedGroupCount { get; }

    /// <summary>
    ///     Gets a value indicating whether anything was skipped.
    /// </summary>
    public bool HasSkipped => SkippedNodeCount > 0;

    /// <summary>
    ///     Gets a ready-to-show sentence describing what was skipped, or an empty string when nothing was.
    /// </summary>
    public string Message => !HasSkipped
        ? string.Empty
        : $"Skipped {SkippedNodeCount} node(s) of unknown type(s) {string.Join(", ", SkippedNodeTypes)}, " +
          $"{SkippedWireCount} wire(s) and {SkippedGroupCount} group(s) that depended on them.";
}
