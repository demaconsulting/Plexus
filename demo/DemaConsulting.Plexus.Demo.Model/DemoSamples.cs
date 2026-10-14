namespace DemaConsulting.Plexus.Demo.Model;

/// <summary>
///     Builds the demo's sample diagrams: one per node domain, a groups demo and a showcase of all of them.
/// </summary>
/// <remarks>
///     Samples double as documentation of the library's "hello diagram" path: add pages, add
///     nodes by type key, set properties, connect ports. Each call builds a fresh, independent
///     diagram with a clean (empty) undo history, because constructing a sample is not a user edit.
///     Node names are unique across a whole diagram, so every page uses names of its own.
/// </remarks>
public static partial class DemoSamples
{
    /// <summary>
    ///     Gets the names accepted by <see cref="Create"/>.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ["math", "plumbing", "logic", "audio", "workflow", "groups", "wiring", "showcase"];

    /// <summary>
    ///     Creates a sample diagram by name.
    /// </summary>
    /// <param name="name">One of <see cref="Names"/> (case-insensitive).</param>
    /// <returns>A new diagram.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is not a known sample.</exception>
    public static Diagram Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.ToUpperInvariant() switch
        {
            "MATH" => Math(),
            "PLUMBING" => Plumbing(),
            "LOGIC" => Logic(),
            "AUDIO" => Audio(),
            "WORKFLOW" => Workflow(),
            "GROUPS" => Groups(),
            "WIRING" => Wiring(),
            "SHOWCASE" => Showcase(),
            _ => throw new ArgumentException($"Unknown sample '{name}'. Known samples: {string.Join(", ", Names)}.", nameof(name)),
        };
    }

    /// <summary>
    ///     Creates the math dataflow sample (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Math() => Build(diagram => AddMathPage(diagram.Pages.Add("Math")));

    /// <summary>
    ///     Creates the plumbing sample (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Plumbing() => Build(diagram => AddPlumbingPage(diagram.Pages.Add("Plumbing")));

    /// <summary>
    ///     Creates the logic gate sample (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Logic() => Build(diagram => AddLogicPage(diagram.Pages.Add("Logic")));

    /// <summary>
    ///     Creates the audio synth sample (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Audio() => Build(diagram => AddAudioPage(diagram.Pages.Add("Audio")));

    /// <summary>
    ///     Creates the business workflow sample (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Workflow() => Build(diagram => AddWorkflowPage(diagram.Pages.Add("Workflow")));

    /// <summary>
    ///     Creates the groups sample: an expanded group nesting another group, and a collapsed group (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Groups() => Build(diagram => AddGroupsPage(diagram.Pages.Add("Groups")));

    /// <summary>
    ///     Creates the wiring sample: junction dots, square T corners and crossing hops in many net configurations (one page).
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Wiring() => Build(diagram => AddWiringPage(diagram.Pages.Add("Wiring")));

    /// <summary>
    ///     Creates the multi-page showcase: every domain sample plus the groups and wiring pages.
    /// </summary>
    /// <returns>A new diagram.</returns>
    public static Diagram Showcase() => Build(diagram =>
    {
        AddMathPage(diagram.Pages.Add("Math"));
        AddPlumbingPage(diagram.Pages.Add("Plumbing"));
        AddLogicPage(diagram.Pages.Add("Logic"));
        AddAudioPage(diagram.Pages.Add("Audio"));
        AddWorkflowPage(diagram.Pages.Add("Workflow"));
        AddGroupsPage(diagram.Pages.Add("Groups"));
        AddWiringPage(diagram.Pages.Add("Wiring"));
    });

    /// <summary>
    ///     Creates an empty demo diagram, fills it without recording undo history, and returns it clean.
    /// </summary>
    /// <remarks>
    ///     Ending the suspension discards the (empty) history and restores the clean state the new diagram had.
    /// </remarks>
    /// <param name="fill">Adds the pages and elements.</param>
    /// <returns>The populated diagram.</returns>
    private static Diagram Build(Action<Diagram> fill)
    {
        // The demo options are installed first, so the sample draws live wire colors like every other demo diagram
        var diagram = new Diagram(DemoNodeTypes.CreateRegistry(), DemoAppearance.Options);
        using (diagram.History.Suspend())
        {
            fill(diagram);
        }

        return diagram;
    }

    /// <summary>
    ///     Adds a node and sets some of its properties.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="type">The node type key.</param>
    /// <param name="name">The node name.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="properties">Property names and values to set, in pairs.</param>
    /// <returns>The new node.</returns>
    private static Node AddNode(DiagramPage page, string type, string name, double x, double y, params (string Name, object Value)[] properties)
    {
        var node = page.AddNode(type, x, y, name);
        foreach (var (propertyName, value) in properties)
        {
            node.Properties[propertyName] = value;
        }

        return node;
    }
}
