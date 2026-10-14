using Avalonia.Automation.Peers;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Makes the node palette visible to UI automation as a named, focusable list.
/// </summary>
/// <remarks>
///     Plain custom controls are hidden from the automation tree by default. The peer exposes the
///     palette itself as a list; each item is exposed as a list item by
///     <see cref="NodePaletteItemAutomationPeer"/>, named after the node type's display name.
/// </remarks>
internal sealed class NodePaletteAutomationPeer : ControlAutomationPeer
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePaletteAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The palette the peer describes.</param>
    public NodePaletteAutomationPeer(NodePalette owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(NodePalette);

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;
}
