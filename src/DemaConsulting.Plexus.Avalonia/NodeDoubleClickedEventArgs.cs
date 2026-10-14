namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a double-click on the body of a node (not on its name, which starts an inline rename).
/// </summary>
/// <remarks>
///     Hosts typically react by opening a property editor for <see cref="Node"/>. The editor itself
///     never opens any property UI.
/// </remarks>
/// <example>
///     Open the ready-made properties dialog of the <c>DemaConsulting.Plexus.Avalonia.PropertyGrid</c> package for the double-clicked node:
///     it edits the name, properties and host data and applies them as one undo entry on OK.
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///     using DemaConsulting.Plexus.Avalonia.PropertyGrid.Diagrams;
///
///     static class DoubleClickSample
///     {
///         public static void Wire(DiagramEditor editor, Window owner)
///         {
///             editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(owner, e.Node);
///         }
///     }
///     ]]></code>
/// </example>
public sealed class NodeDoubleClickedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeDoubleClickedEventArgs"/> class.
    /// </summary>
    /// <param name="node">The node that was double-clicked.</param>
    public NodeDoubleClickedEventArgs(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
    }

    /// <summary>
    ///     Gets the node that was double-clicked.
    /// </summary>
    public Node Node { get; }
}
