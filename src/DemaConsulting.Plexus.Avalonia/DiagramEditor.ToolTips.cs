using Avalonia;
using Avalonia.Controls;
using DemaConsulting.Plexus.Scene;

namespace DemaConsulting.Plexus.Avalonia;

/// <content>
///     Hover tooltips for nodes, ports and wires, shown with Avalonia's standard <c>ToolTip</c> service.
/// </content>
public sealed partial class DiagramEditor
{
    /// <summary>
    ///     Defines the <see cref="ShowToolTips"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowToolTipsProperty =
        AvaloniaProperty.Register<DiagramEditor, bool>(nameof(ShowToolTips), true);

    /// <summary>
    ///     The element and port the current tooltip describes, or null when no tooltip is set.
    /// </summary>
    private ToolTipTarget? _toolTipTarget;

    /// <summary>
    ///     The element and port that were under the pointer when a button was pressed: no tooltip is shown for them until the pointer moves to something else.
    /// </summary>
    private ToolTipTarget? _toolTipSuppressed;

    /// <summary>
    ///     Occurs when the pointer moves onto a node, port, wire, annotation or group, before its tooltip is shown; lets the host change or suppress the tooltip.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Raised once per element (or port) the pointer moves onto, while no drag, connection, inline edit or context
    ///         request is in progress and <see cref="ShowToolTips"/> is true. <see cref="DiagramToolTipRequestedEventArgs.Content"/>
    ///         carries the editor's default text (see <see cref="GetToolTipContent"/>); replace it, for example with the host's
    ///         validation messages or live values, or set <see cref="DiagramToolTipRequestedEventArgs.Cancel"/> to show nothing.
    ///         Setting <see cref="DiagramToolTipRequestedEventArgs.Content"/> to null or an empty string also shows nothing.
    ///         A handler may supply content for annotations and groups, which have no default text.
    ///         Also raised by <see cref="GetToolTipContent"/>, which does not depend on <see cref="ShowToolTips"/>.
    ///     </para>
    ///     <para>
    ///         The tooltip itself is Avalonia's: it opens after <c>ToolTip.ShowDelay</c> near the pointer (<c>ToolTip.Placement</c>
    ///         defaults to the pointer) and closes when the pointer leaves the element or a button is pressed. Tune the delay or
    ///         placement with the attached properties on the editor, for example <c>ToolTip.SetShowDelay(editor, 600)</c>.
    ///         The editor owns its own <c>ToolTip.Tip</c>; a host that wants to manage that itself sets <see cref="ShowToolTips"/> to false.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     Show the host's validation messages in node tooltips and suppress wire tooltips.
    ///     <code><![CDATA[
    ///     editor.ToolTipRequested += (_, e) =>
    ///     {
    ///         if (e.Element is Wire)
    ///         {
    ///             e.Cancel = true;
    ///         }
    ///         else if (e.Element is Node node && e.Port is null && validator.Check(node) is { Count: > 0 } problems)   // validator: your checks
    ///         {
    ///             e.Content = $"{e.Content}\n{string.Join("\n", problems)}";
    ///         }
    ///     };
    ///     ]]></code>
    /// </example>
    public event EventHandler<DiagramToolTipRequestedEventArgs>? ToolTipRequested;

    /// <summary>
    ///     Gets or sets a value indicating whether hovering nodes, ports and wires shows tooltips (default true).
    /// </summary>
    /// <remarks>
    ///     Turning it off hides a tooltip that is showing; while it is false hovering neither sets a tooltip nor raises
    ///     <see cref="ToolTipRequested"/>. It does not affect <see cref="GetToolTipContent"/>, which always raises the event.
    /// </remarks>
    public bool ShowToolTips
    {
        get => GetValue(ShowToolTipsProperty);
        set => SetValue(ShowToolTipsProperty, value);
    }

