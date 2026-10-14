namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a node type the user activated in a <see cref="NodePalette"/> (double-click or Enter on its item).
/// </summary>
/// <remarks>
///     Hosts typically react by adding a node of <see cref="NodeType"/> at the center of the editor,
///     for example <c>page.AddNode(e.NodeType, center)</c>. Dragging an item onto the editor needs no
///     event at all: the editor creates the node itself. A palette that follows an editor
///     (<see cref="NodePalette.Editor"/>) also adds the node itself, at the editor's visible center,
///     unless a handler sets <see cref="Handled"/>.
/// </remarks>
/// <example>
///     Add the activated node type at a fixed place and take over from the palette's default action.
///     <code><![CDATA[
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class ActivationSample
///     {
///         public static void Wire(NodePalette palette, DiagramEditor editor)
///         {
///             palette.NodeTypeActivated += (_, e) =>
///             {
///                 editor.Page?.AddNode(e.NodeType, new DemaConsulting.Plexus.Geometry.Point(120, 120));
///                 e.Handled = true;                           // do not also add one at the editor's center
///             };
///         }
///     }
///     ]]></code>
/// </example>
public sealed class NodeTypeActivatedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeTypeActivatedEventArgs"/> class.
    /// </summary>
    /// <param name="nodeType">The node type that was activated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nodeType"/> is null.</exception>
    public NodeTypeActivatedEventArgs(NodeType nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        NodeType = nodeType;
    }

    /// <summary>
    ///     Gets the node type that was activated.
    /// </summary>
    public NodeType NodeType { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether a handler dealt with the activation, which suppresses the palette's default action (default false).
    /// </summary>
    public bool Handled { get; set; }
}
