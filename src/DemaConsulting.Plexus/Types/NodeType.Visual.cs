using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus;

/// <content>
///     Appearance hook: an optional custom visual replacing the standard node template.
/// </content>
public sealed partial class NodeType
{
    /// <summary>
    ///     Gets a custom visual that draws nodes of this type; null selects the <see cref="StandardNodeVisual"/> template.
    /// </summary>
    /// <remarks>
    ///     The escape hatch for nodes that need a completely different look. Because the visual
    ///     emits painter-agnostic primitives it works in every painter. Ports still attach where
    ///     <see cref="NodeLayout"/> places them.
    /// </remarks>
    public INodeVisual? Visual { get; init; }
}
