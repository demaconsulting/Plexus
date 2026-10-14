namespace DemaConsulting.Plexus;

/// <summary>
///     Content that passed validation and is ready to be attached to a page, or the reasons it could not be prepared.
/// </summary>
/// <remarks>
///     Preparation constructs detached elements only (no page or history is touched), so a failed
///     preparation leaves the target untouched and <see cref="Apply"/> is the single mutation step.
/// </remarks>
internal sealed class PreparedImport
{
    /// <summary>
    ///     The created elements in attach (z) order.
    /// </summary>
    private readonly List<DiagramElement> _elements = [];

    /// <summary>
    ///     Non-fatal findings collected during preparation and application.
    /// </summary>
    private readonly List<string> _warnings = [];

    /// <summary>
    ///     Gets a value indicating whether the content is ready to apply.
    /// </summary>
    public bool Success => Error is null;

    /// <summary>
    ///     Gets the reason preparation failed, or null.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    ///     Gets every node type key the registry lacks; empty unless unknown types caused the failure.
    /// </summary>
    public IReadOnlyList<string> MissingNodeTypes { get; private set; } = [];

    /// <summary>
    ///     Gets the non-fatal findings.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    ///     Gets the prepared elements in z-order (bottom-most first).
    /// </summary>
    public IReadOnlyList<DiagramElement> Elements => _elements;

    /// <summary>
    ///     Marks the preparation as failed.
    /// </summary>
    /// <param name="error">The reason.</param>
    /// <param name="missingNodeTypes">The missing node type keys, if that is the reason.</param>
    public void Fail(string error, IReadOnlyList<string>? missingNodeTypes = null)
    {
        Error = error;
        MissingNodeTypes = missingNodeTypes ?? [];
    }

    /// <summary>
    ///     Records a non-fatal finding.
    /// </summary>
    /// <param name="warning">The message.</param>
    public void Warn(string warning) => _warnings.Add(warning);

    /// <summary>
    ///     Registers a prepared element in attach order.
    /// </summary>
    /// <param name="element">The element.</param>
    public void AddElement(DiagramElement element) => _elements.Add(element);

    /// <summary>
    ///     Attaches the prepared elements to a page as steps of an operation plan.
    /// </summary>
    /// <remarks>
    ///     Restoring attaches in exact z-order. Cloning attaches everything except wires first and
    ///     then each wire after consulting <see cref="DiagramPage.CanConnect"/>, so wires the target's
    ///     connection rules reject are skipped with a warning instead of producing an invalid diagram.
    /// </remarks>
    /// <param name="page">The target page.</param>
    /// <param name="plan">The plan collecting the undoable steps.</param>
    /// <param name="exactZOrder">True to keep the prepared z-order exactly and skip rule checks.</param>
    /// <returns>The elements that were attached, in z-order.</returns>
    public List<DiagramElement> Apply(DiagramPage page, CommandPlan plan, bool exactZOrder)
    {
        var attached = new List<DiagramElement>(_elements.Count);
        if (exactZOrder)
        {
            foreach (var element in _elements)
            {
                plan.Run(new ElementPresenceCommand(DescribeAdd(element), page, element, add: true));
                attached.Add(element);
            }

            return attached;
        }

        // Everything a wire depends on goes in first, so the rule check sees the final nodes
        foreach (var element in _elements.Where(e => e is not Wire))
        {
            plan.Run(new ElementPresenceCommand(DescribeAdd(element), page, element, add: true));
            attached.Add(element);
        }

        foreach (var wire in _elements.OfType<Wire>())
        {
            var check = page.CanConnect(wire.Source, wire.Target);
            if (!check.IsAllowed)
            {
                Warn($"A wire from '{wire.Source}' to '{wire.Target}' was not pasted: {check.Reason}");
                continue;
            }

            plan.Run(new ElementPresenceCommand(DescribeAdd(wire), page, wire, add: true));
            attached.Add(wire);
        }

        return attached;
    }

    /// <summary>
    ///     Describes the step that adds an element.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The step description.</returns>
    private static string DescribeAdd(DiagramElement element) => element switch
    {
        Node => "Add node",
        Wire => "Connect",
        Annotation => "Add annotation",
        _ => "Add group",
    };
}
