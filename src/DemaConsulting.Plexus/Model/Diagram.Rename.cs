namespace DemaConsulting.Plexus;

/// <content>
///     The node rename pipeline: default validation, host correction or veto through <see cref="NodeRenaming"/>, then an undoable change.
/// </content>
public sealed partial class Diagram
{
    /// <summary>
    ///     Renames a node, throwing when the name is rejected.
    /// </summary>
    /// <remarks>
    ///     This is what the <see cref="Node.Name"/> setter calls. UIs that want to show the reason
    ///     instead of handling an exception should use <see cref="TryRename"/>.
    /// </remarks>
    /// <param name="node">The node to rename; must be attached to this diagram.</param>
    /// <param name="proposed">The proposed name; surrounding whitespace is trimmed.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the node is not attached to this diagram or the name is rejected.</exception>
    public void Rename(Node node, string proposed)
    {
        if (!TryRename(node, proposed, out var reason))
        {
            throw new ArgumentException(reason, nameof(proposed));
        }
    }

    /// <summary>
    ///     Attempts to rename a node, reporting why the rename was rejected instead of throwing.
    /// </summary>
    /// <remarks>
    ///     The proposal is trimmed and validated first (non-empty, unique when
    ///     <see cref="RequireUniqueNodeNames"/> is set). If it passes, <see cref="NodeRenaming"/> is
    ///     raised so a handler can correct or cancel it; a corrected name is validated again. A
    ///     successful rename is one undo entry and raises <see cref="NodeRenamed"/>. Renaming to the
    ///     node's current name succeeds without any change.
    /// </remarks>
    /// <param name="node">The node to rename; must be attached to this diagram.</param>
    /// <param name="proposed">The proposed name.</param>
    /// <param name="reason">Receives the rejection reason, or null on success.</param>
    /// <returns><see langword="true"/> when the node now has the requested (or corrected) name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the node is not attached to this diagram.</exception>
    public bool TryRename(Node node, string proposed, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(proposed);
        if (node.Page is not { IsAttached: true } page || !ReferenceEquals(page.Diagram, this))
        {
            throw new ArgumentException("The node is not attached to this diagram.", nameof(node));
        }

        // Step 1: default validation of the proposal, before any host code runs
        var trimmed = proposed.Trim();
        reason = ValidateNodeName(trimmed, node);
        if (reason is not null)
        {
            return false;
        }

        // Renaming to the current name is a successful no-op and must not pollute history
        if (string.Equals(trimmed, node.Name, StringComparison.Ordinal))
        {
            return true;
        }

        // Step 2: let the host correct or veto
        var args = new NodeRenamingEventArgs(node, node.Name, trimmed);
        OnNodeRenaming(args);
        if (args.Cancel)
        {
            reason = args.Reason ?? "The rename was cancelled.";
            return false;
        }

        // Step 3: a corrected name must still satisfy the default rules
        var finalName = args.NewName.Trim();
        if (!string.Equals(finalName, trimmed, StringComparison.Ordinal))
        {
            reason = ValidateNodeName(finalName, node);
            if (reason is not null)
            {
                return false;
            }
        }

        if (string.Equals(finalName, node.Name, StringComparison.Ordinal))
        {
            return true;
        }

        // Step 4: apply as an undoable change
        var oldName = node.Name;
        History.Execute(new SetValueCommand<string>(
            "Rename node",
            node,
            "Name",
            oldName,
            finalName,
            value => ApplyNodeName(node, value)));
        return true;
    }

    /// <summary>
    ///     Applies a name to a node and raises the change notifications; used by do, undo and redo.
    /// </summary>
    /// <param name="node">The node to rename.</param>
    /// <param name="name">The name to apply.</param>
    internal void ApplyNodeName(Node node, string name)
    {
        var oldName = node.Name;
        node.SetNameCore(name);
        node.NotifyChanged(nameof(Node.Name));
        OnNodeRenamed(new NodeRenamedEventArgs(node, oldName, name));
    }

    /// <summary>
    ///     Applies the default name rules.
    /// </summary>
    /// <param name="name">The trimmed candidate name.</param>
    /// <param name="self">The node being named (excluded from the uniqueness check), or null for a new node.</param>
    /// <returns>The reason the name is invalid, or null when it is acceptable.</returns>
    internal string? ValidateNodeName(string name, Node? self)
    {
        if (name.Length == 0)
        {
            return "A node name cannot be empty.";
        }

        if (RequireUniqueNodeNames && AllNodes().Any(n => !ReferenceEquals(n, self) && string.Equals(n.Name, name, StringComparison.Ordinal)))
        {
            return $"The name '{name}' is already used by another node.";
        }

        return null;
    }
}
