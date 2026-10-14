namespace DemaConsulting.Plexus;

/// <content>
///     Snapshot support: initializing the payload of a node that is not attached yet.
/// </content>
public sealed partial class Node
{
    /// <summary>
    ///     Stores the host payload without history or notifications.
    /// </summary>
    /// <remarks>
    ///     Used while a node is being materialized from a snapshot, clipboard payload or duplicate,
    ///     before it is attached (the public setter requires an attached node and records history).
    /// </remarks>
    /// <param name="data">The payload, or null.</param>
    internal void InitializeData(object? data) => _data = data;
}
