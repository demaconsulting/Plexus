using System.Diagnostics.CodeAnalysis;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <content>
///     Dynamic appearance: the per-wire and per-node stylers and the refresh hook for styles that depend on host state.
/// </content>
public sealed partial class Diagram
{
    /// <summary>
    ///     The host callback that styles individual wires, or null for theme-only wires.
    /// </summary>
    private Func<Wire, WireStyle?>? _wireStyler;

    /// <summary>
    ///     Gets or sets the callback that styles individual wires, or null (the default) to draw every wire from the theme.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The type is <c>Func&lt;Wire, WireStyle?&gt;?</c>: the property may be null (no styler) and the styler may return null for a wire.
    ///         The callback is evaluated for every wire each time a scene is built, so the style follows
    ///         whatever the wire is connected to: its ports, their types, the connected nodes, node
    ///         properties or host data. Return null, or leave members of the returned
    ///         <see cref="WireStyle"/> null, to use the <see cref="Theme"/> for those members.
    ///     </para>
    ///     <para>
    ///         The styler is configuration, not content: it is not undoable and not serialized. Setting
    ///         it raises <see cref="Changed"/> with <see cref="DiagramChangeKind.SettingsChanged"/> so
    ///         editors repaint. When the styler depends on state outside the diagram, call
    ///         <see cref="RefreshAppearance"/> after that state changes. An exception thrown by the
    ///         styler never breaks rendering; the affected wire is drawn from the theme.
    ///     </para>
    ///     <para>
    ///         <see cref="WireStylers"/> offers ready-made stylers for the common cases; use
    ///         <see cref="WireStylers.PerPage"/> when the styling needs a computation that is shared by all
    ///         the wires of a page, so it runs once per page and <see cref="Version"/> instead of once per wire.
    ///     </para>
    /// </remarks>
    public Func<Wire, WireStyle?>? WireStyler
    {
        get => _wireStyler;
        set
        {
            _wireStyler = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(WireStyler));
        }
    }

    /// <summary>
    ///     The host callback that styles individual nodes, or null for theme-only nodes.
    /// </summary>
    private Func<Node, NodeStyle?>? _nodeStyler;

    /// <summary>
    ///     Gets or sets the callback that styles individual nodes, or null (the default) to draw every node from the theme and its type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The node equivalent of <see cref="WireStyler"/>, with the same rules; its type is <c>Func&lt;Node, NodeStyle?&gt;?</c> (the property and the returned style may each be null). The callback is
    ///         evaluated for every visible node each time a scene is built, so the style follows the
    ///         node's properties, its connections or host data. Return null, or leave members of the
    ///         returned <see cref="NodeStyle"/> null, to use the <see cref="Theme"/> for those members.
    ///     </para>
    ///     <para>
    ///         The styler is configuration, not content: it is not undoable and not serialized. Setting
    ///         it raises <see cref="Changed"/> with <see cref="DiagramChangeKind.SettingsChanged"/> so
    ///         editors repaint. When the styler depends on state outside the diagram, call
    ///         <see cref="RefreshAppearance"/> after that state changes. An exception thrown by the
    ///         styler never breaks rendering; the affected node is drawn from the theme.
    ///     </para>
    ///     <para>
    ///         <see cref="NodeStylers"/> offers ready-made stylers; use <see cref="NodeStylers.PerPage"/>
    ///         when the styling needs a computation shared by all the nodes of a page. Selection and hover
    ///         outlines are always drawn on top of the styled node. Node palette prototypes are not styled
    ///         by this callback: they show the node type's default look.
    ///     </para>
    /// </remarks>
    public Func<Node, NodeStyle?>? NodeStyler
    {
        get => _nodeStyler;
        set
        {
            _nodeStyler = value;
            RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, nameof(NodeStyler));
        }
    }

    /// <summary>
    ///     Asks editors and painters to rebuild, without changing the model or adding an undo entry.
    /// </summary>
    /// <remarks>
    ///     Call this when a <see cref="WireStyler"/> or <see cref="NodeStyler"/> depends on host state outside the diagram (for
    ///     example a live simulation value) and that state changed. It raises <see cref="Changed"/>
    ///     with <see cref="DiagramChangeKind.SettingsChanged"/> and the property name
    ///     <c>"Appearance"</c>, and so advances <see cref="Version"/>; <see cref="History"/> is not touched.
    /// </remarks>
    public void RefreshAppearance() =>
        RaiseChanged(DiagramChangeKind.SettingsChanged, null, null, "Appearance");

    /// <summary>
    ///     Evaluates the <see cref="WireStyler"/> for one wire, treating a failing styler as "no style".
    /// </summary>
    /// <param name="wire">The wire to style.</param>
    /// <returns>The style, or null when there is no styler, it returned null, or it threw.</returns>
    [SuppressMessage("Major Code Smell", "S2221:Exception should not be caught when not required by called methods", Justification = "The styler is host code; whatever it throws must not break rendering, so the wire falls back to the theme.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The styler is host code; whatever it throws must not break rendering, so the wire falls back to the theme.")]
    internal WireStyle? ResolveWireStyle(Wire wire)
    {
        var styler = _wireStyler;
        if (styler is null)
        {
            return null;
        }

        try
        {
            return styler(wire);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Evaluates the <see cref="NodeStyler"/> for one node, treating a failing styler as "no style".
    /// </summary>
    /// <param name="node">The node to style.</param>
    /// <returns>The style, or null when there is no styler, it returned null, or it threw.</returns>
    [SuppressMessage("Major Code Smell", "S2221:Exception should not be caught when not required by called methods", Justification = "The styler is host code; whatever it throws must not break rendering, so the node falls back to the theme.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The styler is host code; whatever it throws must not break rendering, so the node falls back to the theme.")]
    internal NodeStyle? ResolveNodeStyle(Node node)
    {
        var styler = _nodeStyler;
        if (styler is null)
        {
            return null;
        }

        try
        {
            return styler(node);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
