using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Makes one node palette item visible to UI automation as a list item that can be scrolled into view and invoked.
/// </summary>
/// <remarks>
///     <para>
///         The item carries its name (the node type's display name) and AutomationId
///         (<see cref="NodePalette.GetAutomationId(NodeType)"/>) through the standard automation properties.
///     </para>
///     <para>
///         Two patterns make the palette drivable without filtering it down or guessing pixels. The
///         ScrollItem pattern (<c>ScrollIntoView</c>) is provided by Avalonia for every peer and reaches
///         <see cref="ControlAutomationPeer"/>'s bring-into-view, which scrolls the palette to the item;
///         an automation client calls it before reading the item's bounds. The Invoke pattern
///         (<see cref="IInvokeProvider"/>) selects and activates the type, exactly like a double-click.
///     </para>
/// </remarks>
internal sealed class NodePaletteItemAutomationPeer : ControlAutomationPeer, IInvokeProvider
{
    /// <summary>
    ///     The item the peer describes.
    /// </summary>
    private readonly NodePaletteItem _item;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodePaletteItemAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The item the peer describes.</param>
    public NodePaletteItemAutomationPeer(NodePaletteItem owner)
        : base(owner)
    {
        _item = owner;
    }

    /// <summary>
    ///     Selects and activates the node type of the item (the Invoke pattern).
    /// </summary>
    public void Invoke() => _item.Invoke();

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(NodePaletteItem);

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;
}
