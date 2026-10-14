namespace DemaConsulting.Plexus;

/// <summary>
///     Event data raised before a node is renamed, allowing a host to correct or veto the new name.
/// </summary>
/// <remarks>
///     Handlers may assign <see cref="NewName"/> to correct the proposal, or set <see cref="Cancel"/>
///     (optionally with a <see cref="Reason"/>) to reject it. The corrected name is validated again
///     with the default rules (non-empty, unique when required).
/// </remarks>
public sealed class NodeRenamingEventArgs : EventArgs
{
    /// <summary>
    ///     The name that will be applied; starts as the proposed name.
    /// </summary>
    private string _newName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeRenamingEventArgs"/> class.
    /// </summary>
    /// <param name="node">The node being renamed.</param>
    /// <param name="oldName">The current name.</param>
    /// <param name="proposedName">The name that was requested.</param>
    internal NodeRenamingEventArgs(Node node, string oldName, string proposedName)
    {
        Node = node;
        OldName = oldName;
        ProposedName = proposedName;
        _newName = proposedName;
    }

    /// <summary>
    ///     Gets the node being renamed.
    /// </summary>
    public Node Node { get; }

    /// <summary>
    ///     Gets the node's current name.
    /// </summary>
    public string OldName { get; }

    /// <summary>
    ///     Gets the name that was requested (trimmed).
    /// </summary>
    public string ProposedName { get; }

    /// <summary>
    ///     Gets or sets the name that will be applied; assign to correct the proposal.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
    public string NewName
    {
        get => _newName;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _newName = value;
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the rename is rejected.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    ///     Gets or sets the explanation reported to the caller when the rename is cancelled.
    /// </summary>
    public string? Reason { get; set; }
}
