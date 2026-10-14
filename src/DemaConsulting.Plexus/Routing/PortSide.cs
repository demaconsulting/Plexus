namespace DemaConsulting.Plexus.Routing;

/// <summary>
///     Identifies the side of a node box a wire leaves or enters through.
/// </summary>
/// <remarks>
///     Inputs sit on the left side and outputs on the right side today; top and bottom exist so the
///     router contract does not need to change if ports on other sides are ever added.
/// </remarks>
public enum PortSide
{
    /// <summary>The left edge of the node box.</summary>
    Left,

    /// <summary>The right edge of the node box.</summary>
    Right,

    /// <summary>The top edge of the node box.</summary>
    Top,

    /// <summary>The bottom edge of the node box.</summary>
    Bottom,
}
