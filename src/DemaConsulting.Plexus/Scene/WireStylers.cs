using System.Runtime.CompilerServices;

namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Ready-made <see cref="Diagram.WireStyler"/> callbacks for the common cases, which compose with <see cref="Combine"/>.
/// </summary>
/// <remarks>
///     Each helper returns a plain <c>Func&lt;Wire, WireStyle?&gt;</c>, so they can be assigned directly,
///     combined, or called from a hand-written styler. <see cref="Combine"/> takes its stylers as a <c>params</c> array: pass them as separate arguments,
///     <c>WireStylers.Combine(first, second)</c>. A styler may return null for a wire to use the theme for that wire;
///     parameters named <c>map</c> receive a property value, which may be null.
/// </remarks>
/// <example>
///     Color wires by the type they carry and dash the wires leaving disabled nodes.
///     <code>
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Scene;
///
///     var diagram = new Diagram(types);                    // types: the registry of the DemaConsulting.Plexus example
///     diagram.WireStyler = WireStylers.Combine(
///         WireStylers.ByPortType(),
///         WireStylers.BySourceNodeProperty("Enabled", v =&gt; v is false ? new WireStyle { Dash = [6, 4] } : null));
///     </code>
/// </example>
public static class WireStylers
{
    /// <summary>
    ///     Creates a styler that colors each wire with the color of its source port's data type.
    /// </summary>
    /// <remarks>
    ///     The color comes from the diagram's <see cref="NodeTypeRegistry.PortTypes"/>; unregistered type
    ///     keys get the registry's neutral color.
    /// </remarks>
    /// <returns>The styler.</returns>
    public static Func<Wire, WireStyle?> ByPortType() =>
        wire => wire.Page is { } page
            ? new WireStyle { Color = page.Diagram.Types.PortTypes.Get(wire.Source.TypeKey).Color }
            : null;

    /// <summary>
    ///     Creates a styler driven by a property of the node the wire starts at.
    /// </summary>
    /// <param name="propertyName">The property name; a node without that property maps <see langword="null"/>.</param>
    /// <param name="map">Maps the property value (or null when the node has no such property) to a style, or null for the theme.</param>
    /// <returns>The styler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Func<Wire, WireStyle?> BySourceNodeProperty(string propertyName, Func<object?, WireStyle?> map)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(map);
        return wire => map(ReadProperty(wire.Source.Node, propertyName));
    }

    /// <summary>
    ///     Creates a styler driven by a property of the node the wire ends at.
    /// </summary>
    /// <param name="propertyName">The property name; a node without that property maps <see langword="null"/>.</param>
    /// <param name="map">Maps the property value (or null when the node has no such property) to a style, or null for the theme.</param>
    /// <returns>The styler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Func<Wire, WireStyle?> ByTargetNodeProperty(string propertyName, Func<object?, WireStyle?> map)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(map);
        return wire => map(ReadProperty(wire.Target.Node, propertyName));
    }

    /// <summary>
    ///     Creates a styler that applies one style to the wires matching a condition.
    /// </summary>
    /// <param name="condition">Decides whether a wire gets the style.</param>
    /// <param name="style">The style for matching wires.</param>
    /// <returns>The styler; non-matching wires get no style (the theme).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static Func<Wire, WireStyle?> When(Func<Wire, bool> condition, WireStyle style)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(style);
        return wire => condition(wire) ? style : null;
    }

    /// <summary>
    ///     Creates a styler whose per-page work is done once per page and diagram version, instead of once per wire.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use it when styling depends on something that is expensive compared with one wire and
    ///         shared by every wire of a page, such as a simulation or a graph traversal. The
    ///         <paramref name="factory"/> runs for a page the first time one of its wires is styled and
    ///         again whenever <see cref="Diagram.Version"/> has changed since (any edit, undo, redo or
    ///         <see cref="Diagram.RefreshAppearance"/>); the styler it returns is then reused for every
    ///         wire of that page. There is no cache to invalidate and no subscription order to get right.
    ///     </para>
    ///     <para>
    ///         Pages are tracked weakly, so a discarded page releases its styler. A factory that throws
    ///         styles the wire from the theme (like any failing styler) and is tried again for the next
    ///         wire; a factory that returns null styles the page's wires from the theme.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using DemaConsulting.Plexus;
    ///     using DemaConsulting.Plexus.Scene;
    ///
    ///     var diagram = new Diagram(types);                // types: the registry of the DemaConsulting.Plexus example
    ///     diagram.WireStyler = WireStylers.Combine(
    ///         WireStylers.ByPortType(),
    ///         WireStylers.PerPage(page =&gt;
    ///         {
    ///             var live = Simulate(page);               // once per page and per change of the diagram
    ///             return wire =&gt; live.Contains(wire.Source) ? new WireStyle { Thickness = 4 } : null;
    ///         }));
    ///
    ///     // Stand-in for host code that is expensive compared with styling one wire: here, the outputs that have a wire
    ///     static HashSet&lt;Port&gt; Simulate(DiagramPage page) =&gt; [.. page.Wires.Select(w =&gt; w.Source)];
    ///     </code>
    /// </example>
    /// <param name="factory">Builds the styler of a page; called with the page the wire is on.</param>
    /// <returns>The styler, to assign to <see cref="Diagram.WireStyler"/> or pass to <see cref="Combine"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public static Func<Wire, WireStyle?> PerPage(Func<DiagramPage, Func<Wire, WireStyle?>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var pages = new ConditionalWeakTable<DiagramPage, PageStyler>();
        return wire =>
        {
            // A wire that is not on a page has nothing to evaluate
            if (wire.Page is not { } page)
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

            return entry.Styler(wire);
        };
    }

    /// <summary>
    ///     Combines stylers so that later ones override the members earlier ones set.
    /// </summary>
    /// <remarks>
    ///     Every styler is evaluated for every wire; a member left null by a later styler keeps the
    ///     value from the earlier ones (see <see cref="WireStyle.WithOverrides"/>). The result is null
    ///     when no styler returned a style.
    /// </remarks>
    /// <param name="stylers">The stylers in increasing priority.</param>
    /// <returns>The combined styler.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the array or one of its stylers is null.</exception>
    public static Func<Wire, WireStyle?> Combine(params Func<Wire, WireStyle?>[] stylers)
    {
        ArgumentNullException.ThrowIfNull(stylers);
        var copy = (Func<Wire, WireStyle?>[])stylers.Clone();
        foreach (var styler in copy)
        {
            ArgumentNullException.ThrowIfNull(styler);
        }

        return wire =>
        {
            WireStyle? result = null;
            foreach (var styler in copy)
            {
                if (styler(wire) is { } style)
                {
                    result = result is null ? style : result.WithOverrides(style);
                }
            }

            return result;
        };
    }

    /// <summary>
    ///     Reads a node property value, or null when the node has no such property.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value, or null.</returns>
    private static object? ReadProperty(Node node, string propertyName) =>
        node.Properties.TryGetValue(propertyName, out var value) ? value : null;

    /// <summary>
    ///     The styler of a page whose factory returned null: it styles nothing.
    /// </summary>
    private static readonly Func<Wire, WireStyle?> NoStyle = _ => null;

    /// <summary>
    ///     The styler built for one page and the diagram version it was built at.
    /// </summary>
    private sealed class PageStyler
    {
        /// <summary>
        ///     Gets or sets the styler, or null before the first build.
        /// </summary>
        public Func<Wire, WireStyle?>? Styler { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="Diagram.Version"/> the styler was built at.
        /// </summary>
        public long Version { get; set; }
    }
}
