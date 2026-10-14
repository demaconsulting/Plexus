using Avalonia.Controls;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Plexus;

/// <summary>
///     The properties dialog of a Plexus node: a modal window that shows the node's name, properties and host data in a
///     staged <see cref="PropertyGrid"/>. OK applies everything as one undo entry; Cancel changes nothing.
/// </summary>
/// <remarks>
///     <para>
///         It is a thin layer over <see cref="PropertyGridDialog"/> and <see cref="NodePropertySource"/>: the title is
///         "&lt;Name&gt; - Properties", the grid is staged, and the node adapter makes the commit one undo entry (the
///         rename, the changed properties and the edited host data together). OK is refused with the reason shown while a
///         value is invalid or the rename is vetoed. Custom editors named by <see cref="PropertyDefinition.EditorHint"/> or
///         <see cref="PropertyEditorAttribute"/> are looked up in <see cref="PropertyGrid.DefaultEditors"/>, so register
///         them there once at start-up.
///     </para>
///     <para>
///         The dialog's controls carry the automation ids of the grid and its dialog: <c>PropertyGridDialog</c>,
///         <c>PropertyGridDialog_OK</c>, <c>PropertyGridDialog_Cancel</c>, <c>PropertyGridDialog_Status</c> and, for every
///         row, <c>PropertyGrid_Row_&lt;path&gt;</c> and <c>PropertyGrid_Editor_&lt;path&gt;</c> (for example
///         <c>PropertyGrid_Row_Name</c>).
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     // One line in the host window: double-click a node to edit it
///     editor.NodeDoubleClicked += async (_, e) => await NodePropertiesDialog.ShowAsync(this, e.Node);
///     ]]></code>
/// </example>
public static class NodePropertiesDialog
{
    /// <summary>
    ///     The width of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogWidth = 480;

    /// <summary>
    ///     The height of the dialog, in device-independent pixels.
    /// </summary>
    private const double DialogHeight = 640;

    /// <summary>
    ///     Shows the properties dialog of a node modally.
    /// </summary>
    /// <param name="owner">The owner window.</param>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>
    ///     A task that completes with <see langword="true"/> when the user pressed OK and the edits were committed as one
    ///     undo entry, and <see langword="false"/> when the user cancelled (nothing changed).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static Task<bool> ShowAsync(Window owner, Node node)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(node);
        return Create(node).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///     Creates the dialog window without showing it (for hosts that show it themselves).
    /// </summary>
    /// <param name="node">The node to edit; it must be attached to a diagram.</param>
    /// <returns>The window; use <see cref="PropertyGridDialog.Accept"/> or <see cref="Window.Close()"/> to finish it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not attached to a diagram.</exception>
    public static PropertyGridDialog Create(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new PropertyGridDialog(node, GetTitle(node)) { Width = DialogWidth, Height = DialogHeight };
    }

    /// <summary>
    ///     Gets the title of a node's dialog: "&lt;Name&gt; - Properties".
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static string GetTitle(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Name + " - Properties";
    }
}
