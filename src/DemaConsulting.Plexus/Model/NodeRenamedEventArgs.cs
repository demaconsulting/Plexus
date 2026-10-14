namespace DemaConsulting.Plexus;

/// <summary>
///     Event data raised after a node's name changed, including changes caused by undo and redo.
/// </summary>
public sealed class NodeRenamedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeRenamedEventArgs"/> class.
    /// </summary>
    /// <param name="node">The renamed node.</param>
    /// <param name="oldName">The previous name.</param>
    /// <param name="newName">The new name.</param>
    internal NodeRenamedEventArgs(Node node, string oldName, string newName)
    {
        Node = node;
        OldName = oldName;
        NewName = newName;
    }

    /// <summary>
    ///     Gets the renamed node.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    ///     Gets the name before the change.
    /// </summary>
    public string OldName { get; }

    /// <summary>
    ///     Gets the name after the change.
    /// </summary>
    public string NewName { get; }
}
