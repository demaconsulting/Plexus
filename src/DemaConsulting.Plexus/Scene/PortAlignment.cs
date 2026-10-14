namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Selects how a node's port column on one edge is positioned vertically along that edge.
/// </summary>
/// <remarks>
///     The alignment is a <see cref="DiagramTheme"/> setting (see <see cref="DiagramTheme.PortAlignment"/>);
///     the model, the routers and every painter read it through <see cref="NodeGeometry"/>, so
///     port anchors stay identical everywhere.
/// </remarks>
public enum PortAlignment
{
    /// <summary>
    ///     The inputs and the outputs each form a stack that is vertically centered on the node box.
    /// </summary>
    /// <remarks>
    ///     Anchors lie <see cref="DiagramTheme.PortPitch"/> apart; a stack with an even number of
    ///     ports therefore straddles the box's middle and lands on half-grid positions.
    /// </remarks>
    Center,

    /// <summary>
    ///     Each stack starts <see cref="DiagramTheme.FirstPortOffset"/> below the header and runs downwards.
    /// </summary>
    /// <remarks>
    ///     This is a vertical placement only: it does not move ports to the top edge of the node. Inputs stay on the left edge and outputs on the right edge.
    /// </remarks>
    Top,
}
