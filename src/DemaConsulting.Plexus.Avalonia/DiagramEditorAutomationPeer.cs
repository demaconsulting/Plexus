using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Makes the editor visible to UI automation as a named, focusable pane.
/// </summary>
/// <remarks>
///     Plain custom controls are hidden from the automation tree by default, which would stop tools
///     such as UIA drivers from finding the editor by its AutomationId. The peer exposes the editor
///     itself; individual nodes and ports are located through
///     <see cref="DiagramEditor.GetElementScreenBounds"/> and <see cref="DiagramEditor.GetPortScreenPosition"/>.
/// </remarks>
internal sealed class DiagramEditorAutomationPeer : ControlAutomationPeer
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramEditorAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The editor the peer describes.</param>
    public DiagramEditorAutomationPeer(DiagramEditor owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(DiagramEditor);

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;
}
