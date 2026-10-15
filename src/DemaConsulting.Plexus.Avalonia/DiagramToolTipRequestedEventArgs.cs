namespace DemaConsulting.Plexus.Avalonia;

/// <summary>
///     Describes the tooltip the editor is about to show for the element (and port) under the pointer, and lets the host change or suppress it.
/// </summary>
/// <remarks>
///     <para>
///         Raised by <see cref="DiagramEditor.ToolTipRequested"/> when the pointer moves onto a node, a port, a wire, an
///         annotation or a group (once per element or port it moves onto), and by <see cref="DiagramEditor.GetToolTipContent"/>.
///         <see cref="Content"/> arrives pre-filled with the editor's default text: for a port its label, the display name of its
///         <c>PortType</c> (its type key when the type is not registered) and its <c>PortDefinition.Description</c> on a new line;
///         for a node <c>NodeType.GetToolTip</c> (the type's tooltip function, else its description; a null or empty result means
///         none); for a wire <c>"source → target"</c> (for example <c>"A.out → B.in"</c>); for annotations and groups nothing
///         (null), although a handler may supply content for them.
///     </para>
///     <para>
///         Set <see cref="Content"/> to a string or to any Avalonia content (for example a <c>TextBlock</c> or a <c>StackPanel</c>
///         with a validation icon) to replace the text, or set <see cref="Cancel"/> (or a null or empty <see cref="Content"/>) to show
///         no tooltip. The handler runs on the UI thread and should be quick: it runs while the pointer moves.
///     </para>
/// </remarks>
/// <example>
///     Add the host's validation messages to node tooltips and show no tooltip for wires.
///     <code><![CDATA[
///     using DemaConsulting.Plexus;
///     using DemaConsulting.Plexus.Avalonia;
///
///     static class ToolTipSample
///     {
///         public static void Wire(DiagramEditor editor, Func<Node, IReadOnlyList<string>> validate)
///         {
///             editor.ToolTipRequested += (_, e) =>
///             {
///                 if (e.Element is Wire)
///                 {
///                     e.Cancel = true;                                  // no tooltip for wires
///                 }
///                 else if (e.Element is Node node && e.Port is null && validate(node) is { Count: > 0 } problems)
///                 {
///                     e.Content = $"{e.Content}\n⚠ {string.Join("\n⚠ ", problems)}";   // keep the default text, add the problems
///                 }
///             };
///         }
///     }
///     ]]></code>
/// </example>
public sealed class DiagramToolTipRequestedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagramToolTipRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="element">The element under the pointer; for a port, the port's node.</param>
    /// <param name="port">The port under the pointer, or null.</param>
    /// <param name="content">The default content the editor would show, or null for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public DiagramToolTipRequestedEventArgs(DiagramElement element, Port? port, object? content)
    {
        ArgumentNullException.ThrowIfNull(element);
        Element = element;
        Port = port;
        Content = content;
    }

    /// <summary>
    ///     Gets the element under the pointer: a node (also when a port of it is hovered), a wire, an annotation or a group.
    /// </summary>
    public DiagramElement Element { get; }

    /// <summary>
    ///     Gets the port under the pointer, or null when the pointer is not over a port.
    /// </summary>
    public Port? Port { get; }

    /// <summary>
    ///     Gets or sets the tooltip content: a string or any Avalonia content; pre-filled with the editor's default. Null or an empty string shows no tooltip.
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether no tooltip is shown for this element; the default is false.
    /// </summary>
    public bool Cancel { get; set; }
}
