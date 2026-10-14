namespace DemaConsulting.Plexus;

/// <content>
///     Editing support: a deep copy of the host payload to edit without touching the live one.
/// </content>
public sealed partial class Node
{
    /// <summary>
    ///     Creates an independent deep copy of the host payload, for an editor to modify and commit later.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The copy goes through the node type's own persistence (<see cref="NodeType.DataSerializer"/> or
    ///         <see cref="NodeType.DataType"/>, as snapshots do), so it is exactly what saving and loading would produce.
    ///         Edit the copy freely, then commit it together with property changes as one undo entry:
    ///     </para>
    ///     <code>
    ///     var copy = node.CloneData();                          // null when the node has no data
    ///     // ... a dialog edits copy ...
    ///     diagram.History.Transact("Edit node", () =&gt;
    ///     {
    ///         node.Properties.TrySetAll(changes, out _);
    ///         node.Data = copy;                                  // one undo entry for both
    ///     });
    ///     </code>
    /// </remarks>
    /// <returns>The copy, or null when <see cref="Data"/> is null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the node has data but its type declares neither <see cref="NodeType.DataType"/> nor <see cref="NodeType.DataSerializer"/>, or the data does not round-trip.</exception>
    public object? CloneData()
    {
        if (_data is null)
        {
            return null;
        }

        var json = NodeDataCodec.Serialize(this)
            ?? throw new InvalidOperationException($"Node '{Name}' has data, but its type '{Type.Key}' declares no DataType or DataSerializer, so the data cannot be copied.");
        return NodeDataCodec.TryDeserialize(Type, json, out var copy, out var problem)
            ? copy
            : throw new InvalidOperationException($"The data of node '{Name}' could not be copied: {problem}");
    }
}