    /// <summary>
    ///     Gets the tooltip content the editor shows for an element or a port: the default text, as changed by <see cref="ToolTipRequested"/> handlers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The default text is: for a port, <c>"label (type)"</c> where type is the <c>PortType.DisplayName</c> of the port's
    ///         type (the type key itself when the type is not registered), followed by the port's description on a new line when
    ///         it has one; for a node, <c>NodeType.GetToolTip</c> (the type's tooltip function when it has one, else its description;
    ///         a null or empty result means no tooltip and the description is not used instead); for a wire,
    ///         <c>"source → target"</c> (for example <c>"A.out → B.in"</c>); for annotations and groups, nothing unless a handler
    ///         supplies content.
    ///     </para>
    ///     <para>
    ///         Raises <see cref="ToolTipRequested"/> with that default, then returns what the handlers left. Usable for any element,
    ///         also one on another page, for example to show the same text in a host's own panel or in automation. Does not depend
    ///         on <see cref="ShowToolTips"/>. An exception thrown by a node type's tooltip function or by a handler propagates
    ///         unchanged to the caller (and, while hovering, out of the pointer-move handling); the editor never swallows it.
    ///     </para>
    /// </remarks>
    /// <param name="element">The element; for a port, the port's node.</param>
    /// <param name="port">The port, or null to describe the element itself.</param>
    /// <returns>The content (a string unless a handler supplied other content), or null when there is no tooltip (canceled, null or empty).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public object? GetToolTipContent(DiagramElement element, Port? port = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        // Fill in the default, then let the host change or suppress it
        var args = new DiagramToolTipRequestedEventArgs(element, port, DefaultToolTip(element, port));
        ToolTipRequested?.Invoke(this, args);
        return args.Cancel || args.Content is null || args.Content is string { Length: 0 } ? null : args.Content;
    }

    /// <summary>
    ///     Hides the tooltip and forgets what it described; the next pointer move onto an element shows a fresh one.
    /// </summary>
    internal void ResetToolTip()
    {
        _toolTipTarget = null;
        if (ToolTip.GetTip(this) is not null)
        {
            ToolTip.SetTip(this, null);
        }
    }

    /// <summary>
    ///     Builds the editor's default tooltip text for an element or port.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="port">The port, or null.</param>
    /// <returns>The default text, or null when the element has none.</returns>
    private static string? DefaultToolTip(DiagramElement element, Port? port)
    {
        if (port is not null)
        {
            // The type's display name comes from the diagram's registry; unregistered keys show as themselves
            var typeName = element.Page?.Diagram.Types.PortTypes.Get(port.TypeKey).DisplayName ?? port.TypeKey;
            var text = $"{port.Label} ({typeName})";
            return string.IsNullOrEmpty(port.Description) ? text : $"{text}\n{port.Description}";
        }

        return element switch
        {
            Node node => node.Type.GetToolTip(node),
            Wire wire => $"{wire.Source} → {wire.Target}",
            _ => null,
        };
    }

    /// <summary>
    ///     Suppresses the tooltip of what lies under a pressed pointer, and hides the current tooltip.
    /// </summary>
    /// <remarks>
    ///     A press starts a drag, a connection, a double-click or a context request: the tooltip goes away and does not come
    ///     back for the same element or port until the pointer has moved to something else.
    /// </remarks>
    /// <param name="hit">What the press landed on.</param>
    private void SuppressToolTip(HitResult hit)
    {
        _toolTipSuppressed = hit.Element is { } element ? new ToolTipTarget(element, hit.Port) : null;
        ResetToolTip();
    }

    /// <summary>
    ///     Shows, changes or hides the tooltip for what is under a pointer that is not dragging.
    /// </summary>
    /// <remarks>
    ///     Setting <c>ToolTip.Tip</c> lets Avalonia's tooltip service open the tooltip after its usual delay near the pointer;
    ///     clearing it closes the tooltip. When a tooltip is already open and the pointer moves straight to another element, the
    ///     tooltip is reopened so it appears next to the pointer with the new content.
    /// </remarks>
    /// <param name="hit">What is under the pointer.</param>
    private void UpdateToolTip(HitResult hit)
    {
        // A suppressed target stays suppressed until the pointer moves to something else
        ToolTipTarget? hovered = hit.Element is { } element ? new ToolTipTarget(element, hit.Port) : null;
        if (_toolTipSuppressed is { } suppressed && hovered != suppressed)
        {
            _toolTipSuppressed = null;
        }

        var target = CanShowToolTip() && _toolTipSuppressed is null ? hovered : null;
        if (target == _toolTipTarget)
        {
            return;
        }

        // Ask for the content once per target, so a host's live text is current when the pointer arrives
        _toolTipTarget = target;
        var content = target is { } t ? GetToolTipContent(t.Element, t.Port) : null;
        var wasOpen = ToolTip.GetIsOpen(this);
        ToolTip.SetTip(this, content);
        if (wasOpen && content is not null)
        {
            ToolTip.SetIsOpen(this, false);
            ToolTip.SetIsOpen(this, true);
        }
    }

    /// <summary>
    ///     Hides the tooltip when the element or port it describes has left the shown page or its node.
    /// </summary>
    private void ResetToolTipIfStale()
    {
        if (_toolTipTarget is { } target
            && (!ReferenceEquals(target.Element.Page, Page)
                || (target.Port is { } port && !port.Node.Ports.Contains(port))))
        {
            ResetToolTip();
        }
    }

    /// <summary>
    ///     Determines whether a tooltip may be shown now: tooltips are on and no gesture, inline edit, pan or context request is in progress.
    /// </summary>
    /// <returns><see langword="true"/> when hovering may show a tooltip.</returns>
    private bool CanShowToolTip() => ShowToolTips && _gesture is null && !IsEditing && !_spaceDown && _pendingContext is null;

    /// <summary>
    ///     What a tooltip describes: an element and, for port tooltips, one of its ports.
    /// </summary>
    /// <param name="Element">The element (a port's node for port tooltips).</param>
    /// <param name="Port">The port, or null.</param>
    private readonly record struct ToolTipTarget(DiagramElement Element, Port? Port);
}
