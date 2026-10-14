namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes a double-click on the body of a node (not on its name, which starts an inline rename).
/// </summary>
/// <remarks>
///     Hosts typically react by opening a property editor for <see cref="Node"/>. The editor itself
///     never opens any property UI.
/// </remarks>
/// <example>
///     Open a host-built dialog for the double-clicked node (see the namespace example for a complete properties dialog).
///     <code><![CDATA[
///     using Avalonia.Controls;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class DoubleClickSample
///     {
///         public static void Wire(DiagramEditor editor, Window owner)
///         {
///             editor.NodeDoubleClicked += async (_, e) =>
///             {
///                 var dialog = new Window { Title = $"Properties - {e.Node.Name}", Width = 300, Height = 200 };
///                 await dialog.ShowDialog(owner);
///             };
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
