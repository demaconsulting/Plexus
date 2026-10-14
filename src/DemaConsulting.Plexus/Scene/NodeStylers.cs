using System.Runtime.CompilerServices;

namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Ready-made <see cref="Diagram.NodeStyler"/> callbacks for the common cases, which compose with <see cref="Combine"/>.
/// </summary>
/// <remarks>
///     Each helper returns a plain <c>Func&lt;Node, NodeStyle?&gt;</c>, so they can be assigned directly,
///     combined, or called from a hand-written styler. They mirror <see cref="WireStylers"/>. Nullability: <see cref="Diagram.NodeStyler"/> has the same type,
///     <c>Func&lt;Node, NodeStyle?&gt;?</c>; the styler may return null for a node (use the theme for that node), and the <c>map</c> parameter of
///     <see cref="ByProperty"/> is a <c>Func&lt;object?, NodeStyle?&gt;</c>: its value may be null and so may the style it returns.
/// </remarks>
/// <example>
///     Fade disabled nodes and give the node named "Output" a green halo.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Geometry;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     diagram.NodeStyler = NodeStylers.Combine(
///         NodeStylers.ByProperty("Enabled", v =&gt; v is false ? new NodeStyle { Opacity = 0.5 } : null),
///         NodeStylers.When(node =&gt; node.Name == "Output", new NodeStyle { Glow = Color.Parse("#40FF80") }));
///     </code>
/// </example>
public static class NodeStylers
{
    /// <summary>
    ///     Creates a styler driven by a property of the node.
    /// </summary>
    /// <param name="propertyName">The property name; a node without that property maps <see langword="null"/>.</param>
    /// <param name="map">Maps the property value (or null when the node has no such property) to a style, or null for the theme.</param>
    /// <returns>The styler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Func<Node, NodeStyle?> ByProperty(string propertyName, Func<object?, NodeStyle?> map)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(map);
        return node => map(node.Properties.TryGetValue(propertyName, out var value) ? value : null);
    }

    /// <summary>
    ///     Creates a styler that applies one style to the nodes matching a condition.
    /// </summary>
    /// <param name="condition">Decides whether a node gets the style.</param>
    /// <param name="style">The style for matching nodes.</param>
    /// <returns>The styler; non-matching nodes get no style (the theme).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Func<Node, NodeStyle?> When(Func<Node, bool> condition, NodeStyle style)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(style);
        return node => condition(node) ? style : null;
    }

    /// <summary>
    ///     Creates a styler whose per-page work is done once per page and diagram version, instead of once per node.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use it when styling depends on something that is expensive compared with one node and
    ///         shared by every node of a page, such as a simulation or a graph traversal. The
    ///         <paramref name="factory"/> runs for a page the first time one of its nodes is styled and
    ///         again whenever <see cref="Diagram.Version"/> has changed since (any edit, undo, redo or
    ///         <see cref="Diagram.RefreshAppearance"/>); the styler it returns is then reused for every
    ///         node of that page. There is no cache to invalidate and no subscription order to get right.
    ///     </para>
    ///     <para>
    ///         Pages are tracked weakly, so a discarded page releases its styler. A factory that throws
    ///         styles the node from the theme (like any failing styler) and is tried again for the next
    ///         node; a factory that returns null styles the page's nodes from the theme.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Geometry;
    ///     using DemaConsulting.Plexus.Scene;
    ///
    ///     var diagram = new Diagram(types);                // types: the registry of the DemaConsulting.Plexus example
    ///     diagram.NodeStyler = NodeStylers.PerPage(page =&gt;
    ///     {
    ///         var live = Simulate(page);                   // once per page and per change of the diagram
    ///         return node =&gt; live.Contains(node) ? new NodeStyle { Fill = Color.Parse("#FFC040") } : null;
    ///     });
    ///
    ///     // Stand-in for host code that is expensive compared with styling one node: here, the nodes that have wires
    ///     static HashSet&lt;Node&gt; Simulate(DiagramPage page) =&gt; [.. page.Nodes.Where(n =&gt; n.Wires.Count &gt; 0)];
    ///     </code>
    /// </example>
    /// <param name="factory">Builds the styler of a page; called with the page the node is on.</param>
    /// <returns>The styler, to assign to <see cref="Diagram.NodeStyler"/> or pass to <see cref="Combine"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public static Func<Node, NodeStyle?> PerPage(Func<DiagramPage, Func<Node, NodeStyle?>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var pages = new ConditionalWeakTable<DiagramPage, PageStyler>();
        return node =>
        {
            // A node that is not on a page has nothing to evaluate
            if (node.Page is not { } page)
            {
                return null;
            }

            // Reuse the page's styler while the diagram has not changed since it was built
            var entry = pages.GetValue(page, _ => new PageStyler());
            var version = page.Diagram.Version;
            if (entry.Styler is null || entry.Version != version)
            {
                entry.Styler = factory(page) ?? NoStyle;
                entry.Version = version;
            }

            return entry.Styler(node);
        };
    }

    /// <summary>
    ///     Combines stylers so that later ones override the members earlier ones set.
    /// </summary>
    /// <remarks>
    ///     Every styler is evaluated for every node; a member left null by a later styler keeps the
    ///     value from the earlier ones (see <see cref="NodeStyle.WithOverrides"/>). The result is null
    ///     when no styler returned a style.
    /// </remarks>
    /// <param name="stylers">The stylers in increasing priority.</param>
    /// <returns>The combined styler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the array or one of its stylers is null.</exception>
    public static Func<Node, NodeStyle?> Combine(params Func<Node, NodeStyle?>[] stylers)
    {
        ArgumentNullException.ThrowIfNull(stylers);
        var copy = (Func<Node, NodeStyle?>[])stylers.Clone();
        foreach (var styler in copy)
        {
            ArgumentNullException.ThrowIfNull(styler);
        }

        return node =>
        {
            NodeStyle? result = null;
            foreach (var styler in copy)
            {
                if (styler(node) is { } style)
                {
                    result = result is null ? style : result.WithOverrides(style);
                }
            }

            return result;
        };
    }

    /// <summary>
    ///     The styler of a page whose factory returned null: it styles nothing.
    /// </summary>
    private static readonly Func<Node, NodeStyle?> NoStyle = _ => null;

    /// <summary>
    ///     The styler built for one page and the diagram version it was built at.
    /// </summary>
    private sealed class PageStyler
    {
        /// <summary>
        ///     Gets or sets the styler, or null before the first build.
        /// </summary>
        public Func<Node, NodeStyle?>? Styler { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="Diagram.Version"/> the styler was built at.
        /// </summary>
        public long Version { get; set; }
    }
}
